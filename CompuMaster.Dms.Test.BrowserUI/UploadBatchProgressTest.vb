Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports CompuMaster.Dms.BrowserUI
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA)>
Public Class UploadBatchProgressTest
    <TestCase(0), TestCase(1), TestCase(2)>
    Public Sub BatchWaitsForConfirmationAndPreservesTerminalStates(outcome As Integer)
        Using dispatcher As New UiTestDispatcher(), cancellation As New CancellationTokenSource(), browser As New DmsBrowser(New NoDmsProvider())
            Dim provider As New ControlledProvider()
            Dim observer As New Collector()
            Dim transfer = browser.RunTransferAsync(Function() DmsBrowser.UploadBatchWithProgressAsync(provider, "folder", {"first", "second", "third"}, observer, cancellation.Token))
            Assert.That(provider.Calls, [Is].EqualTo(1))
            Assert.That(browser.Enabled, [Is].False)
            provider.Observer.Report(New DmsTransferProgress(4000000000L, 4000000000L, DmsTransferPhase.Finalizing))
            dispatcher.PumpUntil(Function() observer.Latest.States(0) = UploadFileState.Finalizing)
            Assert.That(transfer.IsCompleted, [Is].False)
            Assert.That(observer.Latest.OverallPercent, [Is].Null, "Unstarted files have unknown totals.")
            provider.Completion.SetResult(True)
            dispatcher.PumpUntil(Function() provider.Calls = 2)
            Assert.That(observer.Latest.States(0), [Is].EqualTo(UploadFileState.Completed))
            Dim secondObserver = provider.Observer
            Select Case outcome
                Case 0
                    provider.Observer.Report(New DmsTransferProgress(Nothing, Nothing, DmsTransferPhase.Transferring))
                    provider.Completion.SetResult(True)
                    dispatcher.PumpUntil(Function() provider.Calls = 3)
                    provider.Completion.SetResult(True)
                    dispatcher.Finish(transfer)
                Case 1
                    provider.Completion.SetException(New InvalidOperationException("fixture failure"))
                    dispatcher.PumpUntil(Function() transfer.IsCompleted)
                    Assert.Throws(Of InvalidOperationException)(Sub() transfer.GetAwaiter().GetResult())
                Case 2
                    cancellation.Cancel()
                    provider.Completion.SetCanceled()
                    dispatcher.PumpUntil(Function() transfer.IsCompleted)
                    Assert.Throws(Of TaskCanceledException)(Sub() transfer.GetAwaiter().GetResult())
            End Select
            Dim finalSnapshot = observer.Latest
            secondObserver.Report(New DmsTransferProgress(1, 1, DmsTransferPhase.Completed))
            Dim drain As New TaskCompletionSource(Of Boolean)()
            SynchronizationContext.Current.Post(Sub(state) drain.SetResult(True), Nothing)
            dispatcher.Finish(drain.Task)
            Assert.That(observer.Latest, [Is].SameAs(finalSnapshot), "Stale callbacks cannot replace terminal status.")
            Assert.That(browser.Enabled, [Is].True)
            Assert.That(browser.UseWaitCursor, [Is].False)
            Assert.That(finalSnapshot.States(1), [Is].EqualTo(If(outcome = 0, UploadFileState.Completed, If(outcome = 1, UploadFileState.Failed, UploadFileState.Cancelled))))
            Assert.That(finalSnapshot.States(2), [Is].EqualTo(If(outcome = 0, UploadFileState.Completed, UploadFileState.Waiting)))
        End Using
    End Sub

    <Test>
    Public Sub OverallProgressHandlesLargeAndZeroTotalsWithoutInventingUnknownValues()
        Dim files = {"first", "second"}
        Dim states = {UploadFileState.Completed, UploadFileState.Finalizing}
        Dim counters = {New DmsTransferProgress(Long.MaxValue, Long.MaxValue, DmsTransferPhase.Completed), New DmsTransferProgress(Long.MaxValue \ 2, Long.MaxValue, DmsTransferPhase.Transferring)}
        Assert.That(New UploadBatchSnapshot(files, states, counters, 1).OverallPercent, [Is].EqualTo(74))
        counters = {New DmsTransferProgress(0, 0, DmsTransferPhase.Completed), New DmsTransferProgress(0, 0, DmsTransferPhase.Finalizing)}
        Assert.That(New UploadBatchSnapshot(files, states, counters, 1).OverallPercent, [Is].EqualTo(0))
        states(1) = UploadFileState.Completed
        Assert.That(New UploadBatchSnapshot(files, states, counters, 1).OverallPercent, [Is].EqualTo(100))
        counters(1) = New DmsTransferProgress(10, Nothing, DmsTransferPhase.Transferring)
        Assert.That(New UploadBatchSnapshot(files, states, counters, 1).OverallPercent, [Is].Null)
    End Sub

    <Test>
    Public Sub ProgressDialogClosesOnlyAfterTransferCompletionAndIgnoresDisposedCallbacks()
        Using dialog As New UploadProgressDialog({"file"})
            dialog.Show()
            dialog.Close()
            Assert.That(dialog.IsDisposed, [Is].False)
            Assert.That(dialog.CancellationToken.IsCancellationRequested, [Is].True)
            dialog.Finish()
            dialog.Close()
            Assert.That(dialog.IsDisposed, [Is].True)
            Assert.DoesNotThrow(Sub() dialog.Report(New UploadBatchSnapshot({"file"}, {UploadFileState.Completed}, {New DmsTransferProgress(1, 1, DmsTransferPhase.Completed)}, 0)))
        End Using
    End Sub

    Private Class Collector
        Implements IProgress(Of UploadBatchSnapshot)
        Friend Latest As UploadBatchSnapshot
        Public Sub Report(value As UploadBatchSnapshot) Implements IProgress(Of UploadBatchSnapshot).Report
            Latest = value
        End Sub
    End Class
    Private Class ControlledProvider
        Inherits NoDmsProvider
        Friend Calls As Integer
        Friend Completion As TaskCompletionSource(Of Boolean)
        Friend Observer As IProgress(Of DmsTransferProgress)
        Public Overrides Function UploadFileWithProgressAsync(path As String, file As String, progress As IProgress(Of DmsTransferProgress), Optional cancellationToken As CancellationToken = Nothing) As Task
            Calls += 1
            Observer = progress
            Completion = New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            Return Completion.Task
        End Function
    End Class
End Class
