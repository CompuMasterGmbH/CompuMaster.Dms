Option Explicit On
Option Strict On

Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest.Clients.Collections
Imports CenterDevice.Rest.Clients.Documents
Imports CenterDevice.Rest.Clients.Documents.Metadata
Imports CenterDevice.Rest.Clients.Folders
Imports CenterDevice.Rest.Clients.Link
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class CenterDeviceNativeIdentityTest
    <TestCase("collection"), TestCase("folder"), TestCase("file")>
    Public Async Function IdentifierLookupDoesNotSelectAnArbitraryPath(kind As String) As Task
        Dim provider As New IdentityProvider()
        Using cancellation As New CancellationTokenSource()
            Dim item = Await LookupAsync(provider, kind, cancellation.Token)
            Assert.That(provider.RequestId, [Is].EqualTo("requested-id"))
            Assert.That(provider.RequestToken, [Is].EqualTo(cancellation.Token))
            Assert.That(item.FullName, [Is].EqualTo("duplicate"))
            Assert.That(item.Collection, [Is].Empty)
            Assert.That(item.Folder, [Is].Empty)
            Assert.That(item.ExtendedInfosAssignedCollectionID, [Is].Null)
            Assert.That(item.ExtendedInfosAssignedFolderID, [Is].Null)
            Assert.That(provider.SupportsAsynchronousIo, [Is].False)
            Select Case kind
                Case "collection"
                    Assert.That(item.ExtendedInfosCollectionID, [Is].EqualTo("requested-id"))
                    Assert.That(provider.LinkCalls, [Is].EqualTo(1))
                    Assert.That(item.HasChildDirectories, [Is].EqualTo(False))
                Case "folder"
                    Assert.That(item.ExtendedInfosFolderID, [Is].EqualTo("requested-id"))
                    Assert.That(provider.LinkCalls, [Is].Zero)
                    Assert.That(item.HasChildDirectories, [Is].EqualTo(True))
                Case "file"
                    Assert.That(item.ExtendedInfosFileID, [Is].EqualTo("requested-id"))
                    Assert.That(provider.LinkCalls, [Is].Zero)
                    Assert.That(item.ContentLength, [Is].EqualTo(5L * 1024L * 1024L * 1024L))
                    Assert.That(item.ExtendedInfosReferencedFromCollectionIDs, [Is].EqualTo(New String() {"collection-a", "collection-b"}))
                    Assert.That(item.ExtendedInfosReferencedFromFolderIDs, [Is].EqualTo(New String() {"folder-a", "folder-b"}))
                    Assert.That(item.ExtendedInfosHasHiddenUserSharings, [Is].True)
                    Assert.That(item.ExtendedInfosUserSharings(0).User.ID, [Is].EqualTo("shared-user"))
                    Assert.That(item.ExtendedInfosUserSharings(0).User.DisplayName, [Is].EqualTo("Name shared-user"))
                    Assert.That(item.ExtendedInfosUserSharings(0).User.GetDisplayName, [Is].Null)
                    Assert.That(item.ExtendedInfosLinks(0).ID, [Is].EqualTo("link-id"))
                    Assert.That(item.ExtendedInfosLinks(0).DetailsInitialized, [Is].True)
                    Assert.That(item.ExtendedInfosLinks(0).WebUrl, [Is].EqualTo("https://fixture.invalid/view"))
                    Assert.That(item.ExtendedInfosLinks(0).MaxDownloads, [Is].EqualTo(17))
            End Select
        End Using
    End Function

    <TestCase("collection"), TestCase("folder"), TestCase("file")>
    Public Async Function ActiveIdentifierLookupCanBeCanceledAndRetried(kind As String) As Task
        Dim provider As New IdentityProvider With {.BlockRequest = True}
        Using cancellation As New CancellationTokenSource()
            Dim operation = LookupAsync(provider, kind, cancellation.Token)
            Await provider.Entered.Task
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                     Await operation
                                                                 End Function, Func(Of Task)))
        End Using
        provider.BlockRequest = False
        Assert.That(Await LookupAsync(provider, kind, CancellationToken.None), [Is].Not.Null)
    End Function

    <TestCase("collection"), TestCase("folder"), TestCase("file")>
    Public Sub PreCanceledLookupDoesNotStartARequest(kind As String)
        Dim provider As New IdentityProvider()
        Using cancellation As New CancellationTokenSource()
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                     Await LookupAsync(provider, kind, cancellation.Token)
                                                                 End Function, Func(Of Task)))
            Assert.That(provider.RequestId, [Is].Null)
        End Using
    End Sub

    Private Shared Function LookupAsync(provider As IdentityProvider, kind As String, token As CancellationToken) As Task(Of DmsResourceItem)
        Select Case kind
            Case "collection" : Return provider.FindCollectionByIdAsync("requested-id", token)
            Case "folder" : Return provider.FindFolderByIdAsync("requested-id", token)
            Case Else : Return provider.FindFileByIdAsync("requested-id", token)
        End Select
    End Function

    Private Class IdentityProvider
        Inherits ScopevisioTeamworkDmsProvider
        Public RequestId As String
        Public RequestToken As CancellationToken
        Public BlockRequest As Boolean
        Public LinkCalls As Integer
        Public ReadOnly Entered As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Public Sub New()
            Me.IOClient = New FixtureIo()
        End Sub
        Protected Overrides Function LoadNativeUserSnapshotAsync(id As String, cancellationToken As CancellationToken) As Task(Of DmsUser)
            cancellationToken.ThrowIfCancellationRequested()
            Return Task.FromResult(New DmsUser With {.ID = id, .DisplayName = "Name " & id, .EMailAddress = id & "@fixture.invalid"})
        End Function
        Private Async Function RecordRequestAsync(id As String, cancellationToken As CancellationToken) As Task
            RequestId = id
            RequestToken = cancellationToken
            Entered.TrySetResult(True)
            If BlockRequest Then Await Task.Delay(Timeout.Infinite, cancellationToken)
        End Function
        Protected Overrides Async Function LoadNativeCollectionByIdAsync(id As String, cancellationToken As CancellationToken) As Task(Of Collection)
            Await RecordRequestAsync(id, cancellationToken)
            Return New Collection With {.Id = id, .Name = "duplicate", .HasFoldersServerInfo = False}
        End Function
        Protected Overrides Async Function LoadNativeFolderByIdAsync(id As String, cancellationToken As CancellationToken) As Task(Of Folder)
            Await RecordRequestAsync(id, cancellationToken)
            Return New FolderWithChildMetadata With {.Id = id, .Name = "duplicate", .HasSubFoldersMetadata = True}
        End Function
        Protected Overrides Async Function LoadNativeFileByIdAsync(id As String, cancellationToken As CancellationToken) As Task(Of DocumentFullMetadata)
            Await RecordRequestAsync(id, cancellationToken)
            Return New DocumentFullMetadata With {
                .Id = id, .Filename = "duplicate", .Size = 5L * 1024L * 1024L * 1024L, .Link = "link-id",
                .Collections = New SharingInfo With {.Visible = New List(Of String) From {"collection-a", "collection-b"}},
                .Folders = New List(Of String) From {"folder-a", "folder-b"},
                .Users = New CenterDevice.Rest.Clients.Common.Sharings With {.Visible = New List(Of String) From {"shared-user"}, .NotVisibleCount = 1}
            }
        End Function
        Protected Overrides Function LoadNativeDownloadLinkAsync(id As String, cancellationToken As CancellationToken) As Task(Of CenterDevice.Rest.Clients.Link.Link)
            cancellationToken.ThrowIfCancellationRequested()
            Return Task.FromResult(New CenterDevice.Rest.Clients.Link.Link With {.Id = id, .Web = "https://fixture.invalid/view", .AccessControl = New LinkAccessControl With {.MaxDownloads = 17}})
        End Function
        Protected Overrides Function LoadNativeUploadLinksAsync(cancellationToken As CancellationToken) As Task(Of UploadLinks)
            cancellationToken.ThrowIfCancellationRequested()
            LinkCalls += 1
            Return Task.FromResult(New UploadLinks With {.UploadLinksList = New List(Of UploadLink)()})
        End Function
    End Class

    Private Class FixtureIo
        Inherits Global.CenterDevice.IO.IOClientBase
        Public Sub New()
            MyBase.New(Nothing, "fixture-user")
        End Sub
    End Class
End Class
