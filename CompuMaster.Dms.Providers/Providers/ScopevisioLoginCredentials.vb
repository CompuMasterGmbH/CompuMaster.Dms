Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data

Namespace Providers

    Public Class ScopevisioLoginCredentials
        Inherits BaseDmsLoginCredentials

        Public Sub New()
            MyBase.New
            Me.DmsProvider = BaseDmsProvider.DmsProviders.Scopevisio
        End Sub

        Public Overrides Property BaseUrl As String
            Get
                Return MyBase.BaseUrl
            End Get
            Set(value As String)
                Throw New NotSupportedException(ProviderStrings.GetText("CustomWebserviceURLsAreNotSupportedForThis"))
            End Set
        End Property

        Public Property ClientNumber As String
        Public Property OrganisationName As String

        Public Overrides Sub Validate()
            MyBase.Validate()
            If Me.ClientNumber = Nothing Then Throw New MissingFieldException(ProviderStrings.GetText("DMSClientNumber"))
        End Sub

    End Class

End Namespace