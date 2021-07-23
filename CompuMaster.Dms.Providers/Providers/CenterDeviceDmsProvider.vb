Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports CompuMaster.Scopevisio.OpenApi
Imports CompuMaster.Scopevisio.OpenApi.Api
Imports CompuMaster.Scopevisio.OpenApi.Client
Imports CompuMaster.Scopevisio.OpenApi.Model
Imports RestSharp

Namespace Providers

    ''' <summary>
    ''' Center Device REST API
    ''' </summary>
    ''' <inheritdoc path="https://public.centerdevice.de/02bf3cfd-06c6-4d43-9cd4-3c18aab0020a"/>
    Public Class CenterDeviceDmsProvider
        Inherits CenterDeviceDmsProviderBase

        Public Overrides ReadOnly Property DmsProviderID As DmsProviders
            Get
                Return DmsProviders.CenterDevice
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return "CenterDevice"
            End Get
        End Property

        Public Overrides ReadOnly Property WebApiDefaultUrl As String
            Get
                Return "https://auth.centerdevice.de/authorize"
            End Get
        End Property

        Public Overrides ReadOnly Property WebApiUrlCustomization As UrlCustomizationType
            Get
                Return UrlCustomizationType.WebApiUrlNotCustomizable
            End Get
        End Property

        Public Overrides ReadOnly Property WebApiUserCustomerReferenceRequirement As UserCustomerReferenceType
            Get
                Return UserCustomerReferenceType.CustomerReferenceRequired
            End Get
        End Property

        Protected Overrides Function CustomizedWebApiUrl(loginCredentials As BaseDmsLoginCredentials) As String
            Return Me.WebApiDefaultUrl
        End Function

        Public Overloads Sub Authorize(loginCredentials As CenterDeviceLoginCredentials)
            'Dim Url As String = Me.CustomizedWebApiUrl(loginCredentials)
            'Dim OpenScopeConfig As New Global.CompuMaster.Scopevisio.OpenApi.Client.Configuration()
            'OpenScopeConfig.Username = loginCredentials.Username
            'OpenScopeConfig.Password = loginCredentials.Password
            'OpenScopeConfig.ClientNumber = loginCredentials.ClientNumber

            'Dim AuthApi As New CompuMaster.Scopevisio.OpenApi.Api.AuthorizationApi()
            'Dim TokenResult As ApiResponse(Of TokenResponse) = AuthApi.TokenWithHttpInfo("password", loginCredentials.ClientNumber, Nothing, Nothing, loginCredentials.Username, Nothing, Nothing, loginCredentials.Password, Nothing, Nothing, Nothing, Nothing)
            'Me.ApiToken = TokenResult.Data

            Dim CenterDeviceRestClient As CenterDevice.Rest.Clients.CenterDeviceClient = Nothing 'TODO
            'CenterDeviceRestClient = New CenterDevice.Rest.Clients.CenterDeviceClient(oAuthInfoProvider, Configuration, errorHandler) 'TODO
            Me.IOClient = New CenterDevice.IO.CenterDeviceIOClient(CenterDeviceRestClient, loginCredentials.Username)
        End Sub

        Public Overrides Function CreateNewCredentialsInstance() As BaseDmsLoginCredentials
            Return New CenterDeviceLoginCredentials()
        End Function

        Public Overrides Sub Authorize(dmsProfile As IDmsLoginProfile)
            Dim Credentials As CenterDeviceLoginCredentials = CType(Me.CreateNewCredentialsInstance(), CenterDeviceLoginCredentials)
            Credentials.Username = dmsProfile.UserName
            Credentials.ClientNumber = dmsProfile.CustomerInstance
            Credentials.Password = dmsProfile.Password
            Me.Authorize(Credentials)
        End Sub

        Public Overrides ReadOnly Property DocumentationGuideFiBuUploadsFileName As String
            Get
                Return "FiBu-Upload-Guide-CenterDevice.pdf"
            End Get
        End Property

    End Class

End Namespace