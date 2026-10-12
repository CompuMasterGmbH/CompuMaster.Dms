Option Explicit On
Option Strict On

Imports System.Drawing
Imports System.Globalization
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports NUnit.Framework

''' <summary>Checks representative component text in Windows-owned dialogs on the installed desktop.</summary>
<TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("LocalizationExtended")>
Public Class NativeLocalizedDialogAcceptanceTest
    ''' <summary>Provides the supported cultures and initial monitor locations.</summary>
    ''' <returns>The culture and monitor combinations.</returns>
    Public Shared Iterator Function DialogCases() As IEnumerable(Of TestCaseData)
        For Each culture In {"en-US", "de-DE", "fr-FR", "es-ES", "zh-CN", "zh-TW", "ja-JP", "ar-SA", "he-IL", "hi-IN"}
            Yield New TestCaseData(culture, False)
            Yield New TestCaseData(culture, True)
        Next
    End Function

    ''' <summary>Checks native dialog bounds and preservation of localized text and Unicode arguments.</summary>
    ''' <param name="cultureName">The UI culture used for component text.</param>
    ''' <param name="startOnRight">Whether the owner starts on the nearest right-hand monitor.</param>
    <TestCaseSource(NameOf(DialogCases))>
    Public Sub NativeDialogsPreserveLocalizedTextAndFitTheDisplay(cultureName As String, startOnRight As Boolean)
        If Environment.GetEnvironmentVariable("DMS_LOCALIZATION_EXTENDED_TESTS") <> "1" Then Assert.Ignore("Opt in with DMS_LOCALIZATION_EXTENDED_TESTS=1.")
        VerifyDialogs(cultureName, startOnRight)
    End Sub

    ''' <summary>Checks localized tooltip size on either physical-DPI display.</summary>
    ''' <param name="cultureName">The UI culture used for the retry hint.</param>
    ''' <param name="startOnRight">Whether the tooltip owner starts on the right-hand monitor.</param>
    <TestCaseSource(NameOf(DialogCases))>
    Public Sub NativeRetryTooltipFitsItsPhysicalDpiDisplay(cultureName As String, startOnRight As Boolean)
        If Environment.GetEnvironmentVariable("DMS_LOCALIZATION_EXTENDED_TESTS") <> "1" OrElse Environment.GetEnvironmentVariable("DMS_PHYSICAL_DPI_TESTS") <> "1" Then Assert.Ignore("Use the opt-in physical-DPI executable or configure a physical-DPI test host explicitly.")
        VerifyTooltip(cultureName, startOnRight)
    End Sub

    Friend Shared Sub VerifyDialogs(cultureName As String, startOnRight As Boolean)
        Dim previous = CultureInfo.CurrentUICulture
        Dim primary = Screen.PrimaryScreen
        Dim right = Screen.AllScreens.Where(Function(screen) screen.Bounds.Left >= primary.Bounds.Right).OrderBy(Function(screen) screen.Bounds.Left).FirstOrDefault()
        If startOnRight AndAlso right Is Nothing Then Assert.Ignore("A right-hand display is required for this case.")
        Dim targetScreen = If(startOnRight, right, primary)
        Dim directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DmsLocalization-" & Guid.NewGuid().ToString("N"))
        Assert.That(System.IO.Directory.Exists(directory), [Is].False, "The local owned scope must be absent before setup.")
        Dim originalFailure As Exception = Nothing
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName)
            System.IO.Directory.CreateDirectory(directory)
            Using owner As New Form With {.Text = "Localization acceptance", .StartPosition = FormStartPosition.Manual, .ShowInTaskbar = True, .Size = New Size(320, 180)}
                owner.Location = targetScreen.WorkingArea.Location + New Size(24, 24)
                owner.Show()
                Dim message = UiStrings.GetText("BrowserOperationFailed") & Environment.NewLine & "Présentation — 文件 — 日本語 — दस्तावेज़ — {0}"
                InspectModal(owner, "message", UiStrings.GetText("DownloadTitle"), message,
                    Function() MessageBox.Show(owner, message, UiStrings.GetText("DownloadTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error))
                Using picker As New OpenFileDialog With {.InitialDirectory = directory, .Title = UiStrings.GetText("UploadTitle"), .Filter = UiStrings.GetText("AllFilesFilter"), .Multiselect = True}
                    InspectModal(owner, "open", picker.Title, Nothing, Function() picker.ShowDialog(owner))
                End Using
                Using picker As New SaveFileDialog With {.InitialDirectory = directory, .Title = UiStrings.GetText("DownloadTitle"), .Filter = UiStrings.GetText("AllFilesFilter"), .FileName = "Présentation — 文件 — 日本語 — दस्तावेज़.txt"}
                    InspectModal(owner, "save", picker.Title, picker.FileName, Function() picker.ShowDialog(owner))
                End Using
                Using picker As New FolderBrowserDialog With {.SelectedPath = directory, .Description = UiStrings.GetText("ActionUploadFolder"), .ShowNewFolderButton = False}
                    InspectModal(owner, "folder", Nothing, picker.Description, Function() picker.ShowDialog(owner))
                End Using
                owner.Close()
            End Using
        Catch ex As Exception
            originalFailure = ex
            Throw
        Finally
            CultureInfo.CurrentUICulture = previous
            'Only this unique local scope is removed, including after partial setup/failure.
            Try
                If System.IO.Directory.Exists(directory) Then System.IO.Directory.Delete(directory, True)
                Assert.That(System.IO.Directory.Exists(directory), [Is].False, "The owned local scope must be removed.")
            Catch cleanupFailure As Exception
                If originalFailure Is Nothing Then Throw
                Throw New AggregateException("Native localization acceptance and local cleanup failed.", originalFailure, cleanupFailure)
            End Try
        End Try
    End Sub

    Friend Shared Sub VerifyTooltip(cultureName As String, startOnRight As Boolean)
        Dim previous = CultureInfo.CurrentUICulture
        Dim primary = Screen.PrimaryScreen
        Dim right = Screen.AllScreens.Where(Function(screen) screen.Bounds.Left >= primary.Bounds.Right).OrderBy(Function(screen) screen.Bounds.Left).FirstOrDefault()
        If startOnRight AndAlso right Is Nothing Then Assert.Ignore("A right-hand display is required for this case.")
        Dim targetScreen = If(startOnRight, right, primary)
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName)
            Using dialog As New UploadProgressDialog({"Présentation — 文件 — दस्तावेज़.txt"}, False, False)
                dialog.ShowInTaskbar = True
                dialog.PerformAutoScale()
                dialog.StartPosition = FormStartPosition.Manual
                dialog.Location = targetScreen.WorkingArea.Location + New Size(24, 24)
                dialog.Show()
                dialog.Report(New UploadBatchSnapshot({"Présentation — 文件 — दस्तावेज़.txt"}, {UploadFileState.Failed}, {Nothing}, 0, {New System.IO.IOException("test diagnostic")}))
                dialog.Finish()
                dialog.Show()
                Application.DoEvents()
                Dim scale As Integer
                Marshal.ThrowExceptionForHR(GetScaleFactorForMonitor(MonitorFromWindow(dialog.Handle, 2), scale))
                Dim expectedDpi = CInt(Math.Round(scale * 96.0 / 100.0))
                Assert.That(dialog.DeviceDpi, [Is].EqualTo(expectedDpi))
                Assert.That(GetDpiForWindow(dialog.Handle), [Is].EqualTo(CUInt(expectedDpi)))
                Dim retry = dialog.Controls.Find("RetryFailed", True).Single()
                Dim tip = DirectCast(GetType(UploadProgressDialog).GetField("DiagnosticTip", System.Reflection.BindingFlags.Instance Or System.Reflection.BindingFlags.NonPublic).GetValue(dialog), ToolTip)
                'The standalone acceptance host may not own foreground focus after the notice.
                'Force this explicitly requested popup to render even for an inactive owner.
                tip.ShowAlways = True
                Dim popup As PopupEventArgs = Nothing
                AddHandler tip.Popup, Sub(sender, args) popup = args
                Try
                    tip.Show(tip.GetToolTip(retry), dialog, 20, 20, 2000)
                    Dim deadline = DateTime.UtcNow.AddSeconds(5)
                    While popup Is Nothing AndAlso DateTime.UtcNow < deadline
                        Application.DoEvents()
                        Thread.Sleep(1)
                    End While
                    Assert.That(popup, [Is].Not.Null, "A native tooltip popup must actually be raised.")
                    Dim area = Screen.FromControl(dialog).WorkingArea
                    Assert.That(popup.ToolTipSize.Width, [Is].GreaterThan(0).And.LessThanOrEqualTo(area.Width))
                    Assert.That(popup.ToolTipSize.Height, [Is].GreaterThan(0).And.LessThanOrEqualTo(area.Height))
                    Assert.That(tip.GetToolTip(retry), [Is].EqualTo(UiStrings.GetText("UploadRetryUnsupported")))
                    Console.WriteLine($"Native tooltip {cultureName}: dpi={expectedDpi}; size={popup.ToolTipSize}; monitor={targetScreen.DeviceName}.")
                Finally
                    tip.Hide(dialog)
                    dialog.Close()
                End Try
            End Using
        Finally
            CultureInfo.CurrentUICulture = previous
        End Try
    End Sub

    Private Shared Sub InspectModal(owner As Form, scenario As String, title As String, expectedText As String, show As Func(Of DialogResult))
        Dim ownerHandle = owner.Handle
        Dim threadId = GetCurrentThreadId()
        Dim observed = False
        Dim failure As Exception = Nothing
        Dim timedOut = 0
        Using timer As New System.Windows.Forms.Timer With {.Interval = 350},
              watchdog As New System.Threading.Timer(
                  Sub(state)
                      Interlocked.Exchange(timedOut, 1)
                      Dim dialog = FindOwnedDialog(threadId, ownerHandle)
                      If dialog <> IntPtr.Zero Then PostMessage(dialog, &H10, IntPtr.Zero, IntPtr.Zero)
                  End Sub, Nothing, 10000, 1000)
            AddHandler timer.Tick,
                Sub(sender, args)
                    Dim dialog = FindOwnedDialog(threadId, ownerHandle)
                    If dialog = IntPtr.Zero Then Return
                    timer.Stop()
                    Try
                        Dim bounds = ReadBounds(dialog)
                        SavePreview(dialog, bounds, scenario, owner)
                        Assert.That(Screen.FromHandle(dialog).WorkingArea.Contains(bounds), [Is].True, scenario & " must fit its monitor: " & bounds.ToString())
                        Assert.That(Screen.FromHandle(dialog).DeviceName, [Is].EqualTo(Screen.FromControl(owner).DeviceName), "The modal window must use the owner's display.")
                        If title IsNot Nothing Then Assert.That(ReadText(dialog), [Is].EqualTo(title), "The localized title must remain complete.")
                        Dim texts As New List(Of String)
                        Dim childFailure As Exception = Nothing
                        EnumChildWindows(dialog,
                            Function(child, data)
                                Try
                                    If IsWindowVisible(child) Then
                                        Dim text = ReadText(child)
                                        texts.Add(text)
                                        If scenario = "message" AndAlso text = expectedText Then
                                            InspectMessageHeight(child, text)
                                        End If
                                    End If
                                    Return True
                                Catch ex As Exception
                                    childFailure = ex
                                    Return False
                                End Try
                            End Function, IntPtr.Zero)
                        If childFailure IsNot Nothing Then Throw childFailure
                        If expectedText IsNot Nothing Then Assert.That(texts, Does.Contain(expectedText), "The component message/Unicode filename must reach a visible native control unchanged.")
                        Console.WriteLine($"Native dialog {CultureInfo.CurrentUICulture.Name}/{scenario}: dpi={GetDpiForWindow(dialog)}; bounds={bounds}; monitor={Screen.FromHandle(dialog).DeviceName}.")
                        observed = True
                    Catch ex As Exception
                        'Never throw from a modal-loop timer or native callback.
                        failure = ex
                    Finally
                        PostMessage(dialog, &H10, IntPtr.Zero, IntPtr.Zero)
                    End Try
                End Sub
            timer.Start()
            Try
                show()
            Finally
                timer.Stop()
            End Try
        End Using
        If failure IsNot Nothing Then Throw New InvalidOperationException("Native " & scenario & " acceptance failed.", failure)
        Assert.That(timedOut, [Is].Zero, "The owned modal dialog exceeded its ten-second deadline.")
        Assert.That(observed, [Is].True, "The owned modal dialog must actually be observed.")
    End Sub

    Private Shared Sub InspectMessageHeight(window As IntPtr, text As String)
        'Use the native control's client coordinates and selected HFONT. Converting
        'HFONT through a managed Font can change wrapping on Framework after a
        'monitor transition; physical screen bounds also differ from client units.
        Dim available As NativeRectangle
        If Not GetClientRect(window, available) Then Throw New System.ComponentModel.Win32Exception()
        Dim measured As New NativeRectangle With {.Right = available.Right}
        Dim dc = GetDC(window)
        If dc = IntPtr.Zero Then Throw New System.ComponentModel.Win32Exception()
        Dim font = SendMessage(window, &H31, IntPtr.Zero, IntPtr.Zero)
        Dim previous = If(font = IntPtr.Zero, IntPtr.Zero, SelectObject(dc, font))
        Try
            Dim height = DrawText(dc, text, text.Length, measured, &H400UI Or &H10UI Or &H800UI)
            Assert.That(height, [Is].GreaterThan(0))
            Assert.That(available.Bottom, [Is].GreaterThanOrEqualTo(measured.Bottom), "The complete Unicode message must fit vertically in the native control's client units.")
        Finally
            If previous <> IntPtr.Zero Then SelectObject(dc, previous)
            ReleaseDC(window, dc)
        End Try
    End Sub

    Private Shared Sub SavePreview(dialog As IntPtr, bounds As Rectangle, scenario As String, owner As Form)
        Dim output = Environment.GetEnvironmentVariable("DMS_LOCALIZATION_PREVIEW_DIR")
        If String.IsNullOrEmpty(output) Then Return
        System.IO.Directory.CreateDirectory(output)
        Using bitmap As New Bitmap(bounds.Width, bounds.Height), canvas = Graphics.FromImage(bitmap)
            Dim dc = canvas.GetHdc()
            Try
                If Not PrintWindow(dialog, dc, 0) Then Throw New System.ComponentModel.Win32Exception()
            Finally
                canvas.ReleaseHdc(dc)
            End Try
            bitmap.Save(System.IO.Path.Combine(output, CultureInfo.CurrentUICulture.Name & "-native-" & scenario & "-" & Screen.FromControl(owner).DeviceName.Replace("\", "").Replace(".", "") & ".png"), Imaging.ImageFormat.Png)
        End Using
    End Sub

    Private Shared Function FindOwnedDialog(threadId As UInteger, owner As IntPtr) As IntPtr
        Dim result = IntPtr.Zero
        EnumThreadWindows(threadId,
            Function(window, data)
                If GetWindow(window, 4) = owner AndAlso IsWindowVisible(window) Then
                    result = window
                    Return False
                End If
                Return True
            End Function, IntPtr.Zero)
        Return result
    End Function

    Private Shared Function ReadText(window As IntPtr) As String
        Dim text As New StringBuilder(GetWindowTextLength(window) + 1)
        GetWindowText(window, text, text.Capacity)
        Return text.ToString()
    End Function

    Private Shared Function ReadBounds(window As IntPtr) As Rectangle
        Dim bounds As NativeRectangle
        If Not GetWindowRect(window, bounds) Then Throw New System.ComponentModel.Win32Exception()
        Return Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom)
    End Function

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativeRectangle
        Friend Left, Top, Right, Bottom As Integer
    End Structure
    Private Delegate Function WindowCallback(window As IntPtr, data As IntPtr) As Boolean
    <DllImport("kernel32.dll")>
    Private Shared Function GetCurrentThreadId() As UInteger
    End Function
    <DllImport("user32.dll")>
    Private Shared Function EnumThreadWindows(threadId As UInteger, callback As WindowCallback, data As IntPtr) As Boolean
    End Function
    <DllImport("user32.dll")>
    Private Shared Function EnumChildWindows(window As IntPtr, callback As WindowCallback, data As IntPtr) As Boolean
    End Function
    <DllImport("user32.dll")>
    Private Shared Function GetWindow(window As IntPtr, command As UInteger) As IntPtr
    End Function
    <DllImport("user32.dll")>
    Private Shared Function IsWindowVisible(window As IntPtr) As Boolean
    End Function
    <DllImport("user32.dll", EntryPoint:="GetWindowTextLengthW", CharSet:=CharSet.Unicode)>
    Private Shared Function GetWindowTextLength(window As IntPtr) As Integer
    End Function
    <DllImport("user32.dll", EntryPoint:="GetWindowTextW", CharSet:=CharSet.Unicode)>
    Private Shared Function GetWindowText(window As IntPtr, text As StringBuilder, count As Integer) As Integer
    End Function
    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function GetWindowRect(window As IntPtr, ByRef bounds As NativeRectangle) As Boolean
    End Function
    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function GetClientRect(window As IntPtr, ByRef bounds As NativeRectangle) As Boolean
    End Function
    <DllImport("user32.dll")>
    Private Shared Function GetDC(window As IntPtr) As IntPtr
    End Function
    <DllImport("user32.dll")>
    Private Shared Function ReleaseDC(window As IntPtr, dc As IntPtr) As Integer
    End Function
    <DllImport("gdi32.dll")>
    Private Shared Function SelectObject(dc As IntPtr, value As IntPtr) As IntPtr
    End Function
    <DllImport("user32.dll", EntryPoint:="DrawTextW", CharSet:=CharSet.Unicode)>
    Private Shared Function DrawText(dc As IntPtr, text As String, count As Integer, ByRef bounds As NativeRectangle, flags As UInteger) As Integer
    End Function
    <DllImport("user32.dll")>
    Private Shared Function GetDpiForWindow(window As IntPtr) As UInteger
    End Function
    <DllImport("user32.dll")>
    Private Shared Function MonitorFromWindow(window As IntPtr, flags As UInteger) As IntPtr
    End Function
    <DllImport("shcore.dll")>
    Private Shared Function GetScaleFactorForMonitor(monitor As IntPtr, ByRef scale As Integer) As Integer
    End Function
    <DllImport("user32.dll", EntryPoint:="SendMessageW")>
    Private Shared Function SendMessage(window As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function
    <DllImport("user32.dll", EntryPoint:="PostMessageW")>
    Private Shared Function PostMessage(window As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
    End Function
    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function PrintWindow(window As IntPtr, dc As IntPtr, flags As UInteger) As Boolean
    End Function
End Class
