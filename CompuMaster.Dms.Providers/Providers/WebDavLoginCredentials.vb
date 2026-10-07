Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data

Namespace Providers

    ''' <summary>Stores a WebDAV server address and user credentials.</summary>
    Public Class WebDavLoginCredentials
        Inherits BaseDmsLoginCredentials

        ''' <summary>Initializes the provider credential settings.</summary>
        Public Sub New()
            Me.DmsProvider = BaseDmsProvider.DmsProviders.WebDAV
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub Validate()
            MyBase.Validate()
            Select Case Me.DmsProvider
                Case BaseDmsProvider.DmsProviders.WebDAV
                Case Else
                    Throw New NotSupportedException(ProviderStrings.Format("LoginCredentialsProviderWebDAVExpectedButWas", Me.DmsProvider.ToString))
            End Select
        End Sub

    End Class

End Namespace
