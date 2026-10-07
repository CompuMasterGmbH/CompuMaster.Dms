Option Explicit On
Option Strict On

Imports System.Net
Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest
Imports CenterDevice.Rest.Clients.Link
Imports CenterDevice.Rest.Clients.OAuth
Imports CompuMaster.Dms.Providers
Imports Newtonsoft.Json.Linq
Imports NUnit.Framework
Imports RestSharp

<TestFixture>
Public Class CenterDeviceNativeUploadLinkBytesTest
    <Test>
    Public Async Function NativeCreatePreserves64BitMaxBytesAndCancellation() As Task
        Dim client As New CapturingClient()
        Using cancellation As New CancellationTokenSource()
            Dim response = Await client.CreateCollectionLinkAsync("user", "selected-collection", "name", Nothing, 7, 5L * 1073741824L, "password", cancellation.Token)
            Assert.That(response.Id, [Is].EqualTo("link-id"))
            Assert.That(client.Request.Resource, [Is].EqualTo("upload-links"))
            Assert.That(client.Request.Method, [Is].EqualTo(Method.Post))
            Assert.That(Body(client.Request).Value(Of Long)(RestApiConstants.MAX_BYTES), [Is].EqualTo(5L * 1073741824L))
            Assert.That(Body(client.Request).Value(Of String)(RestApiConstants.COLLECTION), [Is].EqualTo("selected-collection"))
            Assert.That(client.Token, [Is].EqualTo(cancellation.Token))
        End Using
    End Function

    <Test>
    Public Async Function NativeUpdatePreserves64BitMaxBytesWithoutChangingCollection() As Task
        Dim client As New CapturingClient()
        Await client.UpdateLinkAsync("user", "selected-link", "name", Nothing, 7, 5L * 1073741824L, "password", CancellationToken.None)
        Assert.That(client.Request.Resource, [Is].EqualTo("upload-link/selected-link"))
        Assert.That(client.Request.Method, [Is].EqualTo(Method.Put))
        Assert.That(Body(client.Request).Value(Of Long)(RestApiConstants.MAX_BYTES), [Is].EqualTo(5L * 1073741824L))
        Assert.That(Body(client.Request).ContainsKey(RestApiConstants.COLLECTION), [Is].False)
    End Function

    <Test>
    Public Sub CanceledWriteCannotReachTheAsyncTransport()
        Dim client As New CapturingClient()
        Using cancellation As New CancellationTokenSource()
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                       Await client.UpdateLinkAsync("user", "link-id", "name", Nothing, Nothing, 1073741824L, Nothing, cancellation.Token)
                                                                   End Function, Func(Of Task)))
        End Using
        Assert.That(client.Request, [Is].Null)
    End Sub

    Private Shared Function Body(request As RestRequest) As JObject
        Return JObject.Parse(CStr(request.Parameters.OfType(Of BodyParameter)().Single().Value))
    End Function

    Private Class CapturingClient
        Inherits CenterDeviceUploadLinkBytesClient
        Public Request As RestRequest
        Public Token As CancellationToken
        Public Sub New()
            MyBase.New(New FixtureAuthorization(), New FixtureConfiguration(), Nothing, String.Empty)
        End Sub
        Protected Overrides Function Execute(Of T As New)(info As OAuthInfo, request As RestRequest) As RestResponse(Of T)
            Throw New InvalidOperationException("Synchronous execution is forbidden in this fixture.")
        End Function
        Protected Overrides Function Execute(info As OAuthInfo, request As RestRequest) As RestResponse
            Throw New InvalidOperationException("Synchronous execution is forbidden in this fixture.")
        End Function
        Protected Overrides Function ExecuteAsync(Of T As New)(info As OAuthInfo, request As RestRequest, Optional cancellationToken As CancellationToken = Nothing) As Task(Of RestResponse(Of T))
            Me.Request = request
            Token = cancellationToken
            Return Task.FromResult(New RestResponse(Of T)(request) With {.StatusCode = HttpStatusCode.Created, .Data = CType(CObj(New UploadLinkCreationResponse With {.Id = "link-id"}), T)})
        End Function
        Protected Overrides Function ExecuteAsync(info As OAuthInfo, request As RestRequest, Optional cancellationToken As CancellationToken = Nothing) As Task(Of RestResponse)
            Me.Request = request
            Token = cancellationToken
            Return Task.FromResult(New RestResponse(request) With {.StatusCode = HttpStatusCode.NoContent})
        End Function
    End Class

    Private Class FixtureAuthorization
        Implements IOAuthInfoProvider
        Public Function GetOAuthInfo(userId As String) As OAuthInfo Implements IOAuthInfoProvider.GetOAuthInfo
            Return New OAuthInfo With {.UserId = userId, .access_token = "fixture-token"}
        End Function
    End Class

    Private Class FixtureConfiguration
        Implements IRestClientConfiguration
        Public ReadOnly Property BaseAddress As String Implements IRestClientConfiguration.BaseAddress
            Get
                Return "https://fixture.invalid/"
            End Get
        End Property
        Public ReadOnly Property UserAgent As String Implements IRestClientConfiguration.UserAgent
            Get
                Return "Native isolated fixture"
            End Get
        End Property
    End Class
End Class
