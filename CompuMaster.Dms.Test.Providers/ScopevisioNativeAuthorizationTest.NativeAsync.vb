Option Explicit On
Option Strict On

Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class ScopevisioNativeAuthorizationTest
    <Test>
    Public Sub SynchronousAuthorizationUsesTheNativeConfigurationAndAccountTransport()
        Dim handler As New AuthorizationHandler()
        Using transport As New HttpClient(handler)
            Dim provider As New TestProvider(transport)
            provider.Authorize(Profile())
            Assert.That(handler.Calls, [Is].EqualTo(2))
            Assert.That(provider.AuthorizedClient, [Is].Not.Null)
            Assert.That(provider.AuthorizedClient.CurrentAuthenticationContextUserID, [Is].EqualTo("fixture-user-id"))
        End Using
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub SynchronousAuthorizationRetainsNativeAdmissionAndCredentialOptions(ignoreSslErrors As Boolean)
        Dim provider As New AdmissionProvider()
        Dim credentials = Profile()
        provider.Authorize(credentials, ignoreSslErrors)
        Assert.That(provider.Credentials, [Is].SameAs(credentials))
        Assert.That(provider.IgnoreSslErrors, [Is].EqualTo(ignoreSslErrors))
        Assert.That(provider.Token.CanBeCanceled, [Is].False)
    End Sub

    <Test>
    Public Sub SynchronousAuthorizationPreservesTheNativeFailureWithoutAggregateWrapping()
        Dim failure As New InvalidOperationException("fixture failure")
        Dim provider As New AdmissionProvider With {.Failure = failure}
        Dim actual = Assert.Throws(Of InvalidOperationException)(Sub() provider.Authorize(Profile()))
        Assert.That(actual, [Is].SameAs(failure))
    End Sub

    <Test>
    Public Async Function NativeAuthorizationInstallsAnAsynchronouslyConstructedClient() As Task
        Dim handler As New AuthorizationHandler()
        Using transport As New HttpClient(handler)
            Dim provider As New TestProvider(transport)
            Await provider.AuthorizeAsync(Profile())
            Assert.That(handler.Calls, [Is].EqualTo(2))
            Assert.That(provider.AuthorizedClient, [Is].Not.Null)
            Assert.That(provider.AuthorizedClient.CurrentAuthenticationContextUserID, [Is].EqualTo("fixture-user-id"))
        End Using
    End Function

    <TestCase(False), TestCase(True)>
    Public Async Function ActiveCancellationPreservesThePreviousAuthorizedClient(blockAccount As Boolean) As Task
        Dim handler As New AuthorizationHandler()
        Using transport As New HttpClient(handler)
            Dim provider As New TestProvider(transport)
            Await provider.AuthorizeAsync(Profile())
            Dim previousClient = provider.AuthorizedClient
            handler.BlockRequests = True
            handler.BlockAccount = blockAccount
            Using cancellation As New CancellationTokenSource()
                Dim operation = provider.AuthorizeAsync(Profile(), cancellation.Token)
                Await handler.Entered.Task
                cancellation.Cancel()
                Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                         Await operation
                                                                     End Function, Func(Of Task)))
            End Using
            Assert.That(provider.AuthorizedClient, [Is].SameAs(previousClient))
        End Using
    End Function

    <Test>
    Public Sub TokenAuthenticationFailuresPreserveTheirOriginalException()
        Dim handler As New AuthorizationHandler With {.Status = HttpStatusCode.Unauthorized}
        Using transport As New HttpClient(handler)
            Dim provider As New TestProvider(transport)
            Dim exception = Assert.ThrowsAsync(Of DmsUserAuthenticationException)(CType(Async Function()
                                                                                          Await provider.AuthorizeAsync(Profile())
                                                                                      End Function, Func(Of Task)))
            Assert.That(exception.InnerException, [Is].InstanceOf(Of Global.CompuMaster.Scopevisio.OpenApi.Client.ApiException)())
            Assert.That(provider.AuthorizedClient, [Is].Null)
        End Using
    End Sub

    Private Shared Function Profile() As ScopevisioLoginCredentials
        Return New ScopevisioLoginCredentials With {.Username = "fixture-login", .Password = "fixture-password", .ClientNumber = "fixture-customer", .OrganisationName = "fixture-org"}
    End Function

    Private Class AdmissionProvider
        Inherits ScopevisioTeamworkDmsProvider
        Public Credentials As ScopevisioLoginCredentials
        Public IgnoreSslErrors As Boolean
        Public Token As CancellationToken
        Public Failure As Exception
        Protected Overrides Function AuthorizeNativeCoreAsync(credentials As ScopevisioLoginCredentials, ignoreSslErrors As Boolean, cancellationToken As CancellationToken) As Task
            Me.Credentials = credentials
            Me.IgnoreSslErrors = ignoreSslErrors
            Me.Token = cancellationToken
            If Failure IsNot Nothing Then Return Task.FromException(Failure)
            Return Task.CompletedTask
        End Function
    End Class

    Private Class TestProvider
        Inherits ScopevisioTeamworkDmsProvider
        Private ReadOnly transport As HttpClient
        Public Sub New(transport As HttpClient)
            Me.transport = transport
        End Sub
        Public ReadOnly Property AuthorizedClient As Global.CenterDevice.IO.IOClientBase
            Get
                Return Me.IOClient
            End Get
        End Property
        Protected Overrides Function CreateNativeAuthorizationConfiguration(credentials As ScopevisioLoginCredentials, ignoreSslErrors As Boolean) As Global.CompuMaster.Scopevisio.OpenApi.Client.Configuration
            Return New Global.CompuMaster.Scopevisio.OpenApi.Client.Configuration With {
                .Username = credentials.Username, .Password = credentials.Password, .ClientNumber = credentials.ClientNumber,
                .OrganisationName = credentials.OrganisationName, .HttpClient = Me.transport
            }
        End Function
    End Class

    Private Class AuthorizationHandler
        Inherits HttpMessageHandler
        Public Calls As Integer
        Public Status As HttpStatusCode = HttpStatusCode.OK
        Public BlockRequests As Boolean
        Public BlockAccount As Boolean
        Public ReadOnly Entered As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Calls += 1
            If BlockRequests AndAlso ((request.Method = HttpMethod.Get) = BlockAccount) Then
                Entered.TrySetResult(True)
                Await Task.Delay(Timeout.Infinite, cancellationToken)
            End If
            Dim json = If(request.Method = HttpMethod.Get,
                          "{""user"":{""uid"":""fixture-account-id"",""login"":""fixture-login""}}",
                          "{""token_type"":""bearer"",""access_token"":""fixture-access"",""expires_in"":3600,""refresh_token"":""fixture-refresh"",""uid"":""fixture-user-id"",""organisationId"":1,""organisationName"":""fixture-org"",""teamworkTenantId"":""fixture-tenant""}")
            Return New HttpResponseMessage(Status) With {.Content = New StringContent(json, System.Text.Encoding.UTF8, "application/json")}
        End Function
    End Class
End Class
