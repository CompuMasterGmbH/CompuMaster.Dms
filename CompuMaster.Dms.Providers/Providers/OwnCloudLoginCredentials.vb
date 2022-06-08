Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data

Namespace Providers

    Public Class OwnCloudLoginCredentials
        Inherits BaseDmsLoginCredentials

        Public Sub New()
            Me.DmsProvider = BaseDmsProvider.DmsProviders.OwnCloud
        End Sub

        Public Overrides Sub Validate()
            MyBase.Validate()
            Select Case Me.DmsProvider
                Case BaseDmsProvider.DmsProviders.OwnCloud
                Case Else
                    Throw New NotSupportedException("Login credentials provider OwnCloud expected, but was " & Me.DmsProvider.ToString)
            End Select
        End Sub

    End Class

End Namespace