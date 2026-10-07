Option Strict On

Imports System.Collections.Concurrent
Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Threading.Tasks

Namespace Providers

    'Applies to HTTP transports, so synchronous and asynchronous callers share the same allowance.
    Friend NotInheritable Class ServiceRequestPolicy
        Inherits DelegatingHandler

        Private Shared ReadOnly Gates As New ConcurrentDictionary(Of String, ServiceGate)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly Gate As ServiceGate
        Private ReadOnly Clock As IRequestClock
        Private ReadOnly MaxAttempts As Integer
        Private ReadOnly MaxElapsed As TimeSpan

        Friend Sub New(serviceUri As Uri, inner As HttpMessageHandler, Optional clock As IRequestClock = Nothing,
                       Optional maxConcurrent As Integer = 2, Optional maxRequestsPerMinute As Integer = 30,
                       Optional maxAttempts As Integer = 3, Optional maxElapsed As TimeSpan = Nothing)
            MyBase.New(inner)
            If serviceUri Is Nothing Then Throw New ArgumentNullException(NameOf(serviceUri))
            If maxConcurrent < 1 Then Throw New ArgumentOutOfRangeException(NameOf(maxConcurrent))
            If maxRequestsPerMinute < 1 Then Throw New ArgumentOutOfRangeException(NameOf(maxRequestsPerMinute))
            If maxAttempts < 1 Then Throw New ArgumentOutOfRangeException(NameOf(maxAttempts))
            Me.Clock = If(clock, New SystemRequestClock())
            Me.MaxAttempts = maxAttempts
            Me.MaxElapsed = If(maxElapsed = Nothing, TimeSpan.FromSeconds(30), maxElapsed)
            If Me.MaxElapsed <= TimeSpan.Zero Then Throw New ArgumentOutOfRangeException(NameOf(maxElapsed))
            Dim key As String = serviceUri.Scheme & "://" & serviceUri.IdnHost & ":" & serviceUri.Port.ToString(Globalization.CultureInfo.InvariantCulture) & ":" & maxConcurrent.ToString(Globalization.CultureInfo.InvariantCulture) & ":" & maxRequestsPerMinute.ToString(Globalization.CultureInfo.InvariantCulture)
            Me.Gate = Gates.GetOrAdd(key, Function(ignored) New ServiceGate(maxConcurrent, maxRequestsPerMinute))
        End Sub

        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Dim retryable As Boolean = request.Method = HttpMethod.Get OrElse request.Method = HttpMethod.Head OrElse request.Method.Method.Equals("PROPFIND", StringComparison.OrdinalIgnoreCase)
            Dim body As Byte() = Nothing
            If retryable AndAlso request.Content IsNot Nothing Then body = Await request.Content.ReadAsByteArrayAsync().ConfigureAwait(False)
            Dim started As DateTimeOffset = Clock.UtcNow
            Using deadline As CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                Dim activeToken As CancellationToken = deadline.Token
                For attempt As Integer = 1 To If(retryable, MaxAttempts, 1)
                    activeToken.ThrowIfCancellationRequested()
                    Await Gate.ReserveAsync(Clock, activeToken).ConfigureAwait(False)
                    Await Gate.Concurrent.WaitAsync(activeToken).ConfigureAwait(False)
                    Dim response As HttpResponseMessage = Nothing
                    Dim transportFailure As HttpRequestException = Nothing
                    Dim capacityTransferred As Boolean = False
                    Try
                        If attempt = 1 Then
                            response = Await MyBase.SendAsync(request, activeToken).ConfigureAwait(False)
                        Else
                            Using retryRequest As HttpRequestMessage = CloneReadRequest(request, body)
                                response = Await MyBase.SendAsync(retryRequest, activeToken).ConfigureAwait(False)
                            End Using
                        End If
                        If response IsNot Nothing Then
                            response.Content = New CapacityContent(response.Content, AddressOf Gate.Concurrent.Release)
                            capacityTransferred = True
                        End If
                    Catch ex As HttpRequestException When retryable AndAlso attempt < MaxAttempts
                        transportFailure = ex
                    Finally
                        If Not capacityTransferred Then Gate.Concurrent.Release()
                    End Try
                    If transportFailure IsNot Nothing Then
                        Dim failureDelay As TimeSpan = TimeSpan.FromMilliseconds(Math.Min(4000, 250 * Math.Pow(2, attempt - 1)) + ThreadLocalRandom.Next(0, 150))
                        Dim remainingAfterFailure As TimeSpan = MaxElapsed - (Clock.UtcNow - started)
                        If failureDelay >= remainingAfterFailure Then ExceptionDispatchInfo.Capture(transportFailure).Throw()
                        deadline.CancelAfter(remainingAfterFailure)
                        Await Clock.DelayAsync(failureDelay, activeToken).ConfigureAwait(False)
                        Continue For
                    End If
                    If Not retryable OrElse Not IsTransient(response.StatusCode) OrElse attempt = MaxAttempts Then Return response
                    Dim delay As TimeSpan = RetryDelay(response, attempt)
                    Dim remaining As TimeSpan = MaxElapsed - (Clock.UtcNow - started)
                    If delay >= remaining Then Return response
                    response.Dispose()
                    deadline.CancelAfter(remaining)
                    Await Clock.DelayAsync(delay, activeToken).ConfigureAwait(False)
                Next
            End Using
            Throw New InvalidOperationException(ProviderStrings.GetText("TheRetryLoopEndedUnexpectedly"))
        End Function

        Private Shared Function CloneReadRequest(original As HttpRequestMessage, body As Byte()) As HttpRequestMessage
            Dim copy As New HttpRequestMessage(original.Method, original.RequestUri) With {.Version = original.Version}
            For Each header In original.Headers
                copy.Headers.TryAddWithoutValidation(header.Key, header.Value)
            Next
            If body IsNot Nothing Then
                copy.Content = New ByteArrayContent(body)
                For Each header In original.Content.Headers
                    copy.Content.Headers.TryAddWithoutValidation(header.Key, header.Value)
                Next
            End If
            Return copy
        End Function

        Private Shared Function IsTransient(status As HttpStatusCode) As Boolean
            Select Case CInt(status)
                Case 408, 429, 500, 502, 503, 504
                    Return True
                Case Else
                    Return False
            End Select
        End Function

        Private Function RetryDelay(response As HttpResponseMessage, attempt As Integer) As TimeSpan
            If response.Headers.RetryAfter IsNot Nothing Then
                If response.Headers.RetryAfter.Delta.HasValue Then Return Max(TimeSpan.Zero, response.Headers.RetryAfter.Delta.Value)
                If response.Headers.RetryAfter.Date.HasValue Then Return Max(TimeSpan.Zero, response.Headers.RetryAfter.Date.Value - Clock.UtcNow)
            End If
            'A small random spread prevents providers sharing one gate from retrying in lockstep.
            Dim jitter As Integer = ThreadLocalRandom.Next(0, 150)
            Return TimeSpan.FromMilliseconds(Math.Min(4000, 250 * Math.Pow(2, attempt - 1)) + jitter)
        End Function

        Private Shared Function Max(first As TimeSpan, second As TimeSpan) As TimeSpan
            Return If(first > second, first, second)
        End Function

        Private NotInheritable Class ServiceGate
            Friend ReadOnly Concurrent As SemaphoreSlim
            Private ReadOnly MaxPerMinute As Integer
            Private ReadOnly Recent As New Queue(Of DateTimeOffset)

            Friend Sub New(maxConcurrent As Integer, maxPerMinute As Integer)
                Concurrent = New SemaphoreSlim(maxConcurrent, maxConcurrent)
                Me.MaxPerMinute = maxPerMinute
            End Sub

            Friend Async Function ReserveAsync(clock As IRequestClock, token As CancellationToken) As Task
                Do
                    token.ThrowIfCancellationRequested()
                    Dim wait As TimeSpan
                    SyncLock Recent
                        Dim now As DateTimeOffset = clock.UtcNow
                        While Recent.Count > 0 AndAlso now - Recent.Peek() >= TimeSpan.FromMinutes(1)
                            Recent.Dequeue()
                        End While
                        If Recent.Count < MaxPerMinute Then
                            Recent.Enqueue(now)
                            Return
                        End If
                        wait = Recent.Peek().AddMinutes(1) - now
                    End SyncLock
                    Await clock.DelayAsync(wait, token).ConfigureAwait(False)
                Loop
            End Function
        End Class

        Private NotInheritable Class ThreadLocalRandom
            Private Shared ReadOnly Local As New ThreadLocal(Of Random)(Function() New Random(Guid.NewGuid().GetHashCode()))
            Friend Shared Function [Next](min As Integer, max As Integer) As Integer
                Return Local.Value.Next(min, max)
            End Function
        End Class

        'The response body can still be active after SendAsync returns (notably GetRawFile).
        'Release the request slot only after buffering, stream completion, or disposal.
        Private NotInheritable Class CapacityContent
            Inherits HttpContent

            Private ReadOnly Inner As HttpContent
            Private ReadOnly ReleaseSlot As Action
            Private Released As Integer

            Friend Sub New(inner As HttpContent, releaseSlot As Action)
                Me.Inner = inner
                Me.ReleaseSlot = releaseSlot
                If inner IsNot Nothing Then
                    For Each header In inner.Headers
                        Me.Headers.TryAddWithoutValidation(header.Key, header.Value)
                    Next
                End If
            End Sub

            Protected Overrides Async Function SerializeToStreamAsync(stream As Stream, context As TransportContext) As Task
                Try
                    If Inner IsNot Nothing Then Await Inner.CopyToAsync(stream).ConfigureAwait(False)
                Finally
                    Release()
                End Try
            End Function

            Protected Overrides Async Function CreateContentReadStreamAsync() As Task(Of Stream)
                If Inner Is Nothing Then
                    Release()
                    Return New MemoryStream(Array.Empty(Of Byte)())
                End If
                Return New CapacityStream(Await Inner.ReadAsStreamAsync().ConfigureAwait(False), AddressOf Release)
            End Function

            Protected Overrides Function TryComputeLength(ByRef length As Long) As Boolean
                If Inner Is Nothing Then
                    length = 0
                    Return True
                End If
                If Inner.Headers.ContentLength.HasValue Then
                    length = Inner.Headers.ContentLength.Value
                    Return True
                End If
                length = 0
                Return False
            End Function

            Protected Overrides Sub Dispose(disposing As Boolean)
                If disposing Then
                    Inner?.Dispose()
                    Release()
                End If
                MyBase.Dispose(disposing)
            End Sub

            Private Sub Release()
                If Interlocked.Exchange(Released, 1) = 0 Then ReleaseSlot()
            End Sub
        End Class

        Private NotInheritable Class CapacityStream
            Inherits Stream

            Private ReadOnly Inner As Stream
            Private ReadOnly ReleaseSlot As Action
            Private Released As Integer

            Friend Sub New(inner As Stream, releaseSlot As Action)
                Me.Inner = inner
                Me.ReleaseSlot = releaseSlot
            End Sub

            Public Overrides ReadOnly Property CanRead As Boolean
                Get
                    Return Inner.CanRead
                End Get
            End Property
            Public Overrides ReadOnly Property CanSeek As Boolean
                Get
                    Return Inner.CanSeek
                End Get
            End Property
            Public Overrides ReadOnly Property CanWrite As Boolean
                Get
                    Return Inner.CanWrite
                End Get
            End Property
            Public Overrides ReadOnly Property Length As Long
                Get
                    Return Inner.Length
                End Get
            End Property
            Public Overrides Property Position As Long
                Get
                    Return Inner.Position
                End Get
                Set(value As Long)
                    Inner.Position = value
                End Set
            End Property
            Public Overrides Sub Flush()
                Inner.Flush()
            End Sub
            Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
                Return Inner.Seek(offset, origin)
            End Function
            Public Overrides Sub SetLength(value As Long)
                Inner.SetLength(value)
            End Sub
            Public Overrides Sub Write(buffer As Byte(), offset As Integer, count As Integer)
                Inner.Write(buffer, offset, count)
            End Sub
            Public Overrides Function Read(buffer As Byte(), offset As Integer, count As Integer) As Integer
                Dim amount As Integer = Inner.Read(buffer, offset, count)
                If amount = 0 Then Release()
                Return amount
            End Function
            Public Overrides Async Function ReadAsync(buffer As Byte(), offset As Integer, count As Integer, cancellationToken As CancellationToken) As Task(Of Integer)
                Dim amount As Integer = Await Inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(False)
                If amount = 0 Then Release()
                Return amount
            End Function
            Protected Overrides Sub Dispose(disposing As Boolean)
                If disposing Then
                    Inner.Dispose()
                    Release()
                End If
                MyBase.Dispose(disposing)
            End Sub
            Private Sub Release()
                If Interlocked.Exchange(Released, 1) = 0 Then ReleaseSlot()
            End Sub
        End Class
    End Class

    Friend Interface IRequestClock
        ReadOnly Property UtcNow As DateTimeOffset
        Function DelayAsync(delay As TimeSpan, cancellationToken As CancellationToken) As Task
    End Interface

    Friend NotInheritable Class SystemRequestClock
        Implements IRequestClock

        Public ReadOnly Property UtcNow As DateTimeOffset Implements IRequestClock.UtcNow
            Get
                Return DateTimeOffset.UtcNow
            End Get
        End Property

        Public Function DelayAsync(delay As TimeSpan, cancellationToken As CancellationToken) As Task Implements IRequestClock.DelayAsync
            Return Task.Delay(delay, cancellationToken)
        End Function
    End Class
End Namespace
