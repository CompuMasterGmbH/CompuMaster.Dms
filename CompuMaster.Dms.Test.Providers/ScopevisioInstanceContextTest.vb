Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Providers
Imports CompuMaster.Scopevisio.OpenApi
Imports CompuMaster.Scopevisio.OpenApi.Model
Imports NUnit.Framework

<TestFixture>
Public Class ScopevisioInstanceContextTest
    <Test>
    Public Sub DiscoveryTokenDoesNotChangeTheActiveBearerOrToken()
        Using transport As New HttpClient(New InstanceHandler())
            Dim active = FixtureClient(transport)
            Dim original = active.Token
            Dim instances = New SwitchingProvider(active).ListAvailableDmsInstances()
            Assert.That(instances.Select(Function(item) item.ID).ToArray(), [Is].EqualTo({"tenant-1", "tenant-2"}))
            Assert.That(active.Config.AccessToken, [Is].EqualTo("bearer-1"))
            Assert.That(active.Token, [Is].SameAs(original))
        End Using
    End Sub

    <Test>
    Public Sub SwitchingUsesFreshAccountAndTokenContextsInBothDirections()
        Dim handler As New InstanceHandler()
        Using transport As New HttpClient(handler)
            Dim provider As New SwitchingProvider(FixtureClient(transport))
            provider.SwitchTo(2)
            Dim secondIO = provider.IOClient
            Assert.That(provider.CurrentDmsInstance.ID, [Is].EqualTo("tenant-2"))
            Assert.That(provider.ApplicationContext.Organisation.Id, [Is].EqualTo(2))
            provider.SwitchTo(1)
            Assert.That(Object.ReferenceEquals(provider.IOClient, secondIO), [Is].False)
            Assert.That(provider.CurrentDmsInstance.ID, [Is].EqualTo("tenant-1"))
            Assert.That(provider.ApplicationContext.Organisation.Id, [Is].EqualTo(1))
            Assert.That(handler.AccountBearers, [Is].EqualTo({"bearer-2", "bearer-1"}))
        End Using
    End Sub

    <TestCase("token"), TestCase("tenant"), TestCase("account"), TestCase("account-identity")>
    Public Sub FailedSwitchPreservesTheAuthenticatedInstanceAndCanRecover(failure As String)
        Dim handler As New InstanceHandler()
        Using transport As New HttpClient(handler)
            Dim provider As New SwitchingProvider(FixtureClient(transport))
            provider.SwitchTo(1)
            Dim originalIO = provider.IOClient
            handler.Failure = failure
            Assert.Catch(Sub() provider.SwitchTo(2))
            Assert.That(provider.CurrentDmsInstance.ID, [Is].EqualTo("tenant-1"))
            Assert.That(provider.IOClient, [Is].SameAs(originalIO))
            Assert.That(provider.ApplicationContext.Organisation.Id, [Is].EqualTo(1))
            handler.Failure = Nothing
            provider.SwitchTo(2)
            Assert.That(provider.CurrentDmsInstance.ID, [Is].EqualTo("tenant-2"))
            Assert.That(provider.ApplicationContext.Organisation.Id, [Is].EqualTo(2))
        End Using
    End Sub

    Private Shared Function FixtureClient(transport As HttpClient) As OpenScopeApiClient
        Dim config As New Global.CompuMaster.Scopevisio.OpenApi.Client.Configuration With {
            .HttpClient = transport, .BasePath = "https://fixture.invalid/", .Username = "fixture-user",
            .Password = "fixture-password", .ClientNumber = "fixture-customer", .AccessToken = "bearer-1"
        }
        Return New OpenScopeApiClient(config) With {.Token = New TokenResponse(TokenResponse.TokenTypeEnum.Bearer, "bearer-1", 3600, "refresh-1", "fixture-user", 1, "organisation-1", "tenant-1")}
    End Function

    Private Class SwitchingProvider
        Inherits ScopevisioTeamworkDmsProvider
        Friend Sub New(client As OpenScopeApiClient)
            GetType(ScopevisioTeamworkDmsProvider).GetField("_OpenScopeClient", BindingFlags.NonPublic Or BindingFlags.Instance).SetValue(Me, client)
        End Sub
        Friend Sub SwitchTo(id As Long)
            ApplyOrganisation(New Organisation With {.Id = id, .Name = "organisation-" & id, .TeamworkTenantId = "tenant-" & id})
        End Sub
    End Class

    Private Class InstanceHandler
        Inherits HttpMessageHandler
        Friend Failure As String
        Friend ReadOnly AccountBearers As New List(Of String)
        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Dim result As Newtonsoft.Json.Linq.JObject
            If request.RequestUri.AbsolutePath = "/token" Then
                Dim body = Await request.Content.ReadAsStringAsync()
                Dim id = If(body.Contains("organisation_id=2"), 2, 1)
                result = Newtonsoft.Json.Linq.JObject.FromObject(New With {
                    .access_token = "bearer-" & id, .refresh_token = "refresh-" & id, .uid = "fixture-user",
                    .organisationId = If(Failure = "token", 99, id), .organisationName = "organisation-" & id,
                    .teamworkTenantId = If(Failure = "tenant", "wrong-tenant", "tenant-" & id)
                })
            ElseIf request.RequestUri.AbsolutePath = "/organisations" Then
                result = Newtonsoft.Json.Linq.JObject.FromObject(New With {.records = {
                    New With {.id = 1, .name = "organisation-1"}, New With {.id = 2, .name = "organisation-2"}
                }})
            ElseIf request.RequestUri.AbsolutePath.StartsWith("/token/organisation/") Then
                Return New HttpResponseMessage(HttpStatusCode.NoContent)
            ElseIf request.RequestUri.AbsolutePath = "/myaccount" Then
                If Failure = "account" Then Return New HttpResponseMessage(HttpStatusCode.ServiceUnavailable) With {.Content = New StringContent("{}")}
                Dim bearer = request.Headers.Authorization.Parameter
                AccountBearers.Add(bearer)
                Dim id = If(bearer = "bearer-2", 2, 1)
                result = Newtonsoft.Json.Linq.JObject.FromObject(New With {.user = New With {.login = "fixture-login"}, .organisation = New With {.id = If(Failure = "account-identity", 99, id)}})
            Else
                Throw New InvalidOperationException("Unexpected fixture request: " & request.Method.ToString() & " " & request.RequestUri.AbsolutePath)
            End If
            Return New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent(result.ToString(), Text.Encoding.UTF8, "application/json")}
        End Function
    End Class
End Class
