Imports System
Imports System.Drawing
Imports System.Windows.Forms

Partial Public Class DmsBrowser
    Private Sub ArrangeLocalizedBrowserControls()
        LocalizedLayout.FitButtons(Me)
        LocalizedLayout.FitColumnHeaders(Me.ListViewDmsFiles)
        Dim leftControls As New List(Of Control) From {Me.ButtonCreateNewFolder, Me.ButtonShowFiles}
        If Me.InstanceButton IsNot Nothing AndAlso (Me.AllowedActions And FileOrFolderActions.AllowSwitchDmsInstance) <> 0 Then leftControls.Add(Me.InstanceButton)
        Dim rightControls As Control() = If(Me.DialogOperationModeInternal = DialogOperationModes.NoResults, New Control() {Me.ButtonClose}, New Control() {Me.ButtonOkay, Me.ButtonCancel})
        Dim rowHeight = leftControls.Concat(rightControls).Max(Function(control) control.Height)
        Dim minimumWidth = Math.Max(575, leftControls.Sum(Function(control) control.Width + 8) + rightControls.Sum(Function(control) control.Width + 8) + 28)
        Dim minimumHeight = Math.Max(263, Me.FlowLayoutPanel1.Height + rowHeight + 90)
        Me.MinimumSize = New Size(minimumWidth + Me.Width - Me.ClientSize.Width, minimumHeight + Me.Height - Me.ClientSize.Height)
        Dim top = Me.ClientSize.Height - rowHeight - 14
        Dim left As Integer = 14
        For Each control In leftControls
            control.Location = New Point(left, top)
            left = control.Right + 8
        Next
        Dim right = Me.ClientSize.Width - 14
        For Each control In rightControls.Reverse()
            control.Location = New Point(right - control.Width, top)
            right = control.Left - 8
        Next
        Me.SplitContainer.Height = Math.Max(40, top - Me.SplitContainer.Top - 8)
        If (Me.SplitContainer.Anchor And AnchorStyles.Right) <> 0 Then Me.SplitContainer.Width = Me.ClientSize.Width - Me.SplitContainer.Left - 14
        Me.UpdateFileToolbarLayout()
    End Sub

    Private Sub UpdateFileToolbarLayout()
        Me.FlowLayoutPanel1.PerformLayout()
        Me.SplitContainer.Panel2.PerformLayout()
    End Sub

    Friend Sub UpdateFileToolbarIconSize(deviceDpi As Integer)
        If deviceDpi <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(deviceDpi))

        Const LogicalDpi As Integer = 96
        Const FileToolbarIconLogicalSize As Integer = 16
        Dim scaledIconLength As Integer = CInt(Math.Round(FileToolbarIconLogicalSize * deviceDpi / CDbl(LogicalDpi), MidpointRounding.AwayFromZero))
        Dim scaledIconSize As New Size(scaledIconLength, scaledIconLength)
        Me.ToolStripFileActions.ImageScalingSize = scaledIconSize
        Me.ToolStripFileShareActions.ImageScalingSize = scaledIconSize
        Me.ToolStripFolderShareActions.ImageScalingSize = scaledIconSize
        Me.ToolStripProperties.ImageScalingSize = scaledIconSize
        Me.UpdateFileToolbarLayout()
    End Sub

    ''' <inheritdoc/>
    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Me.UpdateFileToolbarIconSize(Me.DeviceDpi)
    End Sub

End Class
