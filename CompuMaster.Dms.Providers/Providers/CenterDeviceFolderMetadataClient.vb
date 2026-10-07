Option Explicit On
Option Strict On

Imports System.Net
Imports System.Reflection
Imports System.Text.Json.Serialization
Imports CenterDevice.Rest
Imports CenterDevice.Rest.Clients
Imports CenterDevice.Rest.Clients.OAuth
Imports CenterDevice.Rest.ResponseHandler
Imports RestSharp

Namespace Providers

    'The CenterDevice package's Folder.HasSubFoldersServerInfo property is private, so
    'its default JSON deserializer does not populate the requested server value.
    Partial Friend Class CenterDeviceFolderMetadataClient
        Inherits CenterDevice.Rest.Clients.Folders.FoldersRestClient

        Friend Const ListingFields As String = "collection,id,name,parent,users,groups,link,has-subfolders"

        Friend Sub New(authInfoProvider As IOAuthInfoProvider, configuration As IRestClientConfiguration,
                        errorHandler As IRestClientErrorHandler, apiVersionPrefix As String)
            MyBase.New(authInfoProvider, configuration, errorHandler, apiVersionPrefix)
        End Sub

        Friend Shared Function Create(apiClient As CenterDeviceClientBase) As CenterDeviceFolderMetadataClient
            Return New CenterDeviceFolderMetadataClient(
                ReadClientField(Of IOAuthInfoProvider)(apiClient, "oAuthInfoProvider"),
                ReadClientField(Of IRestClientConfiguration)(apiClient, "configuration"),
                ReadClientField(Of IRestClientErrorHandler)(apiClient, "errorHandler"),
                ReadClientField(Of String)(apiClient, "apiVersionPrefix"))
        End Function

        Friend Shared Function ReadClientField(Of T)(apiClient As CenterDeviceClientBase, name As String) As T
            Dim Field As FieldInfo = GetType(CenterDeviceClientBase).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic)
            If Field Is Nothing Then Throw New MissingFieldException(GetType(CenterDeviceClientBase).FullName, name)
            Return CType(Field.GetValue(apiClient), T)
        End Function

        Friend Function GetFoldersWithMetadata(userId As String, collectionId As String, parentFolderId As String) As List(Of FolderWithChildMetadata)
            Dim Request As RestRequest = Me.CreateRestRequest(Me.ApiVersionPrefix & "folders/", Method.Get)
            If collectionId IsNot Nothing Then Request.AddQueryParameter(CenterDevice.Rest.RestApiConstants.COLLECTION, collectionId)
            If parentFolderId IsNot Nothing Then Request.AddQueryParameter(CenterDevice.Rest.RestApiConstants.PARENT, parentFolderId)
            Request.AddQueryParameter(CenterDevice.Rest.RestApiConstants.FIELDS, ListingFields)

            Dim Response As RestResponse(Of FolderMetadataResponse) = Me.Execute(Of FolderMetadataResponse)(Me.GetOAuthInfo(userId), Request)
            Me.ValidateResponse(Response, New GetFoldersResponseHandler())
            If Response.StatusCode = HttpStatusCode.NoContent Then Return New List(Of FolderWithChildMetadata)
            If Response.StatusCode <> HttpStatusCode.OK Then
                Throw New InvalidOperationException(ProviderStrings.Format("TheFolderListingFailedWithHTTP", CInt(Response.StatusCode).ToString()),
                                                    Response.ErrorException)
            End If
            If Response.Data?.Folders Is Nothing Then Throw New InvalidOperationException(ProviderStrings.GetText("TheFolderListingResponseHasNoFoldersArray"))
            Return Response.Data.Folders
        End Function
    End Class

    Friend NotInheritable Class FolderMetadataResponse
        <JsonPropertyName("folders")>
        Public Property Folders As List(Of FolderWithChildMetadata)
    End Class

    Friend NotInheritable Class FolderWithChildMetadata
        Inherits CenterDevice.Rest.Clients.Folders.Folder

        <JsonPropertyName("has-subfolders")>
        Public Property HasSubFoldersMetadata As Boolean?
    End Class

End Namespace
