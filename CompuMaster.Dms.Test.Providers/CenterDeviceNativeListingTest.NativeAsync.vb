Option Explicit On
Option Strict On

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
Public Class CenterDeviceNativeListingTest
    <Test>
    Public Async Function DirectoryEntriesKeepSharingMetadataWithoutPerEntryDetailRequests() As Task
        Dim io As New FakeIo With {.IncludeDownloadLink = True}
        Dim provider As New TestProvider(io)
        Dim items = Await provider.ListDirectoryEntriesAsync("/")
        Assert.That(items.Count, [Is].EqualTo(2))
        Assert.That(items(0).ExtendedInfosCollectionID, [Is].EqualTo("collection-1"))
        Assert.That(items(0).ExtendedInfosOwner.ID, [Is].EqualTo("owner-id"))
        Assert.That(items(0).ExtendedInfosCollisionDetected, [Is].True)
        Assert.That(items(0).ExtendedInfosHasUserSharings, [Is].True)
        Assert.That(items(0).ExtendedInfosHasHiddenUserSharings, [Is].True)
        Assert.That(items(0).ExtendedInfosUserSharings(0).User.ID, [Is].EqualTo("shared-user"))
        Assert.That(items(0).ExtendedInfosLinks.Count, [Is].EqualTo(2), "Download and upload links must both remain available to the tree.")
        Assert.That(items(0).ExtendedInfosLinks(0).ID, [Is].EqualTo("download-link"))
        Assert.That(items(0).ExtendedInfosLinks(1).AllowUpload, [Is].True)
        Assert.That(items(0).ExtendedInfosLinks(1).MaxBytes, [Is].EqualTo(5L * 1024L * 1024L * 1024L))
        Assert.That(items(0).ExtendedInfosIsShared, [Is].True)
        Assert.That(provider.LinkCalls, [Is].EqualTo(1), "Upload links are retrieved once as a batch, as in synchronous browsing.")
        Assert.That(provider.UserCalls, [Is].Zero, "Browsing must not resolve every principal before publishing entries.")
        Assert.That(provider.DownloadLinkCalls, [Is].Zero, "Browsing must not resolve every download link before publishing entries.")
        Assert.That(io.CollectionCalls, [Is].EqualTo(1))
    End Function

    <Test>
    Public Async Function FileEntriesKeepIdentitySizeAndOwnerWithoutPrincipalDetailRequests() As Task
        Dim provider As New TestProvider(New FakeIo())
        Dim items = Await provider.ListFileEntriesAsync("duplicate")
        Assert.That(items.Count, [Is].EqualTo(2))
        Assert.That(items(0).ExtendedInfosFileID, [Is].EqualTo("file-1"))
        Assert.That(items(0).ContentLength, [Is].EqualTo(5L * 1024L * 1024L * 1024L))
        Assert.That(items(0).ExtendedInfosOwner.ID, [Is].EqualTo("owner-id"))
        Assert.That(items(0).ExtendedInfosCollisionDetected, [Is].True)
        Assert.That(provider.UserCalls, [Is].Zero)
        Assert.That(provider.LinkCalls, [Is].Zero)
    End Function

    <Test>
    Public Async Function DirectoryEntriesRetainKnownEmptyKnownNonemptyAndUnknownChildFlags() As Task
        Dim provider As New TestProvider(New FakeIo())
        Dim items = Await provider.ListDirectoryEntriesAsync("duplicate")
        Assert.That(items.Select(Function(item) item.HasChildDirectories), [Is].EqualTo(New Boolean?() {False, True, Nothing}))
        Assert.That(provider.UserCalls, [Is].Zero)
        Assert.That(provider.LinkCalls, [Is].Zero)
    End Function
    <Test>
    Public Async Function CollectionSnapshotsKeepSharingLinksAndCollisionsAfterCacheReset() As Task
        Dim io As New FakeIo()
        Dim provider As New TestProvider(io) With {.ResetDuringLinkLookup = True}
        Dim items = Await provider.ListAllCollectionItemsAsync("/")
        Assert.That(items.Count, [Is].EqualTo(2))
        Assert.That(io.CollectionCalls, [Is].EqualTo(1), "Conversion must not synchronously reload a reset sibling cache.")
        Assert.That(items(0).ExtendedInfosCollisionDetected, [Is].True)
        Assert.That(items(0).ExtendedInfosOwner.ID, [Is].EqualTo("owner-id"))
        Assert.That(items(0).ExtendedInfosOwner.DisplayName, [Is].EqualTo("Name owner-id"))
        Assert.That(items(0).ExtendedInfosOwner.GetDisplayName, [Is].Null)
        Assert.That(items(0).ExtendedInfosHasHiddenUserSharings, [Is].True)
        Assert.That(items(0).ExtendedInfosUserSharings(0).User.ID, [Is].EqualTo("shared-user"))
        Assert.That(items(0).ExtendedInfosUserSharings(0).User.EMailAddress, [Is].EqualTo("shared-user@fixture.invalid"))
        Assert.That(items(0).ExtendedInfosUserSharings(0).User.GetEMailAddress, [Is].Null)
        Assert.That(items(0).ExtendedInfosLinks.Count, [Is].EqualTo(1))
        Assert.That(items(0).ExtendedInfosLinks(0).MaxBytes, [Is].EqualTo(5L * 1024L * 1024L * 1024L))
    End Function

    <Test>
    Public Async Function FileSnapshotsKeepLargeSizesAndDuplicateIds() As Task
        Dim provider As New TestProvider(New FakeIo())
        Dim items = Await provider.ListAllFileItemsAsync("duplicate")
        Assert.That(items.Count, [Is].EqualTo(2))
        Assert.That(items(0).ContentLength, [Is].EqualTo(5L * 1024L * 1024L * 1024L))
        Assert.That(items(0).ExtendedInfosFileID, [Is].EqualTo("file-1"))
        Assert.That(items(1).ExtendedInfosFileID, [Is].EqualTo("file-2"))
        Assert.That(items(0).ExtendedInfosCollisionDetected, [Is].True)
        Assert.That(items(0).ExtendedInfosAssignedCollectionID, [Is].EqualTo("collection-1"))
        Assert.That(items(0).ExtendedInfosOwner.ID, [Is].EqualTo("owner-id"))
        Assert.That(items(0).ExtendedInfosOwner.DisplayName, [Is].EqualTo("Name owner-id"))
        Assert.That(items(0).ExtendedInfosOwner.GetDisplayName, [Is].Null)
    End Function

    <Test>
    Public Async Function NameListingAvoidsUnrelatedSharingAndFileRequests() As Task
        Dim io As New FakeIo()
        Dim provider As New TestProvider(io)
        Assert.That(Await provider.ListAllCollectionNamesAsync("/"), [Is].EqualTo(New String() {"duplicate", "duplicate"}))
        Assert.That(Await provider.ListAllFolderNamesAsync("/"), [Is].EqualTo(New String() {"duplicate", "duplicate"}))
        Assert.That(provider.LinkCalls, [Is].Zero)
        Assert.That(io.FileCalls, [Is].Zero)
        Assert.That(provider.UserCalls, [Is].Zero)
    End Function

    <Test>
    Public Async Function ActiveListingCancellationDoesNotCacheFailure() As Task
        Dim io As New FakeIo With {.BlockCollections = True}
        Dim provider As New TestProvider(io)
        Using cancellation As New CancellationTokenSource()
            Dim operation = provider.ListAllCollectionNamesAsync("/", cancellation.Token)
            Await io.Entered.Task
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                     Await operation
                                                                 End Function, Func(Of Task)))
        End Using
        io.BlockCollections = False
        Assert.That((Await provider.ListAllCollectionNamesAsync("/")).Count, [Is].EqualTo(2))
        Assert.That(io.CollectionCalls, [Is].EqualTo(2))
    End Function

    <Test>
    Public Async Function NativeCacheResetCausesTheNextListingToReload() As Task
        Dim io As New FakeIo()
        Dim provider As New TestProvider(io)
        Await provider.ListAllCollectionNamesAsync("/")
        Await provider.ResetCachesForRemoteItemsAsync("/", BaseDmsProvider.SearchItemType.Collections)
        Await provider.ListAllCollectionNamesAsync("/")
        Assert.That(io.CollectionCalls, [Is].EqualTo(2))
        Assert.That(provider.SupportsAsynchronousIo, [Is].False, "Remaining operations still use fallback.")
    End Function

    <Test>
    Public Async Function DirectoryOnlyListingPreservesChildFlagsWithoutFileLookups() As Task
        Dim io As New FakeIo()
        Dim provider As New TestProvider(io)
        Dim items = Await provider.ListAllDirectoryItemsAsync("duplicate")
        Assert.That(items.Count, [Is].EqualTo(3))
        Assert.That(items(0).HasChildDirectories, [Is].EqualTo(False))
        Assert.That(items(1).HasChildDirectories, [Is].EqualTo(True))
        Assert.That(items(2).HasChildDirectories.HasValue, [Is].False)
        Assert.That(items(0).ExtendedInfosAssignedCollectionID, [Is].EqualTo("collection-1"))
        Assert.That(items(0).ExtendedInfosOwner.DisplayName, [Is].EqualTo("Name owner-id"))
        Assert.That(items(0).ExtendedInfosOwner.GetDisplayName, [Is].Null)
        Assert.That(io.FileCalls, [Is].Zero)
        Assert.That(provider.LinkCalls, [Is].Zero)
    End Function

    <Test>
    Public Async Function NativePathLookupKeepsIdentityAndCollisionInformation() As Task
        Dim provider As New TestProvider(New FakeIo())
        Dim file = Await provider.ListRemoteItemAsync("duplicate/fixture.bin")
        Assert.That(file.ExtendedInfosFileID, [Is].EqualTo("file-1"))
        Assert.That(Await provider.RemoteItemExistsAsAsync("duplicate/fixture.bin"), [Is].EqualTo(DmsResourceItem.FoundItemType.File))
        Assert.That(Await provider.RemoteItemExistsUniquelyAsAsync("duplicate/fixture.bin"), [Is].EqualTo(DmsResourceItem.FoundItemResult.WithNameCollisions))
        Dim directory = Await provider.ListRemoteItemAsync("duplicate")
        Assert.That(directory.ExtendedInfosCollectionID, [Is].EqualTo("collection-1"))
        Assert.That(directory.ExtendedInfosCollisionDetected, [Is].True)
        Assert.That((Await provider.ListRemoteItemAsync("/")).ItemType, [Is].EqualTo(DmsResourceItem.ItemTypes.Root))
    End Function

    <TestCase("missing"), TestCase("missing/file.bin"), TestCase("duplicate/missing")>
    Public Async Function MissingNativePathsReturnNotFound(path As String) As Task
        Dim provider As New TestProvider(New FakeIo())
        Assert.That(Await provider.ListRemoteItemAsync(path), [Is].Null)
        Assert.That(Await provider.RemoteItemExistsAsync(path), [Is].False)
        Assert.That(Await provider.RemoteItemExistsAsAsync(path), [Is].EqualTo(DmsResourceItem.FoundItemType.NotFound))
    End Function

    <Test>
    Public Async Function ExistenceAndTypeQueriesAvoidSharingLinkRequests() As Task
        Dim provider As New TestProvider(New FakeIo())
        Assert.That(Await provider.RemoteItemExistsAsync("duplicate"), [Is].True)
        Assert.That(Await provider.RemoteItemExistsAsAsync("duplicate"), [Is].EqualTo(DmsResourceItem.FoundItemType.Collection))
        Assert.That(Await provider.RemoteItemExistsAsAsync("duplicate/fixture.bin"), [Is].EqualTo(DmsResourceItem.FoundItemType.File))
        Assert.That(Await provider.RemoteItemExistsAsAsync("/"), [Is].EqualTo(DmsResourceItem.FoundItemType.Root))
        Assert.That(provider.LinkCalls, [Is].Zero)
    End Function

    Private Class TestProvider
        Inherits ScopevisioTeamworkDmsProvider
        Public LinkCalls As Integer
        Public UserCalls As Integer
        Public DownloadLinkCalls As Integer
        Public ResetDuringLinkLookup As Boolean
        Public Sub New(io As Global.CenterDevice.IO.IOClientBase)
            Me.IOClient = io
        End Sub
        Protected Overrides Function LoadNativeDownloadLinkAsync(id As String, cancellationToken As CancellationToken) As Task(Of CenterDevice.Rest.Clients.Link.Link)
            DownloadLinkCalls += 1
            Throw New InvalidOperationException("Directory entry browsing must not fetch download-link details.")
        End Function
        Protected Overrides Function LoadNativeUserSnapshotAsync(id As String, cancellationToken As CancellationToken) As Task(Of DmsUser)
            cancellationToken.ThrowIfCancellationRequested()
            UserCalls += 1
            Return Task.FromResult(New DmsUser With {.ID = id, .DisplayName = "Name " & id, .EMailAddress = id & "@fixture.invalid"})
        End Function
        Protected Overrides Function LoadNativeChildFolderMetadataAsync(directory As Global.CenterDevice.IO.DirectoryInfo, cancellationToken As CancellationToken) As Task(Of List(Of Folder))
            cancellationToken.ThrowIfCancellationRequested()
            Return Task.FromResult(New List(Of Folder) From {
                New FolderWithChildMetadata With {.Id = "empty", .Name = "empty", .Collection = directory.CollectionID, .HasSubFoldersMetadata = False},
                New FolderWithChildMetadata With {.Id = "nested", .Name = "nested", .Collection = directory.CollectionID, .HasSubFoldersMetadata = True},
                New FolderWithChildMetadata With {.Id = "unknown", .Name = "unknown", .Collection = directory.CollectionID}
            })
        End Function
        Protected Overrides Function LoadNativeUploadLinksAsync(cancellationToken As CancellationToken) As Task(Of UploadLinks)
            cancellationToken.ThrowIfCancellationRequested()
            LinkCalls += 1
            If ResetDuringLinkLookup Then Me.IOClient.RootDirectory.ResetDirectoriesCache()
            Dim link As New UploadLink With {.Id = "upload-link", .Collection = "collection-1", .MaxBytes = 5L * 1024L * 1024L * 1024L}
            GetType(UploadLink).GetField("UploadLinkBaseUrl", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).SetValue(link, "https://fixture.invalid/upload/")
            Return Task.FromResult(New UploadLinks With {.UploadLinksList = New List(Of UploadLink) From {link}})
        End Function
    End Class

    Private Class FakeIo
        Inherits Global.CenterDevice.IO.IOClientBase
        Public CollectionCalls As Integer
        Public FileCalls As Integer
        Public BlockCollections As Boolean
        Public IncludeDownloadLink As Boolean
        Public ReadOnly Entered As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Public Sub New()
            MyBase.New(Nothing, "fixture-user")
        End Sub
        Protected Overrides Async Function LookupCollectionsAsync(cancellationToken As CancellationToken) As Task(Of List(Of Collection))
            CollectionCalls += 1
            If BlockCollections Then
                Entered.TrySetResult(True)
                Await Task.Delay(Timeout.Infinite, cancellationToken)
            End If
            Return New List(Of Collection) From {
                New Collection With {.Id = "collection-1", .Name = "duplicate", .Owner = "owner-id", .Link = If(IncludeDownloadLink, "download-link", Nothing), .Users = New CenterDevice.Rest.Clients.Common.Sharings With {.NotVisibleCount = 1, .Visible = New List(Of String) From {"shared-user"}}},
                New Collection With {.Id = "collection-2", .Name = "duplicate"}
            }
        End Function
        Protected Overrides Function LookupChildFoldersAsync(collectionId As String, parentId As String, cancellationToken As CancellationToken) As Task(Of List(Of Folder))
            Return Task.FromResult(New List(Of Folder)())
        End Function
        Protected Overrides Function LookupChildDocumentsAsync(collectionId As String, parentId As String, cancellationToken As CancellationToken) As Task(Of List(Of DocumentFullMetadata))
            FileCalls += 1
            Return Task.FromResult(New List(Of DocumentFullMetadata) From {
                New DocumentFullMetadata With {.Id = "file-1", .Filename = "fixture.bin", .Owner = "owner-id", .Size = 5L * 1024L * 1024L * 1024L},
                New DocumentFullMetadata With {.Id = "file-2", .Filename = "fixture.bin"}
            })
        End Function
    End Class
End Class
