Option Explicit On
Option Strict On

Imports NUnit.Framework

<TestFixture>
Public Class ScopevisioTestSelectionTest
    <TestCase("", "fixture-tenant"), TestCase(Nothing, "fixture-tenant"), TestCase("7", ""), TestCase("7", Nothing),
     TestCase("0", "fixture-tenant"), TestCase("7.25", "fixture-tenant"), TestCase("tenant-guid", "fixture-tenant"), TestCase("7", "tenant" & vbCrLf)>
    Public Sub InvalidOrIncompleteSelectionRejectsBeforeReadingLoginCredentials(organisation As String, tenant As String)
        Dim requested As New List(Of String)
        Assert.Catch(Sub() ScopevisioTestSelection.CreateProfile(
            Function(field)
                requested.Add(field)
                If field = ScopevisioDemoSelection.OrganisationField Then Return organisation
                If field = ScopevisioDemoSelection.TenantField Then Return tenant
                Throw New InvalidOperationException("Login credentials were requested before selection admission.")
            End Function))
        Assert.That(requested, [Is].EqualTo(New String() {ScopevisioDemoSelection.OrganisationField, ScopevisioDemoSelection.TenantField}))
    End Sub

    <Test>
    Public Sub CompleteSelectionUsesDistinctVerifiedIdentifiersAndStableEnvironmentNames()
        Dim values As New Dictionary(Of String, String) From {
            {ScopevisioDemoSelection.OrganisationField, "7"}, {ScopevisioDemoSelection.TenantField, "fixture-tenant"},
            {"username", "fixture-login"}, {"password", "fixture-password"}, {"customer no.", "fixture-customer"}}
        Dim profile = ScopevisioTestSelection.CreateProfile(Function(field) values(field))
        Assert.That(profile.OrganisationId, [Is].EqualTo(7))
        Assert.That(profile.ExpectedTeamworkTenantId, [Is].EqualTo("fixture-tenant"))
        Assert.That(profile.ClientNumber, [Is].EqualTo(profile.CustomerInstance))
        Assert.That(ScopevisioTestSelection.EnvironmentName(ScopevisioDemoSelection.OrganisationField), [Is].EqualTo("TEST_SCOPEVISIOTEAMWORK_ORGANISATIONID"))
        Assert.That(ScopevisioTestSelection.EnvironmentName(ScopevisioDemoSelection.TenantField), [Is].EqualTo("TEST_SCOPEVISIOTEAMWORK_EXPECTEDTEAMWORKTENANTID"))
    End Sub
End Class
