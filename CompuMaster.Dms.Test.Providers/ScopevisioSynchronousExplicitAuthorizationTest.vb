Option Explicit On
Option Strict On

Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Providers
Imports CompuMaster.Scopevisio.OpenApi

<NUnit.Framework.TestFixture>
Public Class ScopevisioSynchronousExplicitAuthorizationTest
    Private Shared Function Credentials() As ScopevisioLoginCredentials
        Return New ScopevisioLoginCredentials With {.Username = "fixture-login", .Password = "fixture-password", .ClientNumber = "fixture-customer", .OrganisationName = "must-not-send", .OrganisationId = 7, .ExpectedTeamworkTenantId = "fixture-tenant"}
    End Function

    <NUnit.Framework.Test>
    Public Sub SynchronousNumericLoginUsesTheInjectedTransportAndValidatedAccount()
        Dim handler As New FixtureHandler()
        Using transport As New HttpClient(handler)
            Dim provider As New FixtureProvider(transport)
            provider.Authorize(Credentials())
            NUnit.Framework.Assert.That(handler.Paths, NUnit.Framework.Is.EqualTo(New String() {"/token", "/myaccount"}))
            NUnit.Framework.Assert.That(handler.Form, NUnit.Framework.Does.Contain("organisation_id=7"))
            NUnit.Framework.Assert.That(handler.Form, NUnit.Framework.Does.Not.Contain("organisation="))
            NUnit.Framework.Assert.That(provider.CurrentDmsInstance.ID, NUnit.Framework.Is.EqualTo("fixture-tenant"))
        End Using
    End Sub

    <NUnit.Framework.TestCase("token"), NUnit.Framework.TestCase("account"), NUnit.Framework.TestCase("profile")>
    Public Sub FailedSynchronousAdmissionKeepsThePreviousClientAndBearer(mode As String)
        Dim handler As New FixtureHandler()
        Using transport As New HttpClient(handler)
            Dim provider As New FixtureProvider(transport)
            provider.Authorize(Credentials())
            Dim old = provider.CurrentClient
            Dim oldIO = provider.IOClient
            Dim profile = Credentials()
            handler.Mode = mode
            If mode = "profile" Then handler.TokenReturned = Sub() profile.ExpectedTeamworkTenantId = ""
            NUnit.Framework.Assert.Throws(Of InvalidOperationException)(Sub() provider.Authorize(profile))
            NUnit.Framework.Assert.That(provider.CurrentClient, NUnit.Framework.Is.SameAs(old))
            NUnit.Framework.Assert.That(provider.IOClient, NUnit.Framework.Is.SameAs(oldIO))
            NUnit.Framework.Assert.That(old.Config.AccessToken, NUnit.Framework.Is.EqualTo("fixture-access"))
        End Using
    End Sub

    Private Class FixtureProvider
        Inherits ScopevisioTeamworkDmsProvider
        Private ReadOnly Transport As HttpClient
        Friend Sub New(transport As HttpClient)
            Me.Transport = transport
        End Sub
        Friend ReadOnly Property CurrentClient As OpenScopeApiClient
            Get
                Return CType(GetType(ScopevisioTeamworkDmsProvider).GetField("_OpenScopeClient", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), OpenScopeApiClient)
            End Get
        End Property
        Protected Overrides Function CreateAuthorizationConfiguration(profile As ScopevisioLoginCredentials, ignoreSslErrors As Boolean) As Global.CompuMaster.Scopevisio.OpenApi.Client.Configuration
            Return New Global.CompuMaster.Scopevisio.OpenApi.Client.Configuration With {.HttpClient = Transport, .BasePath = "https://fixture.invalid/", .Username = profile.Username, .Password = profile.Password, .ClientNumber = profile.ClientNumber, .OrganisationName = profile.OrganisationName}
        End Function
    End Class

    Private Class FixtureHandler
        Inherits HttpMessageHandler
        Friend Mode As String
        Friend TokenReturned As Action
        Friend Form As String
        Friend ReadOnly Paths As New List(Of String)
        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Paths.Add(request.RequestUri.AbsolutePath)
            Dim body As String
            Select Case request.RequestUri.AbsolutePath
                Case "/token"
                    Form = Await request.Content.ReadAsStringAsync()
                    body = "{""token_type"":""bearer"",""access_token"":""fixture-access"",""refresh_token"":""fixture-refresh"",""uid"":""fixture-scope-uid"",""organisationId"":7,""teamworkTenantId"":""" & If(Mode = "token", "other-tenant", "fixture-tenant") & """}"
                    If TokenReturned IsNot Nothing Then TokenReturned()
                Case "/myaccount"
                    body = "{""user"":{""uid"":""fixture-scope-uid"",""login"":""fixture-login""},""organisation"":{""id"":" & If(Mode = "account", "8", "7") & "}}"
                Case Else
                    Throw New InvalidOperationException("Unexpected isolated fixture route.")
            End Select
            Return New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent(body, Text.Encoding.UTF8, "application/json")}
        End Function
    End Class
End Class
