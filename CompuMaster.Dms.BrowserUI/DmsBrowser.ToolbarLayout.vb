Imports System
Imports System.Drawing
Imports System.Windows.Forms

Partial Public Class DmsBrowser

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
