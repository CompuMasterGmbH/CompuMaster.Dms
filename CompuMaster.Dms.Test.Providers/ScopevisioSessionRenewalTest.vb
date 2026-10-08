Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest.Clients.OAuth
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports CompuMaster.Scopevisio.OpenApi
Imports CompuMaster.Scopevisio.OpenApi.Model
Imports NUnit.Framework

<TestFixture>
Public Class ScopevisioSessionRenewalTest
    <Test>
    Public Async Function ConcurrentRejectedTokensShareOneRefreshAndPreserveIdentity() As Task
        Dim handler As New TokenHandler With {.Block = True}
        Using transport As New HttpClient(handler)
            Dim client = ClientWithToken(transport)
            Dim session = Authorization(client)
            Dim rejected As New OAuthInfo With {.access_token = "fixture-old"}
            Dim first = session.RefreshTokenAsync(rejected)
            Await handler.Entered.Task
            Dim second = session.RefreshTokenAsync(rejected)
            handler.Release.SetResult(True)
            Dim values = Await Task.WhenAll(first, second)
            Assert.That(handler.Refreshes, [Is].EqualTo(1))
            Assert.That(values.All(Function(value) value.access_token = "fixture-new" AndAlso value.UserId = "fixture-user" AndAlso value.TenantId = "fixture-tenant"), [Is].True)
            Assert.That(client.Token.RefreshToken, [Is].EqualTo("fixture-rotated"))
            Dim reused = Await session.GetOAuthInfoAsync("fixture-user")
            Assert.That(reused.access_token, [Is].EqualTo("fixture-new"))
            Assert.That(handler.Refreshes, [Is].EqualTo(1))
        End Using
    End Function

    <Test>
    Public Sub SynchronousRefreshAndTokenReadsUseTheSameFreshSession()
        Dim handler As New TokenHandler()
        Using transport As New HttpClient(handler)
            Dim session = Authorization(ClientWithToken(transport))
            Dim fresh = session.RefreshToken(New OAuthInfo With {.access_token = "fixture-old"})
            Assert.That(fresh.access_token, [Is].EqualTo("fixture-new"))
            Assert.That(session.GetOAuthInfo("fixture-user").access_token, [Is].EqualTo("fixture-new"))
            Assert.That(session.RefreshToken(New OAuthInfo With {.access_token = "fixture-old"}).access_token, [Is].EqualTo("fixture-new"))
            Assert.That(handler.Refreshes, [Is].EqualTo(1))
        End Using
    End Sub

    <TestCase(400, False), TestCase(400, True), TestCase(401, False), TestCase(401, True)>
    Public Async Function PermanentRenewalFailureRequiresReauthorizationAndRetainsOriginal(status As Integer, asynchronous As Boolean) As Task
        Dim handler As New TokenHandler With {.Status = CType(status, HttpStatusCode)}
        Using transport As New HttpClient(handler)
            Dim client = ClientWithToken(transport)
            Dim session = Authorization(client)
            Dim rejected As New OAuthInfo With {.access_token = "fixture-old"}
            Dim failure As DmsUserAuthenticationException
            If asynchronous Then
                failure = Assert.ThrowsAsync(Of DmsUserAuthenticationException)(Function() session.RefreshTokenAsync(rejected))
            Else
                failure = Assert.Throws(Of DmsUserAuthenticationException)(CType(Sub() session.RefreshToken(rejected), Action))
            End If
            Assert.That(failure.Message, Does.Not.Contain("fixture-sensitive"))
            Assert.That(failure.InnerException, [Is].InstanceOf(Of Global.CompuMaster.Scopevisio.OpenApi.Client.ApiException)())
            Assert.That(client.Token.AccessToken, [Is].EqualTo("fixture-old"))
            handler.Status = HttpStatusCode.OK
            Assert.That((Await session.RefreshTokenAsync(New OAuthInfo With {.access_token = "fixture-old"})).access_token, [Is].EqualTo("fixture-new"))
        End Using
    End Function

    <Test>
    Public Async Function TransientFailureAndCancellationPreserveTheTokenAndReleaseTheGate() As Task
        Dim handler As New TokenHandler With {.Status = HttpStatusCode.ServiceUnavailable}
        Using transport As New HttpClient(handler), cancellation As New CancellationTokenSource()
            Dim client = ClientWithToken(transport)
            Dim session = Authorization(client)
            Assert.ThrowsAsync(Of Global.CompuMaster.Scopevisio.OpenApi.Client.ApiException)(Function() session.RefreshTokenAsync(New OAuthInfo With {.access_token = "fixture-old"}))
            Assert.That(client.Token.AccessToken, [Is].EqualTo("fixture-old"))
            handler.Status = HttpStatusCode.OK
            handler.Block = True
            Dim operation = session.RefreshTokenAsync(New OAuthInfo With {.access_token = "fixture-old"}, cancellation.Token)
            Await handler.Entered.Task
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(Function() operation)
            Assert.That(client.Token.AccessToken, [Is].EqualTo("fixture-old"))
            handler.Block = False
            Assert.That((Await session.RefreshTokenAsync(New OAuthInfo With {.access_token = "fixture-old"})).access_token, [Is].EqualTo("fixture-new"))
        End Using
    End Function

    <Test>
    Public Sub MissingRefreshCredentialsAndRepeatedRejectionAreActionableWithoutBodyLeakage()
        Using transport As New HttpClient(New TokenHandler())
            Dim client = ClientWithToken(transport)
            client.Token.RefreshToken = Nothing
            Dim session = Authorization(client)
            Assert.Throws(Of DmsUserAuthenticationException)(Sub() session.RefreshToken(New OAuthInfo With {.access_token = "fixture-old"}))
            Dim failure = Assert.Throws(Of DmsUserAuthenticationException)(Sub() session.ValidateResponse(New RestSharp.RestResponse With {.StatusCode = HttpStatusCode.Unauthorized, .Content = "fixture-sensitive"}))
            Assert.That(failure.Message, Does.Not.Contain("fixture-sensitive"))
            Assert.DoesNotThrow(Sub() session.ValidateResponse(New RestSharp.RestResponse With {.StatusCode = HttpStatusCode.Forbidden}))
        End Using
    End Sub

    Private Shared Function ClientWithToken(transport As HttpClient) As OpenScopeApiClient
        Dim client As New OpenScopeApiClient(New Global.CompuMaster.Scopevisio.OpenApi.Client.Configuration With {.HttpClient = transport, .BasePath = "https://fixture.invalid/", .ClientNumber = "fixture-customer"})
        client.Token = New TokenResponse(TokenResponse.TokenTypeEnum.Bearer, "fixture-old", 3600, "fixture-refresh", "fixture-user", 1, "fixture-org", "fixture-tenant")
        Return client
    End Function
    Private Shared Function Authorization(client As OpenScopeApiClient) As ScopevisioSessionAuthorization
        Return New ScopevisioSessionAuthorization(client, New AccountInfo With {.User = New User With {.Login = "fixture-login"}})
    End Function
    Private Class TokenHandler
        Inherits HttpMessageHandler
        Friend Refreshes As Integer
        Friend Status As HttpStatusCode = HttpStatusCode.OK
        Friend Block As Boolean
        Friend ReadOnly Entered As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Friend ReadOnly Release As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Dim json As String
            If request.Method = HttpMethod.Get Then
                json = "{""user"":{""login"":""fixture-login""}}"
            Else
                Refreshes += 1
                If Block Then
                    Entered.TrySetResult(True)
                    While Not Release.Task.IsCompleted
                        Await Task.Delay(1, cancellationToken)
                    End While
                End If
                json = If(Status = HttpStatusCode.OK,
                          "{""access_token"":""fixture-new"",""refresh_token"":""fixture-rotated"",""uid"":""fixture-user"",""teamworkTenantId"":""fixture-tenant"",""organisationId"":1,""organisationName"":""fixture-org""}",
                          "{""error"":""invalid_grant"",""error_description"":""fixture-sensitive""}")
            End If
            Return New HttpResponseMessage(Status) With {.Content = New StringContent(json, Text.Encoding.UTF8, "application/json")}
        End Function
    End Class
End Class
