Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Providers

Friend NotInheritable Class ScopevisioTestSelection
    Private Sub New()
    End Sub

    Friend Shared Function CreateProfile(readInput As Func(Of String, String)) As ScopevisioLoginCredentials
        If readInput Is Nothing Then Throw New ArgumentNullException(NameOf(readInput))
        'Test admission requires both verified identifiers before any login credential is read or authorization can start.
        Dim organisation = readInput(ScopevisioDemoSelection.OrganisationField)
        Dim tenant = readInput(ScopevisioDemoSelection.TenantField)
        If String.IsNullOrWhiteSpace(organisation) OrElse String.IsNullOrWhiteSpace(tenant) Then
            Throw New InvalidOperationException("Scopevisio tests require a verified numeric demo organisation ID and a separate expected Teamwork tenant ID. Default organisation fallback is not admitted.")
        End If
        ScopevisioDemoSelection.ValidateSelectorInputs(organisation, tenant, "The configured demo organisation ID must be a positive integer.")
        Return DirectCast(ScopevisioDemoSelection.CreateProfile(readInput("username"), readInput("password"), readInput("customer no."), organisation, tenant), ScopevisioLoginCredentials)
    End Function

    Friend Shared Function EnvironmentName(fieldName As String) As String
        Return "TEST_SCOPEVISIOTEAMWORK_" & fieldName.Replace(" ", "").Replace(".", "").ToUpperInvariant()
    End Function
End Class
