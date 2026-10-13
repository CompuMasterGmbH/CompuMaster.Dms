Imports System.Globalization
Imports System.Threading
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class TransferPreparationTest
    <Test>
    Public Sub PhaseTimingsAreCapturedBeforeQueuedUiCallbacks()
        Using dispatcher As New UiTestDispatcher()
            Dim seconds As Double
            Dim callbacks As Integer
            Dim recorder As New UploadProgressRecorder(Sub(value) callbacks += 1, Function() seconds)
            seconds = 2
            recorder.Report(New DmsTransferProgress(1, 10, DmsTransferPhase.Transferring))
            seconds = 8
            recorder.Report(New DmsTransferProgress(10, 10, DmsTransferPhase.Finalizing))
            seconds = 10
            Assert.That(callbacks, [Is].Zero)
            Assert.That(recorder.FirstSourceBytesSeconds, [Is].EqualTo(2))
            Assert.That(recorder.FinalizingSeconds, [Is].EqualTo(8))
            Assert.That(recorder.ElapsedSeconds, [Is].EqualTo(10))
            Assert.That(recorder.Latest.Phase, [Is].EqualTo(DmsTransferPhase.Finalizing))
        End Using
    End Sub

    <Test>
    Public Sub ANewAttemptDoesNotCountPreviouslyCompletedBytesAsNewThroughput()
        WithEnglish(Sub()
                        Dim seconds As Double
                        Dim display As New TransferDisplay(Function() seconds)
                        Dim sizes As Long?() = {100, 100}
                        Dim counters As DmsTransferProgress() = {New DmsTransferProgress(100, 100, DmsTransferPhase.Completed), Nothing}
                        display.DescribeBatch(New UploadBatchSnapshot({"completed", "pending"}, {UploadFileState.Completed, UploadFileState.Waiting}, counters, 1, plannedTotals:=sizes))
                        seconds = 2
                        counters(1) = New DmsTransferProgress(50, 100, DmsTransferPhase.Transferring)
                        Dim value = display.DescribeBatch(New UploadBatchSnapshot({"completed", "pending"}, {UploadFileState.Completed, UploadFileState.Transferring}, counters, 1, plannedTotals:=sizes))
                        Assert.That(value, Does.Contain("25 Bytes/s · ETA 00:00:02"))
                    End Sub)
    End Sub

    <Test>
    Public Sub PreparingRemoteFoldersNeverAnnouncesTheFirstFileOrPayloadProgress()
        Using dialog As New UploadProgressDialog({"one", "two"})
            dialog.ReportPlanning()
            Assert.That(DirectCast(dialog.Controls.Find("TransferLayout", True).Single(), TableLayoutPanel).Controls.OfType(Of Label)().First().Text, [Is].EqualTo(UiStrings.GetText("UploadPlanningSelection")))
            dialog.ReportPreparation("destination/folder", 3, 8)
            Dim labels = DirectCast(dialog.Controls.Find("TransferLayout", True).Single(), TableLayoutPanel).Controls.OfType(Of Label)().ToArray()
            Assert.That(labels(0).Text, [Is].EqualTo(UiStrings.Format("UploadPreparingFolderCount", 3, 8)))
            Assert.That(labels(0).Text, Does.Not.Contain("one"))
            dialog.Report(New UploadBatchSnapshot({"one", "two"}, {UploadFileState.Starting, UploadFileState.Waiting}, {Nothing, Nothing}, 0))
            Assert.That(labels(0).Text, [Is].EqualTo(UiStrings.Format("UploadFilePosition", 1, 2, "one")))
            Assert.That(dialog.Controls.Find("TransferStatus", True).Single().Text, [Is].EqualTo(UiStrings.GetText("UploadStarting")))
            dialog.Finish()
        End Using
    End Sub

    <Test>
    Public Sub CounterOnlyUpdatesDoNotRebuildOrRemeasureTheBatchRows()
        Dim files = Enumerable.Range(0, 266).Select(Function(index) "file-" & index).ToArray()
        Dim states = Enumerable.Repeat(UploadFileState.Waiting, files.Length).ToArray()
        Dim counters(files.Length - 1) As DmsTransferProgress
        Using dialog As New UploadProgressDialog(files)
            Dim rows = DirectCast(dialog.Controls.Find("TransferLayout", True).Single(), TableLayoutPanel).Controls.OfType(Of ListBox)().Single()
            rows.SelectedIndex = 150
            Dim initialMeasurements = dialog.RowMeasurements
            states(0) = UploadFileState.Transferring
            For index = 1 To 1000
                counters(0) = New DmsTransferProgress(index, 2000, DmsTransferPhase.Transferring)
                dialog.Report(New UploadBatchSnapshot(files, states, counters, 0))
            Next
            Assert.That(dialog.RowMeasurements - initialMeasurements, [Is].EqualTo(1), "Only the row changing from Waiting to Transferring needs measurement.")
            Assert.That(rows.SelectedIndex, [Is].EqualTo(150))
            Assert.That(rows.Items.Count, [Is].EqualTo(266))
            Assert.That(CStr(rows.Items(0)), Does.Contain(UiStrings.GetText("UploadTransferring")))
            dialog.Finish()
        End Using
    End Sub

    <Test>
    Public Sub BatchRateAndEstimateContinueAcrossFileBoundariesAndStalls()
        WithEnglish(Sub()
                        Dim seconds As Double
                        Dim display As New TransferDisplay(Function() seconds)
                        Dim sizes As Long?() = {1024, 3072}
                        Dim states = {UploadFileState.Waiting, UploadFileState.Waiting}
                        Dim counters As DmsTransferProgress() = {Nothing, Nothing}
                        display.DescribeBatch(New UploadBatchSnapshot({"one", "two"}, states, counters, 0, plannedTotals:=sizes))
                        seconds = 1
                        states(0) = UploadFileState.Completed
                        counters(0) = New DmsTransferProgress(1024, 1024, DmsTransferPhase.Completed)
                        states(1) = UploadFileState.Starting
                        counters(1) = New DmsTransferProgress(0, 3072, DmsTransferPhase.Transferring)
                        Dim boundary = New UploadBatchSnapshot({"one", "two"}, states, counters, 1, plannedTotals:=sizes)
                        Assert.That(display.DescribeBatch(boundary), Does.Contain("1.0 KiB/s · ETA 00:00:03"))
                        seconds = 3
                        Assert.That(display.DescribeBatch(boundary), Does.Contain("0 Bytes/s · ETA 00:00:09"))
                        seconds = 4
                        counters(1) = New DmsTransferProgress(1024, 3072, DmsTransferPhase.Transferring)
                        states(1) = UploadFileState.Transferring
                        Assert.That(display.DescribeBatch(New UploadBatchSnapshot({"one", "two"}, states, counters, 1, plannedTotals:=sizes)), Does.EndWith("ETA 00:00:04"))
                    End Sub)
    End Sub

    <Test>
    Public Sub BatchUnknownSizesAndConfirmationDoNotPretendToHaveACompletionTime()
        WithEnglish(Sub()
                        Dim display As New TransferDisplay(Function() 1)
                        Assert.That(display.DescribeBatch(New UploadBatchSnapshot({"file"}, {UploadFileState.Waiting}, {Nothing}, 0)), Does.EndWith(UiStrings.GetText("TransferBatchSizeUnknown")))
                        Assert.That(display.DescribeBatch(New UploadBatchSnapshot({"file"}, {UploadFileState.Finalizing}, {New DmsTransferProgress(10, 10, DmsTransferPhase.Finalizing)}, 0)), Does.EndWith(UiStrings.GetText("TransferAwaitingConfirmation")))
                        Assert.That(display.DescribeBatch(New UploadBatchSnapshot({"file"}, {UploadFileState.Completed}, {New DmsTransferProgress(10, 10, DmsTransferPhase.Completed)}, 0)), Does.EndWith("ETA 00:00:00"))
                    End Sub)
    End Sub

    Private Shared Sub WithEnglish(action As Action)
        Dim culture = CultureInfo.CurrentCulture
        Dim uiCulture = CultureInfo.CurrentUICulture
        Try
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US")
            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture
            action()
        Finally
            CultureInfo.CurrentCulture = culture
            CultureInfo.CurrentUICulture = uiCulture
        End Try
    End Sub
End Class
