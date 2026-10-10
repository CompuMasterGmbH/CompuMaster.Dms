Option Explicit On
Option Strict On

Imports System.Drawing
Imports System.Globalization
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports NUnit.Framework

''' <summary>Checks native monitor DPI and monitor transitions without changing display settings.</summary>
<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class LocalizedMonitorDpiTest
    ''' <summary>Provides every supported culture with both initial monitor locations.</summary>
    ''' <returns>The culture and initial-monitor combinations.</returns>
    Public Shared Iterator Function MonitorCases() As IEnumerable(Of TestCaseData)
        For Each culture In {"en-US", "de-DE", "fr-FR", "es-ES", "zh-CN", "zh-TW", "ja-JP", "ar-SA", "he-IL", "hi-IN"}
            Yield New TestCaseData(culture, False)
            Yield New TestCaseData(culture, True)
        Next
    End Function

    ''' <summary>Verifies native-DPI creation, constrained layouts and transitions starting on either monitor.</summary>
    ''' <param name="cultureName">The UI culture used when creating the forms.</param>
    ''' <param name="startOnRight">Whether the first window location is on the right-hand monitor.</param>
    <TestCaseSource(NameOf(MonitorCases))>
    Public Sub ControlsFitAcrossNativeMonitors(cultureName As String, startOnRight As Boolean)
        If Environment.GetEnvironmentVariable("DMS_PHYSICAL_DPI_TESTS") <> "1" Then
            Assert.Ignore("Opt in with DMS_PHYSICAL_DPI_TESTS=1 on an interactive multi-monitor workstation.")
        End If
#If NET8_0_OR_GREATER Then
        Assert.That(Application.HighDpiMode, [Is].EqualTo(HighDpiMode.PerMonitorV2), "The test host must enable native DPI support before creating windows.")
