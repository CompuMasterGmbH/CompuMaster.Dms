Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class UploadProgressTest
    <TestCase(False, 0), TestCase(False, 17), TestCase(True, 17)>
    Public Async Function SourceCountersDoNotAnnounceSuccessBeforeProviderConfirmation(nonSeekable As Boolean, size As Integer) As Task
        Dim provider As New ReadingProvider()
        Dim observer As New Collector()
        Dim source As New MemoryStream(New Byte(Math.Max(0, size) - 1) {})
        Dim stream As Stream = If(nonSeekable, CType(New NonSeekableStream(source), Stream), source)
        Dim upload = provider.UploadFileWithProgressAsync("destination", Function() stream, observer)
        Await provider.ReadFinished.Task
        Assert.That(upload.IsCompleted, [Is].False)
        Assert.That(observer.Values.Last().Phase, [Is].EqualTo(DmsTransferPhase.Finalizing))
        Assert.That(observer.Values.Last().BytesTransferred, [Is].EqualTo(CLng(size)))
        Assert.That(observer.Values.Last().TotalBytes, [Is].EqualTo(If(nonSeekable, CType(Nothing, Long?), CLng(size))))
        Assert.That(observer.Values.Any(Function(value) value.Phase = DmsTransferPhase.Completed), [Is].False)
        provider.Confirmation.SetResult(True)
        Await upload
        Assert.That(observer.Values.Last().Phase, [Is].EqualTo(DmsTransferPhase.Completed))
        Assert.That(source.CanRead, [Is].False, "The provider owns and disposes its source stream.")
    End Function

    <TestCase(False), TestCase(True)>
    Public Async Function FailureAndCancellationDoNotReportCompletion(cancel As Boolean) As Task
        Dim provider As New ReadingProvider()
        Dim observer As New Collector()
        Dim upload = provider.UploadFileWithProgressAsync("destination", Function() New MemoryStream(New Byte(15) {}), observer)
        Await provider.ReadFinished.Task
        If cancel Then
            provider.Confirmation.SetCanceled()
            Assert.ThrowsAsync(Of TaskCanceledException)(Function() upload)
        Else
            provider.Confirmation.SetException(New IOException("fixture failure"))
            Assert.ThrowsAsync(Of IOException)(Function() upload)
        End If
        Assert.That(observer.Values.Any(Function(value) value.Phase = DmsTransferPhase.Completed), [Is].False)
    End Function

    <Test>
    Public Sub RewindDoesNotDoubleCountAndZeroLengthReadDoesNotFinalize()
        Dim observer As New Collector()
        Using stream As New UploadProgressStream(New MemoryStream(New Byte(299999) {}), observer)
            Dim buffer(299999) As Byte
            stream.Read(buffer, 0, 0)
            Assert.That(observer.Values.Last().Phase, [Is].EqualTo(DmsTransferPhase.Transferring))
            stream.Read(buffer, 0, 270000)
            stream.Position = 0
            stream.Read(buffer, 0, 270000)
            Assert.That(stream.Snapshot(DmsTransferPhase.Transferring).BytesTransferred, [Is].EqualTo(270000L))
            stream.Read(buffer, 0, 30000)
            Assert.That(observer.Values.Last().BytesTransferred, [Is].EqualTo(300000L))
            Assert.That(observer.Values.Count, [Is].LessThanOrEqualTo(4), "Chunk telemetry is throttled.")
        End Using
    End Sub

    <Test>
    Public Sub EmptyReadsAndSeekingCannotFabricateConsumedBytes()
        Dim observer As New Collector()
        Using empty As New UploadProgressStream(New MemoryStream(), observer)
            empty.Read(New Byte(0) {}, 0, 0)
            Assert.That(observer.Values.Last().Phase, [Is].EqualTo(DmsTransferPhase.Transferring))
        End Using
        Using stream As New UploadProgressStream(New MemoryStream(New Byte(99) {}), observer)
            stream.Seek(0, SeekOrigin.End)
            stream.Read(New Byte(0) {}, 0, 1)
            Assert.That(observer.Values.Last().BytesTransferred, [Is].EqualTo(0L), "Seeking to EOF does not consume source bytes.")
        End Using
        Using stream As New UploadProgressStream(New MemoryStream(New Byte(99) {}), observer)
            stream.Position = 50
            stream.Read(New Byte(0) {}, 0, 1)
            Assert.That(stream.Snapshot(DmsTransferPhase.Transferring).BytesTransferred, [Is].Null, "A skipped source range makes consumed-byte telemetry unreliable.")
        End Using
    End Sub

    <Test>
    Public Async Function DefaultLocalFileProgressPreservesExistingProviderOverride() As Task
        Dim provider As New FileOverrideProvider()
        Dim observer As New Collector()
        Await provider.UploadFileWithProgressAsync("destination", "source", observer)
        Assert.That(provider.Calls, [Is].EqualTo(1))
        Assert.That(observer.Values.Last().BytesTransferred, [Is].Null)
        Assert.That(observer.Values.Last().TotalBytes, [Is].Null)
        Assert.That(observer.Values.Last().Phase, [Is].EqualTo(DmsTransferPhase.Completed))
    End Function

    <Test>
    Public Sub ProgressUses64BitCountersAndRejectsInvalidValues()
        Dim value As New DmsTransferProgress(Long.MaxValue - 1, Long.MaxValue, DmsTransferPhase.Transferring)
        Assert.That(value.BytesTransferred, [Is].EqualTo(Long.MaxValue - 1))
        Assert.Throws(Of ArgumentOutOfRangeException)(Sub()
                                                         Dim invalid = New DmsTransferProgress(-1, Nothing, DmsTransferPhase.Transferring)
                                                     End Sub)
        Assert.Throws(Of ArgumentOutOfRangeException)(Sub()
                                                         Dim invalid = New DmsTransferProgress(Nothing, -1, DmsTransferPhase.Transferring)
                                                     End Sub)
        Assert.Throws(Of ArgumentOutOfRangeException)(Sub()
                                                         Dim invalid = New DmsTransferProgress(Nothing, Nothing, CType(99, DmsTransferPhase))
                                                     End Sub)
    End Sub

    Private Class Collector
        Implements IProgress(Of DmsTransferProgress)
        Friend ReadOnly Values As New List(Of DmsTransferProgress)
        Public Sub Report(value As DmsTransferProgress) Implements IProgress(Of DmsTransferProgress).Report
            Values.Add(value)
        End Sub
    End Class
    Private Class ReadingProvider
        Inherits NoDmsProvider
        Friend ReadOnly ReadFinished As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Friend ReadOnly Confirmation As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Public Overrides Async Function UploadFileAsync(path As String, factory As Func(Of Stream), Optional cancellationToken As CancellationToken = Nothing) As Task
            Using source = factory()
                Await source.CopyToAsync(Stream.Null, 81920, cancellationToken)
            End Using
            ReadFinished.SetResult(True)
            Await Confirmation.Task
        End Function
    End Class
    Private Class FileOverrideProvider
        Inherits NoDmsProvider
        Friend Calls As Integer
        Public Overrides Function UploadFileAsync(path As String, source As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Calls += 1
            Return Task.CompletedTask
        End Function
    End Class
    Private Class NonSeekableStream
        Inherits Stream
        Private ReadOnly Source As Stream
        Friend Sub New(source As Stream)
            Me.Source = source
        End Sub
        Public Overrides ReadOnly Property CanRead As Boolean = True
        Public Overrides ReadOnly Property CanWrite As Boolean = False
        Public Overrides ReadOnly Property CanSeek As Boolean = False
        Public Overrides ReadOnly Property Length As Long
            Get
                Throw New NotSupportedException()
            End Get
        End Property
        Public Overrides Property Position As Long
            Get
                Throw New NotSupportedException()
            End Get
            Set(value As Long)
                Throw New NotSupportedException()
            End Set
        End Property
        Public Overrides Function Read(buffer As Byte(), offset As Integer, count As Integer) As Integer
            Return Source.Read(buffer, offset, count)
        End Function
        Public Overrides Sub Flush()
        End Sub
        Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
            Throw New NotSupportedException()
        End Function
        Public Overrides Sub SetLength(value As Long)
            Throw New NotSupportedException()
        End Sub
        Public Overrides Sub Write(buffer As Byte(), offset As Integer, count As Integer)
            Throw New NotSupportedException()
        End Sub
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then Source.Dispose()
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Class
