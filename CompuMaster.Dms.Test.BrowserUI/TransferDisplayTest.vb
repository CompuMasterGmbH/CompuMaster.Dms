Imports System.Drawing
Imports System.Globalization
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class TransferDisplayTest
    <TestCase(0L, "0 Bytes"), TestCase(1023L, "1,023 Bytes"), TestCase(1024L, "1.0 KiB"), TestCase(1572864L, "1.5 MiB"), TestCase(1073741824L, "1.0 GiB"), TestCase(Long.MaxValue, "8.0 EiB")>
    Public Sub BinaryUnitsHaveOneDecimalAndHandleLargeCounters(bytes As Long, expected As String)
        WithCulture("en-US", Sub() Assert.That(TransferDisplay.FormatBytes(bytes), [Is].EqualTo(expected)))
    End Sub

    <Test>
    Public Sub GermanAmountsUseTheLocalDecimalSeparator()
        WithCulture("de-DE", Sub() Assert.That(TransferDisplay.FormatBytes(1572864), [Is].EqualTo("1,5 MiB")))
    End Sub

    <Test>
    Public Sub RecentRateDecaysDuringStallsWhileEtaUsesTheWholeTransferAverage()
        WithCulture("en-US",
            Sub()
                Dim seconds As Double
                Dim display As New TransferDisplay(Function() seconds)
                display.Describe(0, Nothing, UploadFileState.Transferring)
                seconds = 1
                Assert.That(display.Describe(0, New DmsTransferProgress(1024, 4096, DmsTransferPhase.Transferring), UploadFileState.Transferring), [Is].EqualTo("1.0 of 4.0 KiB processed · 1.0 KiB/s · ETA 00:00:03"))
                seconds = 3
                Assert.That(display.Describe(0, New DmsTransferProgress(1024, 4096, DmsTransferPhase.Transferring), UploadFileState.Transferring), Does.Contain("0 Bytes/s · ETA 00:00:09"))
                seconds = 4
                Assert.That(display.Describe(0, New DmsTransferProgress(2048, 4096, DmsTransferPhase.Transferring), UploadFileState.Transferring), Does.EndWith("ETA 00:00:04"))
            End Sub)
    End Sub

    <Test>
    Public Sub UnknownCountersFinalizationAndNewFilesDoNotInventAnEta()
        WithCulture("en-US",
            Sub()
                Dim seconds As Double
                Dim display As New TransferDisplay(Function() seconds)
                Assert.That(display.Describe(0, Nothing, UploadFileState.Waiting), Does.EndWith("Unknown/s · ETA Unknown"))
                seconds = 1
                Assert.That(display.Describe(0, New DmsTransferProgress(100, Nothing, DmsTransferPhase.Transferring), UploadFileState.Transferring), Does.EndWith("ETA Unknown"))
                Assert.That(display.Describe(0, New DmsTransferProgress(100, 100, DmsTransferPhase.Finalizing), UploadFileState.Finalizing), Does.Contain("0 Bytes/s · ETA Unknown"))
                Assert.That(display.Describe(0, New DmsTransferProgress(100, 100, DmsTransferPhase.Completed), UploadFileState.Completed), Does.EndWith("ETA 00:00:00"))
                Assert.That(display.Describe(1, New DmsTransferProgress(0, 100, DmsTransferPhase.Transferring), UploadFileState.Transferring), Does.EndWith("ETA Unknown"))
            End Sub)
    End Sub

    <TestCase(True), TestCase(False)>
    Public Sub RecoverySkipsSuccessfulFilesAndPreservesFailureDiagnostics(retryFailed As Boolean)
        Using dispatcher As New UiTestDispatcher()
            Dim failure As New System.IO.IOException("Connection reset", New InvalidOperationException("inner diagnostic"))
            Dim files = {"successful", "failed", "pending"}
            Dim previous As New UploadBatchSnapshot(files, {UploadFileState.Completed, UploadFileState.Failed, UploadFileState.Waiting}, {New DmsTransferProgress(100, 100, DmsTransferPhase.Completed), New DmsTransferProgress(50, 100, DmsTransferPhase.Transferring), Nothing}, 1, {Nothing, failure, Nothing})
            Dim provider As New FixtureProvider()
            Dim collector As New Collector()
            dispatcher.Finish(DmsBrowser.UploadBatchWithProgressAsync(provider, "folder", files, collector, CancellationToken.None, previous, retryFailed))
            Assert.That(provider.Paths.Select(Function(path) System.IO.Path.GetFileName(path)), [Is].EqualTo(If(retryFailed, {"failed", "pending"}, {"pending"})))
            Assert.That(collector.Latest.States(0), [Is].EqualTo(UploadFileState.Completed))
            Assert.That(collector.Latest.States(1), [Is].EqualTo(If(retryFailed, UploadFileState.Completed, UploadFileState.Failed)))
            Assert.That(collector.Latest.Errors(1), [Is].EqualTo(If(retryFailed, Nothing, failure)))
            Assert.That(previous.Errors(1), [Is].SameAs(failure), "Previous snapshots remain immutable.")
        End Using
    End Sub

    <TestCase(True), TestCase(False)>
    Public Sub DialogShowsTheFailureAndRespectsRetrySafety(allowRetry As Boolean)
        Dim failure As Exception
        Try
            Throw New System.IO.IOException("Connection reset by peer", New InvalidOperationException("inner diagnostic"))
        Catch ex As Exception
            failure = ex
        End Try
        Using icon = DirectCast(SystemIcons.Warning.Clone(), Icon), dialog As New UploadProgressDialog({"failed", "pending"}, False, allowRetry, icon)
            dialog.Show()
            dialog.Report(New UploadBatchSnapshot({"failed", "pending"}, {UploadFileState.Failed, UploadFileState.Waiting}, {Nothing, Nothing}, 0, {failure, Nothing}))
            Assert.That(dialog.Controls.Find("TransferStatus", True).Single().Text, Does.Contain(failure.Message))
            Dim field = GetType(UploadProgressDialog).GetField("DiagnosticTip", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic)
            Assert.That(DirectCast(field.GetValue(dialog), ToolTip).GetToolTip(dialog.Controls.Find("TransferStatus", True).Single()), Does.Contain("inner diagnostic").And.Contain(NameOf(DialogShowsTheFailureAndRespectsRetrySafety)))
            Assert.That(dialog.Icon.Handle, [Is].EqualTo(icon.Handle))
            dialog.Finish()
            dialog.Show()
            Assert.That(dialog.Controls.Find("RetryFailed", True).Single().Enabled, [Is].EqualTo(allowRetry))
            Assert.That(dialog.Controls.Find("ContinueRemaining", True).Single().Visible, [Is].True)
            dialog.PrepareAttempt()
            Assert.That(dialog.CancellationToken.IsCancellationRequested, [Is].False)
            Assert.That(dialog.Controls.Find("RetryFailed", True).Single().Visible, [Is].False)
            dialog.Finish()
            dialog.Close()
        End Using
    End Sub

    <Test>
    Public Sub DownloadUsesTheSelectedResourceAndPropagatesCancellation()
        Using dispatcher As New UiTestDispatcher(), cancellation As New CancellationTokenSource()
            Dim provider As New FixtureProvider()
            Dim item As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .FullName = "duplicate", .Name = "file", .ExtendedInfosFileID = "selected-id"}
            Dim collector As New Collector()
            dispatcher.Finish(DmsBrowser.DownloadBatchWithProgressAsync(provider, {item}, {"local"}, collector, cancellation.Token))
            Assert.That(provider.SelectedItem, [Is].SameAs(item))
            Assert.That(provider.Token, [Is].EqualTo(cancellation.Token))
            Assert.That(collector.Latest.States(0), [Is].EqualTo(UploadFileState.Completed))
            Assert.That(collector.Latest.Counters(0).BytesTransferred, [Is].EqualTo(100))
        End Using
    End Sub

    <Test>
    Public Sub ClosingTheFailedUploadDialogDoesNotRethrowTheDisplayedFailure()
        Using dispatcher As New UiTestDispatcher(), browser As New PreviewBrowser(New FixtureProvider With {.FailUpload = True}), timer As New System.Windows.Forms.Timer With {.Interval = 10}
            Dim observed As Boolean
            AddHandler timer.Tick,
                Sub()
                    Dim dialog = Application.OpenForms.OfType(Of UploadProgressDialog)().FirstOrDefault()
                    If dialog Is Nothing OrElse Not dialog.Modal Then Return
                    Assert.That(dialog.Controls.Find("TransferStatus", True).Single().Text, Does.Contain("Connection reset"))
                    Assert.That(dialog.Icon.Handle, [Is].EqualTo(browser.Icon.Handle))
                    observed = True
                    timer.Stop()
                    dialog.Close()
                End Sub
            timer.Start()
            browser.Show()
            Dim method = GetType(DmsBrowser).GetMethod("UploadWithDialogAsync", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic)
            Dim operation = DirectCast(method.Invoke(browser, {"folder", New String() {"file"}}), Task(Of Boolean))
            Assert.DoesNotThrow(Sub() dispatcher.Finish(operation))
            Assert.That(observed, [Is].True)
            Assert.That(operation.Result, [Is].False, "A displayed failure must not become a success notification.")
        End Using
    End Sub

    Private Shared Sub WithCulture(name As String, action As Action)
        Dim previous = CultureInfo.CurrentCulture
        Dim previousUi = CultureInfo.CurrentUICulture
        Try
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name)
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name)
            action()
        Finally
            CultureInfo.CurrentCulture = previous
            CultureInfo.CurrentUICulture = previousUi
        End Try
    End Sub

    Private Class Collector
        Implements IProgress(Of UploadBatchSnapshot)
        Friend Latest As UploadBatchSnapshot
        Public Sub Report(value As UploadBatchSnapshot) Implements IProgress(Of UploadBatchSnapshot).Report
            Latest = value
        End Sub
    End Class
    Private Class FixtureProvider
        Inherits NoDmsProvider
        Friend ReadOnly Paths As New List(Of String)
        Friend SelectedItem As DmsResourceItem
        Friend Token As CancellationToken
        Friend FailUpload As Boolean
        Public Overrides Function UploadFileWithProgressAsync(path As String, local As String, progress As IProgress(Of DmsTransferProgress), Optional cancellationToken As CancellationToken = Nothing) As Task
            Paths.Add(path)
            If FailUpload Then Return Task.FromException(New System.IO.IOException("Connection reset"))
            progress.Report(New DmsTransferProgress(100, 100, DmsTransferPhase.Completed))
            Return Task.CompletedTask
        End Function
        Public Overrides Function DownloadFileWithProgressAsync(remoteFile As DmsResourceItem, localFilePath As String, progress As IProgress(Of DmsTransferProgress), Optional cancellationToken As CancellationToken = Nothing) As Task
            SelectedItem = remoteFile
            Token = cancellationToken
            progress.Report(New DmsTransferProgress(100, 100, DmsTransferPhase.Completed))
            Return Task.CompletedTask
        End Function
    End Class
    Private Class PreviewBrowser
        Inherits DmsBrowser
        Friend Sub New(provider As BaseDmsProvider)
            MyBase.New(provider)
        End Sub
        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub
    End Class
End Class
