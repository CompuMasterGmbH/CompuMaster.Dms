Option Explicit On
Option Strict On

Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Reflection
Imports Newtonsoft.Json.Linq
Imports Newtonsoft.Json
Imports NUnit.Framework
Imports CompuMaster.Dms.Providers
Imports CompuMaster.Scopevisio.OpenApi
Imports CompuMaster.Scopevisio.OpenApi.Model

<TestFixture>
Public Class ScopevisioExplicitAuthorizationTest
    Private Shared Function Profile() As ScopevisioLoginCredentials
        Return New ScopevisioLoginCredentials With {.Username = "fixture-login", .Password = "fixture-password", .ClientNumber = "fixture-customer", .OrganisationName = "must-not-send", .OrganisationId = 7, .ExpectedTeamworkTenantId = "fixture-tenant"}
    End Function

    <TestCase(False), TestCase(True)>
    Public Async Function NumericLoginUsesOnlyDirectTokenAndFreshAccount(native As Boolean) As Task
        Dim handler As New Handler()
        Using transport As New HttpClient(handler)
            Dim provider As New FixtureProvider(transport)
            If native Then
                Await provider.AuthorizeAsync(Profile())
            Else
                provider.Authorize(Profile())
            End If
            Assert.That(handler.Paths, [Is].EqualTo(New String() {"/token", "/myaccount"}))
            Assert.That(handler.Forms(0), Does.Contain("organisation_id=7"))
            Assert.That(handler.Forms(0), Does.Not.Contain("organisation="))
            Assert.That(provider.CurrentDmsInstance.ID, [Is].EqualTo("fixture-tenant"))
            Assert.That(provider.CurrentClient.Token.OrganisationId, [Is].EqualTo(7))
        End Using
    End Function

    <TestCase("token-fraction"), TestCase("token-org"), TestCase("token-tenant"), TestCase("account-fraction"), TestCase("account-org"), TestCase("account-uid")>
    Public Async Function FailedExplicitLoginRetainsPriorClientAndBearer(mode As String) As Task
        Dim handler As New Handler()
        Using transport As New HttpClient(handler)
            Dim provider As New FixtureProvider(transport)
            Await provider.AuthorizeAsync(Profile())
            Dim old = provider.CurrentClient
            Dim oldIO = provider.IOClient
            handler.Mode = mode
            Assert.CatchAsync(CType(Async Function() As Task
                                       Await provider.AuthorizeAsync(Profile())
                                   End Function, Func(Of Task)))
            Assert.That(provider.CurrentClient, [Is].SameAs(old))
            Assert.That(provider.IOClient, [Is].SameAs(oldIO))
            Assert.That(old.Token.AccessToken, [Is].EqualTo("fixture-access"))
            Assert.That(provider.CurrentDmsInstance.ID, [Is].EqualTo("fixture-tenant"))
        End Using
    End Function

    <Test>
    Public Async Function AccountCancellationRetainsPriorState() As Task
        Dim handler As New Handler()
        Using transport As New HttpClient(handler)
            Dim provider As New FixtureProvider(transport)
            Await provider.AuthorizeAsync(Profile())
            Dim old = provider.CurrentClient
            handler.BlockAccount = True
            Using cancellation As New CancellationTokenSource()
                Dim task = provider.AuthorizeAsync(Profile(), cancellation.Token)
                Await handler.Entered.Task
                cancellation.Cancel()
                Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function() As Task
                                                                         Await task
                                                                     End Function, Func(Of Task)))
            End Using
            Assert.That(provider.CurrentClient, [Is].SameAs(old))
        End Using
    End Function

    <TestCase("token-fraction"), TestCase("token-org"), TestCase("token-tenant"), TestCase("token-uid"), TestCase("account-fraction"), TestCase("account-uid")>
    Public Async Function RejectedRefreshDoesNotInstallItsCandidate(mode As String) As Task
        Dim handler As New Handler()
        Using transport As New HttpClient(handler)
            Dim provider As New FixtureProvider(transport)
            Await provider.AuthorizeAsync(Profile())
            Dim client = provider.CurrentClient
            Dim account = JsonConvert.DeserializeObject(Of AccountInfo)(Handler.AccountBody(Nothing))
            Dim identity = ScopevisioExplicitIdentity.FromToken(client.Token, Handler.TokenBody(Nothing), 7, "fixture-tenant")
            Dim session As New ScopevisioExplicitSession(client, account, identity)
            Dim rejected = session.GetOAuthInfo()
            Dim old = client.Token
            handler.Mode = mode
            Assert.CatchAsync(CType(Async Function() As Task
                                       Await session.RefreshAsync(rejected, CancellationToken.None)
                                   End Function, Func(Of Task)))
            Assert.That(client.Token, [Is].SameAs(old))
            Assert.That(client.Config.AccessToken, [Is].EqualTo("fixture-access"))
            Assert.That(handler.Forms.Last(), Does.Contain("grant_type=refresh_token"))
            Assert.That(handler.Forms.Last(), Does.Not.Contain("organisation_id="))
        End Using
    End Function

    <Test>
    Public Async Function SuccessfulConcurrentRejectionsReuseTheAlreadyRotatedToken() As Task
        Dim handler As New Handler()
        Using transport As New HttpClient(handler)
            Dim provider As New FixtureProvider(transport)
            Await provider.AuthorizeAsync(Profile())
            Dim client = provider.CurrentClient
            Dim identity = ScopevisioExplicitIdentity.FromToken(client.Token, Handler.TokenBody(Nothing), 7, "fixture-tenant")
            Dim session As New ScopevisioExplicitSession(client, JsonConvert.DeserializeObject(Of AccountInfo)(Handler.AccountBody(Nothing)), identity)
            Dim rejected = session.GetOAuthInfo()
            handler.Rotate = True
            Await Task.WhenAll(session.RefreshAsync(rejected, CancellationToken.None), session.RefreshAsync(rejected, CancellationToken.None))
            Assert.That(client.Token.AccessToken, [Is].EqualTo("rotated-access"))
            Assert.That(handler.Forms.Where(Function(form) form.Contains("grant_type=refresh_token")).Count(), [Is].EqualTo(1))
            Assert.That(session.GetOAuthInfo().access_token, [Is].EqualTo("rotated-access"))
            Assert.That(handler.Paths, Does.Not.Contain("/organisations"))
        End Using
    End Function

    <Test>
    Public Async Function ExplicitSelectionRejectsCatalogAndProtectedSwitchWithoutRequests() As Task
        Dim handler As New Handler()
        Using transport As New HttpClient(handler)
            Dim provider As New FixtureProvider(transport)
            Await provider.AuthorizeAsync(Profile())
            Dim old = provider.CurrentClient
            Assert.That(provider.ListAvailableDmsInstances().Select(Function(instance) instance.ID), [Is].EqualTo(New String() {"fixture-tenant"}))
            Assert.DoesNotThrow(Sub() provider.SelectDmsInstance("fixture-tenant"))
            Assert.Throws(Of InvalidOperationException)(Sub() provider.SelectDmsInstance("other-tenant"))
            Assert.Throws(Of InvalidOperationException)(Sub() provider.ApplyForTest(8, "other-tenant"))
            Assert.That(provider.CurrentClient, [Is].SameAs(old))
            Assert.That(handler.Paths, [Is].EqualTo(New String() {"/token", "/myaccount"}))
        End Using
    End Function

    <Test>
    Public Async Function CancelledRefreshPreservesActiveTokenAndBorrowedTransport() As Task
        Dim handler As New Handler()
        Using transport As New HttpClient(handler)
            Dim provider As New FixtureProvider(transport)
            Await provider.AuthorizeAsync(Profile())
            Dim client = provider.CurrentClient
            Dim old = client.Token
            Dim identity = ScopevisioExplicitIdentity.FromToken(old, Handler.TokenBody(Nothing), 7, "fixture-tenant")
            Dim session As New ScopevisioExplicitSession(client, JsonConvert.DeserializeObject(Of AccountInfo)(Handler.AccountBody(Nothing)), identity)
            Dim rejected = session.GetOAuthInfo()
            handler.Rotate = True
            handler.BlockAccount = True
            Using cancellation As New CancellationTokenSource()
                Dim task = session.RefreshAsync(rejected, cancellation.Token)
                Await handler.Entered.Task
                cancellation.Cancel()
                Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function() As Task
                                                                         Await task
                                                                     End Function, Func(Of Task)))
            End Using
            Assert.That(client.Token, [Is].SameAs(old))
            Assert.That(client.Config.AccessToken, [Is].EqualTo("fixture-access"))
            Assert.That(handler.Disposed, [Is].False)
            handler.BlockAccount = False
            Await session.RefreshAsync(rejected, CancellationToken.None)
            Assert.That(client.Token.AccessToken, [Is].EqualTo("rotated-access"))
            Assert.That(handler.Disposed, [Is].False)
        End Using
    End Function

    <TestCase(False), TestCase(True)>
    Public Async Function ChangedExplicitProfileDuringAuthorizationRetainsPriorState(changeGuard As Boolean) As Task
        Dim handler As New Handler()
        Using transport As New HttpClient(handler)
            Dim provider As New FixtureProvider(transport)
            Await provider.AuthorizeAsync(Profile())
            Dim old = provider.CurrentClient
            Dim credentials = Profile()
            handler.TokenReturned = Sub()
                                        If changeGuard Then
                                            credentials.ExpectedTeamworkTenantId = ""
                                        Else
                                            credentials.OrganisationId = 8
                                        End If
                                    End Sub
            Assert.ThrowsAsync(Of InvalidOperationException)(CType(Async Function() As Task
                                                                       Await provider.AuthorizeAsync(credentials)
                                                                   End Function, Func(Of Task)))
            Assert.That(provider.CurrentClient, [Is].SameAs(old))
            Assert.That(provider.CurrentDmsInstance.ID, [Is].EqualTo("fixture-tenant"))
        End Using
    End Function

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
        Friend Sub ApplyForTest(organisationId As Long, tenant As String)
            Me.ApplyOrganisation(New Organisation With {.Id = organisationId, .TeamworkTenantId = tenant})
        End Sub
        Protected Overrides Function CreateNativeAuthorizationConfiguration(credentials As ScopevisioLoginCredentials, ignoreSslErrors As Boolean) As Global.CompuMaster.Scopevisio.OpenApi.Client.Configuration
            Return New Global.CompuMaster.Scopevisio.OpenApi.Client.Configuration With {.HttpClient = Transport, .BasePath = "https://fixture.invalid/", .Username = credentials.Username, .Password = credentials.Password, .ClientNumber = credentials.ClientNumber, .OrganisationName = credentials.OrganisationName}
        End Function
    End Class

    Private Class Handler
        Inherits HttpMessageHandler
        Friend Mode As String
        Friend Rotate As Boolean
        Friend BlockAccount As Boolean
        Friend Disposed As Boolean
        Friend TokenReturned As Action
        Friend ReadOnly Entered As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Friend ReadOnly Paths As New List(Of String)
        Friend ReadOnly Forms As New List(Of String)
        Friend Shared Function TokenBody(mode As String, Optional rotate As Boolean = False) As String
            Dim body As New JObject From {{"token_type", "bearer"}, {"access_token", If(rotate, "rotated-access", "fixture-access")}, {"refresh_token", "fixture-refresh"}, {"uid", If(mode = "token-uid", "different-scope-uid", "fixture-scope-uid")}, {"organisationId", JToken.Parse(If(mode = "token-fraction", "7.25", If(mode = "token-org", "8", "7")))}, {"teamworkTenantId", If(mode = "token-tenant", "other-tenant", "fixture-tenant")}}
            Return body.ToString()
        End Function
        Friend Shared Function AccountBody(mode As String) As String
            Return New JObject From {{"user", New JObject From {{"uid", If(mode = "account-uid", "different-scope-uid", "fixture-scope-uid")}, {"login", "fixture-login"}}}, {"organisation", New JObject From {{"id", JToken.Parse(If(mode = "account-fraction", "7.25", If(mode = "account-org", "8", "7")))}}}}.ToString()
        End Function
        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Paths.Add(request.RequestUri.AbsolutePath)
            Dim body As String
            If request.RequestUri.AbsolutePath = "/token" Then
                Forms.Add(Await request.Content.ReadAsStringAsync())
                body = TokenBody(Mode, Rotate)
                If TokenReturned IsNot Nothing Then TokenReturned()
            ElseIf request.RequestUri.AbsolutePath = "/myaccount" Then
                If BlockAccount Then
                    Entered.TrySetResult(True)
                    Await Task.Delay(Timeout.Infinite, cancellationToken)
                End If
                body = AccountBody(Mode)
            Else
                Throw New InvalidOperationException("Unexpected isolated fixture route.")
            End If
            Return New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent(body, Text.Encoding.UTF8, "application/json")}
        End Function
        Protected Overrides Sub Dispose(disposing As Boolean)
            Disposed = True
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Class
