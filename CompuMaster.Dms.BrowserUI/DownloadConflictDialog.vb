Imports System.Drawing
Imports System.Windows.Forms

Friend Enum DownloadConflictAction
    Cancel
    Replace
    Skip
End Enum

Friend Structure DownloadConflictDecision
    Friend Choice As DownloadConflictAction
    Friend ApplyToAll As Boolean
End Structure

Friend NotInheritable Class DownloadConflictPolicy
    Private ReadOnly Prompt As Func(Of String, DownloadConflictDecision)
    Private All As DownloadConflictAction?
    Friend Sub New(prompt As Func(Of String, DownloadConflictDecision))
        Me.Prompt = prompt
    End Sub
    Friend Function Resolve(path As String) As DownloadConflictAction
        If All.HasValue Then Return All.Value
        Dim decision = Prompt(path)
        If decision.ApplyToAll AndAlso decision.Choice <> DownloadConflictAction.Cancel Then All = decision.Choice
        Return decision.Choice
    End Function
End Class

Friend NotInheritable Class DownloadConflictDialog
    Inherits Form
    Friend ReadOnly ApplyToAll As New CheckBox With {.AutoSize = True, .Text = UiStrings.GetText("DownloadApplyToAll")}
    Friend Property Choice As DownloadConflictAction = DownloadConflictAction.Cancel

    Friend Sub New(path As String, windowIcon As Icon)
        Me.Icon = windowIcon
        Me.Text = UiStrings.GetText("DownloadConflictTitle")
        Me.StartPosition = FormStartPosition.CenterParent
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.ShowInTaskbar = False
        Me.AutoScaleMode = AutoScaleMode.Font
        Me.AutoSize = True
        Me.AutoSizeMode = AutoSizeMode.GrowAndShrink
        Me.Padding = New Padding(12)
        Dim message As New Label With {.AutoSize = True, .Text = UiStrings.Format("OverwriteFileQuestion", path), .MaximumSize = New Size(600, 0), .Margin = New Padding(3, 3, 3, 12)}
        Dim buttons As New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .WrapContents = True, .Margin = New Padding(0, 12, 0, 0)}
        For Each action In New DownloadConflictAction() {DownloadConflictAction.Replace, DownloadConflictAction.Skip, DownloadConflictAction.Cancel}
            Dim current = action
            Dim key = If(action = DownloadConflictAction.Replace, "DownloadReplace", If(action = DownloadConflictAction.Skip, "DownloadSkip", "ActionCancel"))
            Dim button As New Button With {.AutoSize = True, .Text = UiStrings.GetText(key), .Padding = New Padding(10, 3, 10, 3), .DialogResult = If(action = DownloadConflictAction.Cancel, DialogResult.Cancel, DialogResult.OK)}
            AddHandler button.Click, Sub(sender, e) Me.Choice = current
            buttons.Controls.Add(button)
            If action = DownloadConflictAction.Cancel Then Me.CancelButton = button
        Next
        Dim layout As New TableLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .ColumnCount = 1, .RowCount = 3, .Dock = DockStyle.Fill}
        layout.Controls.Add(message, 0, 0)
        layout.Controls.Add(ApplyToAll, 0, 1)
        layout.Controls.Add(buttons, 0, 2)
        Me.Controls.Add(layout)
        LocalizedLayout.Bind(Me, Sub()
                                    message.MaximumSize = New Size(CInt(Math.Min(600 * Me.DeviceDpi / 96.0, Screen.FromControl(Me).WorkingArea.Width * 0.8)), 0)
                                    buttons.MaximumSize = New Size(message.MaximumSize.Width, 0)
                                    LocalizedLayout.FitButtons(Me)
                                End Sub)
    End Sub
End Class
