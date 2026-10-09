Option Explicit On
Option Strict On

Imports System.Windows.Forms

'Linked into each demo so DPI configuration remains the application's responsibility.
Friend Module DemoStartup
    <STAThread>
    Friend Sub Main()
#If NET8_0_OR_GREATER Then
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)
#End If
        Application.EnableVisualStyles()
        Application.Run(My.Forms.LoginForm)
    End Sub
End Module
