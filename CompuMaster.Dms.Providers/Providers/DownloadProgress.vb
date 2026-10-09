Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data

Namespace Providers
    Friend NotInheritable Class DownloadProgress
        Friend Shared Async Function CopyAsync(input As Stream, output As Stream, total As Long?, observer As IProgress(Of DmsTransferProgress), cancellationToken As CancellationToken) As Task(Of Long)
            Dim bytes As Long
            Dim reported As Long
            Dim timer = Diagnostics.Stopwatch.StartNew()
            Dim lastReport As Long
            Dim buffer(81919) As Byte
            observer?.Report(New DmsTransferProgress(0, total, DmsTransferPhase.Transferring))
            While True
                Dim count = Await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(False)
                If count = 0 Then Exit While
                Await output.WriteAsync(buffer, 0, count, cancellationToken).ConfigureAwait(False)
                bytes += count
                If bytes - reported >= 262144 OrElse timer.ElapsedMilliseconds - lastReport >= 100 Then
                    observer?.Report(New DmsTransferProgress(bytes, total, DmsTransferPhase.Transferring))
                    reported = bytes
                    lastReport = timer.ElapsedMilliseconds
                End If
            End While
            Await output.FlushAsync(cancellationToken).ConfigureAwait(False)
            observer?.Report(New DmsTransferProgress(bytes, total, DmsTransferPhase.Finalizing))
            Return bytes
        End Function
    End Class
End Namespace
