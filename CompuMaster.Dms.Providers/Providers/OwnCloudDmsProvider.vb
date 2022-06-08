Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Namespace Providers

    ''' <summary>
    ''' OwnCloud provider (OCS+WebDAV)
    ''' </summary>
    Public Class OwnCloudDmsProvider
        Inherits OcsDmsProvider

        Public Overrides ReadOnly Property DmsProviderID As DmsProviders
            Get
                Return DmsProviders.OwnCloud
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return "OwnCloud"
            End Get
        End Property

        Public Overloads Sub Authorize(loginCredentials As OwnCloudLoginCredentials)
            Dim OcsCredentials As New OcsLoginCredentials() With
                {
                .BaseUrl = loginCredentials.BaseUrl,
                .DmsProvider = loginCredentials.DmsProvider,
                .CustomerInstance = loginCredentials.CustomerInstance,
                .EncryptionProvider = loginCredentials.EncryptionProvider,
                .Username = loginCredentials.Username,
                .Password = loginCredentials.Password
                }
            MyBase.Authorize(OcsCredentials)
        End Sub

        Public Overrides Function CreateNewCredentialsInstance() As BaseDmsLoginCredentials
            Return New OwnCloudLoginCredentials
        End Function

        Public Overrides Sub Authorize(dmsProfile As Data.IDmsLoginProfile)
            Dim Credentials As OwnCloudLoginCredentials = CType(Me.CreateNewCredentialsInstance(), OwnCloudLoginCredentials)
            Credentials.BaseUrl = dmsProfile.ServerAddress
            Credentials.Username = dmsProfile.UserName
            Credentials.Password = dmsProfile.Password
            Me.Authorize(Credentials)
        End Sub

    End Class

End Namespace