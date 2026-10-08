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
        Dim frameWidth = Me.Width - Me.ClientSize.Width
        Dim frameHeight = Me.Height - Me.ClientSize.Height
        Dim availableWidth = Screen.FromControl(Me).WorkingArea.Width
        If Me.MaximumSize.Width > 0 Then availableWidth = Math.Min(availableWidth, Me.MaximumSize.Width)
        Dim minimumWidth = Math.Min(availableWidth, Math.Max(575, leftControls.Sum(Function(control) control.Width + 8) + rightControls.Sum(Function(control) control.Width + 8) + 28) + frameWidth)
        Me.MinimumSize = New Size(minimumWidth, Math.Max(263, Me.FlowLayoutPanel1.Height + rowHeight + 90) + frameHeight)
        'Windows can constrain a top-level form to the display's working area.
        'Wrap the bottom actions instead of assuming the requested minimum fits.
        Dim positions As New List(Of KeyValuePair(Of Control, Point))
        Dim row As Integer
        Dim left As Integer = 14
        For Each control In leftControls
            If left > 14 AndAlso left + control.Width > Me.ClientSize.Width - 14 Then
                row += 1
                left = 14
            End If
            positions.Add(New KeyValuePair(Of Control, Point)(control, New Point(left, row)))
            left += control.Width + 8
        Next
        Dim rightWidth = rightControls.Sum(Function(control) control.Width) + (rightControls.Length - 1) * 8
        If left > Me.ClientSize.Width - 14 - rightWidth Then row += 1
        Dim rowsHeight = (row + 1) * rowHeight + row * 8
        Me.MinimumSize = New Size(minimumWidth, Math.Max(263, Me.FlowLayoutPanel1.Height + rowsHeight + 90) + frameHeight)
        Dim top = Me.ClientSize.Height - rowsHeight - 14
        For Each position In positions
            position.Key.Location = New Point(position.Value.X, top + position.Value.Y * (rowHeight + 8))
        Next
        Dim right = Me.ClientSize.Width - 14
        For Each control In rightControls.Reverse()
            control.Location = New Point(right - control.Width, top + row * (rowHeight + 8))
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
