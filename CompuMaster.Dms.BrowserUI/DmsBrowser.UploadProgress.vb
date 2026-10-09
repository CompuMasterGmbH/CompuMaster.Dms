Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Partial Public Class DmsBrowser
    Private Async Function UploadWithDialogAsync(remoteFolder As String, files As String()) As Task
        Using dialog As New UploadProgressDialog(files)
            dialog.Show(Me)
            Dim failure As Exception = Nothing
            Try
                Await UploadBatchWithProgressAsync(Me.DmsProvider, remoteFolder, files, dialog, dialog.CancellationToken)
            Catch ex As Exception
                failure = ex
            End Try
            'Refresh also after partial writes, without losing the original transfer failure.
            Try
                Await Me.RefreshFilesListAfterTransferAsync()
            Catch ex As Exception
                If failure Is Nothing Then
                    failure = ex
                Else
                    failure.Data("DestinationRefreshFailure") = ex
                End If
            End Try
            dialog.Finish()
            If failure IsNot Nothing Then
                dialog.ShowDialog(Me)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw()
            End If
        End Using
    End Function

    Friend Shared Async Function UploadBatchWithProgressAsync(provider As BaseDmsProvider, remoteFolder As String, files As String(), observer As IProgress(Of UploadBatchSnapshot), cancellationToken As CancellationToken) As Task
        Dim states = Enumerable.Repeat(UploadFileState.Waiting, files.Length).ToArray()
        Dim counters(files.Length - 1) As DmsTransferProgress
        For index As Integer = 0 To files.Length - 1
            Dim current = index
            If cancellationToken.IsCancellationRequested Then
                observer.Report(New UploadBatchSnapshot(files, states, counters, current))
                cancellationToken.ThrowIfCancellationRequested()
            End If
            states(current) = UploadFileState.Transferring
            observer.Report(New UploadBatchSnapshot(files, states, counters, current))
            Dim active As Boolean = True
            Dim progress As New UploadProgressRecorder(Sub(value)
                                                                     If Not active Then Return
                                                                     counters(current) = value
                                                                     'Only task success can mark a file complete.
                                                                     states(current) = If(value.Phase = DmsTransferPhase.Finalizing OrElse value.Phase = DmsTransferPhase.Completed, UploadFileState.Finalizing, UploadFileState.Transferring)
                                                                     observer.Report(New UploadBatchSnapshot(files, states, counters, current))
                                                                 End Sub)
            Try
                Await provider.UploadFileWithProgressAsync(provider.CombinePath(remoteFolder, System.IO.Path.GetFileName(files(current))), files(current), progress, cancellationToken)
                active = False
                counters(current) = progress.Latest
                states(current) = UploadFileState.Completed
            Catch ex As OperationCanceledException
                active = False
                counters(current) = progress.Latest
                states(current) = UploadFileState.Cancelled
                observer.Report(New UploadBatchSnapshot(files, states, counters, current))
                Throw
            Catch
                active = False
                counters(current) = progress.Latest
                states(current) = UploadFileState.Failed
                observer.Report(New UploadBatchSnapshot(files, states, counters, current))
                Throw
            End Try
            observer.Report(New UploadBatchSnapshot(files, states, counters, current))
        Next
    End Function
End Class

Friend NotInheritable Class UploadProgressRecorder
    Implements IProgress(Of DmsTransferProgress)
    Private ReadOnly Dispatch As IProgress(Of DmsTransferProgress)
    Private Snapshot As DmsTransferProgress
    Friend Sub New(callback As Action(Of DmsTransferProgress))
        Dispatch = New Progress(Of DmsTransferProgress)(callback)
    End Sub
    Friend ReadOnly Property Latest As DmsTransferProgress
        Get
            Return Volatile.Read(Snapshot)
        End Get
    End Property
    Public Sub Report(value As DmsTransferProgress) Implements IProgress(Of DmsTransferProgress).Report
        Interlocked.Exchange(Snapshot, value)
        Dispatch.Report(value)
    End Sub
End Class

Friend Enum UploadFileState
    Waiting
    Transferring
    Finalizing
    Completed
    Failed
    Cancelled
End Enum

