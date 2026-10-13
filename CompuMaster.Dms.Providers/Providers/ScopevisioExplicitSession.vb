Option Explicit On
Option Strict On

Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest.Clients.OAuth
Imports CompuMaster.Scopevisio.CenterDeviceApi
Imports CompuMaster.Scopevisio.OpenApi
Imports CompuMaster.Scopevisio.OpenApi.Client
Imports CompuMaster.Scopevisio.OpenApi.Model

Namespace Providers
    'Only explicit selection uses this staged refresh policy; legacy SDK session defaults remain unchanged.
    Friend NotInheritable Class ScopevisioExplicitSession
        Private ReadOnly Client As OpenScopeApiClient
        Private ReadOnly Identity As ScopevisioExplicitIdentity
        Private Account As AccountInfo
        Private ReadOnly Gate As New SemaphoreSlim(1, 1)
        Private StagingClient As OpenScopeApiClient
        Private ReadOnly CreateStagingClient As Func(Of Configuration, OpenScopeApiClient)

        Friend Sub New(client As OpenScopeApiClient, account As AccountInfo, identity As ScopevisioExplicitIdentity,
                       Optional createStagingClient As Func(Of Configuration, OpenScopeApiClient) = Nothing)
            Me.Client = client
            Me.Account = account
            Me.Identity = identity
            If createStagingClient Is Nothing Then
                Me.CreateStagingClient = Function(configuration As Configuration) New OpenScopeApiClient(configuration)
            Else
                Me.CreateStagingClient = createStagingClient
            End If
        End Sub

        Friend Function GetOAuthInfo() As OAuthInfo
            Gate.Wait()
            Try
                Return CurrentInfo()
            Finally
                Gate.Release()
            End Try
        End Function

        Friend Async Function GetOAuthInfoAsync(cancellationToken As CancellationToken) As Task(Of OAuthInfo)
            Await Gate.WaitAsync(cancellationToken).ConfigureAwait(False)
            Try
                cancellationToken.ThrowIfCancellationRequested()
                Return CurrentInfo()
            Finally
                Gate.Release()
            End Try
        End Function

        Friend Async Function RefreshAsync(rejected As OAuthInfo, cancellationToken As CancellationToken) As Task(Of OAuthInfo)
            Await Gate.WaitAsync(cancellationToken).ConfigureAwait(False)
            Try
                cancellationToken.ThrowIfCancellationRequested()
                Identity.ValidateInstalledToken(Client.Token)
                If rejected IsNot Nothing AndAlso Not String.Equals(rejected.access_token, Client.Token.AccessToken, StringComparison.Ordinal) Then Return CurrentInfo()
                Dim previous = Client.Token
                If String.IsNullOrEmpty(previous.RefreshToken) Then Throw New InvalidOperationException("The session has no refresh credential.")
                'The gate protects one staging SDK wrapper per session, rather than allocating an undisposable wrapper per retry.
                If StagingClient Is Nothing Then StagingClient = CreateStagingClient(CloneConfiguration(Client.Config))
                Dim candidate = StagingClient
                Dim config = candidate.Config
                'Discard every previous candidate token/bearer, including one from an interrupted or rejected account lookup.
                candidate.Token = Nothing
                config.AccessToken = Client.Config.AccessToken
                candidate.AuthorizationApi.ExceptionFactory = Client.AuthorizationApi.ExceptionFactory
                candidate.AdditionalApi.ExceptionFactory = Client.AdditionalApi.ExceptionFactory
                'Keep the existing refresh form; organisation selection is verified in the response, not added as an unsupported refresh argument.
                Dim response = Await candidate.AuthorizationApi.TokenAsyncWithHttpInfo("refresh_token", config.ClientNumber, cancellationToken,
                    refreshToken:=previous.RefreshToken).ConfigureAwait(False)
                Dim admitted = ScopevisioExplicitIdentity.FromToken(response.Data, response.RawJsonContent,
                    Identity.OrganisationId, Identity.TeamworkTenantId, Identity.ScopeUserId)
                candidate.Token = response.Data
                Dim accountResponse = Await candidate.AdditionalApi.GetApplicationContextAsyncWithHttpInfo(cancellationToken).ConfigureAwait(False)
                admitted.ValidateAccount(accountResponse.Data, accountResponse.RawJsonContent)
                cancellationToken.ThrowIfCancellationRequested()
                'No active bearer/account installation until both raw responses and cancellation have passed.
                Client.Config.AccessToken = response.Data.AccessToken
                Client.Token = response.Data
                Account = accountResponse.Data
                Return CurrentInfo()
            Finally
                If StagingClient IsNot Nothing Then
                    StagingClient.Token = Nothing
                    StagingClient.Config.AccessToken = Client.Config.AccessToken
                End If
                Gate.Release()
            End Try
        End Function

        Private Function CurrentInfo() As OAuthInfo
            Identity.ValidateInstalledToken(Client.Token)
            Return New TeamworkOAuthInfo(Account.User.Login, Client.Token)
        End Function

        Private Shared Function CloneConfiguration(current As Configuration) As Configuration
            Return New Configuration(New Dictionary(Of String, String)(current.DefaultHeader),
                New Dictionary(Of String, String)(current.ApiKey), New Dictionary(Of String, String)(current.ApiKeyPrefix), current.BasePath) With {
                .Username = current.Username, .Password = current.Password, .ClientNumber = current.ClientNumber,
                .OrganisationName = current.OrganisationName, .UserAgent = current.UserAgent, .HttpClient = current.HttpClient,
                .Timeout = current.Timeout, .DateTimeFormat = current.DateTimeFormat, .AccessToken = current.AccessToken}
        End Function
    End Class
End Namespace
