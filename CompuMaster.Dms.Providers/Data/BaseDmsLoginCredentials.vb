Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Providers

Namespace Data

    ''' <summary>Defines provider login credentials and the common login-profile contract.</summary>
    Public MustInherit Class BaseDmsLoginCredentials
        Implements IDmsLoginProfile

        ''' <summary>Gets or sets the provider associated with this object.</summary>
        Public Property DmsProvider As BaseDmsProvider.DmsProviders

        ''' <summary>
        ''' An optional server address
        ''' </summary>
        Public Overridable Property BaseUrl As String

        ''' <summary>
        ''' An optional customer reference
        ''' </summary>
        Public Overridable Property CustomerInstance As String

        ''' <summary>Gets or sets the login user name.</summary>
        Public Property Username As String
        ''' <summary>Gets or sets the credential or link password; it must not be logged.</summary>
        Public Property Password As String

        ''' <summary>
        ''' Ignore SSL handshake errors for this DMS server
        ''' </summary>
        Public Property IgnoreSslErrors As Boolean

        ''' <summary>Gets or sets the legacy encryption selector; the base text helpers currently return their input unchanged.</summary>
        Public Property EncryptionProvider As Byte

        ''' <summary>Gets the provider name used as the login-profile display name.</summary>
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

        ''' <summary>Returns credential text through the legacy encryption extension point.</summary>
        ''' <param name="value">The input credential text; the base implementation does not encrypt or decrypt it.</param>
        ''' <returns>The input text unchanged in this base implementation.</returns>
        Protected Function EncryptText(value As String) As String
            Return value
        End Function

        ''' <summary>Returns credential text through the legacy decryption extension point.</summary>
        ''' <param name="value">The input credential text; the base implementation does not encrypt or decrypt it.</param>
        ''' <returns>The input text unchanged in this base implementation.</returns>
        Protected Function DecryptText(value As String) As String
            Return value
        End Function

        ''' <summary>Validates the required credential fields for this provider.</summary>
        ''' <exception cref="MissingFieldException">A required credential field is absent.</exception>
        ''' <exception cref="NotSupportedException">Credentials are configured for no provider.</exception>
        Public Overridable Sub Validate()
            If Me.DmsProvider = BaseDmsProvider.DmsProviders.None Then
                Throw New NotSupportedException(ProviderStrings.GetText("LoginCredentialsCanTExistForDMSProvider"))
            Else
                'TODO: ask provider for required fields/behaviour --> ATTENTION: circular assembly dependencies!
                'Select Case Data.Dms.Providers.CreateDmsProviderInstance().Type
                '    Case ...
                '        If Me.BaseUrl = Nothing Then Throw New MissingFieldException("DMS Endpoint URL (Base URL)")
                'End Select
                If Me.Username = Nothing Then Throw New MissingFieldException(ProviderStrings.GetText("DMSUsername"))
                If Me.Password = Nothing Then Throw New MissingFieldException(ProviderStrings.GetText("DMSPassword"))
            End If
        End Sub

    End Class

End Namespace
