Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data

Namespace Providers

    ''' <summary>Stores credentials for a Scopevisio customer and optional organization.</summary>
    Public Class ScopevisioLoginCredentials
        Inherits BaseDmsLoginCredentials

        ''' <summary>Initializes the provider credential settings.</summary>
        Public Sub New()
            MyBase.New
            Me.DmsProvider = BaseDmsProvider.DmsProviders.Scopevisio
        End Sub

        ''' <inheritdoc/>
        Public Overrides Property BaseUrl As String
            Get
                Return MyBase.BaseUrl
            End Get
            Set(value As String)
                Throw New NotSupportedException(ProviderStrings.GetText("CustomWebserviceURLsAreNotSupportedForThis"))
            End Set
        End Property

        ''' <summary>Gets or sets the customer number required by the provider.</summary>
        Public Property ClientNumber As String
        ''' <summary>Gets or sets the optional organization name used during initial authorization.</summary>
        Public Property OrganisationName As String

        ''' <summary>Gets or sets the optional numeric organisation selected during initial authorization.</summary>
        ''' <value>A positive Int64 organisation ID, or <see langword="Nothing"/> to retain legacy name/default selection.</value>
        ''' <remarks>This OAuth selector is distinct from a Teamwork tenant identifier. Explicit selection must not fall back to another organisation.</remarks>
        Public Property OrganisationId As Long?

        ''' <summary>Gets or sets the expected Teamwork tenant used to guard the authenticated DMS context.</summary>
        ''' <value>The verified opaque tenant identifier, or <see langword="Nothing"/> or an empty string when no additional tenant guard is configured.</value>
        ''' <remarks>This identity guard does not select the numeric OAuth organisation or authorize operations outside that tenant.</remarks>
        Public Property ExpectedTeamworkTenantId As String

        ''' <inheritdoc/>
        ''' <exception cref="ArgumentOutOfRangeException">The explicit organisation ID is not positive.</exception>
        ''' <exception cref="ArgumentException">The supplied tenant guard is blank or contains control characters.</exception>
        Public Overrides Sub Validate()
            MyBase.Validate()
            If Me.ClientNumber = Nothing Then Throw New MissingFieldException(ProviderStrings.GetText("DMSClientNumber"))
            If OrganisationId.HasValue AndAlso OrganisationId.Value <= 0 Then
                Throw New ArgumentOutOfRangeException(NameOf(OrganisationId), "The explicit organisation ID must be positive.")
            End If
            If ExpectedTeamworkTenantId IsNot Nothing AndAlso ExpectedTeamworkTenantId <> "" AndAlso
                (String.IsNullOrWhiteSpace(ExpectedTeamworkTenantId) OrElse ExpectedTeamworkTenantId.Any(Function(character) Char.IsControl(character))) Then
                Throw New ArgumentException("The expected Teamwork tenant must be a nonblank identifier without control characters.", NameOf(ExpectedTeamworkTenantId))
            End If
        End Sub

    End Class

End Namespace
