Option Strict On

Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class ServiceRequestPolicyTest

    <TestCase(429, False)>
    <TestCase(503, True)>
    Public Async Function WriteCooldownIsSharedAcrossClientsAndPaths(status As Integer, useDate As Boolean) As Task
        Dim clock As New FakeClock
        Dim origin As New Uri("https://" & Guid.NewGuid().ToString("N") & ".test/")
        Dim calls As Integer
        Dim handler As New StubHandler(Function(request)
                                           calls += 1
                                           Dim response As New HttpResponseMessage(CType(status, HttpStatusCode))
                                           response.Headers.RetryAfter = If(useDate, New Headers.RetryConditionHeaderValue(clock.UtcNow.AddSeconds(7)), New Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(7)))
                                           Return response
                                       End Function)
        Using first As New HttpClient(New ServiceRequestPolicy(origin, handler, clock)), second As New HttpClient(New ServiceRequestPolicy(New Uri(origin, "other"), New StubHandler(Function(request) New HttpResponseMessage(HttpStatusCode.OK)), clock))
            Using response = Await first.PostAsync(New Uri(origin, "write"), New StringContent("data"))
            End Using
            Using response = Await second.GetAsync(New Uri(origin, "read"))
            End Using
        End Using
        Assert.That(calls, [Is].EqualTo(1), "The mutation must not be replayed.")
        Assert.That(clock.Delays, [Is].EqualTo({TimeSpan.FromSeconds(7)}))
    End Function

    <TestCase(429)>
    <TestCase(503)>
    Public Async Function OverloadWithoutRetryAfterDefersOtherRequests(status As Integer) As Task
        Dim clock As New FakeClock
        Dim origin As New Uri("https://" & Guid.NewGuid().ToString("N") & ".test/")
        Dim calls As Integer
        Using client As New HttpClient(New ServiceRequestPolicy(origin, New StubHandler(Function(request)
                                                                                          calls += 1
                                                                                          Return New HttpResponseMessage(CType(status, HttpStatusCode))
                                                                                      End Function), clock))
            Using response = Await client.PostAsync(origin, New StringContent("data"))
            End Using
            Using response = Await client.PostAsync(origin, New StringContent("data"))
            End Using
        End Using
        Assert.That(calls, [Is].EqualTo(2))
        Assert.That(clock.Delays.Count, [Is].EqualTo(1))
        Assert.That(clock.Delays(0), [Is].GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(250)))
    End Function

    <Test>
    Public Async Function QueuedRequestRechecksCooldownAndCanCancel() As Task
        Dim clock As New BlockingClock
        Dim origin As New Uri("https://" & Guid.NewGuid().ToString("N") & ".test/")
        Dim calls As Integer
        Using client As New HttpClient(New ServiceRequestPolicy(origin, New StubHandler(Function(request)
                                                                                          calls += 1
                                                                                          Dim response As New HttpResponseMessage(CType(429, HttpStatusCode)) With {.Content = New StringContent("diagnostic")}
                                                                                          response.Headers.RetryAfter = New Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(7))
                                                                                          Return response
                                                                                      End Function), clock, maxConcurrent:=1))
            Using write As New HttpRequestMessage(HttpMethod.Post, origin) With {.Content = New StringContent("data")}, response = Await client.SendAsync(write, HttpCompletionOption.ResponseHeadersRead), cancellation As New CancellationTokenSource
                Dim queued = client.GetAsync(origin, cancellation.Token)
                Assert.That(queued.IsCompleted, [Is].False)
                response.Dispose()
                Await clock.Waiting.Task
                Assert.That(calls, [Is].EqualTo(1))
                cancellation.Cancel()
                Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function() Await queued, Func(Of Task)))
            End Using
        End Using
    End Function

    <Test>
    Public Async Function ExplicitlyDisabledRateLimitDoesNotWait() As Task
        Dim clock As New FakeClock
        Dim origin As New Uri("https://" & Guid.NewGuid().ToString("N") & ".test/")
        WebDavDmsProvider.ConfigureRequestRateLimit(origin, 1)
        WebDavDmsProvider.ConfigureRequestRateLimit(origin, Nothing)
        Using client As New HttpClient(New ServiceRequestPolicy(origin, New StubHandler(Function(request) New HttpResponseMessage(HttpStatusCode.OK)), clock))
            For index As Integer = 1 To 65
                Using response = Await client.GetAsync(origin)
                End Using
            Next
        End Using
        Assert.That(clock.Delays, [Is].Empty)
    End Function

    <Test>
    Public Async Function PublicRateConfigurationIsSharedAndCannotChangeAfterRequestsStart() As Task
        Dim origin As New Uri("https://" & Guid.NewGuid().ToString("N") & ".test/")
        WebDavDmsProvider.ConfigureRequestRateLimit(New Uri("https://fixture-user@" & origin.Host.ToUpperInvariant() & ":443/configured-path"), 1)
        Dim clock As New FakeClock
        Using first As New HttpClient(New ServiceRequestPolicy(origin, New StubHandler(Function(request) New HttpResponseMessage(HttpStatusCode.OK)), clock)), second As New HttpClient(New ServiceRequestPolicy(New Uri(origin, "other"), New StubHandler(Function(request) New HttpResponseMessage(HttpStatusCode.OK)), clock))
            Using response = Await first.GetAsync(origin)
            End Using
            Assert.Throws(Of InvalidOperationException)(Sub() WebDavDmsProvider.ConfigureRequestRateLimit(origin, Nothing))
            Using response = Await second.GetAsync(origin)
            End Using
        End Using
        Assert.That(clock.Delays, [Is].EqualTo({TimeSpan.FromMinutes(1)}))
    End Function

    <Test>
    Public Sub RejectsInvalidPublicRateConfiguration()
        Assert.Throws(Of ArgumentNullException)(Sub() WebDavDmsProvider.ConfigureRequestRateLimit(Nothing, 1))
        Assert.Throws(Of ArgumentException)(Sub() WebDavDmsProvider.ConfigureRequestRateLimit(New Uri("relative", UriKind.Relative), 1))
        Assert.Throws(Of ArgumentException)(Sub() WebDavDmsProvider.ConfigureRequestRateLimit(New Uri("ftp://invalid.test"), 1))
        Assert.Throws(Of ArgumentOutOfRangeException)(Sub() WebDavDmsProvider.ConfigureRequestRateLimit(New Uri("https://invalid.test"), 0))
        Assert.Throws(Of ArgumentOutOfRangeException)(Sub() WebDavDmsProvider.ConfigureRequestRateLimit(New Uri("https://invalid.test"), -1))
    End Sub

    <Test>
    Public Async Function DefaultAdmissionDoesNotImposeAMinuteQuota() As Task
        Dim clock As New FakeClock
        Using client As New HttpClient(New ServiceRequestPolicy(New Uri("https://unlimited-default.test/"), New StubHandler(Function(request) New HttpResponseMessage(HttpStatusCode.OK)), clock))
            For index As Integer = 1 To 65
                Using response = Await client.GetAsync("https://unlimited-default.test/file")
                End Using
            Next
        End Using
        Assert.That(clock.Delays, [Is].Empty)
    End Function

    <Test>
    Public Sub DefaultWebDavClientDoesNotApplyTheShortMetadataTimeoutToTransfers()
        Using client = WebDavDmsProvider.CreateConfiguredHttpClient(New HttpClientHandler(), New WebDav.WebDavClientParams With {.BaseAddress = New Uri("https://timeout-config.test/")})
            Assert.That(client.Timeout, [Is].EqualTo(Timeout.InfiniteTimeSpan))
        End Using
        Using client = WebDavDmsProvider.CreateConfiguredHttpClient(New HttpClientHandler(), New WebDav.WebDavClientParams With {.BaseAddress = New Uri("https://timeout-explicit.test/"), .Timeout = TimeSpan.FromMinutes(8)})
            Assert.That(client.Timeout, [Is].EqualTo(TimeSpan.FromMinutes(8)), "Explicit caller configuration remains respected.")
        End Using
    End Sub

    <Test>
    Public Async Function SlowPutOutlivesMetadataDeadlineButStillHonorsCallerCancellation() As Task
        Dim calls As Integer
        Using client As New HttpClient(New ServiceRequestPolicy(New Uri("https://slow-put.test/"), New DelayedHandler(TimeSpan.FromMilliseconds(200), Sub() calls += 1), requestTimeout:=TimeSpan.FromMilliseconds(30))) With {.Timeout = Timeout.InfiniteTimeSpan}
            Using response = Await client.PutAsync("https://slow-put.test/file", New StringContent("payload"))
                Assert.That(response.StatusCode, [Is].EqualTo(HttpStatusCode.OK))
            End Using
            Using cancellation As New CancellationTokenSource(TimeSpan.FromMilliseconds(30))
                Assert.CatchAsync(Of OperationCanceledException)(Async Function() Await client.PutAsync("https://slow-put.test/file", New StringContent("payload"), cancellation.Token))
            End Using
            Assert.That(calls, [Is].EqualTo(2), "An interrupted PUT must not be replayed.")
        End Using
    End Function

    <Test>
    Public Sub SlowMetadataRequestsRetainABoundedDeadline()
        Using client As New HttpClient(New ServiceRequestPolicy(New Uri("https://slow-metadata.test/"), New DelayedHandler(TimeSpan.FromSeconds(5), Sub()
                                                                                                                                                 End Sub), requestTimeout:=TimeSpan.FromMilliseconds(30))) With {.Timeout = Timeout.InfiniteTimeSpan}
            Assert.CatchAsync(Of OperationCanceledException)(Async Function() Await client.GetAsync("https://slow-metadata.test/metadata"))
        End Using
    End Sub

    Private Class DelayedHandler
        Inherits HttpMessageHandler
        Private ReadOnly Delay As TimeSpan
        Private ReadOnly OnRequest As Action
        Friend Sub New(delay As TimeSpan, onRequest As Action)
            Me.Delay = delay
            Me.OnRequest = onRequest
        End Sub
        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            OnRequest()
            Await Task.Delay(Delay, cancellationToken)
            Return New HttpResponseMessage(HttpStatusCode.OK)
        End Function
    End Class

    <Test>
    Public Sub MetadataBodyRemainsBoundedAfterResponseHeadersArrive()
        Using client As New HttpClient(New ServiceRequestPolicy(New Uri("https://metadata-body.test/"), New StubHandler(Function(request) New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New DelayedContent()}), requestTimeout:=TimeSpan.FromMilliseconds(30))) With {.Timeout = Timeout.InfiniteTimeSpan}
            Assert.CatchAsync(Of OperationCanceledException)(Async Function() Await client.SendAsync(New HttpRequestMessage(New HttpMethod("PROPFIND"), "https://metadata-body.test/")))
        End Using
    End Sub

    <Test>
    Public Async Function DownloadBodyOutlivesTheResponseHeaderDeadline() As Task
        Using client As New HttpClient(New ServiceRequestPolicy(New Uri("https://download-body.test/"), New StubHandler(Function(request) New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New DelayedContent()}), requestTimeout:=TimeSpan.FromMilliseconds(30))) With {.Timeout = Timeout.InfiniteTimeSpan}
            Using response = Await client.GetAsync("https://download-body.test/file")
                Assert.That(Await response.Content.ReadAsStringAsync(), [Is].EqualTo("payload"))
            End Using
        End Using
    End Function

    Private Class DelayedContent
        Inherits HttpContent
        Protected Overrides Async Function SerializeToStreamAsync(stream As IO.Stream, context As TransportContext) As Task
            Await Task.Delay(200)
            Dim bytes = Text.Encoding.UTF8.GetBytes("payload")
            Await stream.WriteAsync(bytes, 0, bytes.Length)
        End Function
        Protected Overrides Function TryComputeLength(ByRef length As Long) As Boolean
            length = 7
            Return True
        End Function
    End Class

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
