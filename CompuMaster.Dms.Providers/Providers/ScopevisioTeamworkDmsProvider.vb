Option Explicit On
Option Strict On

Imports System.Net.Http
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports CompuMaster.Scopevisio.OpenApi
Imports CompuMaster.Scopevisio.OpenApi.Api
Imports CompuMaster.Scopevisio.OpenApi.Client
Imports CompuMaster.Scopevisio.OpenApi.Model
Imports RestSharp

Namespace Providers

    ''' <summary>
    ''' Scopevision TeamWork REST API
    ''' </summary>
    ''' <inheritdoc path="https://www.openscope.de/api.html" />
    ''' <inheritdoc path="https://appload.scopevisio.com/static/browser/index.html#!/documentation"/>
    ''' <inheritdoc path="https://public.centerdevice.de/02bf3cfd-06c6-4d43-9cd4-3c18aab0020a"/>
    Public Class ScopevisioTeamworkDmsProvider
        Inherits CenterDeviceDmsProviderBase

        Public Overrides ReadOnly Property DmsProviderID As DmsProviders
            Get
                Return DmsProviders.Scopevisio
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return "Scopevisio Teamwork"
            End Get
        End Property

        Public Overrides ReadOnly Property WebApiDefaultUrl As String
            Get
                Return "https://appload.scopevisio.com/rest/teamworkbridge/"
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

        Public Overloads Sub Authorize(loginCredentials As ScopevisioLoginCredentials)
            Me.Authorize(loginCredentials, False)
        End Sub
        Public Overloads Sub Authorize(loginCredentials As ScopevisioLoginCredentials, ignoreSslErrors As Boolean)
            Dim IsTokenRequest As Boolean = False
            Try
                Dim OpenScopeConfig As New Global.CompuMaster.Scopevisio.OpenApi.Client.Configuration With {
                    .Username = loginCredentials.Username,
                    .Password = loginCredentials.Password,
                    .ClientNumber = loginCredentials.ClientNumber,
                    .OrganisationName = loginCredentials.OrganisationName
                }
                If ignoreSslErrors Then
                    Dim Handler As New System.Net.Http.HttpClientHandler() With {
                        .ServerCertificateCustomValidationCallback = Function(sender, certificate, chain, sslPolicyErrors) True
                        }
                    OpenScopeConfig.HttpClient = New System.Net.Http.HttpClient(Handler)
                End If
                Dim OpenScopeClient As New CompuMaster.Scopevisio.OpenApi.OpenScopeApiClient(OpenScopeConfig)
                IsTokenRequest = True
                OpenScopeClient.AuthorizeWithUserCredentials()
                IsTokenRequest = False
                Me.IOClient = New CompuMaster.Scopevisio.Teamwork.TeamworkIOClient(OpenScopeClient)
            Catch ex As CompuMaster.Scopevisio.OpenApi.Client.ApiException
                Throw CreateAuthorizationException(ex.ErrorCode, ex.ErrorContent, ex, IsTokenRequest)
            End Try
        End Sub

        Friend Shared Function CreateAuthorizationException(errorCode As Integer, errorContent As Object, originalException As Exception, isTokenRequest As Boolean) As Exception
            If isTokenRequest AndAlso errorCode = 401 Then
                Return New Data.DmsUserAuthenticationException("Scopevisio user authentication failed.", originalException)
            ElseIf errorCode = 403 AndAlso errorContent IsNot Nothing AndAlso errorContent.GetType Is GetType(String) AndAlso CType(errorContent, String).ToLowerInvariant.Contains("""message"":""no organisation found.""") Then
                Return New Data.DmsSystemErrorException("No organisation found, usually Scopevisio user authorizations are required: Rechteprofil Kontakte – alle Rechte oder CRM – alle Rechte", originalException)
            ElseIf errorCode = 403 AndAlso errorContent IsNot Nothing AndAlso errorContent.GetType Is GetType(String) AndAlso CType(errorContent, String).ToLowerInvariant.Contains("""message"":""customer is deleted""") Then
                Return New Data.DmsSystemErrorException("DMS-Instanz des Kunden wurde gelöscht", originalException)
            Else
                Return New Data.DmsSystemErrorException(originalException.Message, originalException)
            End If
        End Function

        Public ReadOnly Property ApplicationContext As CompuMaster.Scopevisio.OpenApi.Model.AccountInfo
            Get
                Return CType(Me.IOClient, CompuMaster.Scopevisio.Teamwork.TeamworkIOClient).TeamworkRestClient.ApplicationContext
            End Get
        End Property

        Public Overrides Function CreateNewCredentialsInstance() As BaseDmsLoginCredentials
            Return New ScopevisioLoginCredentials
        End Function

        Public Overrides Sub Authorize(dmsProfile As Data.IDmsLoginProfile)
            Dim Credentials As ScopevisioLoginCredentials = CType(Me.CreateNewCredentialsInstance(), ScopevisioLoginCredentials)
            Credentials.Username = dmsProfile.UserName
            Credentials.ClientNumber = dmsProfile.CustomerInstance
            Credentials.Password = dmsProfile.Password
            Me.Authorize(Credentials, dmsProfile.IgnoreSslErrors)
        End Sub

    End Class

End Namespace
