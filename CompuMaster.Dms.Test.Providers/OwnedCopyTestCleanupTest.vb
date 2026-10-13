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

    <TestCase(False), TestCase(True)>
    Public Sub NewlyCreatedConfirmedRootIsRemovedAfterChildrenEvenWhenCopyFails(collection As Boolean)
        Dim provider As New CleanupFixture With {.UseCollections = collection}
        Dim scope As New OwnedCopyTestRoot(provider, "root")
        scope.Prepare()
        provider.Add("root/child", DmsResourceItem.ItemTypes.Folder)
        provider.Add("root/child/target.bin", DmsResourceItem.ItemTypes.File)
        Dim actions = OwnedCopyTestCleanup.ForChildren(provider, "root", {"child/target.bin"})
        Dim original As New InvalidOperationException("Original copy failure.")
        Assert.That(Assert.Throws(Of InvalidOperationException)(Sub() OwnedCopyTestCleanup.CompleteScope(actions, scope, original)), [Is].SameAs(original))
        Assert.That(provider.Deleted, [Is].EqualTo(New String() {"root/child/target.bin", "root/child", "root"}))
        Assert.That(provider.RemoteItemExists("root"), [Is].False)
        Assert.DoesNotThrow(Sub() scope.Cleanup())
    End Sub

    <TestCase("existing"), TestCase("configured"), TestCase("configured-descendant")>
    Public Sub PreExistingOrConfiguredRootIsPreserved(mode As String)
        Dim provider As New CleanupFixture With {.ConfiguredRoot = If(mode = "configured-descendant", "/root/starting-point/", If(mode = "configured", "/root/", Nothing))}
        If mode = "existing" Then provider.Add("root", DmsResourceItem.ItemTypes.Folder)
        Dim scope As New OwnedCopyTestRoot(provider, "root")
        scope.Prepare()
        provider.Add("root/source.bin", DmsResourceItem.ItemTypes.File)
        OwnedCopyTestCleanup.CompleteScope(OwnedCopyTestCleanup.ForChildren(provider, "root", {"source.bin"}), scope, Nothing)
        Assert.That(provider.RemoteItemExists("root"), [Is].True)
        Assert.That(provider.Deleted, [Is].EqualTo(New String() {"root/source.bin"}))
        Assert.That(provider.CreateCalls, [Is].EqualTo(If(mode = "existing", 0, 1)))
    End Sub

    <TestCase("unknown-commit"), TestCase("unknown-permanent"), TestCase("missing-confirmation"), TestCase("missing-collection-id")>
    Public Sub UncertainSetupPreservesRootAndOriginalWithReconciliationFailure(mode As String)
        Dim provider As New CleanupFixture With {.CreateMode = If(mode = "unknown-permanent", "unknown-commit", mode), .UseCollections = mode = "missing-collection-id", .ConfiguredRoot = If(mode = "unknown-permanent", "/root/", Nothing)}
        Dim scope As New OwnedCopyTestRoot(provider, "root")
        Dim original = Assert.Catch(Sub() scope.Prepare())
        Dim failure = Assert.Throws(Of AggregateException)(Sub() OwnedCopyTestCleanup.Complete({CType(AddressOf scope.Cleanup, Action)}, original))
        Assert.That(failure.InnerExceptions(0), [Is].SameAs(original))
        Assert.That(provider.CreateCalls, [Is].EqualTo(1), "No replay of an uncertain root creation.")
        Assert.That(provider.RemoteItemExists("root"), [Is].True)
        Assert.That(provider.Deleted, [Is].Empty)
    End Sub

    <TestCase("identity"), TestCase("children"), TestCase("collision")>
    Public Sub ChangedOrNonemptyCreatedRootIsPreserved(mode As String)
        Dim provider As New CleanupFixture()
        Dim scope As New OwnedCopyTestRoot(provider, "root")
        scope.Prepare()
        Select Case mode
            Case "identity" : provider.ListRemoteItem("root").ExtendedInfosFolderID = "replacement-id"
            Case "children" : provider.Add("root/other.bin", DmsResourceItem.ItemTypes.File)
            Case "collision" : provider.ListRemoteItem("root").ExtendedInfosCollisionDetected = True
        End Select
        Assert.Throws(Of InvalidOperationException)(Sub() scope.Cleanup())
        Assert.That(provider.Deleted, [Is].Empty)
    End Sub

    <Test>
    Public Sub FailedCreationWithoutRemoteCommitmentHasNothingToDelete()
        Dim provider As New CleanupFixture With {.CreateMode = "no-commit"}
        Dim scope As New OwnedCopyTestRoot(provider, "root")
        Assert.Throws(Of System.IO.IOException)(Sub() scope.Prepare())
        Assert.DoesNotThrow(Sub() scope.Cleanup())
        Assert.That(provider.CreateCalls, [Is].EqualTo(1))
        Assert.That(provider.Deleted, [Is].Empty)
    End Sub

    <Test>
    Public Sub AcknowledgedPathOnlyWebDavRootIsRemovedAfterConfirmedAbsenceBeforeSetup()
        Dim provider As New CleanupFixture With {.CreateMode = "path-only"}
        Dim scope As New OwnedCopyTestRoot(provider, "root")
        scope.Prepare()
        scope.Cleanup()
        Assert.That(provider.Deleted, [Is].EqualTo(New String() {"root"}))
        Assert.That(provider.RemoteItemExists("root"), [Is].False)
    End Sub

    <TestCase(""), TestCase("/")>
    Public Sub MissingProviderRootIsNeverCreatedOrDeleted(path As String)
        Dim provider As New CleanupFixture()
        Dim scope As New OwnedCopyTestRoot(provider, path)
        Assert.Throws(Of InvalidOperationException)(Sub() scope.Prepare())
        Assert.DoesNotThrow(Sub() scope.Cleanup())
        Assert.That(provider.CreateCalls, [Is].Zero)
        Assert.That(provider.Deleted, [Is].Empty)
    End Sub

    <TestCase("kind"), TestCase("collision"), TestCase("path"), TestCase("unknown-commit"), TestCase("missing-confirmation")>
    Public Sub RejectedOrUnconfirmedRootNeverAdmitsChildDeletion(mode As String)
        Dim uncertain = mode = "unknown-commit" OrElse mode = "missing-confirmation"
        Dim provider As New CleanupFixture With {.CreateMode = If(uncertain, mode, Nothing)}
        If Not uncertain Then
            provider.Add("root", If(mode = "kind", DmsResourceItem.ItemTypes.File, DmsResourceItem.ItemTypes.Folder), mode = "collision")
            If mode = "path" Then provider.ListRemoteItem("root").FullName = "other-root"
        End If
        Dim scope As New OwnedCopyTestRoot(provider, "root")
        Dim original = Assert.Catch(Sub() scope.Prepare())
        provider.Add("root/source.bin", DmsResourceItem.ItemTypes.File)
        Dim actions = OwnedCopyTestCleanup.ForChildren(provider, "root", {"source.bin"})
        Dim failure = Assert.Catch(Sub() OwnedCopyTestCleanup.CompleteScope(actions, scope, original))
        Assert.That(scope.IsAdmitted, [Is].False)
        Assert.That(provider.Deleted, [Is].Empty)
        Assert.That(provider.RemoteItemExists("root/source.bin"), [Is].True)
        If uncertain Then
            Assert.That(failure, [Is].TypeOf(Of AggregateException))
            Assert.That(CType(failure, AggregateException).InnerExceptions(0), [Is].SameAs(original))
        Else
            Assert.That(failure, [Is].SameAs(original))
        End If
    End Sub

    <TestCase("/root/child/"), TestCase("/root/child/start/")>
    Public Sub ConfiguredChildStartingPointAndAncestorsSurviveOwnedChildCleanup(configured As String)
        Dim provider As New CleanupFixture With {.ConfiguredRoot = configured}
        provider.Add("root", DmsResourceItem.ItemTypes.Folder)
        provider.Add("root/child", DmsResourceItem.ItemTypes.Folder)
        Dim start = configured.Trim("/"c)
        If start <> "root/child" Then provider.Add(start, DmsResourceItem.ItemTypes.Folder)
        provider.Add(start & "/work", DmsResourceItem.ItemTypes.Folder)
        provider.Add(start & "/test.bin", DmsResourceItem.ItemTypes.File)
        provider.Add(start & "/work/target.bin", DmsResourceItem.ItemTypes.File)
        provider.Add("root/disposable", DmsResourceItem.ItemTypes.Folder)
        provider.Add("root/disposable/target.bin", DmsResourceItem.ItemTypes.File)
        Dim scope As New OwnedCopyTestRoot(provider, "root")
        scope.Prepare()
        Dim relativeStart = start.Substring("root/".Length)
        Dim actions = OwnedCopyTestCleanup.ForChildren(provider, "root", {relativeStart & "/test.bin", relativeStart & "/work/target.bin", "disposable/target.bin"})
        OwnedCopyTestCleanup.CompleteScope(actions, scope, Nothing)
        Assert.That(provider.RemoteItemExists(start), [Is].True)
        Assert.That(provider.RemoteItemExists("root/child"), [Is].True)
        Assert.That(provider.RemoteItemExists("root"), [Is].True)
        Assert.That(provider.RemoteItemExists(start & "/work"), [Is].False)
        Assert.That(provider.RemoteItemExists("root/disposable"), [Is].False)
        Assert.That(provider.Deleted, Does.Not.Contain(start))
        Assert.That(provider.Deleted, Does.Not.Contain("root/child"))
    End Sub

    Private Class CleanupFixture
        Inherits NoDmsProvider
        Friend ReadOnly Deleted As New List(Of String)
        Friend FailDelete As String
        Friend ConfiguredRoot As String
        Friend UseCollections As Boolean
        Friend CreateMode As String
        Friend CreateCalls As Integer
        Private ConfirmationHidden As Boolean
        Public Overrides ReadOnly Property BrowseInRootFolderName As String
            Get
                Return ConfiguredRoot
            End Get
        End Property
        Public Overrides ReadOnly Property SupportsCollections As Boolean
            Get
                Return UseCollections
            End Get
        End Property
        Public Overrides Sub CreateFolder(remoteDirectoryPath As String)
            CreateOwnedRoot(remoteDirectoryPath, DmsResourceItem.ItemTypes.Folder)
        End Sub
        Public Overrides Sub CreateCollection(remoteDirectoryPath As String)
            CreateOwnedRoot(remoteDirectoryPath, DmsResourceItem.ItemTypes.Collection)
        End Sub
        Private Sub CreateOwnedRoot(path As String, kind As DmsResourceItem.ItemTypes)
            CreateCalls += 1
            If CreateMode = "no-commit" Then Throw New System.IO.IOException("Fixed root-create failure.")
            Add(path, kind)
            Items(path).ExtendedInfosFolderID = If(CreateMode = "path-only", Nothing, "created-folder-id")
            Items(path).ExtendedInfosCollectionID = If(CreateMode = "missing-collection-id", Nothing, "created-collection-id")
            If CreateMode = "unknown-commit" Then Throw New System.IO.IOException("Fixed uncertain root-create failure.")
        End Sub
        Private ReadOnly Items As New Dictionary(Of String, DmsResourceItem)(StringComparer.Ordinal)
        Friend Sub Add(path As String, kind As DmsResourceItem.ItemTypes, Optional collision As Boolean = False)
            Items.Add(path, New DmsResourceItem With {.FullName = path, .ItemType = kind, .ExtendedInfosCollisionDetected = collision})
        End Sub
        Public Overrides Function ListRemoteItem(remotePath As String) As DmsResourceItem
            If CreateCalls > 0 AndAlso CreateMode = "missing-confirmation" AndAlso Not ConfirmationHidden Then
                ConfirmationHidden = True
                Return Nothing
            End If
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
