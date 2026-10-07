Option Explicit On
Option Strict On

Imports System.Net
Imports CenterDevice.Rest
Imports CenterDevice.Rest.Clients.OAuth
Imports CenterDevice.Rest.Clients.Link
Imports CompuMaster.Dms.Providers
Imports Newtonsoft.Json.Linq
Imports NUnit.Framework
Imports RestSharp

<TestFixture, SetUICulture("en")>
Public Class CenterDeviceUploadLinkBytesClientTest

    <Test>
    Public Sub CreatePayloadIncludesUploadByteLimitWithoutNarrowing()
        Dim MaxBytes As Long = 3L * 1073741824L
        Dim Payload = CenterDeviceUploadLinkBytesClient.CreatePayload("collection-id", "upload", Nothing, 7, MaxBytes, "secret")

        Assert.That(Payload.Value(Of String)(RestApiConstants.COLLECTION), [Is].EqualTo("collection-id"))
        Assert.That(Payload.Value(Of Integer)(RestApiConstants.MAX_DOCUMENTS), [Is].EqualTo(7))
        Assert.That(Payload.Value(Of Long)(RestApiConstants.MAX_BYTES), [Is].EqualTo(MaxBytes))
        Assert.That(Payload.Value(Of String)(RestApiConstants.PASSWORD), [Is].EqualTo("secret"))
    End Sub

    <Test>
    Public Sub UpdatePayloadIncludesUploadByteLimitWithoutChangingCollection()
        Dim Payload = CenterDeviceUploadLinkBytesClient.CreatePayload(Nothing, "updated", Nothing, Nothing, 1073741824L, Nothing)

        Assert.That(Payload.ContainsKey(RestApiConstants.COLLECTION), [Is].False)
        Assert.That(Payload.ContainsKey(RestApiConstants.MAX_DOCUMENTS), [Is].False)
        Assert.That(Payload.Value(Of Long)(RestApiConstants.MAX_BYTES), [Is].EqualTo(1073741824L))
        Assert.That(Payload.Value(Of String)(RestApiConstants.NAME), [Is].EqualTo("updated"))
    End Sub

    <Test>
    Public Sub CreateRequestSendsMaxBytesToUploadLinksEndpoint()
        Dim Client As New CapturingUploadLinkClient()

        Client.CreateCollectionLink("user-id", "collection-id", "upload", Nothing, Nothing, 1073741824L, Nothing)

        Assert.That(Client.CapturedRequest.Method, [Is].EqualTo(Method.Post))
        Assert.That(Client.CapturedRequest.Resource, [Is].EqualTo("upload-links"))
        Assert.That(RequestBody(Client.CapturedRequest).Value(Of Long)(RestApiConstants.MAX_BYTES), [Is].EqualTo(1073741824L))
    End Sub

    <Test>
    Public Sub UpdateRequestSendsMaxBytesToSingleUploadLinkEndpoint()
        Dim Client As New CapturingUploadLinkClient()

        Client.UpdateLink("user-id", "link-id", "upload", Nothing, Nothing, 3L * 1073741824L, Nothing)

        Assert.That(Client.CapturedRequest.Method, [Is].EqualTo(Method.Put))
        Assert.That(Client.CapturedRequest.Resource, [Is].EqualTo("upload-link/link-id"))
        Assert.That(RequestBody(Client.CapturedRequest).Value(Of Long)(RestApiConstants.MAX_BYTES), [Is].EqualTo(3L * 1073741824L))
    End Sub

    <TestCase(0L)>
    <TestCase(-1073741824L)>
    <TestCase(1048576L)>
    Public Sub InvalidUploadByteLimitIsRejectedBeforeRequest(maxBytes As Long)
        Dim ErrorResult = Assert.Throws(Of ArgumentOutOfRangeException)(
            Sub() CenterDeviceDmsProviderBase.ValidateUploadLinkMaxBytes(maxBytes))
        Assert.That(ErrorResult.Message, Does.Contain("positive multiple of 1 GiB"))
    End Sub

    <Test>
    Public Sub UnspecifiedUploadByteLimitRemainsAllowed()
        Assert.DoesNotThrow(Sub() CenterDeviceDmsProviderBase.ValidateUploadLinkMaxBytes(Nothing))
    End Sub

    Private Shared Function RequestBody(request As RestRequest) As JObject
        Dim Body = request.Parameters.OfType(Of BodyParameter)().Single()
        Return JObject.Parse(CStr(Body.Value))
    End Function

    Private NotInheritable Class CapturingUploadLinkClient
        Inherits CenterDeviceUploadLinkBytesClient

        Public Property CapturedRequest As RestRequest

        Public Sub New()
            MyBase.New(New TestOAuthInfoProvider(), New TestRestClientConfiguration(), Nothing, String.Empty)
        End Sub

        Protected Overrides Function Execute(Of T As New)(oAuthInfo As OAuthInfo, request As RestRequest) As RestResponse(Of T)
            Me.CapturedRequest = request
            Return New RestResponse(Of T)(request) With {
                .StatusCode = HttpStatusCode.Created,
                .Data = CType(CType(New UploadLinkCreationResponse With {.Id = "link-id"}, Object), T)
            }
        End Function

        Protected Overrides Function Execute(oAuthInfo As OAuthInfo, request As RestRequest) As RestResponse
            Me.CapturedRequest = request
            Return New RestResponse(request) With {.StatusCode = HttpStatusCode.NoContent}
        End Function
    End Class

    Private NotInheritable Class TestOAuthInfoProvider
        Implements IOAuthInfoProvider

        Public Function GetOAuthInfo(userId As String) As OAuthInfo Implements IOAuthInfoProvider.GetOAuthInfo
            Return New OAuthInfo With {.UserId = userId, .access_token = "test-token"}
        End Function
    End Class

    Private NotInheritable Class TestRestClientConfiguration
        Implements IRestClientConfiguration

        Public ReadOnly Property BaseAddress As String Implements IRestClientConfiguration.BaseAddress
            Get
                Return "https://example.invalid/"
            End Get
        End Property

        Public ReadOnly Property UserAgent As String Implements IRestClientConfiguration.UserAgent
            Get
                Return "Dms isolated test"
            End Get
        End Property
    End Class

End Class
