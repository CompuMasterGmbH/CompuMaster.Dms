Option Strict On

Imports NUnit.Framework
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Windows.Forms

<TestFixture>
Public Class DevelopmentGuiTestSessionTest
    <TestCase("WKS08", Nothing, True), TestCase("wks08", "0", True)>
    <TestCase("CI-RUNNER", Nothing, False), TestCase("DEVELOPER-PC", "1", True)>
    Public Sub NoticeIsMandatoryOnWks08AndOptInElsewhere(machine As String, optIn As String, expected As Boolean)
        Assert.That(DevelopmentGuiTestSession.ShouldShowNotice(machine, optIn), [Is].EqualTo(expected))
    End Sub

    <TestCase(Nothing, 60), TestCase("invalid", 60), TestCase("0", 60)>
    <TestCase("-1", 60), TestCase("90000", 60), TestCase("75", 75)>
    Public Sub EstimatesAcceptOnlyPositiveBoundedSeconds(value As String, expected As Integer)
        Assert.That(DevelopmentGuiTestSession.ReadEstimate(value), [Is].EqualTo(expected))
    End Sub

    ''' <summary>Verifies countdown, ETA, foreground preservation, and cleanup on native Windows.</summary>
    <Test, Apartment(ApartmentState.STA), NonParallelizable>
    Public Sub NativeNoticeDoesNotActivateAndClosesAfterCountdownAndEta()
        Using ready As New ManualResetEventSlim(), stopRequested As New ManualResetEventSlim(),
              window As New DevelopmentGuiTestSession.GuiTestNotice(60, ready, stopRequested)
            Dim foreground = GetForegroundWindow()
            window.Show()
            Application.DoEvents()
            Assert.That(window.TopMost, [Is].True)
            Assert.That(window.Controls(0).Text, Does.Contain("3 Sekunden"))
            Assert.That(GetForegroundWindow(), [Is].EqualTo(foreground), "Showing the notice must preserve the current foreground window.")
            Dim deadline = DateTime.UtcNow.AddSeconds(5)
            While Not ready.IsSet AndAlso DateTime.UtcNow < deadline
                Thread.Sleep(20)
                Application.DoEvents()
            End While
            Assert.That(ready.IsSet, [Is].True)
            Assert.That(window.Controls(0).Text, Does.Contain("ETA"))
            Assert.That(GetForegroundWindow(), [Is].EqualTo(foreground), "Countdown and ETA updates must not activate the notice.")
            stopRequested.Set()
            While Not window.IsDisposed AndAlso DateTime.UtcNow < deadline
                Thread.Sleep(20)
                Application.DoEvents()
            End While
            Assert.That(window.IsDisposed, [Is].True)
        End Using
    End Sub

    <DllImport("user32.dll")>
    Private Shared Function GetForegroundWindow() As IntPtr
    End Function
End Class