Friend NotInheritable Class UploadBatchSnapshot
    Friend Sub New(files As String(), states As UploadFileState(), counters As DmsTransferProgress(), current As Integer)
        Me.Files = CType(files.Clone(), String())
        Me.States = CType(states.Clone(), UploadFileState())
        Me.Counters = CType(counters.Clone(), DmsTransferProgress())
        Me.Current = current
    End Sub
    Friend ReadOnly Files As String()
    Friend ReadOnly States As UploadFileState()
    Friend ReadOnly Counters As DmsTransferProgress()
    Friend ReadOnly Current As Integer

    Friend ReadOnly Property OverallPercent As Integer?
        Get
            If Counters.Any(Function(value) value Is Nothing OrElse Not value.TotalBytes.HasValue OrElse Not value.BytesTransferred.HasValue) Then Return Nothing
            Dim total As Decimal = Counters.Sum(Function(value) CDec(value.TotalBytes.Value))
            Dim consumed As Decimal = Counters.Sum(Function(value) CDec(Math.Min(value.BytesTransferred.Value, value.TotalBytes.Value)))
            If total = 0 Then Return If(States.All(Function(state) state = UploadFileState.Completed), 100, 0)
            Return CInt(Math.Min(100D, Math.Floor(consumed * 100D / total)))
        End Get
    End Property
End Class

