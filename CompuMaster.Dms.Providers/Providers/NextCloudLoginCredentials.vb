Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data

Namespace Providers

    Public Class NextCloudLoginCredentials
        Inherits BaseDmsLoginCredentials

        Public Sub New()
            Me.DmsProvider = BaseDmsProvider.DmsProviders.NextCloud
        End Sub

        Public Overrides Sub Validate()
            MyBase.Validate()
            Select Case Me.DmsProvider
                Case BaseDmsProvider.DmsProviders.NextCloud
                Case Else
                    Throw New NotSupportedException("Login credentials provider NextCloud expected, but was " & Me.DmsProvider.ToString)
            End Select
        End Sub

    End Class

End Namespace