Option Explicit On
Option Strict On

Imports System.Diagnostics
Imports System.Drawing
Imports System.Threading
Imports System.Windows.Forms
Imports NUnit.Framework

'Namespace-wide setup starts before any fixture opens a native window.
''' <summary>Provides the development workstation's GUI test countdown and ETA notice.</summary>
<SetUpFixture>
Public Class DevelopmentGuiTestSession
    Private NoticeThread As Thread
    Private ReadOnly StopRequested As New ManualResetEventSlim()

    ''' <summary>Warns interactive development users before GUI tests start.</summary>
    <OneTimeSetUp>
    Public Sub StartNotice()
        If Not ShouldShowNotice(Environment.MachineName, Environment.GetEnvironmentVariable("DMS_GUI_TEST_NOTICE")) Then Return
        Dim estimate = ReadEstimate(Environment.GetEnvironmentVariable("DMS_GUI_TEST_ETA_SECONDS"))
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
    End Sub

    ''' <summary>Closes the notice after successful or failed GUI tests.</summary>
    <OneTimeTearDown>
    Public Sub StopNotice()
        StopRequested.Set()
        If NoticeThread IsNot Nothing AndAlso Not NoticeThread.Join(TimeSpan.FromSeconds(5)) Then
            Throw New TimeoutException("The GUI test notice did not close after the test run.")
        End If
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

        Private ReadOnly Display As New Label With {.Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Padding = New Padding(14)}
        Private ReadOnly RefreshTimer As New System.Windows.Forms.Timer With {.Interval = 100}
        Private ReadOnly Clock As New Stopwatch()
        Private ReadOnly Estimate As Integer
        Private ReadOnly Ready As ManualResetEventSlim
        Private ReadOnly StopRequested As ManualResetEventSlim
        Private Running As Boolean
        Private ExpectedFinish As DateTime

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
            ClientSize = New Size(430, 100)
            Font = SystemFonts.MessageBoxFont
            BackColor = Color.LightYellow
            Dim area = Screen.PrimaryScreen.WorkingArea
            Location = New Point(area.Right - Width - 18, area.Top + 18)
            Controls.Add(Display)
            Display.Text = "GUI-Tests starten in 3 Sekunden." & Environment.NewLine & "Testfenster können anschließend den Fokus übernehmen."
            AddHandler Shown,
                Sub()
                    Clock.Start()
                    RefreshTimer.Start()
                End Sub
            AddHandler RefreshTimer.Tick, AddressOf UpdateNotice
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
        End Sub
    End Class
End Class