#End If
        Dim previousCulture = CultureInfo.CurrentUICulture
        Try
            Dim attached = ReadMonitors()
            For Each monitor In attached
                Console.WriteLine($"Attached {monitor.Name}: bounds={monitor.Bounds}; work={monitor.WorkArea}; scale={monitor.Scale}%; primary={monitor.Primary}.")
            Next
            Dim primary = attached.Single(Function(m) m.Primary)
            Dim right = attached.Where(Function(m) m.Bounds.Left >= primary.Bounds.Right AndAlso m.Scale <> primary.Scale).OrderBy(Function(m) m.Bounds.Left).FirstOrDefault()
            Assert.That(right, [Is].Not.Null, "Use the primary display and the nearest higher/lower-scale display to its right.")
            Dim monitors = {primary, right}
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName)
            Dim failures As New List(Of String)
            For Each monitor In monitors
                Console.WriteLine($"Monitor {monitor.Name}: bounds={monitor.Bounds}; work={monitor.WorkArea}; scale={monitor.Scale}%; primary={monitor.Primary}.")
            Next
            Dim sequence = If(startOnRight, {right, primary, right, primary, right}, {primary, right, primary, right, primary})
            Dim selectedScenarios = ReadSelectedScenarios()
            Dim scenario = 0
            For Each form In LocalizedLayoutAcceptanceTest.CreatePreviewForms()
                Using form
                    scenario += 1
                    If Not selectedScenarios.Contains(scenario) Then Continue For
                    'Standalone windows must not inherit the hidden parking window's
                    'primary-monitor DPI on .NET Framework; real modal windows have an owner.
                    form.ShowInTaskbar = True
                    'Complete design-font scaling before assigning physical desktop coordinates.
                    'Otherwise .NET Framework can scale the manual initial location itself.
                    form.PerformAutoScale()
                    form.StartPosition = FormStartPosition.Manual
                    Console.WriteLine($"{form.GetType().Name}: handle created in constructor={form.IsHandleCreated}.")
                    form.Location = New Point(sequence(0).WorkArea.Left + 24, sequence(0).WorkArea.Top + 24)
                    form.Show()
                    For stepIndex = 0 To sequence.Length - 1
                        Dim monitor = sequence(stepIndex)
                        form.Location = New Point(monitor.WorkArea.Left + 24, monitor.WorkArea.Top + 24)
                        Dim expectedDpi = CUInt(Math.Round(monitor.Scale * 96.0 / 100.0))
                        For attempt = 0 To 20
                            Application.DoEvents()
                            If GetDpiForWindow(form.Handle) = expectedDpi Then Exit For
                            Thread.Sleep(10)
                        Next
                        form.PerformLayout()
                        Dim actualDpi = GetDpiForWindow(form.Handle)
                        Dim label = $"{cultureName}/{scenario}/{form.GetType().Name}/step={stepIndex}/scale={monitor.Scale}"
                        Dim nativeBounds As NativeRectangle
                        If Not GetWindowRect(form.Handle, nativeBounds) Then Throw New System.ComponentModel.Win32Exception()
                        Console.WriteLine($"{label}: native DPI={actualDpi}; WinForms DPI={form.DeviceDpi}; bounds={form.Bounds}; nativeBounds={nativeBounds.ToRectangle()}.")
                        If actualDpi <> expectedDpi OrElse form.DeviceDpi <> CInt(expectedDpi) Then failures.Add(label & $" expected DPI={expectedDpi}; native={actualDpi}; WinForms={form.DeviceDpi}.")
                        LocalizedLayoutAcceptanceTest.Inspect(form, failures, label)
                        If Not monitor.WorkArea.Contains(form.Bounds) Then failures.Add(label & " outside monitor work area: " & form.Bounds.ToString())
                        If form.FormBorderStyle = FormBorderStyle.Sizable Then
                            Dim normal = form.Size
                            form.Size = form.MinimumSize
                            Application.DoEvents()
                            LocalizedLayoutAcceptanceTest.Inspect(form, failures, label & "/minimum")
                            form.Size = New Size(Math.Min(normal.Width + 240, monitor.WorkArea.Width - 48), Math.Min(normal.Height + 100, monitor.WorkArea.Height - 48))
                            Application.DoEvents()
                            LocalizedLayoutAcceptanceTest.Inspect(form, failures, label & "/wide")
                            form.Size = normal
                        End If
                        If TypeOf form Is DmsBrowser Then
                            Dim normal = form.Size
                            form.MaximumSize = New Size(CInt(900 * actualDpi / 96.0), 0)
                            form.Width = form.MaximumSize.Width
                            Application.DoEvents()
                            LocalizedLayoutAcceptanceTest.Inspect(form, failures, label & "/constrained")
                            form.MaximumSize = Size.Empty
                            form.Size = normal
                        End If
                        If TypeOf form Is UploadProgressDialog Then
                            Dim progress = DirectCast(form, UploadProgressDialog)
                            Dim files = {"Présentation — 文件 — दस्तावेज़.txt", New String("X"c, 120) & ".txt"}
                            For Each state In {UploadFileState.Waiting, UploadFileState.Transferring, UploadFileState.Finalizing, UploadFileState.Completed, UploadFileState.Failed, UploadFileState.Cancelled}
                                progress.Report(New UploadBatchSnapshot(files, {UploadFileState.Completed, state}, {New DmsTransferProgress(Long.MaxValue, Long.MaxValue, DmsTransferPhase.Completed), New DmsTransferProgress(Long.MaxValue \ 2, Long.MaxValue, DmsTransferPhase.Transferring)}, 1))
                                progress.PerformLayout()
                                LocalizedLayoutAcceptanceTest.Inspect(progress, failures, label & "/" & state.ToString())
                            Next
                        End If
                        Dim output = Environment.GetEnvironmentVariable("DMS_LOCALIZATION_PREVIEW_DIR")
                        If Not String.IsNullOrEmpty(output) Then
                            System.IO.Directory.CreateDirectory(output)
                            Using bitmap As New Bitmap(form.Width, form.Height)
                                form.DrawToBitmap(bitmap, New Rectangle(Point.Empty, bitmap.Size))
                                bitmap.Save(System.IO.Path.Combine(output, $"{cultureName}-{scenario}-{monitor.Scale}-{startOnRight}-{stepIndex}.png"), Imaging.ImageFormat.Png)
                            End Using
                        End If
                    Next
                    form.Hide()
                End Using
            Next
            Assert.That(failures, [Is].Empty, String.Join(Environment.NewLine, failures))
        Finally
            CultureInfo.CurrentUICulture = previousCulture
        End Try
    End Sub

    Friend Shared Function ReadTimingConfiguration() As KeyValuePair(Of String, Integer)
        'Framework may activate its application DPI configuration lazily at the first
        'window. Read physical rectangles without virtualization, then restore the
        'caller's context before the notice or any test window is created.
        Dim previous = SetThreadDpiAwarenessContext(New IntPtr(-4))
        If previous = IntPtr.Zero Then Throw New System.ComponentModel.Win32Exception()
        Try
            Dim attached = ReadMonitors()
            Dim primary = attached.Single(Function(m) m.Primary)
            Dim right = attached.Where(Function(m) m.Bounds.Left >= primary.Bounds.Right AndAlso m.Scale <> primary.Scale).OrderBy(Function(m) m.Bounds.Left).FirstOrDefault()
            Dim selected = If(right Is Nothing, {primary}, {primary, right})
            Dim scope = String.Join(";", selected.Select(Function(m) $"{m.Bounds.Width}x{m.Bounds.Height}@{m.Scale}"))
            Return New KeyValuePair(Of String, Integer)(scope, selected.Max(Function(m) m.Scale))
        Finally
            SetThreadDpiAwarenessContext(previous)
        End Try
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function SetThreadDpiAwarenessContext(context As IntPtr) As IntPtr
    End Function

    Friend Shared Function ReadSelectedScenarios() As Integer()
        Dim value = Environment.GetEnvironmentVariable("DMS_PHYSICAL_DPI_SCENARIOS")
        If String.IsNullOrWhiteSpace(value) Then Return Enumerable.Range(1, 14).ToArray()
        Dim result As New List(Of Integer)()
        For Each item In value.Split(","c)
            Dim scenario As Integer
            If Not Integer.TryParse(item, scenario) OrElse scenario < 1 OrElse scenario > 14 Then Throw New ArgumentException("DMS_PHYSICAL_DPI_SCENARIOS requires scenario numbers from 1 to 14.")
            If Not result.Contains(scenario) Then result.Add(scenario)
        Next
        Return result.OrderBy(Function(scenario) scenario).ToArray()
    End Function

    Private Shared Function ReadMonitors() As List(Of MonitorDescription)
        Dim result As New List(Of MonitorDescription)
        Dim callback As MonitorCallback =
            Function(handle As IntPtr, dc As IntPtr, ByRef rectangle As NativeRectangle, data As IntPtr)
                Dim info As New MonitorInfo With {.Size = Marshal.SizeOf(Of MonitorInfo)()}
                If Not GetMonitorInfo(handle, info) Then Throw New System.ComponentModel.Win32Exception()
                Dim scale As Integer
                Marshal.ThrowExceptionForHR(GetScaleFactorForMonitor(handle, scale))
                result.Add(New MonitorDescription With {.Name = info.Device, .Bounds = info.Monitor.ToRectangle(), .WorkArea = info.Work.ToRectangle(), .Scale = scale, .Primary = (info.Flags And 1) <> 0})
                Return True
            End Function
        If Not EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero) Then Throw New System.ComponentModel.Win32Exception()
        Return result.OrderByDescending(Function(m) m.Primary).ThenBy(Function(m) m.Bounds.Left).ToList()
    End Function

    Private Class MonitorDescription
        Friend Name As String
        Friend Bounds As Rectangle
        Friend WorkArea As Rectangle
        Friend Scale As Integer
        Friend Primary As Boolean
    End Class

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativeRectangle
        Friend Left As Integer
        Friend Top As Integer
        Friend Right As Integer
        Friend Bottom As Integer
        Friend Function ToRectangle() As Rectangle
            Return Rectangle.FromLTRB(Left, Top, Right, Bottom)
        End Function
    End Structure

    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
    Private Structure MonitorInfo
        Friend Size As Integer
        Friend Monitor As NativeRectangle
        Friend Work As NativeRectangle
        Friend Flags As Integer
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=32)>
        Friend Device As String
    End Structure

    Private Delegate Function MonitorCallback(handle As IntPtr, dc As IntPtr, ByRef rectangle As NativeRectangle, data As IntPtr) As Boolean

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function EnumDisplayMonitors(dc As IntPtr, clip As IntPtr, callback As MonitorCallback, data As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll", EntryPoint:="GetMonitorInfoW", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Private Shared Function GetMonitorInfo(handle As IntPtr, ByRef info As MonitorInfo) As Boolean
    End Function

    <DllImport("shcore.dll")>
    Private Shared Function GetScaleFactorForMonitor(handle As IntPtr, ByRef scale As Integer) As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetDpiForWindow(handle As IntPtr) As UInteger
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function GetWindowRect(handle As IntPtr, ByRef bounds As NativeRectangle) As Boolean
    End Function
End Class
