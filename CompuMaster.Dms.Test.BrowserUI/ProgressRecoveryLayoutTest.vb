Option Explicit On
Option Strict On

Imports System.Drawing
Imports System.Globalization
Imports System.Threading
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class ProgressRecoveryLayoutTest
    <TestCase("de-DE", False), TestCase("es-ES", False), TestCase("ar-SA", False)>
    <TestCase("de-DE", True), TestCase("es-ES", True), TestCase("ar-SA", True)>
    Public Sub RecoveryKeepsProgressRowsAndActionsVisibleAtLargeFonts(cultureName As String, download As Boolean)
        Dim previous = CultureInfo.CurrentUICulture
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName)
            Using progress As New UploadProgressDialog({"Présentation — 文件 — दस्तावेज़.txt"}, download), enlarged As New Font(progress.Font.FontFamily, progress.Font.Size * 2)
                progress.ShowInTaskbar = False
                progress.Opacity = 0
                progress.Font = enlarged
                progress.MinimumSize = Size.Empty
                progress.MaximumSize = New Size(750, 720)
                progress.Show()
                Dim files = {"Présentation — 文件 — दस्तावेज़.txt", New String("X"c, 120) & ".txt"}
                For Each state In {UploadFileState.Waiting, UploadFileState.Transferring, UploadFileState.Finalizing, UploadFileState.Completed, UploadFileState.Failed, UploadFileState.Cancelled}
                    progress.Report(New UploadBatchSnapshot(files, {UploadFileState.Completed, state}, {New DmsTransferProgress(Long.MaxValue, Long.MaxValue, DmsTransferPhase.Completed), New DmsTransferProgress(Long.MaxValue \ 2, Long.MaxValue, DmsTransferPhase.Transferring)}, 1))
                    progress.PerformLayout()
                Next
                progress.Report(New UploadBatchSnapshot(files, {UploadFileState.Completed, UploadFileState.Failed}, {New DmsTransferProgress(Long.MaxValue, Long.MaxValue, DmsTransferPhase.Completed), New DmsTransferProgress(1024, Long.MaxValue, DmsTransferPhase.Transferring)}, 1, {Nothing, New System.IO.IOException("The connection was closed. " & New String("X"c, 160))}))
                progress.Finish()
                progress.Show()
                progress.PerformLayout()
                VerifyRecovery(progress, cultureName & " shown")
                progress.Size = progress.MinimumSize
                progress.PerformLayout()
                VerifyRecovery(progress, cultureName & " minimum")
                progress.Close()
            End Using
        Finally
            CultureInfo.CurrentUICulture = previous
        End Try
    End Sub

    <TestCase("de-DE"), TestCase("es-ES"), TestCase("ar-SA")>
    Public Sub BothRecoveryActionsStayAccessibleWhileFullDiagnosticsScroll(cultureName As String)
        Dim previous = CultureInfo.CurrentUICulture
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName)
            Using progress As New UploadProgressDialog({"completed", "failed", "waiting"}), enlarged As New Font(progress.Font.FontFamily, progress.Font.Size * 2)
                progress.ShowInTaskbar = False
                progress.Opacity = 0
                progress.Font = enlarged
                progress.MinimumSize = Size.Empty
                progress.MaximumSize = New Size(650, 600)
                progress.Show()
                Dim failure As New System.IO.IOException("The connection was closed. " & New String("X"c, 160))
                progress.Report(New UploadBatchSnapshot({"completed", "failed", "waiting"}, {UploadFileState.Completed, UploadFileState.Failed, UploadFileState.Waiting}, {Nothing, Nothing, Nothing}, 1, {Nothing, failure, Nothing}))
                progress.SetFailure(failure)
                progress.Finish()
                progress.Show()
                progress.PerformLayout()
                Assert.That(progress.Height, [Is].LessThanOrEqualTo(600))
                Dim footer = DirectCast(progress.Controls.Find("TransferActions", True).Single(), FlowLayoutPanel)
                Assert.That(progress.ClientRectangle.Contains(footer.Bounds), [Is].True)
                Assert.That(progress.Controls.Find("RetryFailed", True).Single().Visible, [Is].True)
                Assert.That(progress.Controls.Find("ContinueRemaining", True).Single().Visible, [Is].True)
                Dim failures As New List(Of String)
                LocalizedLayoutAcceptanceTest.Inspect(progress, failures, cultureName & " constrained recovery")
                Assert.That(failures, [Is].Empty, String.Join(Environment.NewLine, failures))
                Dim viewport = DirectCast(progress.Controls.Find("TransferViewport", True).Single(), Panel)
                Dim table = DirectCast(progress.Controls.Find("TransferLayout", True).Single(), TableLayoutPanel)
                Dim warning = table.Controls.OfType(Of Label)().Last()
                Assert.That(viewport.VerticalScroll.Visible, [Is].True)
                viewport.ScrollControlIntoView(warning)
                progress.PerformLayout()
                Dim visibleWarning = viewport.RectangleToClient(warning.RectangleToScreen(warning.ClientRectangle))
                Assert.That(viewport.ClientRectangle.Contains(visibleWarning), [Is].True, "The full warning must be reachable through the content scrollbar.")
                Dim rows = table.Controls.OfType(Of ListBox)().Single()
                Assert.That(CStr(rows.Items(1)), Does.Contain(failure.Message), "Scrolling must retain the full original file diagnostic.")
                Assert.That(progress.Controls.Find("TransferStatus", True).Single().Text, Does.Contain(failure.Message), "The status diagnostic must remain complete.")
                Assert.That(progress.ClientRectangle.Contains(footer.Bounds), [Is].True, "Scrolling must not move recovery actions out of the window.")
            End Using
        Finally
            CultureInfo.CurrentUICulture = previous
        End Try
    End Sub

    Private Shared Sub VerifyRecovery(progress As UploadProgressDialog, context As String)
        Assert.That(progress.Height, [Is].LessThanOrEqualTo(720), "The actual dialog must respect the bounded viewport after scaling and recovery.")
        Dim failures As New List(Of String)
        LocalizedLayoutAcceptanceTest.Inspect(progress, failures, context)
        Dim layout = DirectCast(progress.Controls.Find("TransferLayout", True).Single(), TableLayoutPanel)
        Assert.That(failures, [Is].Empty, String.Join(Environment.NewLine, failures))
        Dim files = layout.Controls.OfType(Of ListBox)().Single()
        Dim scrollbar = If(files.HorizontalExtent > files.ClientSize.Width, SystemInformation.HorizontalScrollBarHeight, 0)
        Assert.That(files.ClientSize.Height, [Is].GreaterThanOrEqualTo(files.ItemHeight + scrollbar), "Recovery must leave room to inspect at least one failed file above its horizontal scrollbar.")
    End Sub
End Class
