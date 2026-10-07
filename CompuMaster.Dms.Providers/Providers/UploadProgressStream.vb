Imports System.IO
Imports System.Threading

Namespace Providers
    'Owns the input stream just as the existing stream-factory upload contract does.
    Friend NotInheritable Class UploadProgressStream
        Inherits Stream
        Private ReadOnly Input As Stream
        Private ReadOnly Observer As IProgress(Of Data.DmsTransferProgress)
        Private ReadOnly Total As Long?
        Private ReadOnly StartPosition As Long
        Private Consumed As Long
        Private Reported As Long
        Private LastReport As Long
        Private FinalizingReported As Boolean

        Friend Sub New(input As Stream, observer As IProgress(Of Data.DmsTransferProgress))
            If input Is Nothing Then Throw New ArgumentNullException(NameOf(input))
            Me.Input = input
            Me.Observer = observer
            If input.CanSeek Then
                Me.StartPosition = input.Position
                Me.Total = Math.Max(0, input.Length - Me.StartPosition)
            End If
            Report(Data.DmsTransferPhase.Transferring)
        End Sub

        Private Sub ReadCompleted(count As Integer, requested As Integer)
            If Input.CanSeek Then
                Consumed = Math.Max(Consumed, Math.Max(0, Input.Position - StartPosition))
            Else
                Consumed += count
            End If
            Dim finalizing = (count = 0 AndAlso requested > 0) OrElse (Total.HasValue AndAlso Consumed >= Total.Value)
            If finalizing AndAlso Not FinalizingReported Then
                FinalizingReported = True
                Report(Data.DmsTransferPhase.Finalizing)
            ElseIf Not finalizing AndAlso (Consumed - Reported >= 262144 OrElse Diagnostics.Stopwatch.GetTimestamp() - LastReport >= Diagnostics.Stopwatch.Frequency \ 10) Then
                Report(Data.DmsTransferPhase.Transferring)
            End If
        End Sub

        Private Sub Report(phase As Data.DmsTransferPhase)
            Reported = Consumed
            LastReport = Diagnostics.Stopwatch.GetTimestamp()
            Observer?.Report(Snapshot(phase))
        End Sub

        Friend Function Snapshot(phase As Data.DmsTransferPhase) As Data.DmsTransferProgress
            Return New Data.DmsTransferProgress(If(Total.HasValue, Math.Min(Consumed, Total.Value), Consumed), Total, phase)
        End Function

        Public Overrides Function Read(buffer As Byte(), offset As Integer, count As Integer) As Integer
            Dim readCount = Input.Read(buffer, offset, count)
            ReadCompleted(readCount, count)
            Return readCount
        End Function

        Public Overrides Async Function ReadAsync(buffer As Byte(), offset As Integer, count As Integer, cancellationToken As CancellationToken) As Task(Of Integer)
            Dim readCount = Await Input.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(False)
            ReadCompleted(readCount, count)
            Return readCount
        End Function

        Public Overrides ReadOnly Property CanRead As Boolean
            Get
                Return Input.CanRead
            End Get
        End Property
        Public Overrides ReadOnly Property CanSeek As Boolean
            Get
                Return Input.CanSeek
            End Get
        End Property
        Public Overrides ReadOnly Property CanWrite As Boolean = False
        Public Overrides ReadOnly Property Length As Long
            Get
                Return Input.Length
            End Get
        End Property
        Public Overrides Property Position As Long
            Get
                Return Input.Position
            End Get
            Set(value As Long)
                Input.Position = value
            End Set
        End Property
        Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
            Return Input.Seek(offset, origin)
        End Function
        Public Overrides Sub Flush()
            Input.Flush()
        End Sub
        Public Overrides Sub SetLength(value As Long)
            Throw New NotSupportedException()
        End Sub
        Public Overrides Sub Write(buffer As Byte(), offset As Integer, count As Integer)
            Throw New NotSupportedException()
        End Sub
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then Input.Dispose()
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
