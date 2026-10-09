Option Explicit On
Option Strict On

Imports System.Diagnostics
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Threading
Imports System.Windows.Forms
Imports NUnit.Framework

'Namespace-wide setup starts before any fixture opens a native window.
''' <summary>Provides the development workstation's GUI test countdown and ETA notice.</summary>
<SetUpFixture>
Public Class DevelopmentGuiTestSession
    Private NoticeThread As Thread
    Private ReadOnly StopRequested As New ManualResetEventSlim()
    Private ReadOnly RunClock As New Stopwatch()
    Private TimingKey As String
    Private TimingUnits As Integer
    Private PhysicalRunCompleted As Boolean
    Private PhysicalRunCompletionReported As Boolean

    ''' <summary>Warns interactive development users before GUI tests start.</summary>
    <OneTimeSetUp>
    Public Sub StartNotice()
        ConfigurePhysicalDpiHost()
        If Not ShouldShowNotice(Environment.MachineName, Environment.GetEnvironmentVariable("DMS_GUI_TEST_NOTICE")) Then Return
        TimingKey = Environment.GetEnvironmentVariable("DMS_GUI_TEST_RUN_KEY")
        If Not Integer.TryParse(Environment.GetEnvironmentVariable("DMS_GUI_TEST_WORK_UNITS"), TimingUnits) OrElse TimingUnits < 1 OrElse TimingUnits > 86400 Then TimingUnits = 1
        Dim estimate = EstimateDuration(Environment.GetEnvironmentVariable("DMS_GUI_TEST_ETA_SECONDS"),
            ReadEstimate(Environment.GetEnvironmentVariable("DMS_GUI_TEST_FALLBACK_SECONDS")), TimingUnits,
            ReadHistory(TimingKey))
        Console.WriteLine($"GUI notice: estimate={estimate}s; scope={TimingKey}; workUnits={TimingUnits}.")
        Using ready As New ManualResetEventSlim()
            Dim startupFailure As Exception = Nothing
            NoticeThread = New Thread(
                Sub()
                    Try
                        Using window As New GuiTestNotice(estimate, ready, StopRequested)
                            Application.Run(window)
                        End Using
                    Catch ex As Exception
                        startupFailure = ex
                        ready.Set()
                    End Try
                End Sub) With {.IsBackground = True, .Name = "GUI test notice"}
            NoticeThread.SetApartmentState(ApartmentState.STA)
            NoticeThread.Start()
            If Not ready.Wait(TimeSpan.FromSeconds(15)) Then
                StopNotice()
                Assert.Fail("The development GUI test notice could not start; GUI tests were not started.")
            End If
            If startupFailure IsNot Nothing Then
                StopNotice()
                Throw New InvalidOperationException("The development GUI test notice failed; GUI tests were not started.", startupFailure)
            End If
        End Using
        RunClock.Start()
    End Sub

    ''' <summary>Closes the notice after successful or failed GUI tests.</summary>
    <OneTimeTearDown>
    Public Sub StopNotice()
        Dim measured = RunClock.IsRunning
        If measured Then
            RunClock.Stop()
            Console.WriteLine($"GUI duration: actual={RunClock.Elapsed.TotalSeconds:F1}s; scope={TimingKey}; workUnits={TimingUnits}.")
        End If
        StopRequested.Set()
        If NoticeThread IsNot Nothing AndAlso Not NoticeThread.Join(TimeSpan.FromSeconds(5)) Then
            Throw New TimeoutException("The GUI test notice did not close after the test run.")
        End If
        If measured Then
            Dim completed = If(PhysicalRunCompletionReported, PhysicalRunCompleted,
                TestContext.CurrentContext.Result.Outcome.Status = NUnit.Framework.Interfaces.TestStatus.Passed)
            If completed AndAlso Not String.IsNullOrWhiteSpace(TimingKey) Then SaveHistory(TimingKey, RunClock.Elapsed.TotalSeconds / TimingUnits)
        End If
    End Sub

    Friend Sub CompletePhysicalRun(success As Boolean)
        PhysicalRunCompleted = success
        PhysicalRunCompletionReported = True
    End Sub

    Friend Shared Sub ConfigurePhysicalDpiHost()
        If Environment.GetEnvironmentVariable("DMS_PHYSICAL_DPI_TESTS") <> "1" Then Return
#If NET8_0_OR_GREATER Then
        If Application.HighDpiMode <> HighDpiMode.PerMonitorV2 Then
            Assert.That(Application.SetHighDpiMode(HighDpiMode.PerMonitorV2), [Is].True, "Configure the opt-in native DPI test host before any window is created.")
        End If
#End If
        Application.EnableVisualStyles()
    End Sub

    'Each scope identifies a framework and workload, including screenshot mode.
    'Measurements exclude compilation and the three-second countdown.
    Friend Shared Function EstimateDuration(explicitSeconds As String, fallbackSeconds As Integer, workUnits As Integer, secondsPerUnit As IEnumerable(Of Double)) As Integer
        Dim explicitValue As Integer
        If Integer.TryParse(explicitSeconds, explicitValue) AndAlso explicitValue > 0 AndAlso explicitValue <= 86400 Then Return explicitValue
        Dim recent = secondsPerUnit.Where(Function(value) value > 0 AndAlso Not Double.IsInfinity(value) AndAlso Not Double.IsNaN(value)).Reverse().Take(5).ToArray()
        If recent.Length = 0 Then Return fallbackSeconds
        Return CInt(Math.Min(86400, Math.Ceiling(recent.Max() * Math.Max(1, workUnits) * 1.25 + 5)))
    End Function

    Private Shared ReadOnly Property HistoryPath As String
        Get
            Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gui-test-durations.tsv")
        End Get
    End Property

    Private Shared Function ReadHistory(key As String) As Double()
        If String.IsNullOrWhiteSpace(key) Then Return {}
        Try
            If Not File.Exists(HistoryPath) Then Return {}
            Dim values As New List(Of Double)()
            For Each line In File.ReadLines(HistoryPath)
                Dim columns = line.Split(ControlChars.Tab)
                Dim seconds As Double
                If columns.Length = 3 AndAlso columns(1) = key AndAlso Double.TryParse(columns(2), NumberStyles.Float, CultureInfo.InvariantCulture, seconds) Then values.Add(seconds)
            Next
            Return values.ToArray()
        Catch ex As IOException
            Console.WriteLine("GUI timing history unavailable: " & ex.Message)
        Catch ex As UnauthorizedAccessException
            Console.WriteLine("GUI timing history unavailable: " & ex.Message)
        End Try
        Return {}
    End Function

    Private Shared Sub SaveHistory(key As String, secondsPerUnit As Double)
        If key.Contains(ControlChars.Tab) OrElse key.Contains(ControlChars.Cr) OrElse key.Contains(ControlChars.Lf) Then Return
        Try
            File.AppendAllText(HistoryPath, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) & ControlChars.Tab & key & ControlChars.Tab & secondsPerUnit.ToString("R", CultureInfo.InvariantCulture) & Environment.NewLine, New System.Text.UTF8Encoding(True))
        Catch ex As IOException
            Console.WriteLine("GUI timing history could not be saved: " & ex.Message)
        Catch ex As UnauthorizedAccessException
            Console.WriteLine("GUI timing history could not be saved: " & ex.Message)
        End Try
    End Sub

    Friend Shared Function ShouldShowNotice(machineName As String, optIn As String) As Boolean
        Return String.Equals(machineName, "WKS08", StringComparison.OrdinalIgnoreCase) OrElse optIn = "1"
    End Function

    Friend Shared Function ReadEstimate(value As String) As Integer
        Dim seconds As Integer
        Return If(Integer.TryParse(value, seconds) AndAlso seconds > 0 AndAlso seconds <= 86400, seconds, 60)
    End Function

    Friend Class GuiTestNotice
        Inherits Form

        Private ReadOnly Display As New Label With {.Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleCenter, .ForeColor = Color.White}
        Private ReadOnly RefreshTimer As New System.Windows.Forms.Timer With {.Interval = 100}
        Private ReadOnly Clock As New Stopwatch()
        Private ReadOnly Estimate As Integer
        Private ReadOnly Ready As ManualResetEventSlim
        Private ReadOnly StopRequested As ManualResetEventSlim
        Private Running As Boolean
        Private ExpectedFinish As DateTime
        Private DisplayFont As Font

        Friend Sub New(estimateSeconds As Integer, ready As ManualResetEventSlim, stopRequested As ManualResetEventSlim)
            Estimate = estimateSeconds
            Me.Ready = ready
            Me.StopRequested = stopRequested
            Text = "GUI-Tests auf dem Entwicklungsrechner"
            FormBorderStyle = FormBorderStyle.FixedToolWindow
            ControlBox = False
            ShowInTaskbar = False
            TopMost = True
            StartPosition = FormStartPosition.Manual
            AutoScaleMode = AutoScaleMode.None
            Font = SystemFonts.MessageBoxFont
            BackColor = Color.SkyBlue
            Dim screenBounds = Screen.PrimaryScreen.Bounds
            Size = New Size(screenBounds.Width \ 2, screenBounds.Height \ 2)
            Dim area = Screen.PrimaryScreen.WorkingArea
            Location = New Point(area.Right - Width - 18, area.Top + 18)
            Controls.Add(Display)
            Display.Text = "GUI-Tests starten in 3 Sekunden." & Environment.NewLine & "Testfenster können anschließend den Fokus übernehmen."
            FitDisplayFont()
            AddHandler ClientSizeChanged, Sub() FitDisplayFont()
            AddHandler Shown,
                Sub()
                    Clock.Start()
                    RefreshTimer.Start()
                End Sub
            AddHandler RefreshTimer.Tick, AddressOf UpdateNotice
        End Sub

        Private Sub FitDisplayFont()
            Display.Padding = New Padding(Math.Max(20, ClientSize.Width \ 30))
            Dim messages = {
                "GUI-Tests starten in 3 Sekunden." & Environment.NewLine & "Testfenster können anschließend den Fokus übernehmen.",
                "GUI-Tests laufen · ETA 23:59:59" & Environment.NewLine & "Geschätzte Restzeit: 86400 Sekunden.",
                "GUI-Tests laufen weiterhin." & Environment.NewLine & "Schätzung überschritten um 86400 Sekunden."}
            Dim lower As Single = 12
            Dim upper As Single = Math.Max(lower, (ClientSize.Height - Display.Padding.Vertical) \ 3)
            For iteration As Integer = 0 To 9
                Dim size = (lower + upper) / 2
                Using candidate As New Font(SystemFonts.MessageBoxFont.FontFamily, size, FontStyle.Bold, GraphicsUnit.Pixel),
                      measurement As New Label With {.Font = candidate, .Padding = Display.Padding}
                    Dim fits = messages.All(
                        Function(message)
                            measurement.Text = message
                            Dim measured = measurement.GetPreferredSize(New Size(ClientSize.Width, 0))
                            Return measured.Width <= ClientSize.Width AndAlso measured.Height <= ClientSize.Height - 8
                        End Function)
                    If fits Then
                        lower = size
                    Else
                        upper = size
                    End If
                End Using
            Next
            Dim previous = DisplayFont
            DisplayFont = New Font(SystemFonts.MessageBoxFont.FontFamily, lower, FontStyle.Bold, GraphicsUnit.Pixel)
            Display.Font = DisplayFont
            previous?.Dispose()
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        Protected Overrides ReadOnly Property CreateParams As CreateParams
            Get
                Dim parameters = MyBase.CreateParams
                parameters.ExStyle = parameters.ExStyle Or &H8000000 Or &H80 'WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW.
                Return parameters
            End Get
        End Property

        Private Sub UpdateNotice(sender As Object, e As EventArgs)
            If StopRequested.IsSet Then
                Close()
                Return
            End If
            If Not Running Then
                Dim remaining = Math.Max(0, 3 - Clock.Elapsed.TotalSeconds)
                If remaining > 0 Then
                    Display.Text = $"GUI-Tests starten in {Math.Ceiling(remaining):0} Sekunden." & Environment.NewLine & "Testfenster können anschließend den Fokus übernehmen."
                    Return
                End If
                Running = True
                Clock.Restart()
                ExpectedFinish = DateTime.Now.AddSeconds(Estimate)
                UpdateRunningText()
                Ready.Set()
            Else
                UpdateRunningText()
            End If
        End Sub

        Private Sub UpdateRunningText()
            Dim remaining = Estimate - Clock.Elapsed.TotalSeconds
            Display.Text = If(remaining > 0,
                $"GUI-Tests laufen · ETA {ExpectedFinish:HH:mm:ss}" & Environment.NewLine & $"Geschätzte Restzeit: {Math.Ceiling(remaining):0} Sekunden.",
                "GUI-Tests laufen weiterhin." & Environment.NewLine & $"Schätzung überschritten um {Math.Floor(-remaining):0} Sekunden.")
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then RefreshTimer.Dispose()
            MyBase.Dispose(disposing)
            If disposing Then DisplayFont?.Dispose()
        End Sub
    End Class
End Class
