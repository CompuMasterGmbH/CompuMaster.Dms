Option Explicit On
Option Strict On

Imports System.Net
Imports System.Linq
Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest
Imports CenterDevice.Rest.Clients
Imports CenterDevice.Rest.Clients.OAuth
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports RestSharp

<TestFixture, SetUICulture("en")>
Public Class CenterDeviceNativeMetadataTest
    <Test>
    Public Async Function MetadataListingUsesAsyncAuthorizationAndPreservesRequestFields() As Task
        Dim auth As New AsyncAuthorization()
        Dim client As New MetadataClient(auth)
        Using cancellation As New CancellationTokenSource()
            Dim folders = Await client.GetFoldersWithMetadataAsync("fixture-user", "collection", "none", cancellation.Token)
            Assert.That(auth.AsyncCalls, [Is].EqualTo(1))
            Assert.That(client.RequestToken, [Is].EqualTo(cancellation.Token))
            Assert.That(client.Request.Resource, [Is].EqualTo("v2/folders/"))
            Assert.That(client.Request.Method, [Is].EqualTo(Method.Get))
            Assert.That(client.Request.Parameters.Any(Function(parameter) parameter.Name = "fields" AndAlso parameter.Value.ToString().Contains("has-subfolders")), [Is].True)
            Assert.That(client.Request.Parameters.Any(Function(parameter) parameter.Name = "collection" AndAlso parameter.Value.ToString() = "collection"), [Is].True)
            Assert.That(folders(0).HasSubFoldersMetadata, [Is].EqualTo(False))
        End Using
    End Function

    <Test>
    Public Async Function EmptyMetadataResponsesReturnAnEmptyList() As Task
        Dim client As New MetadataClient(New AsyncAuthorization()) With {.Status = HttpStatusCode.NoContent}
        Assert.That(Await client.GetFoldersWithMetadataAsync("user", Nothing, "parent", CancellationToken.None), [Is].Empty)
    End Function

    <Test>
    Public Sub MissingFoldersArrayFailsWithAnExplicitDiagnostic()
        Dim client As New MetadataClient(New AsyncAuthorization()) With {.MissingFolders = True}
        Dim exception = Assert.ThrowsAsync(Of InvalidOperationException)(CType(Async Function()
                                                                                  Await client.GetFoldersWithMetadataAsync("user", Nothing, "parent", CancellationToken.None)
                                                                              End Function, Func(Of Task)))
        Assert.That(exception.Message, Does.Contain("no folders array"))
    End Sub

    <Test>
    Public Async Function ActiveMetadataCancellationReachesTheRequest() As Task
        Dim client As New MetadataClient(New AsyncAuthorization()) With {.BlockRequest = True}
        Using cancellation As New CancellationTokenSource()
            Dim operation = client.GetFoldersWithMetadataAsync("user", Nothing, "parent", cancellation.Token)
            Await client.Entered.Task
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                     Await operation
                                                                 End Function, Func(Of Task)))
        End Using
    End Function

    Private Class MetadataClient
        Inherits CenterDeviceFolderMetadataClient
        Public Status As HttpStatusCode = HttpStatusCode.OK
        Public MissingFolders As Boolean
        Public BlockRequest As Boolean
        Public Request As RestRequest
        Public RequestToken As CancellationToken
        Public ReadOnly Entered As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Public Sub New(auth As IOAuthInfoProvider)
            MyBase.New(auth, New Configuration(), Nothing, "v2/")
        End Sub
        Protected Overrides Async Function ExecuteAsync(Of T As New)(authorization As OAuthInfo, request As RestRequest, Optional cancellationToken As CancellationToken = Nothing) As Task(Of RestResponse(Of T))
            Me.Request = request
            Me.RequestToken = cancellationToken
            If BlockRequest Then
                Entered.TrySetResult(True)
                Await Task.Delay(Timeout.Infinite, cancellationToken)
            End If
            Dim data As New FolderMetadataResponse()
            If Not MissingFolders Then data.Folders = New List(Of FolderWithChildMetadata) From {New FolderWithChildMetadata With {.Id = "empty", .HasSubFoldersMetadata = False}}
            Return New RestResponse(Of T)(request) With {.StatusCode = Status, .Data = CType(CObj(data), T)}
        End Function
    End Class

    Private Class AsyncAuthorization
        Implements IAsyncOAuthInfoProvider
        Public AsyncCalls As Integer
        Public Function GetOAuthInfo(userId As String) As OAuthInfo Implements IOAuthInfoProvider.GetOAuthInfo
            Throw New AssertionException("Synchronous authorization must not be used.")
        End Function
        Public Function GetOAuthInfoAsync(userId As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of OAuthInfo) Implements IAsyncOAuthInfoProvider.GetOAuthInfoAsync
            cancellationToken.ThrowIfCancellationRequested()
            AsyncCalls += 1
            Return Task.FromResult(New OAuthInfo With {.UserId = userId, .access_token = "fixture-token"})
        End Function
    End Class

    Private Class Configuration
        Implements IRestClientConfiguration
        Public ReadOnly Property BaseAddress As String = "https://fixture.invalid/" Implements IRestClientConfiguration.BaseAddress
        Public ReadOnly Property UserAgent As String = "isolated-native-metadata" Implements IRestClientConfiguration.UserAgent
    End Class
End Class
