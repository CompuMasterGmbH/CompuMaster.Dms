Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Providers

Namespace Data

    Public MustInherit Class BaseDmsLoginCredentials
        Implements IDmsLoginProfile

        Public Property DmsProvider As BaseDmsProvider.DmsProviders

        ''' <summary>
        ''' An optional server address
        ''' </summary>
        ''' <returns></returns>
        Public Overridable Property BaseUrl As String

        ''' <summary>
        ''' An optional customer reference
        ''' </summary>
        ''' <returns></returns>
        Public Overridable Property CustomerInstance As String

        Public Property Username As String
        Public Property Password As String

        ''' <summary>
        ''' Ignore SSL handshake errors for this DMS server
        ''' </summary>
        ''' <returns></returns>
        Public Property IgnoreSslErrors As Boolean

        Public Property EncryptionProvider As Byte

        Protected Overridable ReadOnly Property IDmsLoginProfile_ProfileName As String Implements IDmsLoginProfile.ProfileName
            Get
                Return Me.DmsProvider.ToString
            End Get
        End Property

        Private ReadOnly Property IDmsLoginProfile_ProviderID As BaseDmsProvider.DmsProviders Implements IDmsLoginProfile.ProviderID
            Get
                Return Me.DmsProvider
            End Get
        End Property

        Private ReadOnly Property IDmsLoginProfile_UserName As String Implements IDmsLoginProfile.UserName
            Get
                Return Me.Username
            End Get
        End Property

        Private ReadOnly Property IDmsLoginProfile_Password As String Implements IDmsLoginProfile.Password
            Get
                Return Me.Password
            End Get
        End Property

        Private ReadOnly Property IDmsLoginProfile_CustomerInstance As String Implements IDmsLoginProfile.CustomerInstance
            Get
                Return Me.CustomerInstance
            End Get
        End Property

        Private ReadOnly Property IDmsLoginProfile_ServerAddress As String Implements IDmsLoginProfile.ServerAddress
            Get
                Return Me.BaseUrl
            End Get
        End Property

        Private ReadOnly Property IDmsLoginProfile_IgnoreSslErrors As Boolean Implements IDmsLoginProfile.IgnoreSslErrors
            Get
                Return Me.IgnoreSslErrors
            End Get
        End Property

        Protected Function EncryptText(value As String) As String
            Return value
        End Function

        Protected Function DecryptText(value As String) As String
            Return value
        End Function

        Public Overridable Sub Validate()
            If Me.DmsProvider = BaseDmsProvider.DmsProviders.None Then
                Throw New NotSupportedException("Login credentials can't exist for DMS provider ""None""")
            Else
                'TODO: ask provider for required fields/behaviour --> ATTENTION: circular assembly dependencies!
                'Select Case Data.Dms.Providers.CreateDmsProviderInstance().Type
                '    Case ...
                '        If Me.BaseUrl = Nothing Then Throw New MissingFieldException("DMS Endpoint URL (Base URL)")
                'End Select
                If Me.Username = Nothing Then Throw New MissingFieldException("DMS Username")
                If Me.Password = Nothing Then Throw New MissingFieldException("DMS Password")
            End If
        End Sub

    End Class

End Namespace