Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Partial Public Class DmsBrowser
    Private Async Function UploadWithDialogAsync(remoteFolder As String, files As String()) As Task(Of Boolean)
        Using dialog As New UploadProgressDialog(files, False, Not Me.DmsProvider.SupportsNonUniqueRemoteItems, Me.Icon)
            Dim previous As UploadBatchSnapshot = Nothing
            Dim retryFailed As Boolean = True
            Do
                dialog.PrepareAttempt()
                dialog.Show(Me)
                Dim failure As Exception = Nothing
                Try
                    Await UploadBatchWithProgressAsync(Me.DmsProvider, remoteFolder, files, dialog, dialog.CancellationToken, previous, retryFailed)
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
                dialog.SetFailure(failure)
                dialog.Finish()
                If failure IsNot Nothing OrElse dialog.LatestSnapshot.States.Any(Function(state) state = UploadFileState.Failed OrElse state = UploadFileState.Cancelled) Then
                    Dim action = dialog.ShowDialog(Me)
                    If action = DialogResult.Retry OrElse action = DialogResult.Ignore Then
                        previous = dialog.LatestSnapshot
                        retryFailed = action = DialogResult.Retry
                        Continue Do
                    End If
                    'The transfer dialog has already displayed the failure and its diagnostics.
                    Return False
                End If
                Exit Do
            Loop
        End Using
        Return True
    End Function

    Friend Shared Async Function UploadBatchWithProgressAsync(provider As BaseDmsProvider, remoteFolder As String, files As String(), observer As IProgress(Of UploadBatchSnapshot), cancellationToken As CancellationToken, Optional previous As UploadBatchSnapshot = Nothing, Optional retryFailed As Boolean = True) As Task
        Dim states = If(previous Is Nothing, Enumerable.Repeat(UploadFileState.Waiting, files.Length).ToArray(), CType(previous.States.Clone(), UploadFileState()))
        Dim counters = If(previous Is Nothing, New DmsTransferProgress(files.Length - 1) {}, CType(previous.Counters.Clone(), DmsTransferProgress()))
        Dim errors = If(previous Is Nothing, New Exception(files.Length - 1) {}, CType(previous.Errors.Clone(), Exception()))
        For index As Integer = 0 To files.Length - 1
            Dim current = index
            If states(current) = UploadFileState.Completed OrElse (Not retryFailed AndAlso states(current) <> UploadFileState.Waiting) Then Continue For
            If cancellationToken.IsCancellationRequested Then
                observer.Report(New UploadBatchSnapshot(files, states, counters, current, errors))
                cancellationToken.ThrowIfCancellationRequested()
            End If
            states(current) = UploadFileState.Transferring
            counters(current) = Nothing
            errors(current) = Nothing
            observer.Report(New UploadBatchSnapshot(files, states, counters, current, errors))
            Dim active As Boolean = True
            Dim progress As New UploadProgressRecorder(Sub(value)
                                                                     If Not active Then Return
                                                                     counters(current) = value
                                                                     'Only task success can mark a file complete.
                                                                     states(current) = If(value.Phase = DmsTransferPhase.Finalizing OrElse value.Phase = DmsTransferPhase.Completed, UploadFileState.Finalizing, UploadFileState.Transferring)
                                                                     observer.Report(New UploadBatchSnapshot(files, states, counters, current, errors))
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
                errors(current) = ex
                observer.Report(New UploadBatchSnapshot(files, states, counters, current, errors))
                Throw
            Catch ex As Exception
                active = False
                counters(current) = progress.Latest
                states(current) = UploadFileState.Failed
                errors(current) = ex
                observer.Report(New UploadBatchSnapshot(files, states, counters, current, errors))
                Throw
            End Try
            observer.Report(New UploadBatchSnapshot(files, states, counters, current, errors))
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
    Friend Sub New(files As String(), states As UploadFileState(), counters As DmsTransferProgress(), current As Integer, Optional errors As Exception() = Nothing)
        Me.Files = CType(files.Clone(), String())
        Me.States = CType(states.Clone(), UploadFileState())
        Me.Counters = CType(counters.Clone(), DmsTransferProgress())
        Me.Current = current
        Me.Errors = If(errors Is Nothing, New Exception(files.Length - 1) {}, CType(errors.Clone(), Exception()))
    End Sub
    Friend ReadOnly Files As String()
    Friend ReadOnly States As UploadFileState()
    Friend ReadOnly Counters As DmsTransferProgress()
    Friend ReadOnly Current As Integer
    Friend ReadOnly Errors As Exception()

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

    Private Cancellation As New CancellationTokenSource()
    Private ReadOnly FileLabel As New Label With {.AutoSize = True}
    Private ReadOnly ByteLabel As New Label With {.AutoSize = True}
    Private Display As New TransferDisplay()
    Private ReadOnly RefreshTimer As New System.Windows.Forms.Timer With {.Interval = 250}
    Private LastSnapshot As UploadBatchSnapshot
    Private ReadOnly IsDownload As Boolean
    Private ReadOnly MayRetry As Boolean
    Private ReadOnly DiagnosticTip As New ToolTip With {.ShowAlways = True, .AutoPopDelay = 30000}
    Private ReadOnly RetryFiles As New Button With {.AutoSize = True, .Text = UiStrings.GetText("UploadRetryFailed"), .Visible = False, .Name = "RetryFailed"}
    Private ReadOnly ContinueFiles As New Button With {.AutoSize = True, .Text = UiStrings.GetText("UploadContinueRemaining"), .Visible = False, .Name = "ContinueRemaining"}
    Private ReadOnly ButtonRow As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill, .WrapContents = True}
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

    Friend Sub New(files As String(), Optional download As Boolean = False, Optional allowRetry As Boolean = True, Optional formIcon As Drawing.Icon = Nothing)
        AutoScaleMode = AutoScaleMode.Font
        If formIcon IsNot Nothing Then Icon = formIcon
        IsDownload = download
        MayRetry = allowRetry
        StatusLabel.Name = "TransferStatus"
        AddHandler RetryFiles.Click, Sub() DialogResult = DialogResult.Retry
        AddHandler ContinueFiles.Click, Sub() DialogResult = DialogResult.Ignore
        DiagnosticTip.SetToolTip(RetryFiles, UiStrings.GetText(If(allowRetry, "UploadRetryHint", "UploadRetryUnsupported")))
        AddHandler FilesList.MouseMove, Sub(sender, e)
                                           Dim index = FilesList.IndexFromPoint(e.Location)
                                           Dim errorItem = If(LastSnapshot IsNot Nothing AndAlso index >= 0 AndAlso index < LastSnapshot.Errors.Length, LastSnapshot.Errors(index), Nothing)
                                           DiagnosticTip.SetToolTip(FilesList, If(errorItem?.ToString(), String.Empty))
                                       End Sub
        Text = UiStrings.GetText(If(download, "DownloadTitle", "UploadTitle"))
        If download Then PartialWarning.Text = UiStrings.GetText("DownloadPartialWarning")
        ByteLabel.Name = "TransferDetails"
        AddHandler RefreshTimer.Tick, Sub()
                                          If LastSnapshot Is Nothing OrElse Finished Then Return
                                          UpdateTransferDetails(LastSnapshot)
                                      End Sub
        RefreshTimer.Start()
        StartPosition = FormStartPosition.CenterParent
        MinimumSize = New Drawing.Size(650, 400)
        Size = New Drawing.Size(750, 440)
        MinimizeBox = False
        MaximizeBox = False
        Dim layout = ProgressLayout
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        ButtonRow.Controls.AddRange({CancelUpload, RetryFiles, ContinueFiles})
        For Each control As Control In New Control() {FileLabel, ByteLabel, FileBar, StatusLabel, BatchLabel, BatchBar, FilesList, PartialWarning, ButtonRow}
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
        LastSnapshot = value
        FileLabel.Text = UiStrings.Format("UploadFilePosition", value.Current + 1, value.Files.Length, System.IO.Path.GetFileName(value.Files(value.Current)))
        Dim counter = value.Counters(value.Current)
        Dim percent As Integer? = Nothing
        If counter Is Nothing OrElse Not counter.BytesTransferred.HasValue Then
            percent = Nothing
        Else
            If counter.TotalBytes.HasValue AndAlso counter.TotalBytes.Value > 0 Then percent = CInt(Math.Min(100D, Math.Floor(CDec(counter.BytesTransferred.Value) * 100D / CDec(counter.TotalBytes.Value))))
        End If
        UpdateTransferDetails(value)
        SetProgress(FileBar, percent)
        SetProgress(BatchBar, value.OverallPercent)
        BatchLabel.Text = UiStrings.Format("UploadBatchCount", value.States.Count(Function(state) state = UploadFileState.Completed), value.Files.Length)
        If Not Cancellation.IsCancellationRequested Then StatusLabel.Text = ItemStateText(value, value.Current)
        DiagnosticTip.SetToolTip(StatusLabel, If(value.Errors(value.Current)?.ToString(), String.Empty))
        FilesList.Items.Clear()
        For index As Integer = 0 To value.Files.Length - 1
            FilesList.Items.Add(System.IO.Path.GetFileName(value.Files(index)) & " — " & ItemStateText(value, index))
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
            Dim requiredHeight = labelsHeight + FileBar.Height + BatchBar.Height + ButtonRow.GetPreferredSize(New Drawing.Size(ProgressLayout.ClientSize.Width - ProgressLayout.Padding.Horizontal, 0)).Height + Font.Height * 3 + 70
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

    Private Sub UpdateTransferDetails(value As UploadBatchSnapshot)
        ByteLabel.Text = Display.Describe(value.Current, value.Counters(value.Current), value.States(value.Current))
        ArrangeProgressControls()
    End Sub

    Private Function StateText(state As UploadFileState) As String
        If IsDownload AndAlso state = UploadFileState.Transferring Then Return UiStrings.GetText("DownloadTransferring")
        Return UiStrings.GetText("Upload" & state.ToString())
    End Function

    Private Function ItemStateText(value As UploadBatchSnapshot, index As Integer) As String
        Dim text = StateText(value.States(index))
        If value.Errors(index) IsNot Nothing AndAlso value.States(index) = UploadFileState.Failed Then text &= ": " & value.Errors(index).Message
        Return text
    End Function

    Friend ReadOnly Property LatestSnapshot As UploadBatchSnapshot
        Get
            Return LastSnapshot
        End Get
    End Property

    Friend Sub SetFailure(failure As Exception)
        If failure Is Nothing Then Return
        StatusLabel.Text = UiStrings.GetText(If(TypeOf failure Is OperationCanceledException, "UploadCancelled", "UploadFailed")) & ": " & failure.Message
        DiagnosticTip.SetToolTip(StatusLabel, failure.ToString())
        ArrangeProgressControls()
    End Sub

    Friend Sub PrepareAttempt()
        Cancellation.Dispose()
        Cancellation = New CancellationTokenSource()
        Display = New TransferDisplay()
        Finished = False
        DialogResult = DialogResult.None
        RetryFiles.Visible = False
        ContinueFiles.Visible = False
        CancelUpload.Enabled = True
        CancelUpload.Text = UiStrings.GetText("ActionCancel")
        RefreshTimer.Start()
    End Sub

    Private Shared Sub SetProgress(bar As ProgressBar, percent As Integer?)
        bar.Style = If(percent.HasValue, ProgressBarStyle.Continuous, ProgressBarStyle.Marquee)
        If percent.HasValue Then bar.Value = Math.Max(0, Math.Min(100, percent.Value))
    End Sub

    Friend Sub Finish()
        Finished = True
        RefreshTimer.Stop()
        CancelUpload.Enabled = True
        CancelUpload.Text = UiStrings.GetText("ActionClose")
        If Not IsDownload AndAlso LastSnapshot IsNot Nothing Then
            RetryFiles.Visible = LastSnapshot.States.Any(Function(state) state = UploadFileState.Failed OrElse state = UploadFileState.Cancelled)
            RetryFiles.Enabled = MayRetry
            ContinueFiles.Visible = LastSnapshot.States.Any(Function(state) state = UploadFileState.Waiting)
        End If
        ArrangeProgressControls()
        Hide()
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            RefreshTimer.Dispose()
            DiagnosticTip.Dispose()
            Cancellation.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
