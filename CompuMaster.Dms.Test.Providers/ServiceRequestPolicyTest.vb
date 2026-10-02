Option Strict On

Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class ServiceRequestPolicyTest

    <Test>
    Public Async Function RetriesReadAfterRetryAfter() As Task
        Dim clock As New FakeClock
        Dim calls As Integer
        Using client As New HttpClient(New ServiceRequestPolicy(New Uri("https://retry.test/"), New StubHandler(Function(request)
                                                                                                      calls += 1
                                                                                                      If calls = 1 Then
                                                                                                          Dim throttled As New HttpResponseMessage(CType(429, HttpStatusCode))
                                                                                                          throttled.Headers.RetryAfter = New Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(7))
                                                                                                          Return throttled
                                                                                                      End If
                                                                                                      Return New HttpResponseMessage(HttpStatusCode.OK)
                                                                                                  End Function), clock))
            Dim response = Await client.GetAsync("https://retry.test/file")
            Assert.That(response.StatusCode, [Is].EqualTo(HttpStatusCode.OK))
            Assert.That(calls, [Is].EqualTo(2))
            Assert.That(clock.Delays, [Is].EqualTo({TimeSpan.FromSeconds(7)}))
        End Using
    End Function

    <Test>
    Public Async Function DoesNotRetryWritesOrPermanentErrors() As Task
        Dim clock As New FakeClock
        Dim calls As Integer
        Using client As New HttpClient(New ServiceRequestPolicy(New Uri("https://writes.test/"), New StubHandler(Function(request)
                                                                                                       calls += 1
                                                                                                       Return New HttpResponseMessage(CType(429, HttpStatusCode))
                                                                                                   End Function), clock))
            Dim writeResponse = Await client.PostAsync("https://writes.test/file", New StringContent("data"))
            Assert.That(writeResponse.StatusCode, [Is].EqualTo(CType(429, HttpStatusCode)))
            Assert.That(calls, [Is].EqualTo(1))
        End Using
        Using client As New HttpClient(New ServiceRequestPolicy(New Uri("https://permanent.test/"), New StubHandler(Function(request)
                                                                                                          calls += 1
                                                                                                          Return New HttpResponseMessage(HttpStatusCode.BadRequest)
                                                                                                      End Function), clock))
            Dim response = Await client.GetAsync("https://permanent.test/file")
            Assert.That(response.StatusCode, [Is].EqualTo(HttpStatusCode.BadRequest))
            Assert.That(calls, [Is].EqualTo(2))
        End Using
        Assert.That(clock.Delays, [Is].Empty)
    End Function

    <TestCase("PUT")>
    <TestCase("MKCOL")>
    <TestCase("COPY")>
    <TestCase("MOVE")>
    <TestCase("DELETE")>
    Public Async Function DoesNotReplayAmbiguousWebDavWrites(methodName As String) As Task
        Dim calls As Integer
        Using client As New HttpClient(New ServiceRequestPolicy(New Uri("https://write-methods.test/"), New StubHandler(Function(request)
                                                                                                             calls += 1
                                                                                                             Return New HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                                                                                                         End Function)))
            Using request As New HttpRequestMessage(New HttpMethod(methodName), "https://write-methods.test/target")
                Dim response = Await client.SendAsync(request)
                Assert.That(response.StatusCode, [Is].EqualTo(HttpStatusCode.ServiceUnavailable))
                Assert.That(calls, [Is].EqualTo(1))
            End Using
        End Using
    End Function

    <Test>
    Public Async Function StopsAtAttemptLimit() As Task
        Dim clock As New FakeClock
        Dim calls As Integer
        Using client As New HttpClient(New ServiceRequestPolicy(New Uri("https://exhaustion.test/"), New StubHandler(Function(request)
                                                                                                        calls += 1
                                                                                                        Return New HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                                                                                                    End Function), clock, maxAttempts:=3))
            Dim response = Await client.GetAsync("https://exhaustion.test/file")
            Assert.That(response.StatusCode, [Is].EqualTo(HttpStatusCode.ServiceUnavailable))
            Assert.That(calls, [Is].EqualTo(3))
            Assert.That(clock.Delays.Count, [Is].EqualTo(2))
        End Using
    End Function

    <Test>
    Public Async Function RetriesTransportFailureOnlyForRead() As Task
        Dim clock As New FakeClock
        Dim calls As Integer
        Using client As New HttpClient(New ServiceRequestPolicy(New Uri("https://transport.test/"), New StubHandler(Function(request)
                                                                                                         calls += 1
                                                                                                         If calls = 1 Then Throw New HttpRequestException("connection reset")
                                                                                                         Return New HttpResponseMessage(HttpStatusCode.OK)
                                                                                                     End Function), clock))
            Dim response = Await client.GetAsync("https://transport.test/file")
            Assert.That(response.StatusCode, [Is].EqualTo(HttpStatusCode.OK))
            Assert.That(calls, [Is].EqualTo(2))
            Assert.That(clock.Delays.Count, [Is].EqualTo(1))
        End Using
    End Function

    <Test>
    Public Async Function LimitsRequestRateSeparatelyFromConcurrency() As Task
        Dim clock As New FakeClock
        Dim calls As Integer
        Using client As New HttpClient(New ServiceRequestPolicy(New Uri("https://rate.test/"), New StubHandler(Function(request)
                                                                                                  calls += 1
                                                                                                  Return New HttpResponseMessage(HttpStatusCode.OK)
                                                                                              End Function), clock, maxConcurrent:=2, maxRequestsPerMinute:=1))
            Await client.GetAsync("https://rate.test/first")
            Await client.GetAsync("https://rate.test/second")
            Assert.That(calls, [Is].EqualTo(2))
            Assert.That(clock.Delays, [Is].EqualTo({TimeSpan.FromMinutes(1)}))
        End Using
    End Function

    <Test>
    Public Async Function StopsRetryingWhenElapsedBudgetIsInsufficient() As Task
        Dim clock As New FakeClock
        Dim calls As Integer
        Using client As New HttpClient(New ServiceRequestPolicy(New Uri("https://deadline.test/"), New StubHandler(Function(request)
                                                                                                      calls += 1
                                                                                                      Dim response As New HttpResponseMessage(CType(429, HttpStatusCode))
                                                                                                      response.Headers.RetryAfter = New Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(1))
                                                                                                      Return response
                                                                                                  End Function), clock, maxElapsed:=TimeSpan.FromSeconds(10)))
            Dim response = Await client.GetAsync("https://deadline.test/file")
            Assert.That(response.StatusCode, [Is].EqualTo(CType(429, HttpStatusCode)))
            Assert.That(calls, [Is].EqualTo(1))
            Assert.That(clock.Delays, [Is].Empty)
        End Using
    End Function

    <Test>
    Public Async Function CancelsWhileWaitingForRateAllowance() As Task
        Dim clock As New BlockingClock
        Dim calls As Integer
        Using client As New HttpClient(New ServiceRequestPolicy(New Uri("https://queued-rate.test/"), New StubHandler(Function(request)
                                                                                                         calls += 1
                                                                                                         Return New HttpResponseMessage(HttpStatusCode.OK)
                                                                                                     End Function), clock, maxRequestsPerMinute:=1))
            Await client.GetAsync("https://queued-rate.test/first")
            Using cancellation As New CancellationTokenSource
                Dim queued = client.GetAsync("https://queued-rate.test/second", cancellation.Token)
                Await clock.Waiting.Task
                cancellation.Cancel()
                Assert.ThrowsAsync(Of TaskCanceledException)(Async Function() Await queued)
            End Using
            Assert.That(calls, [Is].EqualTo(1))
        End Using
    End Function

    <Test>
    Public Async Function CancelsWhileWaitingForRetry() As Task
        Dim clock As New BlockingClock
        Dim calls As Integer
        Using client As New HttpClient(New ServiceRequestPolicy(New Uri("https://retry-cancel.test/"), New StubHandler(Function(request)
                                                                                                           calls += 1
                                                                                                           Dim response As New HttpResponseMessage(CType(429, HttpStatusCode))
                                                                                                           response.Headers.RetryAfter = New Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(7))
                                                                                                           Return response
                                                                                                       End Function), clock))
            Using cancellation As New CancellationTokenSource
                Dim request = client.GetAsync("https://retry-cancel.test/file", cancellation.Token)
                Await clock.Waiting.Task
                cancellation.Cancel()
                Assert.ThrowsAsync(Of TaskCanceledException)(Async Function() Await request)
            End Using
            Assert.That(calls, [Is].EqualTo(1))
        End Using
    End Function

    <Test>
    Public Async Function SharesConcurrencyAllowanceAndCancelsQueue() As Task
        Dim started As Integer
        Dim release As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Dim reached As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Dim handler As New StubHandler(Async Function(request)
                                           If Interlocked.Increment(started) = 2 Then reached.TrySetResult(True)
                                           Await release.Task
                                           Return New HttpResponseMessage(HttpStatusCode.OK)
                                       End Function)
        Dim service As New Uri("https://shared.test/")
        Using first As New HttpClient(New ServiceRequestPolicy(service, handler, maxConcurrent:=2)),
              second As New HttpClient(New ServiceRequestPolicy(service, handler, maxConcurrent:=2))
            Dim a = first.GetAsync("https://shared.test/a")
            Dim b = second.GetAsync("https://shared.test/b")
            Await reached.Task
            Using cancellation As New CancellationTokenSource
                Dim queued = first.GetAsync("https://shared.test/c", cancellation.Token)
                cancellation.Cancel()
                Assert.ThrowsAsync(Of TaskCanceledException)(Async Function() Await queued)
            End Using
            Assert.That(started, [Is].EqualTo(2))
            release.SetResult(True)
            Await Task.WhenAll(a, b)
        End Using
    End Function

    <Test>
    Public Async Function StreamingBodyKeepsConcurrencySlotUntilDisposed() As Task
        Dim calls As Integer
        Using client As New HttpClient(New ServiceRequestPolicy(New Uri("https://stream.test/"), New StubHandler(Function(request)
                                                                                                    calls += 1
                                                                                                    Return New HttpResponseMessage(HttpStatusCode.OK) With {
                                                                                                        .Content = New StreamContent(New IO.MemoryStream({1, 2, 3}))
                                                                                                    }
                                                                                                End Function), maxConcurrent:=1))
            Dim first = Await client.SendAsync(New HttpRequestMessage(HttpMethod.Get, "https://stream.test/first"), HttpCompletionOption.ResponseHeadersRead)
            Using cancellation As New CancellationTokenSource
                Dim queued = client.SendAsync(New HttpRequestMessage(HttpMethod.Get, "https://stream.test/second"), HttpCompletionOption.ResponseHeadersRead, cancellation.Token)
                cancellation.Cancel()
                Assert.ThrowsAsync(Of TaskCanceledException)(Async Function() Await queued)
            End Using
            Assert.That(calls, [Is].EqualTo(1))
            first.Dispose()
            Using later = Await client.SendAsync(New HttpRequestMessage(HttpMethod.Get, "https://stream.test/third"), HttpCompletionOption.ResponseHeadersRead)
                Assert.That(calls, [Is].EqualTo(2))
            End Using
        End Using
    End Function

    Private NotInheritable Class StubHandler
        Inherits HttpMessageHandler
        Private ReadOnly Reply As Func(Of HttpRequestMessage, Task(Of HttpResponseMessage))

        Friend Sub New(reply As Func(Of HttpRequestMessage, HttpResponseMessage))
            Me.Reply = Function(request) Task.FromResult(reply(request))
        End Sub

        Friend Sub New(reply As Func(Of HttpRequestMessage, Task(Of HttpResponseMessage)))
            Me.Reply = reply
        End Sub

        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Return Reply(request)
        End Function
    End Class

    Private NotInheritable Class FakeClock
        Implements IRequestClock
        Friend ReadOnly Delays As New List(Of TimeSpan)
        Private Current As DateTimeOffset = New DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)

        Public ReadOnly Property UtcNow As DateTimeOffset Implements IRequestClock.UtcNow
            Get
                Return Current
            End Get
        End Property

        Public Function DelayAsync(delay As TimeSpan, cancellationToken As CancellationToken) As Task Implements IRequestClock.DelayAsync
            cancellationToken.ThrowIfCancellationRequested()
            Delays.Add(delay)
            Current = Current.Add(delay)
            Return Task.CompletedTask
        End Function
    End Class

    Private NotInheritable Class BlockingClock
        Implements IRequestClock
        Friend ReadOnly Waiting As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)

        Public ReadOnly Property UtcNow As DateTimeOffset Implements IRequestClock.UtcNow
            Get
                Return New DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            End Get
        End Property

        Public Function DelayAsync(delay As TimeSpan, cancellationToken As CancellationToken) As Task Implements IRequestClock.DelayAsync
            Waiting.TrySetResult(True)
            Return Task.Delay(Timeout.Infinite, cancellationToken)
        End Function
    End Class
End Class
