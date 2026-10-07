Option Explicit On
Option Strict On

Imports System.Net
Imports System.Threading
Imports System.Threading.Tasks
Imports RestSharp
Imports CenterDevice.Rest.ResponseHandler

Namespace Providers
    Partial Friend Class CenterDeviceFolderMetadataClient
        Friend Async Function GetFoldersWithMetadataAsync(userId As String, collectionId As String, parentFolderId As String, cancellationToken As CancellationToken) As Task(Of List(Of FolderWithChildMetadata))
            cancellationToken.ThrowIfCancellationRequested()
            Dim request = Me.CreateRestRequest(Me.ApiVersionPrefix & "folders/", Method.Get)
            If collectionId IsNot Nothing Then request.AddQueryParameter(CenterDevice.Rest.RestApiConstants.COLLECTION, collectionId)
            If parentFolderId IsNot Nothing Then request.AddQueryParameter(CenterDevice.Rest.RestApiConstants.PARENT, parentFolderId)
            request.AddQueryParameter(CenterDevice.Rest.RestApiConstants.FIELDS, ListingFields)
            Dim authorization = Await Me.GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(False)
            Dim response = Await Me.ExecuteAsync(Of FolderMetadataResponse)(authorization, request, cancellationToken).ConfigureAwait(False)
            Me.ValidateResponse(response, New GetFoldersResponseHandler())
            If response.StatusCode = HttpStatusCode.NoContent Then Return New List(Of FolderWithChildMetadata)
            If response.StatusCode <> HttpStatusCode.OK Then
                Throw New InvalidOperationException("The folder listing failed with HTTP " & CInt(response.StatusCode).ToString() & ".", response.ErrorException)
            End If
            If response.Data?.Folders Is Nothing Then Throw New InvalidOperationException("The folder listing response has no folders array.")
            Return response.Data.Folders
        End Function
    End Class
End Namespace
