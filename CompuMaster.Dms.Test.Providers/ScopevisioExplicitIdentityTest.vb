Option Explicit On
Option Strict On

Imports NUnit.Framework
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports CompuMaster.Dms.Providers
Imports CompuMaster.Scopevisio.OpenApi.Model

<TestFixture>
Public Class ScopevisioExplicitIdentityTest
    Private Shared Function TokenJson(Optional organisation As String = "7") As String
        Return "{""token_type"":""bearer"",""access_token"":""fixture-access"",""uid"":""fixture-scope-uid"",""organisationId"":" & organisation & ",""teamworkTenantId"":""fixture-tenant""}"
    End Function

    Private Shared Function AccountJson(Optional organisation As String = "7") As String
        Return "{""user"":{""uid"":""fixture-scope-uid"",""login"":""fixture-login""},""organisation"":{""id"":" & organisation & "}}"
    End Function

    Private Shared Function Token() As TokenResponse
        Return JsonConvert.DeserializeObject(Of TokenResponse)(TokenJson())
    End Function

    Private Shared Function Account() As AccountInfo
        Return JsonConvert.DeserializeObject(Of AccountInfo)(AccountJson())
    End Function

    Private Shared Function Credentials() As ScopevisioLoginCredentials
        Return New ScopevisioLoginCredentials With {.Username = "fixture-login", .Password = "fixture-password", .ClientNumber = "fixture-customer"}
    End Function

    <Test>
    Public Sub LegacyCredentialsKeepTheirDefaultContractAndCommonProfileHasNoProviderSelectors()
        Dim profile = Credentials()
        Assert.That(profile.OrganisationId, [Is].Null)
        Assert.That(profile.ExpectedTeamworkTenantId, [Is].Null)
        profile.OrganisationName = "legacy-name"
        Assert.DoesNotThrow(Sub() profile.Validate())
        Assert.That(GetType(Data.IDmsLoginProfile).GetProperty("OrganisationId"), [Is].Null)
        Assert.That(GetType(Data.IDmsLoginProfile).GetProperty("ExpectedTeamworkTenantId"), [Is].Null)
    End Sub

    <TestCase(0L), TestCase(-1L)>
    Public Sub NonPositiveExplicitSelectorsAreRejected(organisation As Long)
        Dim profile = Credentials()
        profile.OrganisationId = organisation
        Assert.Throws(Of ArgumentOutOfRangeException)(Sub() profile.Validate())
    End Sub

    <Test>
    Public Sub PositiveFullWidthSelectorAndOpaqueTenantGuardAreSupported()
        Dim profile = Credentials()
        profile.OrganisationId = Long.MaxValue
        profile.ExpectedTeamworkTenantId = "fixture-tenant"
        Assert.DoesNotThrow(Sub() profile.Validate())
        Dim json = TokenJson(Long.MaxValue.ToString(Globalization.CultureInfo.InvariantCulture))
        Dim identity = ScopevisioExplicitIdentity.FromToken(JsonConvert.DeserializeObject(Of TokenResponse)(json), json, Long.MaxValue, "fixture-tenant")
        Assert.That(identity.OrganisationId, [Is].EqualTo(Long.MaxValue))
        Assert.That(identity.ScopeUserId, [Is].EqualTo("fixture-scope-uid"))
        Assert.That(identity.TeamworkTenantId, [Is].EqualTo("fixture-tenant"))
    End Sub

    <TestCase(" "), TestCase("tenant" & vbCrLf), TestCase(vbTab)>
    Public Sub BlankOrControlTenantGuardsAreRejected(expected As String)
        Dim profile = Credentials()
        profile.ExpectedTeamworkTenantId = expected
        Assert.Throws(Of ArgumentException)(Sub() profile.Validate())
    End Sub

    <TestCase("7.25"), TestCase("7.0"), TestCase("7e0"), TestCase("""7"""), TestCase("null"), TestCase("true"), TestCase("[7]"), TestCase("9223372036854775808"), TestCase("8")>
    Public Sub RawTokenOrganisationMustBeTheExactJsonInteger(value As String)
        Assert.Throws(Of InvalidOperationException)(Sub() ScopevisioExplicitIdentity.FromToken(Token(), TokenJson(value), 7, "fixture-tenant"))
    End Sub

    <TestCase("7.25"), TestCase("7.0"), TestCase("7e0"), TestCase("""7"""), TestCase("null"), TestCase("true"), TestCase("[7]"), TestCase("9223372036854775808"), TestCase("8")>
    Public Sub RawAccountOrganisationMustBeTheExactJsonInteger(value As String)
        Dim identity = ScopevisioExplicitIdentity.FromToken(Token(), TokenJson(), 7, "fixture-tenant")
        Assert.Throws(Of InvalidOperationException)(Sub() identity.ValidateAccount(Account(), AccountJson(value)))
    End Sub

    <Test>
    Public Sub FractionalIdsActuallyRoundedByTypedDeserializationAreStillRejected()
        Dim tokenResponse = JsonConvert.DeserializeObject(Of TokenResponse)(TokenJson("7.25"))
        Assert.That(tokenResponse.OrganisationId, [Is].EqualTo(7), "This demonstrates why typed-only admission is insufficient.")
        Assert.Throws(Of InvalidOperationException)(Sub() ScopevisioExplicitIdentity.FromToken(tokenResponse, TokenJson("7.25"), 7, "fixture-tenant"))
        Dim accountResponse = JsonConvert.DeserializeObject(Of AccountInfo)(AccountJson("7.25"))
        Assert.That(accountResponse.Organisation.Id, [Is].EqualTo(7))
        Dim identity = ScopevisioExplicitIdentity.FromToken(Token(), TokenJson(), 7, "fixture-tenant")
        Assert.Throws(Of InvalidOperationException)(Sub() identity.ValidateAccount(accountResponse, AccountJson("7.25")))
    End Sub

    <Test>
    Public Sub MissingRawIdsCannotUseAnOtherwiseMatchingTypedDefault()
        Dim rawToken = JObject.Parse(TokenJson())
        rawToken.Remove("organisationId")
        Assert.Throws(Of InvalidOperationException)(Sub() ScopevisioExplicitIdentity.FromToken(Token(), rawToken.ToString(), 7, "fixture-tenant"))
        Dim rawAccount = JObject.Parse(AccountJson())
        CType(rawAccount("organisation"), JObject).Remove("id")
        Dim identity = ScopevisioExplicitIdentity.FromToken(Token(), TokenJson(), 7, "fixture-tenant")
        Assert.Throws(Of InvalidOperationException)(Sub() identity.ValidateAccount(Account(), rawAccount.ToString()))
    End Sub

    <TestCase("uid"), TestCase("access_token"), TestCase("teamworkTenantId")>
    Public Sub MissingOrMismatchedRawTokenStringsAreRejected(field As String)
        Dim raw = JObject.Parse(TokenJson())
        raw.Remove(field)
        Assert.Throws(Of InvalidOperationException)(Sub() ScopevisioExplicitIdentity.FromToken(Token(), raw.ToString(), 7, "fixture-tenant"))
        raw(field) = "other-context"
        Assert.Throws(Of InvalidOperationException)(Sub() ScopevisioExplicitIdentity.FromToken(Token(), raw.ToString(), 7, "fixture-tenant"))
    End Sub

    <Test>
    Public Sub TenantAndRefreshScopeUidMustMatchTheirRespectiveNamespaces()
        Assert.Throws(Of InvalidOperationException)(Sub() ScopevisioExplicitIdentity.FromToken(Token(), TokenJson(), 7, "other-tenant"))
        Assert.Throws(Of InvalidOperationException)(Sub() ScopevisioExplicitIdentity.FromToken(Token(), TokenJson(), 7, "fixture-tenant", "other-scope-uid"))
        Dim identity = ScopevisioExplicitIdentity.FromToken(Token(), TokenJson(), 7, "fixture-tenant", "fixture-scope-uid")
        Dim accountResponse = Account()
        accountResponse.User.Uid = "mapped-dms-user-id"
        Assert.Throws(Of InvalidOperationException)(Sub() identity.ValidateAccount(accountResponse, AccountJson()))
    End Sub

    <TestCase("""other-tenant"""), TestCase("7"), TestCase("true")>
    Public Sub PresentAccountTenantCannotDisagreeOrBeCoerced(value As String)
        Dim identity = ScopevisioExplicitIdentity.FromToken(Token(), TokenJson(), 7, "fixture-tenant")
        Dim raw = JObject.Parse(AccountJson())
        CType(raw("organisation"), JObject)("teamworkTenantId") = JToken.Parse(value)
        Assert.Throws(Of InvalidOperationException)(Sub() identity.ValidateAccount(Account(), raw.ToString()))
    End Sub

    <Test>
    Public Sub AccountTenantMayBeOmittedButTokenGuardAndAccountIdentityAreStillRequired()
        Dim identity = ScopevisioExplicitIdentity.FromToken(Token(), TokenJson(), 7, "fixture-tenant")
        Assert.DoesNotThrow(Sub() identity.ValidateAccount(Account(), AccountJson()))
        Dim raw = JObject.Parse(AccountJson())
        CType(raw("user"), JObject)("uid") = "wrong-scope-uid"
        Assert.Throws(Of InvalidOperationException)(Sub() identity.ValidateAccount(Account(), raw.ToString()))
        Dim wrongTyped = Account()
        wrongTyped.Organisation.TeamworkTenantId = "wrong-tenant"
        Assert.Throws(Of InvalidOperationException)(Sub() identity.ValidateAccount(wrongTyped, AccountJson()))
    End Sub

    <TestCase("{"), TestCase("[]"), TestCase("null")>
    Public Sub MalformedOrNonobjectTokenContextHasABoundedDiagnostic(raw As String)
        Dim failure = Assert.Throws(Of InvalidOperationException)(Sub() ScopevisioExplicitIdentity.FromToken(Token(), raw, 7, "fixture-tenant"))
        Assert.That(failure.Message, Does.Not.Contain("fixture-access"))
    End Sub
End Class
