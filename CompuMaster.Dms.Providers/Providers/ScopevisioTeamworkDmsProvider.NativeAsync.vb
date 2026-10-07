Option Explicit On
Option Strict On

Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data

Namespace Providers
    Partial Public Class ScopevisioTeamworkDmsProvider

        ''' <inheritdoc/>
        ''' <remarks>Uses a cancellable shared-policy request for the API's hyphenated name fields when the SDK supplies no name.</remarks>
        Protected Overrides Async Function LookupNativeUserDisplayNameAsync(userId As String, cancellationToken As CancellationToken) As Task(Of String)
            Dim name = Await MyBase.LookupNativeUserDisplayNameAsync(userId, cancellationToken).ConfigureAwait(False)
            If Not String.IsNullOrWhiteSpace(name) Then Return name
            Dim token = Me._OpenScopeClient?.Token?.AccessToken
            If String.IsNullOrWhiteSpace(token) Then Return String.Empty
            Dim client = If(Me._ignoreSslErrors, NameLookupClientIgnoringSslErrors, NameLookupClient)
            Dim address As New Uri(New Uri(Me.WebApiDefaultUrl), "user/" & Uri.EscapeDataString(userId))
            Return Await ReadNativeScopevisioUserNameAsync(client, address, token, cancellationToken).ConfigureAwait(False)
        End Function

        Friend Shared Async Function ReadNativeScopevisioUserNameAsync(client As HttpClient, address As Uri, accessToken As String, cancellationToken As CancellationToken) As Task(Of String)
            cancellationToken.ThrowIfCancellationRequested()
            Try
                Using request As New HttpRequestMessage(HttpMethod.Get, address)
                    request.Headers.Authorization = New System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken)
                    'Default completion buffers this small metadata response while the token still covers HTTP I/O.
                    Using response = Await client.SendAsync(request, cancellationToken).ConfigureAwait(False)
                        If Not response.IsSuccessStatusCode Then Return String.Empty
                        Dim json = Await response.Content.ReadAsStringAsync().ConfigureAwait(False)
                        cancellationToken.ThrowIfCancellationRequested()
                        Return ParseUserDisplayName(json)
                    End Using
                End Using
            Catch ex As HttpRequestException
                Return String.Empty
            Catch ex As System.Text.Json.JsonException
                Return String.Empty
            End Try
        End Function

        ''' <inheritdoc/>
        ''' <remarks>The native source build forwards cancellation to token and account requests and shares transport admission with Teamwork requests to the same origin.</remarks>
        Public Overrides Function AuthorizeAsync(dmsProfile As IDmsLoginProfile, Optional cancellationToken As CancellationToken = Nothing) As Task
            If dmsProfile Is Nothing Then Throw New ArgumentNullException(NameOf(dmsProfile))
            Dim credentials = TryCast(dmsProfile, ScopevisioLoginCredentials)
            If credentials Is Nothing Then
                credentials = CType(Me.CreateNewCredentialsInstance(), ScopevisioLoginCredentials)
                credentials.Username = dmsProfile.UserName
                credentials.ClientNumber = dmsProfile.CustomerInstance
                credentials.Password = dmsProfile.Password
            End If
            Return Me.AuthorizeNativeCoreAsync(credentials, dmsProfile.IgnoreSslErrors, cancellationToken)
        End Function

        ''' <summary>Authorizes Scopevisio access through the native cancellable upstream clients.</summary>
        ''' <param name="loginCredentials">The configured Scopevisio credentials.</param>
        ''' <param name="ignoreSslErrors">Whether certificate validation is bypassed for the authorization transport, preserving the existing credential option.</param>
        ''' <param name="cancellationToken">Cancels request admission, token authorization, or account lookup.</param>
        ''' <returns>A task representing successful authorization and provider-state installation.</returns>
        Protected Overridable Async Function AuthorizeNativeCoreAsync(loginCredentials As ScopevisioLoginCredentials, ignoreSslErrors As Boolean, cancellationToken As CancellationToken) As Task
            cancellationToken.ThrowIfCancellationRequested()
            Dim isTokenRequest As Boolean = False
            Try
                Dim configuration = Me.CreateNativeAuthorizationConfiguration(loginCredentials, ignoreSslErrors)
                Dim openScopeClient As New Global.CompuMaster.Scopevisio.OpenApi.OpenScopeApiClient(configuration)
                isTokenRequest = True
                Await openScopeClient.AuthorizeWithUserCredentialsAsync(cancellationToken).ConfigureAwait(False)
                isTokenRequest = False
                Dim client As Global.CenterDevice.IO.IOClientBase = Nothing
                If openScopeClient.Token IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(openScopeClient.Token.TeamworkTenantId) Then
                    client = Await ScopevisioSessionIOClient.CreateAsync(openScopeClient, cancellationToken).ConfigureAwait(False)
                End If
                cancellationToken.ThrowIfCancellationRequested()
                Me._OpenScopeClient = openScopeClient
                Me.IOClient = client
                Me._ignoreSslErrors = ignoreSslErrors
            Catch ex As Global.CompuMaster.Scopevisio.OpenApi.Client.ApiException
                Throw CreateAuthorizationException(ex.ErrorCode, ex.ErrorContent, ex, isTokenRequest)
            End Try
        End Function

        ''' <summary>Creates the authorization configuration with shared native request admission.</summary>
        ''' <param name="loginCredentials">The configured Scopevisio credentials.</param>
        ''' <param name="ignoreSslErrors">Whether the authorization transport bypasses certificate validation.</param>
        ''' <returns>A fresh configuration using a transport whose lifetime is owned by this provider implementation.</returns>
        Protected Overridable Function CreateNativeAuthorizationConfiguration(loginCredentials As ScopevisioLoginCredentials, ignoreSslErrors As Boolean) As Global.CompuMaster.Scopevisio.OpenApi.Client.Configuration
            Return New Global.CompuMaster.Scopevisio.OpenApi.Client.Configuration With {
                .Username = loginCredentials.Username,
                .Password = loginCredentials.Password,
                .ClientNumber = loginCredentials.ClientNumber,
                .OrganisationName = loginCredentials.OrganisationName,
                .HttpClient = If(ignoreSslErrors, NameLookupClientIgnoringSslErrors, NameLookupClient)
            }
        End Function
    End Class
End Namespace
