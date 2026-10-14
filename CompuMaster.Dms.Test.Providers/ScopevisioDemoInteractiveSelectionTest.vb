Option Explicit On
Option Strict On
Imports NUnit.Framework
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports CompuMaster.Scopevisio.OpenApi.Model

<TestFixture>
Public Class ScopevisioDemoInteractiveSelectionTest
    Private Class SelectedProvider
        Inherits ScopevisioTeamworkDmsProvider
        Friend Tenant As String
        Friend Reads As Integer
        Protected Overrides Function GetCurrentOrganisation() As Organisation
            Reads += 1
            If Tenant Is Nothing Then Return Nothing
            Return New Organisation With {.Id = 7, .Name = "Fixture instance", .TeamworkTenantId = Tenant}
        End Function
    End Class

    Private Shared Function CreateTestProfile(organisation As String, tenant As String) As IDmsLoginProfile
        Return ScopevisioDemoSelection.CreateInteractiveProfile("fixture-login", "fixture-password", "fixture-customer", organisation, tenant, "Invalid organisation.")
    End Function

    <Test>
    Public Sub GuardOnlyInteractiveLoginSelectsBeforeCheckingTenant()
        Dim profile = CreateTestProfile("", "demo-tenant")
        Assert.That(profile.GetType(), [Is].EqualTo(GetType(DmsLoginProfile)))
        Dim provider As New SelectedProvider With {.Tenant = "default-tenant"}
        Dim selections = 0
        Dim result = ScopevisioDemoSelection.SelectInteractiveProvider(profile, "demo-tenant",
            Function(credentials)
                Assert.Fail("Guard-only login must not authorize the default tenant directly.")
                Return Nothing
            End Function,
            Function(login)
                Assert.That(login, [Is].SameAs(profile))
                Assert.That(provider.Reads, [Is].Zero)
                selections += 1
                provider.Tenant = "demo-tenant"
                Return provider
            End Function, "Tenant mismatch.")
        Assert.That(result, [Is].SameAs(provider))
        Assert.That(selections, [Is].EqualTo(1))
        Assert.That(provider.Reads, [Is].EqualTo(1))
        Assert.That(ScopevisioDemoSelection.AllowsInstanceSwitch(profile, "demo-tenant"), [Is].False)
    End Sub

    <TestCase("default-tenant"), TestCase("DEMO-TENANT"), TestCase(Nothing)>
    Public Sub WrongOrUnverifiedSelectedTenantCannotReachBrowser(actual As String)
        Dim profile = CreateTestProfile("", "demo-tenant")
        Dim provider As New SelectedProvider With {.Tenant = actual}
        Dim failure = Assert.Throws(Of InvalidOperationException)(Sub()
            ScopevisioDemoSelection.SelectInteractiveProvider(profile, "demo-tenant", Function(credentials) Nothing,
                Function(login) provider, "Localized tenant mismatch.")
        End Sub)
        Assert.That(failure.Message, [Is].EqualTo("Localized tenant mismatch."))
    End Sub

    <TestCase(""), TestCase("demo-tenant")>
    Public Sub CancelledPickerReturnsWithoutBrowserOrTenantLookup(tenant As String)
        Dim result = ScopevisioDemoSelection.SelectInteractiveProvider(CreateTestProfile("", tenant), tenant,
            Function(credentials)
                Assert.Fail("The picker route is required.")
                Return Nothing
            End Function, Function(login) Nothing, "Tenant mismatch.")
        Assert.That(result, [Is].Null)
    End Sub

    <Test>
    Public Sub PickerFailureKeepsOriginalException()
        Dim original As New IO.IOException("Synthetic selector failure.")
        Dim actual = Assert.Throws(Of IO.IOException)(Sub()
            ScopevisioDemoSelection.SelectInteractiveProvider(CreateTestProfile("", "demo-tenant"), "demo-tenant", Function(credentials) Nothing,
                Function(login)
                    Throw original
                    Return Nothing
                End Function, "Tenant mismatch.")
        End Sub)
        Assert.That(actual, [Is].SameAs(original))
    End Sub

    <TestCase(""), TestCase("demo-tenant")>
    Public Sub NumericOrganisationRetainsDirectAuthorizationAndGuard(tenant As String)
        Dim profile = CreateTestProfile("7", tenant)
        Dim provider As New SelectedProvider With {.Tenant = "demo-tenant"}
        Dim calls = 0
        Dim result = ScopevisioDemoSelection.SelectInteractiveProvider(profile, tenant,
            Function(credentials)
                Assert.That(credentials.OrganisationId, [Is].EqualTo(7))
                Assert.That(credentials.ExpectedTeamworkTenantId, [Is].EqualTo(tenant))
                calls += 1
                Return provider
            End Function,
            Function(login)
                Assert.Fail("Explicit organisation must retain direct authorization.")
                Return Nothing
            End Function, "Tenant mismatch.")
        Assert.That(result, [Is].SameAs(provider))
        Assert.That(calls, [Is].EqualTo(1))
        Assert.That(ScopevisioDemoSelection.AllowsInstanceSwitch(profile, tenant), [Is].False)
    End Sub

    <Test>
    Public Sub BlankSelectorsKeepPickerAndLaterInstanceSwitchWithoutTenantReads()
        Dim profile = CreateTestProfile("", "")
        Dim provider As New SelectedProvider()
        Dim result = ScopevisioDemoSelection.SelectInteractiveProvider(profile, "", Function(credentials) Nothing,
            Function(login) provider, "Tenant mismatch.")
        Assert.That(result, [Is].SameAs(provider))
        Assert.That(provider.Reads, [Is].Zero)
        Assert.That(ScopevisioDemoSelection.AllowsInstanceSwitch(profile, ""), [Is].True)
    End Sub

    <TestCase("0", "demo-tenant"), TestCase(" ", "demo-tenant"), TestCase("", " ")>
    Public Sub InvalidInteractiveSelectorsFailBeforeAuthorization(organisation As String, tenant As String)
        Assert.Throws(Of ArgumentException)(Sub() CreateTestProfile(organisation, tenant))
    End Sub
End Class
