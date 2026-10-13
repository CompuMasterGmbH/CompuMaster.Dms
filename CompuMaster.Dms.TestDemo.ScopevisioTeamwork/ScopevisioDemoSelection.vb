Option Explicit On
Option Strict On

Imports System.Globalization
Imports CompuMaster.Dms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Friend NotInheritable Class ScopevisioDemoSelection
    Friend Const OrganisationField As String = "organisation id"
    Friend Const TenantField As String = "expected teamwork tenant id"

    Private Sub New()
    End Sub

    Friend Shared Function CreateProfile(username As String, password As String, customer As String,
                                         organisation As String, expectedTenant As String,
                                         Optional invalidOrganisationMessage As String = "The organisation ID must be a positive integer.") As IDmsLoginProfile
        If String.IsNullOrEmpty(organisation) AndAlso String.IsNullOrEmpty(expectedTenant) Then
            Return New DmsLoginProfile With {.DmsProvider = BaseDmsProvider.DmsProviders.Scopevisio,
                .Username = username, .Password = password, .CustomerInstance = customer}
        End If
        Dim numericId = ValidateSelectorInputs(organisation, expectedTenant, invalidOrganisationMessage)
        Dim credentials As New ScopevisioLoginCredentials With {.Username = username, .Password = password,
            .ClientNumber = customer, .CustomerInstance = customer, .OrganisationId = numericId,
            .ExpectedTeamworkTenantId = expectedTenant}
        credentials.Validate()
        Return credentials
    End Function

    Friend Shared Function ValidateSelectorInputs(organisation As String, expectedTenant As String,
                                                  invalidOrganisationMessage As String) As Long?
        Dim numericId As Long? = Nothing
        If Not String.IsNullOrEmpty(organisation) Then
            Dim value As Long
            If Not Long.TryParse(organisation.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, value) OrElse value <= 0 Then
                Throw New ArgumentException(invalidOrganisationMessage, NameOf(organisation))
            End If
            numericId = value
        End If
        If Not String.IsNullOrEmpty(expectedTenant) AndAlso
           (String.IsNullOrWhiteSpace(expectedTenant) OrElse expectedTenant.Any(Function(character) Char.IsControl(character))) Then
            Throw New ArgumentException("The expected Teamwork tenant must be a nonblank identifier without control characters.", NameOf(expectedTenant))
        End If
        Return numericId
    End Function

    Friend Shared Sub PersistInputs(buffer As DemoInputBuffer, username As String, password As String, customer As String,
                                    startPath As String, organisation As String, expectedTenant As String, invalidOrganisationMessage As String)
        ValidateSelectorInputs(organisation, expectedTenant, invalidOrganisationMessage)
        buffer.Persist("username", username)
        buffer.Persist("password", password)
        buffer.Persist("customer no.", customer)
        buffer.Persist("start path", startPath)
        buffer.Persist(OrganisationField, organisation)
        buffer.Persist(TenantField, expectedTenant)
    End Sub
End Class
