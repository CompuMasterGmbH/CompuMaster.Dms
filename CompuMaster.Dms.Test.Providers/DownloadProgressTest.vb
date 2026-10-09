Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class DownloadProgressTest
    <TestCase(True), TestCase(False)>
    Public Async Function CopyReportsActualBytesAndWaitsForDestinationFinalization(knownTotal As Boolean) As Task
        Dim data(1048575) As Byte
        Dim random As New Random(1)
        random.NextBytes(data)
        Dim progress As New Collector()
        Using input As New MemoryStream(data), output As New MemoryStream()
            Dim bytes = Await DownloadProgress.CopyAsync(input, output, If(knownTotal, CType(data.Length, Long?), Nothing), progress, CancellationToken.None)
            Assert.That(bytes, [Is].EqualTo(data.Length))
            Assert.That(output.ToArray(), [Is].EqualTo(data))
            Assert.That(progress.Values.Last().BytesTransferred, [Is].EqualTo(data.Length))
            Assert.That(progress.Values.Last().TotalBytes, [Is].EqualTo(If(knownTotal, CType(data.Length, Long?), Nothing)))
            Assert.That(progress.Values.Last().Phase, [Is].EqualTo(DmsTransferPhase.Finalizing), "The owning provider confirms success after replacement and timestamp work.")
            Assert.That(progress.Values.Any(Function(value) value.Phase = DmsTransferPhase.Completed), [Is].False)
            Assert.That(progress.Values.Count, [Is].GreaterThan(2))
        End Using
    End Function

    <Test>
    Public Sub PreCancellationNeverReportsCompletion()
        Using input As New MemoryStream(New Byte(9) {}), output As New MemoryStream(), cancellation As New CancellationTokenSource()
            cancellation.Cancel()
            Dim progress As New Collector()
            Assert.CatchAsync(Of OperationCanceledException)(Async Function() As Task
                                                                Await DownloadProgress.CopyAsync(input, output, 10, progress, cancellation.Token)
                                                            End Function)
            Assert.That(output.Length, [Is].Zero)
            Assert.That(progress.Values.Any(Function(value) value.Phase = DmsTransferPhase.Completed), [Is].False)
        End Using
    End Sub

    <Test>
    Public Async Function DefaultProgressFallbackPreservesSelectedResourceAndUnknownCounters() As Task
        Dim provider As New FixtureProvider()
        Dim selected As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .ExtendedInfosFileID = "selected"}
        Dim progress As New Collector()
        Await provider.DownloadFileWithProgressAsync(selected, "unused", progress)
        Assert.That(provider.Selected, [Is].SameAs(selected))
        Assert.That(progress.Values.Last().BytesTransferred, [Is].Null)
        Assert.That(progress.Values.Last().TotalBytes, [Is].Null)
        Assert.That(progress.Values.Last().Phase, [Is].EqualTo(DmsTransferPhase.Completed))
    End Function

    Private Class FixtureProvider
        Inherits NoDmsProvider
        Friend Selected As DmsResourceItem
        Public Overrides Function DownloadFileAsync(remoteFile As DmsResourceItem, local As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Selected = remoteFile
            Return Task.CompletedTask
        End Function
    End Class
    Private Class Collector
        Implements IProgress(Of DmsTransferProgress)
        Friend ReadOnly Values As New List(Of DmsTransferProgress)
        Public Sub Report(value As DmsTransferProgress) Implements IProgress(Of DmsTransferProgress).Report
            Values.Add(value)
        End Sub
    End Class
End Class
