Option Explicit On
Option Strict On

Imports System.Globalization
Imports System.Reflection
Imports System.Threading
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class LocalizedMessageAcceptanceTest
    Public Shared Iterator Function Cultures() As IEnumerable(Of TestCaseData)
        For Each culture In {"en-US", "de-DE", "fr-FR", "es-ES", "zh-CN", "zh-TW", "ja-JP", "ar-SA", "he-IL", "hi-IN", "ko-KR"}
            Yield New TestCaseData(culture)
        Next
    End Function

    <TestCaseSource(NameOf(Cultures))>
    Public Sub FailurePresentationUsesUICultureAndKeepsOriginalDiagnostics(cultureName As String)
        WithCulture(cultureName,
            Sub()
                Dim original As New System.IO.IOException("Server diagnostic: 文件 / תיקייה / {0}", New InvalidOperationException("original cause"))
                Using browser As New CapturingBrowser()
                    Dim reported As DmsBrowserFailureEventArgs = Nothing
                    AddHandler browser.OperationFailed, Sub(sender, args) reported = args
                    browser.HandleBrowserFailure(original)
                    Assert.That(browser.Message, [Is].EqualTo(UiStrings.GetText("BrowserOperationFailed")))
                    Assert.That(browser.Message, Does.Not.Contain(original.Message))
                    Assert.That(reported.Exception, [Is].SameAs(original))
                    Assert.That(reported.Exception.InnerException, [Is].SameAs(original.InnerException))
                    Dim input = "Collection / 文件 / תיקייה / {0} / " & New String("X"c, 80)
                    Assert.That(UiStrings.Format("NewFolderPrompt", input), Does.Contain(input), "Technical arguments must remain complete and literal.")
                    Assert.That(CultureInfo.CurrentCulture.Name, [Is].EqualTo("en-US"), "Resource lookup must not replace the host's formatting culture.")
                End Using
            End Sub)
    End Sub

    <TestCaseSource(NameOf(Cultures))>
    Public Sub RetryHintsAreLocalizedWhileDiagnosticTooltipsRetainTheOriginalException(cultureName As String)
        WithCulture(cultureName,
            Sub()
                For Each mayRetry In {False, True}
                    Using dialog As New UploadProgressDialog({"文件 — תיקייה — दस्तावेज़.txt"}, False, mayRetry)
                        Dim tip = DiagnosticTip(dialog)
                        Dim retry = dialog.Controls.Find("RetryFailed", True).Single()
                        Assert.That(tip.GetToolTip(retry), [Is].EqualTo(UiStrings.GetText(If(mayRetry, "UploadRetryHint", "UploadRetryUnsupported"))))
                        Dim failure As New System.IO.IOException("Technical diagnostic: 文件 / {0}", New InvalidOperationException("original cause"))
                        dialog.Report(New UploadBatchSnapshot({"文件 — תיקייה — दस्तावेज़.txt"}, {UploadFileState.Failed}, {Nothing}, 0, {failure}))
                        Dim status = dialog.Controls.Find("TransferStatus", True).Single()
                        Assert.That(status.Text, Does.Contain(UiStrings.GetText("UploadFailed")))
                        Assert.That(tip.GetToolTip(status), [Is].EqualTo(failure.ToString()), "Diagnostics are supplied verbatim; they are not translated component prose.")
                        dialog.Finish()
                    End Using
                Next
            End Sub)
    End Sub

    <TestCaseSource(NameOf(Cultures)), Category("LocalizationExtended")>
    Public Sub NativeRetryTooltipFitsTheCurrentDisplay(cultureName As String)
        If Environment.GetEnvironmentVariable("DMS_LOCALIZATION_EXTENDED_TESTS") <> "1" Then Assert.Ignore("Opt in with DMS_LOCALIZATION_EXTENDED_TESTS=1.")
        WithCulture(cultureName,
            Sub()
                Using dialog As New UploadProgressDialog({"sample.txt"}, False, False), dispatcher As New UiTestDispatcher()
                    dialog.ShowInTaskbar = False
                    dialog.StartPosition = FormStartPosition.Manual
                    dialog.Location = Screen.PrimaryScreen.WorkingArea.Location + New Drawing.Size(20, 20)
                    dialog.Show()
                    dialog.Report(New UploadBatchSnapshot({"sample.txt"}, {UploadFileState.Failed}, {Nothing}, 0, {New System.IO.IOException("test diagnostic")}))
                    dialog.Finish()
                    dialog.Show()
                    Dim retry = dialog.Controls.Find("RetryFailed", True).Single()
                    Dim tip = DiagnosticTip(dialog)
                    Dim popup As PopupEventArgs = Nothing
                    AddHandler tip.Popup, Sub(sender, args) popup = args
                    Try
                        tip.Show(tip.GetToolTip(retry), dialog, 20, 20, 2000)
                        dispatcher.PumpUntil(Function() popup IsNot Nothing)
                        Dim area = Screen.FromControl(dialog).WorkingArea
                        Assert.That(popup.ToolTipSize.Width, [Is].GreaterThan(0).And.LessThanOrEqualTo(area.Width))
                        Assert.That(popup.ToolTipSize.Height, [Is].GreaterThan(0).And.LessThanOrEqualTo(area.Height))
                    Finally
                        tip.Hide(dialog)
                        dialog.Close()
                    End Try
                End Using
            End Sub)
    End Sub

    Private Shared Function DiagnosticTip(dialog As UploadProgressDialog) As ToolTip
        Return DirectCast(GetType(UploadProgressDialog).GetField("DiagnosticTip", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(dialog), ToolTip)
    End Function

    Private Shared Sub WithCulture(name As String, action As Action)
        Dim previousUI = CultureInfo.CurrentUICulture
        Dim previousFormat = CultureInfo.CurrentCulture
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name)
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US")
            action()
        Finally
            CultureInfo.CurrentUICulture = previousUI
            CultureInfo.CurrentCulture = previousFormat
        End Try
    End Sub

    Private Class CapturingBrowser
        Inherits DmsBrowser
        Friend Message As String
        Friend Sub New()
            MyBase.New(New NoDmsProvider())
        End Sub
        Protected Overrides Sub ShowFailureDialog(message As String, title As String, icon As MessageBoxIcon)
            Me.Message = message
        End Sub
    End Class
End Class
