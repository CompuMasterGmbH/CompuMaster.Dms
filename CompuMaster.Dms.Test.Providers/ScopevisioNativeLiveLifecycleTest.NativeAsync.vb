Option Explicit On
Option Strict On

Imports System.IO
Imports System.Linq
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class ScopevisioNativeLiveLifecycleTest
    <Test>
    Public Async Function CleanupRemovesOwnedChildrenAndLinksBeforeParentsAndPreservesOtherRoots() As Task
        Dim provider As New LifecycleProvider()
        Await ScopevisioNativeLiveTest.RemoveOwnedCollectionAsync(provider, CancellationToken.None)
        Assert.That(provider.Events, [Is].EqualTo(New String() {"link:owned-link", "file:nested-file", "folder:nested", "file:root-file", "collection:owned-root"}))
        Assert.That(provider.Exists, [Is].False)
        Assert.That(Await provider.ListAllCollectionNamesAsync("/"), [Is].EqualTo(New String() {"Permanent unrelated collection"}))
    End Function

    <Test>
    Public Async Function CleanupIsRepeatableWhenTheOwnedCollectionIsAlreadyAbsent() As Task
        Dim provider As New LifecycleProvider With {.Exists = False}
        Await ScopevisioNativeLiveTest.RemoveOwnedCollectionAsync(provider, CancellationToken.None)
        Await ScopevisioNativeLiveTest.RemoveOwnedCollectionAsync(provider, CancellationToken.None)
        Assert.That(provider.Events, [Is].Empty)
        Assert.That(provider.RootLookups, [Is].Zero)
    End Function

    <Test>
    Public Sub AmbiguousOwnedRootFailsBeforeLookupOrDeletion()
        Dim provider As New LifecycleProvider With {.Ambiguous = True}
        Assert.ThrowsAsync(Of InvalidOperationException)(CType(Async Function()
                                                                   Await ScopevisioNativeLiveTest.RemoveOwnedCollectionAsync(provider, CancellationToken.None)
                                                               End Function, Func(Of Task)))
        Assert.That(provider.RootLookups, [Is].Zero)
        Assert.That(provider.Events, [Is].Empty)
    End Sub

    <Test>
    Public Sub MissingOwnedRootIdentifierFailsBeforeDeletingChildren()
        Dim provider As New LifecycleProvider With {.MissingRootId = True}
        Assert.ThrowsAsync(Of InvalidOperationException)(CType(Async Function()
                                                                   Await ScopevisioNativeLiveTest.RemoveOwnedCollectionAsync(provider, CancellationToken.None)
                                                               End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].Empty)
    End Sub

    <Test>
    Public Sub CleanupRejectsFilesReferencedByAnotherCollection()
        Dim provider As New LifecycleProvider With {.ForeignReference = True}
        Assert.ThrowsAsync(Of IOException)(CType(Async Function()
                                                    Await ScopevisioNativeLiveTest.RemoveOwnedCollectionAsync(provider, CancellationToken.None)
                                                End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].Empty)
        Assert.That(provider.Exists, [Is].True)
    End Sub

    <Test>
    Public Sub CleanupReportsADeletionThatDidNotRemoveTheRoot()
        Dim provider As New LifecycleProvider With {.KeepRoot = True}
        Assert.ThrowsAsync(Of IOException)(CType(Async Function()
                                                    Await ScopevisioNativeLiveTest.RemoveOwnedCollectionAsync(provider, CancellationToken.None)
                                                End Function, Func(Of Task)))
        Assert.That(provider.Exists, [Is].True)
    End Sub

    <Test>
    Public Sub CleanupVerifiesChildRemovalBeforeDeletingTheirParent()
        Dim provider As New LifecycleProvider With {.KeepChildren = True}
        Assert.ThrowsAsync(Of IOException)(CType(Async Function()
                                                    Await ScopevisioNativeLiveTest.RemoveOwnedCollectionAsync(provider, CancellationToken.None)
                                                End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].EqualTo(New String() {"link:owned-link", "file:nested-file"}))
        Assert.That(provider.Exists, [Is].True)
    End Sub

    <Test>
    Public Sub ASingleOriginalFailureRetainsItsTypeAndInstance()
        Dim original As New OperationCanceledException("Original cancellation.")
        Dim actual = Assert.Throws(Of OperationCanceledException)(Sub() ScopevisioNativeLiveTest.ThrowNativeLiveFailures(New List(Of Exception) From {original}))
        Assert.That(actual, [Is].SameAs(original))
    End Sub

    <Test>
    Public Sub CleanupFailureDoesNotHideTheOriginalExecutionFailure()
        Dim original As New InvalidOperationException("Original execution failure.")
        Dim cleanup As New IOException("Cleanup failure.")
        Dim actual = Assert.Throws(Of AggregateException)(Sub() ScopevisioNativeLiveTest.ThrowNativeLiveFailures(New List(Of Exception) From {original, cleanup}))
        Assert.That(actual.InnerExceptions, [Is].EqualTo(New Exception() {original, cleanup}))
    End Sub

    <TestCase("false", "true"), TestCase("true", "false"), TestCase(Nothing, "true"), TestCase("true", Nothing)>
    Public Sub LiveGateRejectsMissingCiOrCoordinationBeforeAuthorization(ci As String, coordinated As String)
        Assert.Throws(Of InvalidOperationException)(Sub() ScopevisioNativeLiveTest.ValidateExclusiveCi(ci, coordinated))
    End Sub

    <Test>
    Public Sub LiveGateAcceptsExplicitCoordinatedCi()
        Assert.DoesNotThrow(Sub() ScopevisioNativeLiveTest.ValidateExclusiveCi("true", "true"))
    End Sub

    Private Class LifecycleProvider
        Inherits ScopevisioTeamworkDmsProvider
        Public Exists As Boolean = True
        Public Ambiguous As Boolean
        Public MissingRootId As Boolean
        Public ForeignReference As Boolean
        Public KeepRoot As Boolean
        Public KeepChildren As Boolean
        Public RootLookups As Integer
        Public ReadOnly Events As New List(Of String)()
        Private ReadOnly Deleted As New HashSet(Of String)()
        Private ReadOnly Root As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Collection, .FullName = ScopevisioNativeLiveTest.OwnedCollection, .ExtendedInfosCollectionID = "owned-root"}
        Private ReadOnly Folder As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .FullName = ScopevisioNativeLiveTest.OwnedCollection & "/nested", .ExtendedInfosFolderID = "nested"}
        Public Overrides Function ResetCachesForRemoteItemsAsync(path As String, kind As BaseDmsProvider.SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Task.CompletedTask
        End Function
        Public Overrides Function ListAllCollectionNamesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of String))
            Assert.That(path, [Is].EqualTo("/"))
            Dim result As New List(Of String) From {"Permanent unrelated collection"}
            If Exists Then result.Add(ScopevisioNativeLiveTest.OwnedCollection)
            If Ambiguous Then result.Add(ScopevisioNativeLiveTest.OwnedCollection)
            Return Task.FromResult(result)
        End Function
        Public Overrides Function ListRemoteItemAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            RootLookups += 1
            Assert.That(path, [Is].EqualTo(ScopevisioNativeLiveTest.OwnedCollection))
            If MissingRootId Then Root.ExtendedInfosCollectionID = Nothing
            Return Task.FromResult(Root)
        End Function
        Public Overrides Function ListAllRemoteItemsAsync(path As String, kind As BaseDmsProvider.SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Dim nested = path = Folder.FullName
            Assert.That(path, [Is].EqualTo(If(nested, Folder.FullName, Root.FullName)))
            Dim file As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .FullName = path & "/file.bin", .ExtendedInfosFileID = If(nested, "nested-file", "root-file"), .ExtendedInfosReferencedFromCollectionIDs = If(ForeignReference, New List(Of String) From {"foreign-root"}, New List(Of String) From {"owned-root"})}
            If nested Then file.ExtendedInfosLinks = New List(Of DmsLink) From {New DmsLink(file, "owned-link", Me, Nothing)}
            Dim result As New List(Of DmsResourceItem) From {file}
            If Not nested AndAlso Not ForeignReference Then result.Insert(0, Folder)
            Return Task.FromResult(result.Where(Function(item) Not Deleted.Contains(If(item.ItemType = DmsResourceItem.ItemTypes.File, item.ExtendedInfosFileID, item.ExtendedInfosFolderID))).ToList())
        End Function
        Public Overrides Function DeleteLinkAsync(link As DmsLink, Optional cancellationToken As CancellationToken = Nothing) As Task
            Events.Add("link:" & link.ID)
            Return Task.CompletedTask
        End Function
        Public Overrides Function DeleteRemoteItemAsync(item As DmsResourceItem, Optional cancellationToken As CancellationToken = Nothing) As Task
            Select Case item.ItemType
                Case DmsResourceItem.ItemTypes.File
                    Events.Add("file:" & item.ExtendedInfosFileID)
                    If Not KeepChildren Then Deleted.Add(item.ExtendedInfosFileID)
                Case DmsResourceItem.ItemTypes.Folder
                    Events.Add("folder:" & item.ExtendedInfosFolderID)
                    If Not KeepChildren Then Deleted.Add(item.ExtendedInfosFolderID)
                Case DmsResourceItem.ItemTypes.Collection
                    Assert.That(item, [Is].SameAs(Root))
                    Events.Add("collection:" & item.ExtendedInfosCollectionID)
                    Exists = KeepRoot
                Case Else : Assert.Fail("Unexpected deletion kind.")
            End Select
            Return Task.CompletedTask
        End Function
    End Class
End Class
