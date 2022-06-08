Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data

Namespace Providers

    ''' <summary>
    ''' Login credentials for Open Collaboration Service (OCS)
    ''' </summary>
    Public Class OcsLoginCredentials
        Inherits BaseDmsLoginCredentials

        Public Sub New()
            Me.DmsProvider = BaseDmsProvider.DmsProviders.OCS
        End Sub

        Public Overrides Sub Validate()
            MyBase.Validate()
            Select Case Me.DmsProvider
                Case BaseDmsProvider.DmsProviders.OCS
                Case Else
                    Throw New NotSupportedException("Login credentials provider Open Collaboration Service (OCS) expected, but was " & Me.DmsProvider.ToString)
            End Select
        End Sub

    End Class

End Namespace