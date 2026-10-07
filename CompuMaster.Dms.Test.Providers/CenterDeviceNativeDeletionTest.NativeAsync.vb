Option Explicit On
Option Strict On

Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest.Clients.Collections
Imports CenterDevice.Rest.Clients.Documents.Metadata
Imports CenterDevice.Rest.Clients.Folders
Imports CenterDevice.Rest.Clients.Link
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class CenterDeviceNativeDeletionTest
    <TestCase(DmsResourceItem.ItemTypes.File), TestCase(DmsResourceItem.ItemTypes.Folder), TestCase(DmsResourceItem.ItemTypes.Collection)>
    Public Async Function IdentifierDeletionRetainsTheSelectedResource(kind As DmsResourceItem.ItemTypes) As Task
        Dim provider As New DeletionProvider()
        Dim item = SelectedItem(kind)
        Using cancellation As New CancellationTokenSource()
            Await provider.DeleteRemoteItemAsync(item, cancellation.Token)
            Assert.That(provider.Selected, [Is].SameAs(item))
            Assert.That(provider.Events, [Is].EqualTo(New String() {kind.ToString() & ":selected-id"}))
            Assert.That(provider.RequestToken, [Is].EqualTo(cancellation.Token))
            Assert.That(provider.Io.CollectionCalls, [Is].Zero, "Deleting a selected identifier must not choose a same-name parent path.")
        End Using
    End Function

    <Test>
    Public Async Function CollectionUploadLinksAreRemovedBeforeTheCollection() As Task
        Dim provider As New DeletionProvider()
        Dim item = SelectedItem(DmsResourceItem.ItemTypes.Collection)
        item.ExtendedInfosLinks = New List(Of DmsLink) From {
            New DmsLink(item, provider) With {.ID = "download-id", .AllowDownload = True},
            New DmsLink(item, provider) With {.ID = "upload-id", .AllowUpload = True}
        }
        Await provider.DeleteRemoteItemAsync(item)
        Assert.That(provider.Events, [Is].EqualTo(New String() {"link:upload-id", "Collection:selected-id"}))
    End Function

    <Test>
    Public Async Function FailedUploadLinkCleanupStopsCollectionDeletionAndInvalidatesCache() As Task
        Dim provider As New DeletionProvider With {.FailLink = True}
        Dim item = SelectedItem(DmsResourceItem.ItemTypes.Collection)
        item.ExtendedInfosLinks = New List(Of DmsLink) From {New DmsLink(item, provider) With {.ID = "upload-id", .AllowUpload = True}}
        Await provider.ListAllCollectionNamesAsync("/")
        Assert.ThrowsAsync(Of IOException)(CType(Async Function()
                                                    Await provider.DeleteRemoteItemAsync(item)
                                                End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].EqualTo(New String() {"link:upload-id"}))
        Await provider.ListAllCollectionNamesAsync("/")
        Assert.That(provider.Io.CollectionCalls, [Is].EqualTo(2))
    End Function

    <TestCase(False), TestCase(True)>
    Public Async Function PathDeletionRetainsFileFirstAndTrailingDirectoryRules(forceDirectory As Boolean) As Task
        Dim provider As New DeletionProvider()
        Await provider.DeleteRemoteItemAsync("collection/duplicate" & If(forceDirectory, "/", ""))
        Assert.That(provider.Events, [Is].EqualTo(New String() {If(forceDirectory, "Folder:folder-id", "File:file-1")}))
        Assert.That(provider.Selected.FullName.TrimEnd("/"c), [Is].EqualTo("collection/duplicate"))
    End Function

    <Test>
    Public Async Function TypedPathDeletionUsesNativeLookupAndChecksTheType() As Task
        Dim provider As New DeletionProvider()
        Await provider.DeleteRemoteItemAsync("collection/duplicate", DmsResourceItem.ItemTypes.Folder, DmsResourceItem.ItemTypes.File)
        Assert.That(provider.Events, [Is].EqualTo(New String() {"File:file-1"}))
        provider.Events.Clear()
        Assert.ThrowsAsync(Of ArgumentException)(CType(Async Function()
                                                           Await provider.DeleteRemoteItemAsync("collection/duplicate", DmsResourceItem.ItemTypes.Collection)
                                                       End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].Empty)
    End Function

    <TestCase(False), TestCase(True)>
    Public Async Function TypedSelectedDeletionPreservesIdentity(useAlternative As Boolean) As Task
        Dim provider As New DeletionProvider()
        Dim item = SelectedItem(DmsResourceItem.ItemTypes.File)
        If useAlternative Then
            Await provider.DeleteRemoteItemAsync(item, DmsResourceItem.ItemTypes.Folder, DmsResourceItem.ItemTypes.File)
        Else
            Await provider.DeleteRemoteItemAsync(item, DmsResourceItem.ItemTypes.File)
        End If
        Assert.That(provider.Selected, [Is].SameAs(item))
        Assert.That(provider.Io.CollectionCalls, [Is].Zero)
    End Function

    <Test>
    Public Sub AmbiguousSelectedResourceWithoutAnIdentifierIsRejected()
        Dim provider As New DeletionProvider()
        Dim item = SelectedItem(DmsResourceItem.ItemTypes.File)
        item.ExtendedInfosFileID = Nothing
        Assert.ThrowsAsync(Of RemotePathNotUniqueException)(CType(Async Function()
                                                                     Await provider.DeleteRemoteItemAsync(item)
                                                                 End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].Empty)
        Assert.That(provider.Io.CollectionCalls, [Is].Zero)
    End Sub

    <Test>
    Public Async Function PathOnlyResourceResolvesUniqueIdentityBeforeDeletion() As Task
        Dim provider As New DeletionProvider()
        Dim item As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .FullName = "collection/unique"}
        Await provider.DeleteRemoteItemAsync(item)
        Assert.That(provider.Events, [Is].EqualTo(New String() {"File:unique-id"}))
    End Function

    <Test>
    Public Sub PathOnlyResourceWithUnknownCollisionStateCannotDeleteAnArbitrarySibling()
        Dim provider As New DeletionProvider()
        Dim item As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .FullName = "collection/duplicate"}
        Assert.ThrowsAsync(Of RemotePathNotUniqueException)(CType(Async Function()
                                                                     Await provider.DeleteRemoteItemAsync(item)
                                                                 End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].Empty)
    End Sub

    <Test>
    Public Sub PathOnlyResourceCannotChooseAnArbitraryParentCollection()
        Dim provider As New DeletionProvider()
        provider.Io.DuplicateCollections = True
        Dim item As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .FullName = "collection/unique"}
        Assert.ThrowsAsync(Of RemotePathNotUniqueException)(CType(Async Function()
                                                                     Await provider.DeleteRemoteItemAsync(item)
                                                                 End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].Empty)
    End Sub

    <Test>
    Public Sub MissingServerIdentifierFailsWithoutRecursiveResolutionOrMutation()
        Dim provider As New DeletionProvider()
        provider.Io.MissingFileIdentifier = True
        Dim item As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .FullName = "collection/unique"}
        Dim failure = Assert.ThrowsAsync(Of InvalidOperationException)(CType(Async Function()
                                                                                Await provider.DeleteRemoteItemAsync(item)
                                                                            End Function, Func(Of Task)))
        Assert.That(failure.Message, Does.Contain("identifier"))
        Assert.That(provider.Events, [Is].Empty)
    End Sub

    <Test>
    Public Sub MissingUploadLinkIdentifierCannotDispatchALinkOrCollectionDeletion()
        Dim provider As New DeletionProvider()
        Dim item = SelectedItem(DmsResourceItem.ItemTypes.Collection)
        item.ExtendedInfosLinks = New List(Of DmsLink) From {New DmsLink(item, provider) With {.AllowUpload = True}}
        Assert.ThrowsAsync(Of InvalidOperationException)(CType(Async Function()
                                                                  Await provider.DeleteRemoteItemAsync(item)
                                                              End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].Empty)
    End Sub

    <TestCase(False), TestCase(True)>
    Public Async Function UncertainDeletionFailureInvalidatesCachesWithoutReplay(cancel As Boolean) As Task
        Dim provider As New DeletionProvider With {.Block = cancel, .FailDelete = Not cancel}
        Await provider.ListAllCollectionNamesAsync("/")
        Using cancellation As New CancellationTokenSource()
            Dim operation = provider.DeleteRemoteItemAsync(SelectedItem(DmsResourceItem.ItemTypes.File), cancellation.Token)
            If cancel Then
                Await provider.Entered.Task
                cancellation.Cancel()
                Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                         Await operation
                                                                     End Function, Func(Of Task)))
            Else
                Assert.ThrowsAsync(Of IOException)(CType(Async Function()
                                                            Await operation
                                                        End Function, Func(Of Task)))
            End If
            Assert.That(provider.Events.Count, [Is].EqualTo(1))
            Await provider.ListAllCollectionNamesAsync("/")
            Assert.That(provider.Io.CollectionCalls, [Is].EqualTo(2))
        End Using
    End Function

    <TestCase(False), TestCase(True)>
    Public Sub RootDeletionIsRejectedBeforeMutation(useItem As Boolean)
        Dim provider As New DeletionProvider()
        Assert.ThrowsAsync(Of DmsUserErrorMessageException)(CType(Async Function()
                                                                     If useItem Then
                                                                         Await provider.DeleteRemoteItemAsync(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Root})
                                                                     Else
                                                                         Await provider.DeleteRemoteItemAsync("/")
                                                                     End If
                                                                 End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].Empty)
        Assert.That(provider.Io.CollectionCalls, [Is].Zero)
    End Sub

    <Test>
    Public Sub PreCanceledDeletionDoesNotDispatch()
        Dim provider As New DeletionProvider()
        Using cancellation As New CancellationTokenSource()
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                     Await provider.DeleteRemoteItemAsync(SelectedItem(DmsResourceItem.ItemTypes.File), cancellation.Token)
                                                                 End Function, Func(Of Task)))
        End Using
        Assert.That(provider.Events, [Is].Empty)
    End Sub

    Private Shared Function SelectedItem(kind As DmsResourceItem.ItemTypes) As DmsResourceItem
        Return New DmsResourceItem With {.ItemType = kind, .FullName = "duplicate", .ExtendedInfosCollisionDetected = True,
            .ExtendedInfosCollectionID = "selected-id", .ExtendedInfosFolderID = "selected-id", .ExtendedInfosFileID = "selected-id"}
    End Function

    Private Class DeletionProvider
        Inherits ScopevisioTeamworkDmsProvider
        Public ReadOnly Io As New FixtureIo()
        Public ReadOnly Events As New List(Of String)()
        Public Selected As DmsResourceItem
        Public RequestToken As CancellationToken
        Public Block As Boolean
        Public FailDelete As Boolean
        Public FailLink As Boolean
        Public ReadOnly Entered As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Public Sub New()
            Me.IOClient = Io
        End Sub
        Protected Overrides Async Function DeleteNativeResourceAsync(remoteItem As DmsResourceItem, cancellationToken As CancellationToken) As Task
            Selected = remoteItem
            RequestToken = cancellationToken
            Dim id = If(remoteItem.ItemType = DmsResourceItem.ItemTypes.Collection, remoteItem.ExtendedInfosCollectionID, If(remoteItem.ItemType = DmsResourceItem.ItemTypes.Folder, remoteItem.ExtendedInfosFolderID, remoteItem.ExtendedInfosFileID))
            Events.Add(remoteItem.ItemType.ToString() & ":" & id)
            Entered.TrySetResult(True)
            If Block Then Await Task.Delay(Timeout.Infinite, cancellationToken)
            If FailDelete Then Throw New IOException("Uncertain deletion result.")
        End Function
        Protected Overrides Function DeleteNativeUploadLinkAsync(linkId As String, cancellationToken As CancellationToken) As Task
            Events.Add("link:" & linkId)
            If FailLink Then Throw New IOException("Upload-link cleanup failed.")
            Return Task.CompletedTask
        End Function
        Protected Overrides Function LoadNativeUploadLinksAsync(cancellationToken As CancellationToken) As Task(Of UploadLinks)
            Return Task.FromResult(New UploadLinks With {.UploadLinksList = New List(Of UploadLink)()})
        End Function
    End Class

    Private Class FixtureIo
        Inherits Global.CenterDevice.IO.IOClientBase
        Public CollectionCalls As Integer
        Public DuplicateCollections As Boolean
        Public MissingFileIdentifier As Boolean
        Public Sub New()
            MyBase.New(Nothing, "fixture-user")
        End Sub
        Protected Overrides Function LookupCollectionsAsync(cancellationToken As CancellationToken) As Task(Of List(Of Collection))
            CollectionCalls += 1
            Dim result As New List(Of Collection) From {New Collection With {.Id = "collection-id", .Name = "collection"}}
            If DuplicateCollections Then result.Add(New Collection With {.Id = "second-collection-id", .Name = "collection"})
            Return Task.FromResult(result)
        End Function
        Protected Overrides Function LookupChildFoldersAsync(collectionId As String, parentId As String, cancellationToken As CancellationToken) As Task(Of List(Of Folder))
            Return Task.FromResult(New List(Of Folder) From {New Folder With {.Id = "folder-id", .Name = "duplicate"}})
        End Function
        Protected Overrides Function LookupChildDocumentsAsync(collectionId As String, parentId As String, cancellationToken As CancellationToken) As Task(Of List(Of DocumentFullMetadata))
            Return Task.FromResult(New List(Of DocumentFullMetadata) From {
                New DocumentFullMetadata With {.Id = "file-1", .Filename = "duplicate"},
                New DocumentFullMetadata With {.Id = "file-2", .Filename = "duplicate"},
                New DocumentFullMetadata With {.Id = If(MissingFileIdentifier, Nothing, "unique-id"), .Filename = "unique"}
            })
        End Function
    End Class
End Class
