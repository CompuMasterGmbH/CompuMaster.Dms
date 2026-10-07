Option Explicit On
Option Strict On

Imports System.Net
Imports System.Text.Json.Serialization
Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest
Imports CenterDevice.Rest.Clients
Imports CenterDevice.Rest.Clients.OAuth
Imports CenterDevice.Rest.Clients.User
Imports CenterDevice.Rest.ResponseHandler
Imports RestSharp

Namespace Providers
    'Adds only internal read mappings; the published SDK and synchronous adapter stay compatible.
    Friend Class CenterDeviceUserMetadataClient
        Inherits CenterDeviceRestClient

        Friend Sub New(auth As IOAuthInfoProvider, configuration As IRestClientConfiguration, errors As IRestClientErrorHandler, prefix As String)
            MyBase.New(auth, configuration, errors, prefix)
        End Sub

        Friend Shared Function Create(client As CenterDeviceClientBase) As CenterDeviceUserMetadataClient
            Return New CenterDeviceUserMetadataClient(
                CenterDeviceFolderMetadataClient.ReadClientField(Of IOAuthInfoProvider)(client, "oAuthInfoProvider"),
                CenterDeviceFolderMetadataClient.ReadClientField(Of IRestClientConfiguration)(client, "configuration"),
                CenterDeviceFolderMetadataClient.ReadClientField(Of IRestClientErrorHandler)(client, "errorHandler"),
                CenterDeviceFolderMetadataClient.ReadClientField(Of String)(client, "apiVersionPrefix"))
        End Function

        Friend Async Function GetUsersAsync(userId As String, cancellationToken As CancellationToken) As Task(Of UserList(Of BaseUserData))
            cancellationToken.ThrowIfCancellationRequested()
            Dim request = Me.CreateRestRequest(Me.ApiVersionPrefix & "users/?status=" & UserStatus.ACTIVE, Method.Get)
            Dim auth = Await Me.GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(False)
            Dim response = Await Me.ExecuteAsync(Of UserList(Of NativeUserNameMetadata))(auth, request, cancellationToken).ConfigureAwait(False)
            cancellationToken.ThrowIfCancellationRequested()
            If response.StatusCode = HttpStatusCode.NoContent Then Return New UserList(Of BaseUserData) With {.Users = New List(Of BaseUserData)()}
            Me.ValidateResponse(response, New StatusCodeResponseHandler(Of UserList(Of NativeUserNameMetadata))(HttpStatusCode.OK))
            If response.Data?.Users Is Nothing Then Throw New InvalidOperationException("The native user response contains no user list.")
            Dim result As New UserList(Of BaseUserData) With {.Users = New List(Of BaseUserData)()}
            For Each userRecord In response.Data.Users
                If userRecord Is Nothing Then Throw New InvalidOperationException("The native user response contains a null user.")
                userRecord.ApplyApiNameFields()
                result.Users.Add(userRecord)
            Next
            Return result
        End Function
    End Class

    Friend Class NativeUserNameMetadata
        Inherits BaseUserData

        <JsonPropertyName("first-name")>
        Public Property ApiFirstName As String

        <JsonPropertyName("last-name")>
        Public Property ApiLastName As String

        Friend Sub ApplyApiNameFields()
            If ApiFirstName IsNot Nothing Then Me.FirstName = ApiFirstName
            If ApiLastName IsNot Nothing Then Me.LastName = ApiLastName
        End Sub
    End Class
End Namespace
