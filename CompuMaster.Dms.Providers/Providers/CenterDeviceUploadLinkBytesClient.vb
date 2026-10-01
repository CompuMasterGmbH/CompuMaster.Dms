Option Explicit On
Option Strict On

Imports System.Net
Imports System.Reflection
Imports CenterDevice.Rest
Imports CenterDevice.Rest.Clients
Imports CenterDevice.Rest.Clients.Link
Imports CenterDevice.Rest.Clients.OAuth
Imports CenterDevice.Rest.ResponseHandler
Imports Newtonsoft.Json.Linq
Imports RestSharp

Namespace Providers

    'The current CenterDevice SDK reads max-bytes but omits it from upload-link writes.
    Friend Class CenterDeviceUploadLinkBytesClient
        Inherits CenterDeviceRestClient

        Friend Sub New(authInfoProvider As IOAuthInfoProvider, configuration As IRestClientConfiguration,
                        errorHandler As IRestClientErrorHandler, apiVersionPrefix As String)
            MyBase.New(authInfoProvider, configuration, errorHandler, apiVersionPrefix)
        End Sub

        Friend Shared Function Create(apiClient As CenterDeviceClientBase) As CenterDeviceUploadLinkBytesClient
            Return New CenterDeviceUploadLinkBytesClient(
                ReadClientField(Of IOAuthInfoProvider)(apiClient, "oAuthInfoProvider"),
                ReadClientField(Of IRestClientConfiguration)(apiClient, "configuration"),
                ReadClientField(Of IRestClientErrorHandler)(apiClient, "errorHandler"),
                ReadClientField(Of String)(apiClient, "apiVersionPrefix"))
        End Function

        Private Shared Function ReadClientField(Of T)(apiClient As CenterDeviceClientBase, name As String) As T
            Dim Field As FieldInfo = GetType(CenterDeviceClientBase).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic)
            If Field Is Nothing Then Throw New MissingFieldException(GetType(CenterDeviceClientBase).FullName, name)
            Return CType(Field.GetValue(apiClient), T)
        End Function

        Friend Function CreateCollectionLink(userId As String, collectionId As String, name As String,
                                             expiryDate As DateTime?, maxDocuments As Integer?, maxBytes As Long,
                                             password As String) As UploadLinkCreationResponse
            Dim Request As RestRequest = Me.CreateRestRequest(Me.ApiVersionPrefix & "upload-links", Method.Post, CenterDevice.Rest.Clients.ContentType.APPLICATION_JSON)
            Request.AddStringBody(CreatePayload(collectionId, name, expiryDate, maxDocuments, maxBytes, password).ToString(), RestSharp.ContentType.Json)
            Dim Response As RestResponse(Of UploadLinkCreationResponse) = Me.Execute(Of UploadLinkCreationResponse)(Me.GetOAuthInfo(userId), Request)
            Return Me.UnwrapResponse(Response, New StatusCodeResponseHandler(Of UploadLinkCreationResponse)(HttpStatusCode.Created))
        End Function

        Friend Sub UpdateLink(userId As String, linkId As String, name As String,
                              expiryDate As DateTime?, maxDocuments As Integer?, maxBytes As Long,
                              password As String)
            Dim Request As RestRequest = Me.CreateRestRequest(Me.ApiVersionPrefix & "upload-link/" & linkId, Method.Put, CenterDevice.Rest.Clients.ContentType.APPLICATION_JSON)
            Request.AddStringBody(CreatePayload(Nothing, name, expiryDate, maxDocuments, maxBytes, password).ToString(), RestSharp.ContentType.Json)
            Dim Response As RestResponse = Me.Execute(Me.GetOAuthInfo(userId), Request)
            Me.ValidateResponse(Response, New StatusCodeResponseHandler(HttpStatusCode.NoContent))
        End Sub

        Friend Shared Function CreatePayload(collectionId As String, name As String, expiryDate As DateTime?,
                                             maxDocuments As Integer?, maxBytes As Long, password As String) As JObject
            Dim Payload As New JObject()
            If collectionId IsNot Nothing Then Payload(RestApiConstants.COLLECTION) = collectionId
            If name IsNot Nothing Then Payload(RestApiConstants.NAME) = name
            If expiryDate.HasValue Then Payload(RestApiConstants.EXPIRY_DATE) = expiryDate.Value
            If maxDocuments.HasValue Then Payload(RestApiConstants.MAX_DOCUMENTS) = maxDocuments.Value
            Payload(RestApiConstants.MAX_BYTES) = maxBytes
            If password IsNot Nothing Then Payload(RestApiConstants.PASSWORD) = password
            Return Payload
        End Function

    End Class

End Namespace
