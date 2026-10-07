Option Explicit On
Option Strict On

Imports System.IO
Imports System.Linq
Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest.Clients.Collections
Imports CenterDevice.Rest.Clients.Folders
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class CenterDeviceNativeCreationTest
    <TestCase("directory", "new", False), TestCase("directory", "collection/new", False), TestCase("folder", "collection/new", True), TestCase("collection", "new", True), TestCase("folder", "new", True)>
    Public Async Function CreationRetainsExplicitAndDefaultDirectoryKinds(operation As String, remotePath As String, explicitStyle As Boolean) As Task
        Dim provider As New CreationProvider()
        Using cancellation As New CancellationTokenSource()
            Await CreateAsync(provider, operation, remotePath, cancellation.Token)
            Assert.That(provider.CreatedNames, [Is].EqualTo(New String() {"new"}))
            Assert.That(provider.Styles(0).HasValue, [Is].EqualTo(explicitStyle))
            If explicitStyle Then Assert.That(provider.Styles(0).Value, [Is].EqualTo(If(operation = "collection", Global.CenterDevice.IO.DirectoryInfo.DirectoryType.Collection, Global.CenterDevice.IO.DirectoryInfo.DirectoryType.Folder)))
            Assert.That(provider.RequestToken, [Is].EqualTo(cancellation.Token))
            Assert.That(provider.SupportsAsynchronousIo, [Is].False)
        End Using
    End Function

    <Test>
    Public Sub NestedCollectionsAreRejectedBeforeDispatch()
        Dim provider As New CreationProvider()
        Assert.ThrowsAsync(Of NotSupportedException)(CType(Async Function()
                                                              Await provider.CreateCollectionAsync("collection/nested")
                                                          End Function, Func(Of Task)))
        Assert.That(provider.CreatedNames, [Is].Empty)
        Assert.That(provider.Io.CollectionCalls, [Is].Zero)
    End Sub

    <TestCase("folder"), TestCase("directory")>
    Public Async Function OptionalParentCreationIsSequentialAndKeepsFolderSemantics(operation As String) As Task
        Dim provider As New CreationProvider()
        If operation = "folder" Then
            Await provider.CreateFolderAsync("collection/a/b", True)
        Else
            Await provider.CreateDirectoryAsync("collection/a/b", True)
        End If
        Assert.That(provider.CreatedPaths, [Is].EqualTo(New String() {"collection/a", "collection/a/b"}))
        Assert.That(provider.Styles, [Is].All.EqualTo(Global.CenterDevice.IO.DirectoryInfo.DirectoryType.Folder))
        Assert.That(Await provider.RemoteItemExistsAsync("collection/a/b"), [Is].True)
    End Function

    <Test>
    Public Sub MissingParentFailsWithoutCreatingWhenRecursionIsDisabled()
        Dim provider As New CreationProvider()
        Assert.ThrowsAsync(Of CompuMaster.Dms.Data.DirectoryNotFoundException)(CType(Async Function()
                                                                                        Await provider.CreateFolderAsync("collection/a/b", False)
                                                                                    End Function, Func(Of Task)))
        Assert.That(provider.CreatedNames, [Is].Empty)
    End Sub

    <TestCase("bytes"), TestCase("factory"), TestCase("file")>
    Public Async Function UploadWithOptionalParentCreationUsesNativeComposition(input As String) As Task
        Dim provider As New CreationProvider()
        Dim localPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        Try
            File.WriteAllBytes(localPath, New Byte() {1, 2})
            Select Case input
                Case "bytes" : Await provider.UploadFileAsync("collection/new/fixture", New Byte() {1, 2}, True)
                Case "factory" : Await provider.UploadFileAsync("collection/new/fixture", Function() New MemoryStream(New Byte() {1, 2}), True)
                Case Else : Await provider.UploadFileAsync("collection/new/fixture", localPath, True)
            End Select
            Assert.That(provider.CreatedPaths, [Is].EqualTo(New String() {"collection/new"}))
            Assert.That(provider.UploadParent, [Is].EqualTo("collection/new"))
            Assert.That(provider.UploadedLength, [Is].EqualTo(2))
        Finally
            If File.Exists(localPath) Then File.Delete(localPath)
        End Try
    End Function

    <TestCase("bytes"), TestCase("factory"), TestCase("file")>
    Public Async Function UploadPreservesTheExistingRecursiveInputDifference(input As String) As Task
        Dim provider As New CreationProvider()
        If input = "bytes" Then
            Await provider.UploadFileAsync("collection/a/b/fixture", New Byte() {1}, True)
            Assert.That(provider.CreatedPaths, [Is].EqualTo(New String() {"collection/a", "collection/a/b"}))
        Else
            Assert.ThrowsAsync(Of CompuMaster.Dms.Data.DirectoryNotFoundException)(CType(Async Function()
                                                                                            If input = "factory" Then
                                                                                                Await provider.UploadFileAsync("collection/a/b/fixture", Function() New MemoryStream(), True)
                                                                                            Else
                                                                                                Await provider.UploadFileAsync("collection/a/b/fixture", "unused", True)
                                                                                            End If
                                                                                        End Function, Func(Of Task)))
            Assert.That(provider.CreatedNames, [Is].Empty)
            Assert.That(provider.UploadParent, [Is].Null)
        End If
    End Function

    <TestCase(False), TestCase(True)>
    Public Async Function FailedOrCanceledCreationInvalidatesTheParentCache(cancel As Boolean) As Task
        Dim provider As New CreationProvider With {.BlockCreation = cancel, .CreateFailure = If(cancel, Nothing, New CenterDevice.Rest.Exceptions.RestClientException("Server failure."))}
        Await provider.ListAllCollectionNamesAsync("/")
        Using cancellation As New CancellationTokenSource()
            Dim operation = provider.CreateDirectoryAsync("new", cancellation.Token)
            If cancel Then
                Await provider.Entered.Task
                cancellation.Cancel()
                Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                         Await operation
                                                                     End Function, Func(Of Task)))
            Else
                Dim failure = Assert.ThrowsAsync(Of IOException)(CType(Async Function()
                                                                          Await operation
                                                                      End Function, Func(Of Task)))
                Assert.That(failure.InnerException, [Is].SameAs(provider.CreateFailure))
            End If
            Assert.That(provider.CreatedNames.Count, [Is].EqualTo(1), "An uncertain mutation is not replayed.")
            Await provider.ListAllCollectionNamesAsync("/")
            Assert.That(provider.Io.CollectionCalls, [Is].EqualTo(2))
        End Using
    End Function

    <Test>
    Public Sub PreCanceledCreationDoesNotDispatch()
        Dim provider As New CreationProvider()
        Using cancellation As New CancellationTokenSource()
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                     Await provider.CreateFolderAsync("collection/new", True, cancellation.Token)
                                                                 End Function, Func(Of Task)))
            Assert.That(provider.CreatedNames, [Is].Empty)
            Assert.That(provider.Io.CollectionCalls, [Is].Zero)
        End Using
    End Sub

    <Test>
    Public Async Function CreatingAChildRefreshesTheKnownEmptyCollectionHint() As Task
        Dim provider As New CreationProvider()
        provider.Io.Collections(0).HasFoldersServerInfo = False
        Assert.That((Await provider.ListRemoteItemAsync("collection")).HasChildDirectories, [Is].EqualTo(False))
        Await provider.CreateFolderAsync("collection/new")
        Assert.That((Await provider.ListRemoteItemAsync("collection")).HasChildDirectories, [Is].EqualTo(True))
    End Function

    Private Shared Function CreateAsync(provider As CreationProvider, operation As String, remotePath As String, token As CancellationToken) As Task
        Select Case operation
            Case "folder" : Return provider.CreateFolderAsync(remotePath, token)
            Case "collection" : Return provider.CreateCollectionAsync(remotePath, token)
            Case Else : Return provider.CreateDirectoryAsync(remotePath, token)
        End Select
    End Function

    Private Class CreationProvider
        Inherits ScopevisioTeamworkDmsProvider
        Public ReadOnly Io As New FixtureIo()
        Public ReadOnly CreatedNames As New List(Of String)()
        Public ReadOnly CreatedPaths As New List(Of String)()
        Public ReadOnly Styles As New List(Of Global.CenterDevice.IO.DirectoryInfo.DirectoryType?)()
        Public RequestToken As CancellationToken
        Public BlockCreation As Boolean
        Public CreateFailure As Exception
        Public UploadParent As String
        Public UploadedLength As Long
        Public ReadOnly Entered As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Public Sub New()
            Me.IOClient = Io
        End Sub
        Protected Overrides Async Function CreateNativeDirectoryAsync(parent As Global.CenterDevice.IO.DirectoryInfo, name As String, style As Global.CenterDevice.IO.DirectoryInfo.DirectoryType?, cancellationToken As CancellationToken) As Task
            CreatedNames.Add(name)
            CreatedPaths.Add(Me.CombinePath(parent.FullName, name).TrimStart("/"c))
            Styles.Add(style)
            RequestToken = cancellationToken
            Entered.TrySetResult(True)
            If BlockCreation Then Await Task.Delay(Timeout.Infinite, cancellationToken)
            If CreateFailure IsNot Nothing Then Throw CreateFailure
            If parent.IsRootDirectory Then
                If Not style.HasValue OrElse style.Value = Global.CenterDevice.IO.DirectoryInfo.DirectoryType.Collection Then Io.Collections.Add(New Collection With {.Id = name, .Name = name})
            Else
                Io.Folders.Add(New Folder With {.Id = "folder-" & Io.Folders.Count.ToString(), .Name = name, .Collection = parent.AssociatedCollection.CollectionID, .Parent = parent.FolderID})
                Io.Collections.Find(Function(collection) collection.Id = parent.AssociatedCollection.CollectionID).HasFoldersServerInfo = True
            End If
        End Function
        Protected Overrides Function LoadNativeUploadLinksAsync(cancellationToken As CancellationToken) As Task(Of CenterDevice.Rest.Clients.Link.UploadLinks)
            Return Task.FromResult(New CenterDevice.Rest.Clients.Link.UploadLinks With {.UploadLinksList = New List(Of CenterDevice.Rest.Clients.Link.UploadLink)()})
        End Function
        Protected Overrides Async Function UploadNativeFileAsync(parent As Global.CenterDevice.IO.DirectoryInfo, existingFile As Global.CenterDevice.IO.FileInfo, fileName As String, binaryData As Func(Of Stream), cancellationToken As CancellationToken) As Task
            UploadParent = parent.FullName.TrimStart("/"c)
            Using source = binaryData()
                UploadedLength = source.Length
                Await source.CopyToAsync(Stream.Null, 81920, cancellationToken)
            End Using
        End Function
    End Class

    Private Class FixtureIo
        Inherits Global.CenterDevice.IO.IOClientBase
        Public ReadOnly Collections As New List(Of Collection) From {New Collection With {.Id = "collection-id", .Name = "collection"}}
        Public ReadOnly Folders As New List(Of Folder)()
        Public CollectionCalls As Integer
        Public Sub New()
            MyBase.New(Nothing, "fixture-user")
        End Sub
        Protected Overrides Function LookupCollectionsAsync(cancellationToken As CancellationToken) As Task(Of List(Of Collection))
            CollectionCalls += 1
            'Model independent server snapshots so mutation does not silently update old client metadata.
            Return Task.FromResult(Collections.Select(Function(collection) New Collection With {.Id = collection.Id, .Name = collection.Name, .HasFoldersServerInfo = collection.HasFoldersServerInfo}).ToList())
        End Function
        Protected Overrides Function LookupChildFoldersAsync(collectionId As String, parentId As String, cancellationToken As CancellationToken) As Task(Of List(Of Folder))
            Return Task.FromResult(Folders.Where(Function(folder) (collectionId Is Nothing OrElse folder.Collection = collectionId) AndAlso folder.Parent = If(parentId = CenterDevice.Rest.RestApiConstants.NONE, Nothing, parentId)).ToList())
        End Function
        Protected Overrides Function LookupChildDocumentsAsync(collectionId As String, parentId As String, cancellationToken As CancellationToken) As Task(Of List(Of CenterDevice.Rest.Clients.Documents.Metadata.DocumentFullMetadata))
            Return Task.FromResult(New List(Of CenterDevice.Rest.Clients.Documents.Metadata.DocumentFullMetadata)())
        End Function
    End Class
End Class
