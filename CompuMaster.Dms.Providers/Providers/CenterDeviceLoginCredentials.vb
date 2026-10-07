Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data

Namespace Providers

    ''' <summary>Stores the legacy CenterDevice credential settings; direct authorization remains incomplete.</summary>
    Public Class CenterDeviceLoginCredentials
        Inherits BaseDmsLoginCredentials

        ''' <summary>Initializes the provider credential settings.</summary>
        Public Sub New()
            MyBase.New
            Me.DmsProvider = BaseDmsProvider.DmsProviders.CenterDevice
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

        ''' <inheritdoc/>
        Public Overrides Sub Validate()
            MyBase.Validate()
            If Me.ClientNumber = Nothing Then Throw New MissingFieldException(ProviderStrings.GetText("DMSClientNumber"))
        End Sub

    End Class

End Namespace
