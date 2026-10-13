Option Explicit On
Option Strict On

Imports System.Globalization
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports CompuMaster.Scopevisio.OpenApi.Model

Namespace Providers
    'Keeps Scope UID, numeric organisation and opaque Teamwork tenant in their own identity namespaces.
    Friend NotInheritable Class ScopevisioExplicitIdentity
        Private Sub New(organisationId As Long, scopeUserId As String, teamworkTenantId As String)
            Me.OrganisationId = organisationId
            Me.ScopeUserId = scopeUserId
            Me.TeamworkTenantId = teamworkTenantId
        End Sub

        Friend ReadOnly Property OrganisationId As Long
        Friend ReadOnly Property ScopeUserId As String
        Friend ReadOnly Property TeamworkTenantId As String

        Friend Shared Function FromToken(token As TokenResponse, rawJson As String, requestedOrganisationId As Long, expectedTenantId As String,
                                         Optional expectedScopeUserId As String = Nothing) As ScopevisioExplicitIdentity
            If requestedOrganisationId <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(requestedOrganisationId))
            If token Is Nothing OrElse token.OrganisationId <> requestedOrganisationId OrElse
                String.IsNullOrWhiteSpace(token.Uid) OrElse String.IsNullOrWhiteSpace(token.AccessToken) OrElse
                String.IsNullOrWhiteSpace(token.TeamworkTenantId) Then Throw UnverifiedContext()
            Dim raw = ParseObject(rawJson)
            If Not MatchesInteger(raw("organisationId"), requestedOrganisationId) OrElse
                Not MatchesString(raw("uid"), token.Uid) OrElse Not MatchesString(raw("access_token"), token.AccessToken) OrElse
                Not MatchesString(raw("teamworkTenantId"), token.TeamworkTenantId) Then Throw UnverifiedContext()
            If Not String.IsNullOrEmpty(expectedTenantId) AndAlso Not String.Equals(token.TeamworkTenantId, expectedTenantId, StringComparison.Ordinal) Then Throw UnverifiedContext()
            If Not String.IsNullOrEmpty(expectedScopeUserId) AndAlso Not String.Equals(token.Uid, expectedScopeUserId, StringComparison.Ordinal) Then Throw UnverifiedContext()
            Return New ScopevisioExplicitIdentity(requestedOrganisationId, token.Uid, token.TeamworkTenantId)
        End Function

        Friend Sub ValidateAccount(account As AccountInfo, rawJson As String)
            If account Is Nothing OrElse account.Organisation Is Nothing OrElse account.User Is Nothing OrElse
                account.Organisation.Id <> OrganisationId OrElse Not String.Equals(account.User.Uid, ScopeUserId, StringComparison.Ordinal) OrElse
                String.IsNullOrWhiteSpace(account.User.Login) Then Throw UnverifiedContext()
            Dim raw = ParseObject(rawJson)
            If Not MatchesInteger(raw.SelectToken("organisation.id"), OrganisationId) OrElse
                Not MatchesString(raw.SelectToken("user.uid"), ScopeUserId) OrElse
                Not MatchesString(raw.SelectToken("user.login"), account.User.Login) Then Throw UnverifiedContext()
            'Some account responses omit the tenant. A present value must agree; token admission always verifies it.
            Dim tenant = raw.SelectToken("organisation.teamworkTenantId")
            If tenant IsNot Nothing AndAlso tenant.Type <> JTokenType.Null Then
                If tenant.Type <> JTokenType.String Then Throw UnverifiedContext()
                If CStr(tenant) <> "" AndAlso Not MatchesString(tenant, TeamworkTenantId) Then Throw UnverifiedContext()
            End If
            If Not String.IsNullOrEmpty(account.Organisation.TeamworkTenantId) AndAlso
                Not String.Equals(account.Organisation.TeamworkTenantId, TeamworkTenantId, StringComparison.Ordinal) Then Throw UnverifiedContext()
        End Sub

        Friend Sub ValidateInstalledToken(token As TokenResponse)
            If token Is Nothing OrElse token.OrganisationId <> OrganisationId OrElse
                Not String.Equals(token.Uid, ScopeUserId, StringComparison.Ordinal) OrElse
                Not String.Equals(token.TeamworkTenantId, TeamworkTenantId, StringComparison.Ordinal) OrElse
                String.IsNullOrWhiteSpace(token.AccessToken) Then Throw UnverifiedContext()
        End Sub

        Private Shared Function MatchesInteger(value As JToken, expected As Long) As Boolean
            Dim actual As Long
            Return value IsNot Nothing AndAlso value.Type = JTokenType.Integer AndAlso
                Long.TryParse(value.ToString(Formatting.None), NumberStyles.None, CultureInfo.InvariantCulture, actual) AndAlso actual = expected
        End Function

        Private Shared Function MatchesString(value As JToken, expected As String) As Boolean
            Return value IsNot Nothing AndAlso value.Type = JTokenType.String AndAlso Not String.IsNullOrWhiteSpace(CStr(value)) AndAlso
                String.Equals(CStr(value), expected, StringComparison.Ordinal)
        End Function

        Private Shared Function ParseObject(rawJson As String) As JObject
            Try
                Return JObject.Parse(rawJson)
            Catch failure As JsonException
                Throw New InvalidOperationException("Scopevisio returned an unverified authentication context.", failure)
            Catch failure As ArgumentException
                Throw New InvalidOperationException("Scopevisio returned an unverified authentication context.", failure)
            End Try
        End Function

        Private Shared Function UnverifiedContext() As InvalidOperationException
            'Never include token bodies, credentials or private identity values in the diagnostic.
            Return New InvalidOperationException("Scopevisio returned an unverified authentication context.")
        End Function
    End Class
End Namespace