Friend NotInheritable Class UploadProgressDialog
    Inherits Form
    Implements IProgress(Of UploadBatchSnapshot)

    Private ReadOnly Cancellation As New CancellationTokenSource()
    Private ReadOnly FileLabel As New Label With {.AutoSize = True}
    Private ReadOnly ByteLabel As New Label With {.AutoSize = True}
    Private ReadOnly StatusLabel As New Label With {.AutoSize = True}
    Private ReadOnly BatchLabel As New Label With {.AutoSize = True}
    Private ReadOnly FileBar As New ProgressBar With {.Dock = DockStyle.Top, .Style = ProgressBarStyle.Marquee}
    Private ReadOnly BatchBar As New ProgressBar With {.Dock = DockStyle.Top, .Style = ProgressBarStyle.Marquee}
    Private ReadOnly FilesList As New ListBox With {.Dock = DockStyle.Fill, .IntegralHeight = False}
    Private ReadOnly CancelUpload As New Button With {.AutoSize = True}
    Private ReadOnly PartialWarning As New Label With {.Text = UiStrings.GetText("UploadPartialWarning"), .AutoSize = True}
    Private ReadOnly ProgressLayout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(15), .ColumnCount = 1, .RowCount = 9}
    Private Finished As Boolean
    Private ArrangingProgress As Boolean

    Friend Sub New(files As String())
        AutoScaleMode = AutoScaleMode.Font
        Text = UiStrings.GetText("UploadTitle")
        StartPosition = FormStartPosition.CenterParent
        MinimumSize = New Drawing.Size(650, 400)
        Size = New Drawing.Size(750, 440)
        MinimizeBox = False
        MaximizeBox = False
        Dim layout = ProgressLayout
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        For Each control As Control In New Control() {FileLabel, ByteLabel, FileBar, StatusLabel, BatchLabel, BatchBar, FilesList, PartialWarning, CancelUpload}
            layout.Controls.Add(control)
        Next
        For row As Integer = 0 To 8
            layout.RowStyles.Add(New RowStyle(If(row = 6, SizeType.Percent, SizeType.AutoSize), If(row = 6, 100, 0)))
        Next
        Controls.Add(layout)
        AddHandler layout.SizeChanged, Sub() ConstrainProgressLabels()
        ConstrainProgressLabels()
        LocalizedLayout.Bind(Me, AddressOf ArrangeProgressControls)
        FilesList.HorizontalScrollbar = True
        CancelUpload.Text = UiStrings.GetText("ActionCancel")
        AddHandler CancelUpload.Click, Sub()
                                          If Finished Then
                                              Close()
                                          Else
                                              RequestCancellation()
                                          End If
                                      End Sub
        AddHandler FormClosing, Sub(sender, e)
                                    If Not Finished Then
                                        e.Cancel = True
                                        RequestCancellation()
                                    End If
                                End Sub
        Dim states = Enumerable.Repeat(UploadFileState.Waiting, files.Length).ToArray()
        Dim counters(files.Length - 1) As DmsTransferProgress
        Report(New UploadBatchSnapshot(files, states, counters, 0))
    End Sub

    Friend ReadOnly Property CancellationToken As CancellationToken
        Get
            Return Cancellation.Token
        End Get
    End Property

    Private Sub RequestCancellation()
        Cancellation.Cancel()
        CancelUpload.Enabled = False
        StatusLabel.Text = UiStrings.GetText("UploadCancelling")
    End Sub

    Public Sub Report(value As UploadBatchSnapshot) Implements IProgress(Of UploadBatchSnapshot).Report
        If IsDisposed OrElse Finished Then Return
        FileLabel.Text = UiStrings.Format("UploadFilePosition", value.Current + 1, value.Files.Length, System.IO.Path.GetFileName(value.Files(value.Current)))
        Dim counter = value.Counters(value.Current)
        Dim percent As Integer? = Nothing
        If counter Is Nothing OrElse Not counter.BytesTransferred.HasValue Then
            ByteLabel.Text = UiStrings.GetText("UploadBytesUnknown")
        ElseIf Not counter.TotalBytes.HasValue Then
            ByteLabel.Text = UiStrings.Format("UploadSourceBytesUnknownTotal", counter.BytesTransferred.Value)
        Else
            ByteLabel.Text = UiStrings.Format("UploadSourceBytes", counter.BytesTransferred.Value, counter.TotalBytes.Value)
            If counter.TotalBytes.Value > 0 Then percent = CInt(Math.Min(100D, Math.Floor(CDec(counter.BytesTransferred.Value) * 100D / CDec(counter.TotalBytes.Value))))
        End If
        SetProgress(FileBar, percent)
        SetProgress(BatchBar, value.OverallPercent)
        BatchLabel.Text = UiStrings.Format("UploadBatchCount", value.States.Count(Function(state) state = UploadFileState.Completed), value.Files.Length)
        If Not Cancellation.IsCancellationRequested Then StatusLabel.Text = StateText(value.States(value.Current))
        FilesList.Items.Clear()
        For index As Integer = 0 To value.Files.Length - 1
            FilesList.Items.Add(System.IO.Path.GetFileName(value.Files(index)) & " — " & StateText(value.States(index)))
        Next
        FilesList.HorizontalExtent = FilesList.Items.Cast(Of String)().Max(Function(text) TextRenderer.MeasureText(text, FilesList.Font).Width)
        ConstrainProgressLabels()
        ArrangeProgressControls()
    End Sub

    Private Sub ArrangeProgressControls()
        If ArrangingProgress OrElse IsDisposed Then Return
        ArrangingProgress = True
        Try
            ConstrainProgressLabels()
            Dim labelsHeight = {FileLabel, ByteLabel, StatusLabel, BatchLabel, PartialWarning}.Sum(Function(label) label.GetPreferredSize(label.MaximumSize).Height + label.Margin.Vertical)
            Dim requiredHeight = labelsHeight + FileBar.Height + BatchBar.Height + CancelUpload.GetPreferredSize(Drawing.Size.Empty).Height + Font.Height * 3 + 70
            MinimumSize = New Drawing.Size(650, Math.Max(400, requiredHeight + Height - ClientSize.Height))
            ProgressLayout.PerformLayout()
        Finally
            ArrangingProgress = False
        End Try
    End Sub

    Private Sub ConstrainProgressLabels()
        Dim available = Math.Max(1, ProgressLayout.ClientSize.Width - ProgressLayout.Padding.Horizontal - FileLabel.Margin.Horizontal)
        For Each label In {FileLabel, ByteLabel, StatusLabel, BatchLabel, PartialWarning}
            label.MaximumSize = New Drawing.Size(available, 0)
        Next
    End Sub

    Private Shared Function StateText(state As UploadFileState) As String
        Return UiStrings.GetText("Upload" & state.ToString())
    End Function

    Private Shared Sub SetProgress(bar As ProgressBar, percent As Integer?)
        bar.Style = If(percent.HasValue, ProgressBarStyle.Continuous, ProgressBarStyle.Marquee)
        If percent.HasValue Then bar.Value = Math.Max(0, Math.Min(100, percent.Value))
    End Sub

    Friend Sub Finish()
        Finished = True
        CancelUpload.Enabled = True
        CancelUpload.Text = UiStrings.GetText("ActionClose")
        Hide()
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then Cancellation.Dispose()
        MyBase.Dispose(disposing)
    End Sub
End Class
