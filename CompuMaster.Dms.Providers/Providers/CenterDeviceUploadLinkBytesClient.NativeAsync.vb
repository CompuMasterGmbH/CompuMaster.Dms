Option Explicit On
Option Strict On

Imports System.Net
Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest.Clients.Link
Imports CenterDevice.Rest.ResponseHandler
Imports RestSharp

Namespace Providers
    Partial Friend Class CenterDeviceUploadLinkBytesClient
        Friend Async Function CreateCollectionLinkAsync(userId As String, collectionId As String, name As String, expiryDate As DateTime?, maxDocuments As Integer?, maxBytes As Long, password As String, cancellationToken As CancellationToken) As Task(Of UploadLinkCreationResponse)
            cancellationToken.ThrowIfCancellationRequested()
            Dim request = Me.CreateRestRequest(Me.ApiVersionPrefix & "upload-links", Method.Post, CenterDevice.Rest.Clients.ContentType.APPLICATION_JSON)
            request.AddStringBody(CreatePayload(collectionId, name, expiryDate, maxDocuments, maxBytes, password).ToString(), RestSharp.ContentType.Json)
            Dim response = Await Me.ExecuteAsync(Of UploadLinkCreationResponse)(Await Me.GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(False), request, cancellationToken).ConfigureAwait(False)
            Return Me.UnwrapResponse(response, New StatusCodeResponseHandler(Of UploadLinkCreationResponse)(HttpStatusCode.Created))
        End Function

        Friend Async Function UpdateLinkAsync(userId As String, linkId As String, name As String, expiryDate As DateTime?, maxDocuments As Integer?, maxBytes As Long, password As String, cancellationToken As CancellationToken) As Task
            cancellationToken.ThrowIfCancellationRequested()
            Dim request = Me.CreateRestRequest(Me.ApiVersionPrefix & "upload-link/" & linkId, Method.Put, CenterDevice.Rest.Clients.ContentType.APPLICATION_JSON)
            request.AddStringBody(CreatePayload(Nothing, name, expiryDate, maxDocuments, maxBytes, password).ToString(), RestSharp.ContentType.Json)
            Dim response = Await Me.ExecuteAsync(Await Me.GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(False), request, cancellationToken).ConfigureAwait(False)
            Me.ValidateResponse(response, New StatusCodeResponseHandler(HttpStatusCode.NoContent))
        End Function
    End Class
End Namespace
