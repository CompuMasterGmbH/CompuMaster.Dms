Option Explicit On
Option Strict On

Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest
Imports CenterDevice.Rest.Clients
Imports CenterDevice.Rest.Clients.OAuth
Imports CenterDevice.Rest.Clients.User
Imports CenterDevice.Rest.Clients.Tenant
Imports NUnit.Framework
Imports RestSharp

<TestFixture>
Public Class CenterDeviceMixedRequestPolicyTest
    <Test>
    Public Async Function SynchronousSdkRequestOwnsAdmissionAndQueuedNativeRequestCanCancel() As Task
        Dim origin = "https://" & Guid.NewGuid().ToString("N") & ".invalid/"
        Dim transport As New BlockingTransport()
        Using http = CenterDeviceHttpTransport.CreateHttpClient(transport), rest = New RestClient(http, New RestClientOptions(origin)), cancellation As New CancellationTokenSource()
            Dim client = CreateSdkClient(origin, rest)
            Dim owner = Task.Run(Function() client.GetLoggedInUserData(Authorization()))
            Try
                Await WithinAsync(transport.Entered.Task)
                Dim queued = client.GetLoggedInUserDataAsync(Authorization(), cancellation.Token)
                cancellation.Cancel()
                Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                         Await queued
                                                                     End Function, Func(Of Task)))
                Assert.That(transport.Calls, [Is].EqualTo(1), "Canceled native work must not enter the transport owned by a synchronous call.")
            Finally
                transport.Release.TrySetResult(True)
            End Try
            Await WithinAsync(owner)
            Assert.That((Await owner).Id, [Is].EqualTo("fixture-user"))
            Dim later = Await client.GetLoggedInUserDataAsync(Authorization())
            Assert.That(later.Id, [Is].EqualTo("fixture-user"))
            Assert.That(transport.MaximumActive, [Is].EqualTo(1))
        End Using
    End Function

    <Test>
    Public Async Function NativeSdkRequestSerializesADirectSynchronousCallFromAnotherClient() As Task
        Dim origin = "https://" & Guid.NewGuid().ToString("N") & ".invalid/"
        Dim firstTransport As New BlockingTransport()
        Dim secondTransport As New BlockingTransport()
        secondTransport.Release.TrySetResult(True)
        Using firstHttp = CenterDeviceHttpTransport.CreateHttpClient(firstTransport), secondHttp = CenterDeviceHttpTransport.CreateHttpClient(secondTransport),
            firstRest = New RestClient(firstHttp, New RestClientOptions(origin)), secondRest = New RestClient(secondHttp, New RestClientOptions(origin))
            Dim first = CreateSdkClient(origin, firstRest)
            Dim second = CreateSdkClient(origin, secondRest)
            Dim owner = first.GetLoggedInUserDataAsync(Authorization())
            Dim dispatched As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            Dim queued As Task(Of ExtendedUserData) = Nothing
            Try
                Await WithinAsync(firstTransport.Entered.Task)
                queued = Task.Run(Function()
                                      dispatched.TrySetResult(True)
                                      Return second.GetLoggedInUserData(Authorization())
                                  End Function)
                Await WithinAsync(dispatched.Task)
                Await Task.Delay(100)
                Assert.That(secondTransport.Calls, [Is].Zero, "The synchronous call must share admission across client instances.")
                Assert.That(queued.IsCompleted, [Is].False)
            Finally
                firstTransport.Release.TrySetResult(True)
            End Try
            Await WithinAsync(owner)
            Await WithinAsync(queued)
            Assert.That((Await queued).Id, [Is].EqualTo("fixture-user"))
            Assert.That(secondTransport.Calls, [Is].EqualTo(1))
        End Using
    End Function

    <Test>
    Public Async Function ExternalResponseBodyRetainsAdmissionUntilDisposedBeforeSynchronousSdkRead() As Task
        Dim origin = "https://" & Guid.NewGuid().ToString("N") & ".invalid/"
        Dim transport As New BlockingTransport()
        transport.Release.TrySetResult(True)
        Using http = CenterDeviceHttpTransport.CreateHttpClient(transport), rest = New RestClient(http, New RestClientOptions(origin))
            Dim client = CreateSdkClient(origin, rest)
            Dim queued As Task(Of ExtendedUserData) = Nothing
            Dim dispatched As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            Using response = Await http.GetAsync(origin & "external", HttpCompletionOption.ResponseHeadersRead)
                queued = Task.Run(Function()
                                      dispatched.TrySetResult(True)
                                      Return client.GetLoggedInUserData(Authorization())
                                  End Function)
                Await WithinAsync(dispatched.Task)
                Await Task.Delay(100)
                Assert.That(transport.Calls, [Is].EqualTo(1), "The external unconsumed response must retain its origin permit.")
            End Using
            Await WithinAsync(queued)
            Assert.That((Await queued).Id, [Is].EqualTo("fixture-user"))
            Assert.That(transport.Calls, [Is].EqualTo(2))
        End Using
    End Function

    Private Shared Async Function WithinAsync(operation As Task) As Task
        Assert.That(Await Task.WhenAny(operation, Task.Delay(5000)), [Is].SameAs(operation), "The isolated transport operation did not complete.")
        Await operation
    End Function

    Private Shared Function Authorization() As OAuthInfo
        Return New OAuthInfo With {.UserId = "fixture-user", .access_token = "fixture-token"}
    End Function

    Private Shared Function CreateSdkClient(origin As String, replacement As RestClient) As UserRestClient
        Dim result As New UserRestClient(Nothing, New FixtureConfiguration(origin), Nothing, "v2/")
        result.DisableOfflineModeSimulation()
        'Replace only the network transport; public SDK methods and its authorization/response pipeline execute unchanged.
        Dim field = GetType(CenterDeviceRestClient).GetField("client", BindingFlags.Instance Or BindingFlags.NonPublic)
        Assert.That(field, [Is].Not.Null, "The SDK transport field changed; update this isolated fixture.")
        DirectCast(field.GetValue(result), RestClient).Dispose()
        field.SetValue(result, replacement)
        Return result
    End Function

    Private Class FixtureConfiguration
        Implements IRestClientConfiguration
        Public Sub New(origin As String)
            BaseAddress = origin
        End Sub
        Public ReadOnly Property BaseAddress As String Implements IRestClientConfiguration.BaseAddress
        Public ReadOnly Property UserAgent As String Implements IRestClientConfiguration.UserAgent
            Get
                Return "Isolated mixed-call fixture"
            End Get
        End Property
    End Class

    Private Class BlockingTransport
        Inherits HttpMessageHandler
        Public ReadOnly Entered As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Public ReadOnly Release As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Public Calls As Integer
        Public MaximumActive As Integer
        Private Active As Integer
        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Interlocked.Increment(Calls)
            Dim current = Interlocked.Increment(Active)
            MaximumActive = Math.Max(MaximumActive, current)
            Entered.TrySetResult(True)
            Try
                Await Task.WhenAny(Release.Task, Task.Delay(Timeout.Infinite, cancellationToken))
                cancellationToken.ThrowIfCancellationRequested()
                Return New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StringContent("{""id"":""fixture-user""}", Text.Encoding.UTF8, "application/json")}
            Finally
                Interlocked.Decrement(Active)
            End Try
        End Function
    End Class
End Class
