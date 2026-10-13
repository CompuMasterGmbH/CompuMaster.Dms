Option Explicit On
Option Strict On

Imports NUnit.Framework
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

<TestFixture>
Public Class OwnedCopyTestCleanupTest
    <Test>
    Public Sub CopyFailureStillCleansEveryOwnedChildAndRemainsOriginalFailure()
        Dim calls As New List(Of String)
        Dim original As New InvalidOperationException("Copy failure.")
        Dim failure = Assert.Throws(Of InvalidOperationException)(Sub()
            OwnedCopyTestCleanup.Complete({CType(Sub() calls.Add("source"), Action), CType(Sub() calls.Add("target"), Action)}, original)
        End Sub)
        Assert.That(calls, [Is].EqualTo(New String() {"source", "target"}))
        Assert.That(failure, [Is].SameAs(original))
    End Sub

    <Test>
    Public Sub CleanupFailureDoesNotPreventRemainingCleanupOrHideCopyFailure()
        Dim calls As New List(Of String)
        Dim original As New InvalidOperationException("Copy failure.")
        Dim cleanupFailure As New System.IO.IOException("Cleanup failure.")
        Dim failure = Assert.Throws(Of AggregateException)(Sub()
            OwnedCopyTestCleanup.Complete({CType(Sub() Throw cleanupFailure, Action), CType(Sub() calls.Add("second-child"), Action)}, original)
        End Sub)
        Assert.That(calls, [Is].EqualTo(New String() {"second-child"}))
        Assert.That(failure.InnerExceptions, [Is].EqualTo(New Exception() {original, cleanupFailure}))
    End Sub

    <Test>
    Public Sub CleanupOnlyFailureRemainsVisibleAndCleanSuccessCompletes()
        Dim original As New System.IO.IOException("Cleanup failure.")
        Dim failure = Assert.Throws(Of System.IO.IOException)(Sub() OwnedCopyTestCleanup.Complete({CType(Sub() Throw original, Action)}, Nothing))
        Assert.That(failure, [Is].SameAs(original))
        Assert.DoesNotThrow(Sub() OwnedCopyTestCleanup.Complete({CType(Sub() Return, Action)}, Nothing))
    End Sub
    <Test>
    Public Sub ExactChildrenAreRemovedBeforeNestedParentsAndPermanentRootIsRetained()
        Dim provider As New CleanupFixture()
        provider.Add("root", DmsResourceItem.ItemTypes.Collection)
        provider.Add("root/source.bin", DmsResourceItem.ItemTypes.File)
        provider.Add("root/child", DmsResourceItem.ItemTypes.Folder)
        provider.Add("root/child/nested", DmsResourceItem.ItemTypes.Folder)
        provider.Add("root/child/nested/target.bin", DmsResourceItem.ItemTypes.File)
        Dim actions = OwnedCopyTestCleanup.ForChildren(provider, "root", {"source.bin", "child/nested/target.bin"})
        OwnedCopyTestCleanup.Complete(actions, Nothing)
        Assert.That(provider.Deleted, [Is].EqualTo(New String() {"root/source.bin", "root/child/nested/target.bin", "root/child/nested", "root/child"}))
        Assert.That(provider.RemoteItemExists("root"), [Is].True)
        Assert.DoesNotThrow(Sub() OwnedCopyTestCleanup.Complete(actions, Nothing), "Recovery and final cleanup are repeatable.")
    End Sub

    <Test>
    Public Sub UnexpectedChildPreventsParentDeletionAndSetupAdmission()
        Dim provider As New CleanupFixture()
        provider.Add("root/child", DmsResourceItem.ItemTypes.Folder)
        provider.Add("root/child/other.bin", DmsResourceItem.ItemTypes.File)
        Dim actions = OwnedCopyTestCleanup.ForChildren(provider, "root", {"child/target.bin"})
        Assert.Throws(Of InvalidOperationException)(Sub() OwnedCopyTestCleanup.Complete(actions, Nothing))
        Assert.That(provider.Deleted, [Is].Empty)
        Assert.That(provider.RemoteItemExists("root/child/other.bin"), [Is].True)
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub UnexpectedKindOrAmbiguousFileIsPreserved(collision As Boolean)
        Dim provider As New CleanupFixture()
        provider.Add("root/source.bin", If(collision, DmsResourceItem.ItemTypes.File, DmsResourceItem.ItemTypes.Folder), collision)
        Dim actions = OwnedCopyTestCleanup.ForChildren(provider, "root", {"source.bin"})
        Assert.Throws(Of InvalidOperationException)(Sub() OwnedCopyTestCleanup.Complete(actions, Nothing))
        Assert.That(provider.Deleted, [Is].Empty)
    End Sub

    <Test>
    Public Sub FailedDeleteIsReportedWhileOtherOwnedFilesAreStillRemoved()
        Dim provider As New CleanupFixture() With {.FailDelete = "root/source.bin"}
        provider.Add("root/source.bin", DmsResourceItem.ItemTypes.File)
        provider.Add("root/target.bin", DmsResourceItem.ItemTypes.File)
        Dim actions = OwnedCopyTestCleanup.ForChildren(provider, "root", {"source.bin", "target.bin"})
        Dim original As New InvalidOperationException("Original copy failure.")
        Dim failure = Assert.Throws(Of AggregateException)(Sub() OwnedCopyTestCleanup.Complete(actions, original))
        Assert.That(failure.InnerExceptions(0), [Is].SameAs(original))
        Assert.That(provider.RemoteItemExists("root/source.bin"), [Is].True)
        Assert.That(provider.RemoteItemExists("root/target.bin"), [Is].False)
    End Sub

    <TestCase(""), TestCase("../target.bin"), TestCase("/target.bin")>
    Public Sub NonRelativeOrParentPathsAreRejectedBeforeAnyDeletion(name As String)
        Dim provider As New CleanupFixture()
        Assert.Throws(Of ArgumentException)(Sub() OwnedCopyTestCleanup.ForChildren(provider, "root", {name}))
        Assert.That(provider.Deleted, [Is].Empty)
    End Sub

    Private Class CleanupFixture
        Inherits NoDmsProvider
        Friend ReadOnly Deleted As New List(Of String)
        Friend FailDelete As String
        Private ReadOnly Items As New Dictionary(Of String, DmsResourceItem)(StringComparer.Ordinal)
        Friend Sub Add(path As String, kind As DmsResourceItem.ItemTypes, Optional collision As Boolean = False)
            Items.Add(path, New DmsResourceItem With {.FullName = path, .ItemType = kind, .ExtendedInfosCollisionDetected = collision})
        End Sub
        Public Overrides Function ListRemoteItem(remotePath As String) As DmsResourceItem
            Dim item As DmsResourceItem = Nothing
            Items.TryGetValue(remotePath, item)
            Return item
        End Function
        Public Overrides Function ListAllRemoteItems(remoteFolderPath As String, searchType As BaseDmsProvider.SearchItemType) As List(Of DmsResourceItem)
            Return Items.Where(Function(pair) pair.Key.StartsWith(remoteFolderPath & "/", StringComparison.Ordinal)).Select(Function(pair) pair.Value).ToList()
        End Function
        Public Overrides Sub DeleteRemoteItem(remoteItem As DmsResourceItem)
            If remoteItem.FullName = FailDelete Then Throw New System.IO.IOException("Fixed delete failure.")
            Deleted.Add(remoteItem.FullName)
            Items.Remove(remoteItem.FullName)
        End Sub
    End Class
End Class
