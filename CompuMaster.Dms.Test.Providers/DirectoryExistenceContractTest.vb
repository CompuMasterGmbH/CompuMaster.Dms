Option Strict On

Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class DirectoryExistenceContractTest
    'Missing parents are an ordinary negative existence result, including native and fallback async routes.
    <TestCase(False), TestCase(True)>
    Public Async Function MissingParentsReturnFalse(native As Boolean) As Task
        Dim provider As New NameProvider(native)
        Assert.That(provider.CollectionExists("missing/child"), [Is].False)
        Assert.That(provider.FolderExists("missing/child"), [Is].False)
        Assert.That(Await provider.CollectionExistsAsync("missing/child"), [Is].False)
        Assert.That(Await provider.FolderExistsAsync("missing/child"), [Is].False)
    End Function

    <TestCase(False), TestCase(True)>
    Public Async Function ExistingAndAbsentTargetsRetainNameMatching(native As Boolean) As Task
        Dim provider As New NameProvider(native)
        For Each prefix In {"", "/", "parent/"}
            Assert.That(provider.CollectionExists(prefix & "COLLECTION/"), [Is].True)
            Assert.That(provider.FolderExists(prefix & "FOLDER/"), [Is].True)
            Assert.That(Await provider.CollectionExistsAsync(prefix & "COLLECTION/"), [Is].True)
            Assert.That(Await provider.FolderExistsAsync(prefix & "FOLDER/"), [Is].True)
            Assert.That(provider.CollectionExists(prefix & "absent"), [Is].False)
            Assert.That(provider.FolderExists(prefix & "absent"), [Is].False)
            Assert.That(Await provider.CollectionExistsAsync(prefix & "absent"), [Is].False)
            Assert.That(Await provider.FolderExistsAsync(prefix & "absent"), [Is].False)
        Next
        Assert.That(provider.CollectionExists("folder"), [Is].False)
        Assert.That(provider.FolderExists("collection"), [Is].False)
    End Function

    'A failed lookup is not evidence of absence. Preserve the original failure object.
    <TestCase("permission"), TestCase("transport"), TestCase("server"), TestCase("cancelled"), TestCase("unsupported"), TestCase("authentication")>
    Public Sub ActualFailuresAreNotConvertedToAbsence(kind As String)
        Dim failure As Exception
        Select Case kind
            Case "authentication" : failure = New DmsUserAuthenticationException("fixture")
            Case "permission" : failure = New UnauthorizedAccessException("fixture")
            Case "transport" : failure = New HttpRequestException("fixture")
            Case "cancelled" : failure = New OperationCanceledException()
            Case "unsupported" : failure = New NotSupportedException()
            Case Else : failure = New InvalidOperationException("fixture")
        End Select
        Dim provider As New NameProvider(True) With {.Failure = failure}
        Assert.That(Assert.Catch(Function() provider.CollectionExists("parent/child")), [Is].SameAs(failure))
        Assert.That(Assert.Catch(Function() provider.FolderExists("parent/child")), [Is].SameAs(failure))
        Assert.That(Assert.CatchAsync(Async Function() Await provider.CollectionExistsAsync("parent/child")), [Is].SameAs(failure))
        Assert.That(Assert.CatchAsync(Async Function() Await provider.FolderExistsAsync("parent/child")), [Is].SameAs(failure))
    End Sub

    <Test>
    Public Sub InvalidArgumentsRemainErrors()
        Dim provider As New NameProvider(False)
        Assert.Throws(Of ArgumentNullException)(Function() provider.CollectionExists(Nothing))
        Assert.Throws(Of ArgumentNullException)(Function() provider.FolderExists(Nothing))
        Assert.ThrowsAsync(Of ArgumentNullException)(Async Function() Await provider.CollectionExistsAsync(Nothing))
        Assert.ThrowsAsync(Of ArgumentNullException)(Async Function() Await provider.FolderExistsAsync(Nothing))
    End Sub

    'A cancellation arriving during a negative lookup must not become False.
    <TestCase(False), TestCase(True)>
    Public Sub CancellationDuringMissingParentLookupIsPreserved(native As Boolean)
        Using cancellation As New CancellationTokenSource()
            Dim provider As New NameProvider(native) With {.BeforeLookup = Sub() cancellation.Cancel()}
            Assert.CatchAsync(Of OperationCanceledException)(Async Function() Await provider.CollectionExistsAsync("missing/child", cancellation.Token))
        End Using
        Using cancellation As New CancellationTokenSource()
            Dim provider As New NameProvider(native) With {.BeforeLookup = Sub() cancellation.Cancel()}
            Assert.CatchAsync(Of OperationCanceledException)(Async Function() Await provider.FolderExistsAsync("missing/child", cancellation.Token))
        End Using
    End Sub

    <Test>
    Public Async Function ScopevisioExistingPathsKeepSyncAndAsyncResults() As Task
        Dim provider As New ScopeFixture(True)
        Assert.That(provider.CollectionExists("collection"), [Is].True)
        Assert.That(provider.CollectionExists("/collection/"), [Is].True)
        Assert.That(Await provider.CollectionExistsAsync("collection"), [Is].True)
        Assert.That(provider.FolderExists("collection/folder"), [Is].True)
        Assert.That(Await provider.FolderExistsAsync("collection/folder"), [Is].True)
        Assert.That(provider.FolderExists("collection/absent"), [Is].False)
        Assert.That(Await provider.FolderExistsAsync("collection/absent"), [Is].False)
        Assert.That(provider.CollectionExists("collection/absent"), [Is].False)
        Assert.That(Await provider.CollectionExistsAsync("collection/absent"), [Is].False)
        Assert.That(provider.FolderExists("collection"), [Is].True, "Retain the CenterDevice provider's existing directory-name semantics.")
        Assert.That(Await provider.FolderExistsAsync("collection"), [Is].True)
    End Function

    <Test>
    Public Async Function ScopevisioSdkMissingPathIsNormalizedAndExistenceReturnsFalse() As Task
        Dim provider As New ScopeFixture()
        Dim synchronous = Assert.Throws(Of CompuMaster.Dms.Data.DirectoryNotFoundException)(Function() provider.ListAllRemoteItems("missing", BaseDmsProvider.SearchItemType.Collections))
        Assert.That(synchronous.RemotePath, [Is].EqualTo("missing"))
        Assert.That(synchronous.InnerException, [Is].TypeOf(Of Global.CenterDevice.Model.Exceptions.DirectoryNotFoundException))
        Assert.That(provider.CollectionExists("missing/child"), [Is].False)
        Assert.That(provider.FolderExists("missing/child"), [Is].False)
        Assert.That(provider.CollectionExists("absent"), [Is].False)
        Assert.That(provider.CollectionExists("/absent"), [Is].False)
        Assert.That(Await provider.CollectionExistsAsync("missing/child"), [Is].False)
        Assert.That(Await provider.FolderExistsAsync("missing/child"), [Is].False)
        Dim asynchronous = Assert.ThrowsAsync(Of CompuMaster.Dms.Data.DirectoryNotFoundException)(Async Function() Await provider.ListAllCollectionNamesAsync("missing"))
        Assert.That(asynchronous.InnerException, [Is].TypeOf(Of Global.CenterDevice.Model.Exceptions.DirectoryNotFoundException))
        Dim asynchronousItems = Assert.ThrowsAsync(Of CompuMaster.Dms.Data.DirectoryNotFoundException)(Async Function() Await provider.ListAllRemoteItemsAsync("missing", BaseDmsProvider.SearchItemType.Collections))
        Assert.That(asynchronousItems.RemotePath, [Is].EqualTo("missing"))
        Assert.That(asynchronousItems.InnerException, [Is].TypeOf(Of Global.CenterDevice.Model.Exceptions.DirectoryNotFoundException))
    End Function

    Public Shared Iterator Function WebDavFamilyCases() As IEnumerable(Of TestCaseData)
        For Each baseUrl In {"https://fixture.invalid/", "https://fixture.invalid/remote.php/webdav/", "https://fixture.invalid/remote.php/dav/files/demo/"}
            For Each status In {404, 401, 403, 500}
                Yield New TestCaseData(status, baseUrl)
            Next
        Next
    End Function

    'Generic WebDAV, ownCloud Classic, and Nextcloud use the same provider with different endpoint paths.
    <TestCaseSource(NameOf(WebDavFamilyCases))>
    Public Async Function WebDavDistinguishesMissingParentsFromHttpFailures(status As Integer, baseUrl As String) As Task
        Using client As New HttpClient(New StatusHandler(status))
            Dim provider As New WebDavDmsProvider With {.CustomWebApiUrl = baseUrl}
            GetType(WebDavDmsProvider).GetField("WebDavClient", BindingFlags.NonPublic Or BindingFlags.Instance).SetValue(provider, New WebDav.WebDavClient(client))
            If status = 404 Then
                Assert.That(provider.CollectionExists("missing/child"), [Is].False)
                Assert.That(provider.FolderExists("missing/child"), [Is].False)
                Assert.That(Await provider.CollectionExistsAsync("missing/child"), [Is].False)
                Assert.That(Await provider.FolderExistsAsync("missing/child"), [Is].False)
            Else
                Assert.Catch(Function() provider.CollectionExists("missing/child"))
                Assert.Catch(Function() provider.FolderExists("missing/child"))
                Assert.CatchAsync(Async Function() Await provider.CollectionExistsAsync("missing/child"))
                Assert.CatchAsync(Async Function() Await provider.FolderExistsAsync("missing/child"))
            End If
        End Using
    End Function

    Private Class NameProvider
        Inherits NoDmsProvider
        Private ReadOnly Native As Boolean
        Public Failure As Exception
        Public BeforeLookup As Action
        Sub New(useNative As Boolean)
            Native = useNative
        End Sub
        Private Function Names(path As String, name As String) As List(Of String)
            BeforeLookup?.Invoke()
            If Failure IsNot Nothing Then Throw Failure
            If path = "missing" Then Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(path)
            Return New List(Of String) From {name}
        End Function
        Public Overrides Function ListAllCollectionNames(path As String) As List(Of String)
            Return Names(path, "collection")
        End Function
        Public Overrides Function ListAllFolderNames(path As String) As List(Of String)
            Return Names(path, "folder")
        End Function
        Public Overrides Function ListAllCollectionNamesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of String))
            If Not Native Then Return MyBase.ListAllCollectionNamesAsync(path, cancellationToken)
            cancellationToken.ThrowIfCancellationRequested()
            Return Task.FromResult(Names(path, "collection"))
        End Function
        Public Overrides Function ListAllFolderNamesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of String))
            If Not Native Then Return MyBase.ListAllFolderNamesAsync(path, cancellationToken)
            cancellationToken.ThrowIfCancellationRequested()
            Return Task.FromResult(Names(path, "folder"))
        End Function
    End Class
    Private Class ScopeFixture
        Inherits ScopevisioTeamworkDmsProvider
        Sub New(Optional includeExisting As Boolean = False)
            Me.IOClient = New EmptyIo(includeExisting)
            Me._AllUploadLinks = New Global.CenterDevice.Rest.Clients.Link.UploadLinks With {.UploadLinksList = New List(Of Global.CenterDevice.Rest.Clients.Link.UploadLink)()}
        End Sub
    End Class
    Private Class EmptyIo
        Inherits Global.CenterDevice.IO.IOClientBase
        Sub New(Optional includeExisting As Boolean = False)
            MyBase.New(Nothing, "fixture-user")
            Dim children = GetType(Global.CenterDevice.IO.DirectoryInfo).GetField("getDirectories", BindingFlags.NonPublic Or BindingFlags.Instance)
            If includeExisting Then
                Dim collection As New Global.CenterDevice.IO.DirectoryInfo(Me, Me.RootDirectory, New Global.CenterDevice.Rest.Clients.Collections.Collection With {.Id = "collection-id", .Name = "collection"})
                Dim folder As New Global.CenterDevice.IO.DirectoryInfo(Me, collection, New Global.CenterDevice.Rest.Clients.Folders.Folder With {.Id = "folder-id", .Name = "folder", .Collection = "collection-id"})
                children.SetValue(Me.RootDirectory, New Global.CenterDevice.IO.DirectoryInfo() {collection})
                children.SetValue(collection, New Global.CenterDevice.IO.DirectoryInfo() {folder})
            Else
                children.SetValue(Me.RootDirectory, New Global.CenterDevice.IO.DirectoryInfo() {})
            End If
        End Sub
    End Class
    Private Class StatusHandler
        Inherits HttpMessageHandler
        Private ReadOnly Status As Integer
        Sub New(statusCode As Integer)
            Status = statusCode
        End Sub
        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Return Task.FromResult(New HttpResponseMessage(CType(Status, HttpStatusCode)) With {.Content = New StringContent("")})
        End Function
    End Class
End Class
