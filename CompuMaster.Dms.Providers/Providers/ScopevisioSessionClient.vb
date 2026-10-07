Imports System.Net
Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest.Clients
Imports CenterDevice.Rest.Clients.OAuth
Imports CompuMaster.Scopevisio.CenterDeviceApi
Imports CompuMaster.Scopevisio.OpenApi
Imports CompuMaster.Scopevisio.OpenApi.Model

Namespace Providers
    'Adapts legacy synchronous entry points to the same SDK token owner and refresh gate.
    Friend NotInheritable Class ScopevisioSessionAuthorization
        Implements IOAuthInfoProvider, IAsyncOAuthInfoProvider, IRestClientErrorHandler, IAsyncRestClientErrorHandler
        Private ReadOnly Client As OpenScopeApiClient
        Private ReadOnly Information As TeamworkOAuthInfoProvider
        Private ReadOnly Errors As TeamworkClientErrorHandler

        Friend Sub New(client As OpenScopeApiClient, account As AccountInfo)
            Me.Client = client
            Information = New TeamworkOAuthInfoProvider(client)
            Errors = New TeamworkClientErrorHandler(account.User.Login, Information)
        End Sub

        Public Function GetOAuthInfo(userId As String) As OAuthInfo Implements IOAuthInfoProvider.GetOAuthInfo
            Return GetOAuthInfoAsync(userId, CancellationToken.None).ConfigureAwait(False).GetAwaiter().GetResult()
        End Function

        Public Function GetOAuthInfoAsync(userId As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of OAuthInfo) Implements IAsyncOAuthInfoProvider.GetOAuthInfoAsync
            Return Information.GetOAuthInfoAsync(userId, cancellationToken)
        End Function

        Public Function RefreshToken(info As OAuthInfo) As OAuthInfo Implements IRestClientErrorHandler.RefreshToken
            Return RefreshTokenAsync(info, CancellationToken.None).ConfigureAwait(False).GetAwaiter().GetResult()
        End Function

        Public Async Function RefreshTokenAsync(info As OAuthInfo, Optional cancellationToken As CancellationToken = Nothing) As Task(Of OAuthInfo) Implements IAsyncRestClientErrorHandler.RefreshTokenAsync
            cancellationToken.ThrowIfCancellationRequested()
            If String.IsNullOrEmpty(Client.Token?.RefreshToken) Then
                Throw New Data.DmsUserAuthenticationException(ProviderStrings.GetText("SessionReauthorizationRequired"), New InvalidOperationException("The session has no refresh credential."))
            End If
            Try
                Return Await Errors.RefreshTokenAsync(info, cancellationToken).ConfigureAwait(False)
            Catch ex As Global.CompuMaster.Scopevisio.OpenApi.Client.ApiException
                If IsPermanentRenewalFailure(ex) Then Throw New Data.DmsUserAuthenticationException(ProviderStrings.GetText("SessionReauthorizationRequired"), ex)
                Throw
            End Try
        End Function

        Private Shared Function IsPermanentRenewalFailure(exception As Global.CompuMaster.Scopevisio.OpenApi.Client.ApiException) As Boolean
            If exception.ErrorCode = CInt(HttpStatusCode.Unauthorized) Then Return True
            If exception.ErrorCode <> CInt(HttpStatusCode.BadRequest) Then Return False
            'Inspect the error code only; never copy a server body into the user message.
            Try
                Dim body = Newtonsoft.Json.Linq.JObject.Parse(TryCast(exception.ErrorContent, String))
                Return String.Equals(CStr(body("error")), "invalid_grant", StringComparison.Ordinal)
            Catch ex As Newtonsoft.Json.JsonException
                Return False
            Catch ex As ArgumentException
                Return False
            End Try
        End Function

        Public Sub ValidateResponse(response As RestSharp.RestResponse) Implements IRestClientErrorHandler.ValidateResponse
            Errors.ValidateResponse(response)
            If response.StatusCode = HttpStatusCode.Unauthorized Then
                Throw New Data.DmsUserAuthenticationException(ProviderStrings.GetText("SessionReauthorizationRequired"), New UnauthorizedAccessException("The server rejected session authorization (HTTP 401)."))
            End If
        End Sub
    End Class

    Friend NotInheritable Class ScopevisioSessionRestClient
        Inherits CenterDeviceClientBase
        Friend Sub New(authorization As ScopevisioSessionAuthorization, userAgent As String)
            MyBase.New(authorization, New TeamworkRestClientConfiguration("https://appload.scopevisio.com/rest/teamworkbridge/", userAgent), authorization, "")
        End Sub
        Protected Overrides ReadOnly Property UploadLinkBaseUrl As String = "https://upload.teamwork.scopevisio.com/"
    End Class

    Friend NotInheritable Class ScopevisioSessionIOClient
        Inherits Global.CenterDevice.IO.IOClientBase
        Friend ReadOnly ApplicationContext As AccountInfo
        Private Sub New(client As OpenScopeApiClient, account As AccountInfo)
            MyBase.New(New ScopevisioSessionRestClient(New ScopevisioSessionAuthorization(client, account), client.Config.UserAgent), client.Token.Uid)
            ApplicationContext = account
        End Sub
        Friend Shared Async Function CreateAsync(client As OpenScopeApiClient, cancellationToken As CancellationToken) As Task(Of ScopevisioSessionIOClient)
            'Reuse the SDK's per-token-owner account cache populated during construction.
            Dim sdk = Await TeamworkRestClient.CreateAsync(client, cancellationToken).ConfigureAwait(False)
            Return New ScopevisioSessionIOClient(client, sdk.ApplicationContext)
        End Function
    End Class
End Namespace
