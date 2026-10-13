Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Partial Public Class DmsBrowser
    Private LastUploadSummary As String
    Private Async Function UploadWithDialogAsync(remoteFolder As String, files As String(), Optional relativePaths As String() = Nothing, Optional directories As String() = Nothing, Optional canWrite As Func(Of Boolean) = Nothing) As Task(Of Boolean)
        Dim provider = Me.DmsProvider
        Using dialog = CreateTransferDialog(files, False, Not provider.SupportsNonUniqueRemoteItems)
            Dim conflicts As New UploadConflictPolicy(Function(path)
                                                         Using question As New DownloadConflictDialog(path, Me.Icon, True)
                                                             question.ShowDialog(dialog)
                                                             If question.Choice = DownloadConflictAction.Cancel Then dialog.CancelOperation()
                                                             Return New DownloadConflictDecision With {.Choice = question.Choice, .ApplyToAll = question.ApplyToAll.Checked}
                                                         End Using
                                                     End Function)
            Dim previous As UploadBatchSnapshot = Nothing
            Dim retryFailed As Boolean = True
            Dim plannedTotals As Long?() = Nothing
            Do
                dialog.PrepareAttempt()
                dialog.ReportPlanning()
                dialog.Show(Me)
                Dim failure As Exception = Nothing
                Try
                    If plannedTotals Is Nothing Then
                        Dim planningClock = Diagnostics.Stopwatch.StartNew()
                        plannedTotals = Await Task.Run(Function() ReadLocalUploadSizes(files, dialog.CancellationToken))
                        TraceUploadTiming(String.Format(Globalization.CultureInfo.InvariantCulture, "DMS upload preparation: phase=localSizes; files={0}; seconds={1:F3}", files.Length, planningClock.Elapsed.TotalSeconds))
                    End If
                    If directories IsNot Nothing AndAlso directories.Length > 0 Then
                        Dim prepared As Integer
                        dialog.ReportPreparation(Nothing, prepared, directories.Length)
                        Await EnsureUploadDirectoriesAsync(provider, remoteFolder, directories, dialog.CancellationToken,
                            Sub(path)
                                dialog.ReportPreparation(path, prepared, directories.Length)
                                prepared += 1
                            End Sub, canWrite)
                        dialog.ReportPreparation(Nothing, directories.Length, directories.Length)
                    End If
                    Await UploadBatchWithProgressAsync(provider, remoteFolder, files, dialog, dialog.CancellationToken, previous, retryFailed, relativePaths, plannedTotals, conflicts, canWrite)
                Catch ex As Exception
                    failure = ex
                    If Not (TypeOf ex Is OperationCanceledException AndAlso dialog.CancellationToken.IsCancellationRequested) Then Me.HandleBrowserFailure(ex, showDialog:=False)
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
                    Me.HandleBrowserFailure(ex, DmsBrowserFailurePhase.Refresh, showDialog:=False)
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
            Dim skipped = dialog.LatestSnapshot.States.Count(Function(state) state = UploadFileState.Skipped)
            LastUploadSummary = If(skipped = 0, UiStrings.GetText("UploadSuccessful"), UiStrings.Format("UploadSummary", dialog.LatestSnapshot.States.Count(Function(state) state = UploadFileState.Completed), skipped))
        End Using
        Return True
    End Function

    Friend Shared Function ReadLocalUploadSizes(files As String(), token As CancellationToken) As Long?()
        Return files.Select(Function(path)
                                token.ThrowIfCancellationRequested()
                                Dim file As New System.IO.FileInfo(path)
                                Return If(file.Exists, CType(file.Length, Long?), Nothing)
                            End Function).ToArray()
    End Function

    Friend Shared Async Function UploadBatchWithProgressAsync(provider As BaseDmsProvider, remoteFolder As String, files As String(), observer As IProgress(Of UploadBatchSnapshot), cancellationToken As CancellationToken, Optional previous As UploadBatchSnapshot = Nothing, Optional retryFailed As Boolean = True, Optional relativePaths As String() = Nothing, Optional plannedTotals As Long?() = Nothing, Optional conflicts As UploadConflictPolicy = Nothing, Optional canWrite As Func(Of Boolean) = Nothing) As Task
        Dim states = If(previous Is Nothing, Enumerable.Repeat(UploadFileState.Waiting, files.Length).ToArray(), CType(previous.States.Clone(), UploadFileState()))
        Dim counters = If(previous Is Nothing, New DmsTransferProgress(files.Length - 1) {}, CType(previous.Counters.Clone(), DmsTransferProgress()))
        Dim errors = If(previous Is Nothing, New Exception(files.Length - 1) {}, CType(previous.Errors.Clone(), Exception()))
        For index As Integer = 0 To files.Length - 1
            Dim current = index
            If states(current) = UploadFileState.Completed OrElse states(current) = UploadFileState.Skipped OrElse (Not retryFailed AndAlso states(current) <> UploadFileState.Waiting) Then Continue For
            If cancellationToken.IsCancellationRequested Then
                observer.Report(New UploadBatchSnapshot(files, states, counters, current, errors, plannedTotals))
                cancellationToken.ThrowIfCancellationRequested()
            End If
            states(current) = UploadFileState.Starting
            counters(current) = New DmsTransferProgress(0, If(plannedTotals Is Nothing, Nothing, plannedTotals(current)), DmsTransferPhase.Transferring)
            errors(current) = Nothing
            observer.Report(New UploadBatchSnapshot(files, states, counters, current, errors, plannedTotals))
            Dim active As Boolean = True
            Dim providerCall As Diagnostics.Stopwatch = Nothing
            Dim progress As New UploadProgressRecorder(Sub(value)
                                                                     If Not active Then Return
                                                                     counters(current) = value
                                                                     'Only task success can mark a file complete.
                                                                     states(current) = If(value.Phase = DmsTransferPhase.Finalizing OrElse value.Phase = DmsTransferPhase.Completed, UploadFileState.Finalizing, UploadFileState.Transferring)
                                                                     observer.Report(New UploadBatchSnapshot(files, states, counters, current, errors, plannedTotals))
                                                                 End Sub)
            Try
                If canWrite IsNot Nothing AndAlso Not canWrite() Then Throw New UnauthorizedAccessException()
                Dim destination = UploadDestinationPath(provider, remoteFolder, If(relativePaths Is Nothing, System.IO.Path.GetFileName(files(current)), relativePaths(current)))
                If conflicts IsNot Nothing Then
                    Dim choice = Await conflicts.ResolveAsync(provider, destination, cancellationToken)
                    If choice = DownloadConflictAction.Cancel Then Throw New OperationCanceledException(cancellationToken)
                    If choice = DownloadConflictAction.Skip Then
                        active = False
                        states(current) = UploadFileState.Skipped
                        counters(current) = New DmsTransferProgress(0, 0, DmsTransferPhase.Transferring)
                        observer.Report(New UploadBatchSnapshot(files, states, counters, current, errors, plannedTotals))
                        Continue For
                    End If
                End If
                cancellationToken.ThrowIfCancellationRequested()
                If canWrite IsNot Nothing AndAlso Not canWrite() Then Throw New UnauthorizedAccessException()
                Dim replacement = conflicts?.ApprovedResource(destination)
                providerCall = Diagnostics.Stopwatch.StartNew()
                Try
                    If replacement Is Nothing Then
                        Await provider.UploadFileWithProgressAsync(destination, files(current), progress, cancellationToken)
                    Else
                        Await provider.ReplaceFileWithProgressAsync(replacement, files(current), progress, cancellationToken)
                    End If
                Finally
                    providerCall.Stop()
                End Try
                active = False
                counters(current) = progress.Latest
                states(current) = UploadFileState.Completed
            Catch ex As OperationCanceledException
                active = False
                counters(current) = progress.Latest
                states(current) = UploadFileState.Cancelled
                errors(current) = ex
                observer.Report(New UploadBatchSnapshot(files, states, counters, current, errors, plannedTotals))
                Throw
            Catch ex As Exception
                active = False
                counters(current) = progress.Latest
                states(current) = UploadFileState.Failed
                errors(current) = ex
                observer.Report(New UploadBatchSnapshot(files, states, counters, current, errors, plannedTotals))
                Throw
            Finally
                TraceUploadTiming(String.Format(Globalization.CultureInfo.InvariantCulture,
                    "DMS upload phases: provider={0}; fileIndex={1}; firstSourceBytesSeconds={2}; finalizingSeconds={3}; fileAttemptSeconds={4:F3}; providerCallSeconds={5}; outcome={6}",
                    provider.GetType().Name, current, progress.FirstSourceBytesSeconds, progress.FinalizingSeconds, progress.ElapsedSeconds,
                    If(providerCall Is Nothing, Nothing, CType(providerCall.Elapsed.TotalSeconds, Double?)), states(current)))
            End Try
            observer.Report(New UploadBatchSnapshot(files, states, counters, current, errors, plannedTotals))
        Next
    End Function

    Friend Shared Function UploadDestinationPath(provider As BaseDmsProvider, remoteFolder As String, relativePath As String) As String
        For Each segment In relativePath.Split("/"c)
            If segment.Length = 0 OrElse segment = "." OrElse segment = ".." Then Throw New ArgumentException(NameOf(relativePath))
            remoteFolder = provider.CombinePath(remoteFolder, segment)
        Next
        Return remoteFolder
    End Function

    Friend Shared Async Function EnsureUploadDirectoriesAsync(provider As BaseDmsProvider, remoteFolder As String, directories As String(), token As CancellationToken, Optional preparing As Action(Of String) = Nothing, Optional canWrite As Func(Of Boolean) = Nothing) As Task
        Dim clock = Diagnostics.Stopwatch.StartNew()
        Dim lookups As Integer
        Dim creations As Integer
        Try
            For Each relative In directories
                token.ThrowIfCancellationRequested()
                If canWrite IsNot Nothing AndAlso Not canWrite() Then Throw New UnauthorizedAccessException()
                Dim destination = UploadDestinationPath(provider, remoteFolder, relative)
                preparing?.Invoke(destination)
                lookups += 1
                Dim item = Await provider.ListRemoteItemAsync(destination, token)
                If item?.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(destination)
                If item Is Nothing Then
                    If canWrite IsNot Nothing AndAlso Not canWrite() Then Throw New UnauthorizedAccessException()
                    creations += 1
                    Await provider.CreateDirectoryAsync(destination, token)
                ElseIf item.ItemType = DmsResourceItem.ItemTypes.File Then
                    Throw New System.IO.IOException(UiStrings.Format("UploadSelectionCollision", destination))
                End If
            Next
        Finally
            TraceUploadTiming(String.Format(Globalization.CultureInfo.InvariantCulture,
                "DMS upload preparation: phase=remoteFolders; provider={0}; lookupCalls={1}; createCalls={2}; seconds={3:F3}", provider.GetType().Name, lookups, creations, clock.Elapsed.TotalSeconds))
        End Try
    End Function

    Private Shared Sub TraceUploadTiming(text As String)
        Try
            Diagnostics.Trace.WriteLine(text)
        Catch
            'Optional diagnostic listeners must not change transfer outcomes.
        End Try
    End Sub
End Class

Friend NotInheritable Class UploadProgressRecorder
    Implements IProgress(Of DmsTransferProgress)
    Private ReadOnly Dispatch As IProgress(Of DmsTransferProgress)
    Private Snapshot As DmsTransferProgress
    Private ReadOnly Clock As Func(Of Double)
    Private FirstSourceTime As Double = -1
    Private FinalizingTime As Double = -1
    Friend Sub New(callback As Action(Of DmsTransferProgress), Optional clock As Func(Of Double) = Nothing)
        Dispatch = New Progress(Of DmsTransferProgress)(callback)
        Dim timer = Diagnostics.Stopwatch.StartNew()
        Me.Clock = If(clock, Function() timer.Elapsed.TotalSeconds)
    End Sub
    Friend ReadOnly Property ElapsedSeconds As Double
        Get
            Return Clock()
        End Get
    End Property
    Friend ReadOnly Property FirstSourceBytesSeconds As Double?
        Get
            Dim value = Volatile.Read(FirstSourceTime)
            Return If(value < 0, Nothing, CType(value, Double?))
        End Get
    End Property
    Friend ReadOnly Property FinalizingSeconds As Double?
        Get
            Dim value = Volatile.Read(FinalizingTime)
            Return If(value < 0, Nothing, CType(value, Double?))
        End Get
    End Property
    Friend ReadOnly Property Latest As DmsTransferProgress
        Get
            Return Volatile.Read(Snapshot)
        End Get
    End Property
    Public Sub Report(value As DmsTransferProgress) Implements IProgress(Of DmsTransferProgress).Report
        Dim now = Clock()
        If value.BytesTransferred.GetValueOrDefault() > 0 Then Interlocked.CompareExchange(FirstSourceTime, now, -1)
        If value.Phase = DmsTransferPhase.Finalizing Then Interlocked.CompareExchange(FinalizingTime, now, -1)
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
    Starting
    Skipped
End Enum

Friend NotInheritable Class UploadBatchSnapshot
    Friend Sub New(files As String(), states As UploadFileState(), counters As DmsTransferProgress(), current As Integer, Optional errors As Exception() = Nothing, Optional plannedTotals As Long?() = Nothing)
        Me.Files = CType(files.Clone(), String())
        Me.States = CType(states.Clone(), UploadFileState())
        Me.Counters = CType(counters.Clone(), DmsTransferProgress())
        Me.Current = current
        Me.Errors = If(errors Is Nothing, New Exception(files.Length - 1) {}, CType(errors.Clone(), Exception()))
        If plannedTotals IsNot Nothing AndAlso plannedTotals.Length <> files.Length Then Throw New ArgumentException(NameOf(plannedTotals))
        Me.PlannedTotals = If(plannedTotals Is Nothing, Nothing, CType(plannedTotals.Clone(), Long?()))
    End Sub
    Friend ReadOnly Files As String()
    Friend ReadOnly States As UploadFileState()
    Friend ReadOnly Counters As DmsTransferProgress()
    Friend ReadOnly Current As Integer
    Friend ReadOnly Errors As Exception()
    Friend ReadOnly PlannedTotals As Long?()

    Friend ReadOnly Property OverallPercent As Integer?
        Get
            If Counters.Any(Function(value) value Is Nothing OrElse Not value.TotalBytes.HasValue OrElse Not value.BytesTransferred.HasValue) Then Return Nothing
            Dim total As Decimal = Counters.Sum(Function(value) CDec(value.TotalBytes.Value))
            Dim consumed As Decimal = Counters.Sum(Function(value) CDec(Math.Min(value.BytesTransferred.Value, value.TotalBytes.Value)))
            If total = 0 Then Return If(States.All(Function(state) state = UploadFileState.Completed OrElse state = UploadFileState.Skipped), 100, 0)
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
    Private ReadOnly ButtonRow As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .WrapContents = True, .Padding = New Padding(15, 6, 15, 10), .Name = "TransferActions"}
    Private ReadOnly StatusLabel As New Label With {.AutoSize = True}
    Private ReadOnly BatchLabel As New Label With {.AutoSize = True}
    Private ReadOnly FileBar As New ProgressBar With {.Dock = DockStyle.Top, .Style = ProgressBarStyle.Marquee}
    Private ReadOnly BatchBar As New ProgressBar With {.Dock = DockStyle.Top, .Style = ProgressBarStyle.Marquee}
    Private ReadOnly FilesList As New ListBox With {.Dock = DockStyle.Fill, .IntegralHeight = False}
    Private ReadOnly CancelUpload As New Button With {.AutoSize = True}
    Private ReadOnly PartialWarning As New Label With {.Text = UiStrings.GetText("UploadPartialWarning"), .AutoSize = True}
    Private ReadOnly ProgressLayout As New TableLayoutPanel With {.Padding = New Padding(15), .ColumnCount = 1, .RowCount = 8, .Name = "TransferLayout"}
    Private ReadOnly ContentViewport As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .Name = "TransferViewport"}
    Private Finished As Boolean
    Private HasFailure As Boolean
    Private ArrangingProgress As Boolean
    Private Preparing As Boolean
    Private RowWidths As Integer() = New Integer() {}
    Private RowSnapshot As UploadBatchSnapshot
    Private RowCulture As String
    Private RowFont As Drawing.Font
    Friend Property RowMeasurements As Integer

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
        For Each control As Control In New Control() {FileLabel, ByteLabel, FileBar, StatusLabel, BatchLabel, BatchBar, FilesList, PartialWarning}
            layout.Controls.Add(control)
        Next
        For row As Integer = 0 To 7
            layout.RowStyles.Add(New RowStyle(If(row = 6, SizeType.Absolute, SizeType.AutoSize), If(row = 6, Font.Height * 3, 0)))
        Next
        ContentViewport.Controls.Add(layout)
        Controls.Add(ContentViewport)
        Controls.Add(ButtonRow)
        ContentViewport.BringToFront()
        AddHandler ContentViewport.SizeChanged, Sub() ArrangeProgressControls()
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
        If value.Files.Length = 0 Then
            FileLabel.Text = UiStrings.GetText("UploadPlanningSelection")
            ByteLabel.Text = UiStrings.GetText("UploadBytesUnknown")
            Return
        End If
        If Preparing AndAlso value.States.All(Function(state) state = UploadFileState.Waiting) Then Return
        Preparing = False
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
        UpdateFileRows(value)
        ConstrainProgressLabels()
        ArrangeProgressControls()
    End Sub

    Private Sub UpdateFileRows(value As UploadBatchSnapshot)
        Dim culture = Globalization.CultureInfo.CurrentUICulture.Name
        If RowSnapshot IsNot Nothing AndAlso RowCulture = culture AndAlso RowFont Is FilesList.Font AndAlso
           RowSnapshot.Files.SequenceEqual(value.Files) AndAlso RowSnapshot.States.SequenceEqual(value.States) AndAlso RowSnapshot.Errors.SequenceEqual(value.Errors) Then
            RowSnapshot = value
            Return
        End If
        Dim remeasure = RowFont IsNot FilesList.Font
        Dim changed As Boolean
        FilesList.BeginUpdate()
        Try
            If FilesList.Items.Count <> value.Files.Length Then
                FilesList.Items.Clear()
                RowWidths = New Integer(value.Files.Length - 1) {}
                For index = 0 To value.Files.Length - 1
                    FilesList.Items.Add(String.Empty)
                Next
            End If
            For index = 0 To value.Files.Length - 1
                Dim caption = System.IO.Path.GetFileName(value.Files(index)) & " — " & ItemStateText(value, index)
                If remeasure OrElse Not String.Equals(TryCast(FilesList.Items(index), String), caption, StringComparison.Ordinal) Then
                    FilesList.Items(index) = caption
                    RowWidths(index) = TextRenderer.MeasureText(caption, FilesList.Font).Width
                    RowMeasurements += 1
                    changed = True
                End If
            Next
            If changed Then FilesList.HorizontalExtent = If(RowWidths.Length = 0, 0, RowWidths.Max())
            RowSnapshot = value
            RowCulture = culture
            RowFont = FilesList.Font
        Finally
            FilesList.EndUpdate()
        End Try
    End Sub

    Private Sub ArrangeProgressControls()
        If ArrangingProgress OrElse IsDisposed Then Return
        ArrangingProgress = True
        Try
            Dim available = Screen.FromControl(Me).WorkingArea.Size
            If MaximumSize.Width > 0 Then available.Width = Math.Min(available.Width, MaximumSize.Width)
            If MaximumSize.Height > 0 Then available.Height = Math.Min(available.Height, MaximumSize.Height)
            MinimumSize = New Drawing.Size(Math.Min(650, available.Width), Math.Min(400, available.Height))
            Dim actionWidth = Math.Max(1, ClientSize.Width - ButtonRow.Padding.Horizontal)
            For Each button In {CancelUpload, RetryFiles, ContinueFiles}
                Dim width = Math.Max(1, actionWidth - button.Margin.Horizontal)
                button.MaximumSize = New Drawing.Size(width, 0)
                button.Size = button.GetPreferredSize(New Drawing.Size(width, Integer.MaxValue))
            Next
            Dim rowWidth As Integer
            Dim rowHeight As Integer
            Dim actionHeight = ButtonRow.Padding.Vertical
            For Each button In {CancelUpload, RetryFiles, ContinueFiles}
                Dim participates = button Is CancelUpload OrElse (Finished AndAlso Not IsDownload AndAlso
                    If(button Is RetryFiles, HasFailure OrElse (LastSnapshot IsNot Nothing AndAlso LastSnapshot.States.Any(Function(state) state = UploadFileState.Failed OrElse state = UploadFileState.Cancelled)),
                       LastSnapshot IsNot Nothing AndAlso LastSnapshot.States.Any(Function(state) state = UploadFileState.Waiting)))
                If Not participates Then Continue For
                Dim width = button.Width + button.Margin.Horizontal
                If rowWidth > 0 AndAlso rowWidth + width > actionWidth Then
                    actionHeight += rowHeight
                    rowWidth = 0
                    rowHeight = 0
                End If
                rowWidth += width
                rowHeight = Math.Max(rowHeight, button.Height + button.Margin.Vertical)
            Next
            ButtonRow.Height = actionHeight + rowHeight
            PerformLayout()
            'Remeasure once after the vertical scrollbar changes the viewport width.
            For pass = 0 To 1
                ProgressLayout.Width = Math.Max(1, ContentViewport.ClientSize.Width)
                ConstrainProgressLabels()
                Dim labelsHeight = {FileLabel, ByteLabel, StatusLabel, BatchLabel, PartialWarning}.Sum(Function(label) label.GetPreferredSize(label.MaximumSize).Height + label.Margin.Vertical)
                Dim fixedHeight = labelsHeight + FileBar.Height + FileBar.Margin.Vertical + BatchBar.Height + BatchBar.Margin.Vertical + ProgressLayout.Padding.Vertical + FilesList.Margin.Vertical
                Dim reserve = Math.Max(FilesList.ItemHeight * 3, Font.Height * 3) + SystemInformation.HorizontalScrollBarHeight
                Dim listHeight = Math.Max(reserve, ContentViewport.ClientSize.Height - fixedHeight)
                ProgressLayout.RowStyles(6).Height = listHeight
                ProgressLayout.Height = fixedHeight + listHeight
                ProgressLayout.PerformLayout()
                ContentViewport.PerformLayout()
            Next
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
        If value.Files.Length = 0 Then Return
        Dim batchDetails = Display.DescribeBatch(value)
        If Preparing Then
            ByteLabel.Text = batchDetails
            Return
        End If
        ByteLabel.Text = Display.Describe(value.Current, value.Counters(value.Current), value.States(value.Current)) & Environment.NewLine & batchDetails
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
        HasFailure = True
        StatusLabel.Text = UiStrings.GetText(If(TypeOf failure Is OperationCanceledException, "UploadCancelled", "UploadFailed")) & ": " & failure.Message
        DiagnosticTip.SetToolTip(StatusLabel, failure.ToString())
        ArrangeProgressControls()
    End Sub

    Friend Sub CancelOperation()
        Cancellation.Cancel()
    End Sub

    Friend Sub PrepareAttempt()
        Cancellation.Dispose()
        Cancellation = New CancellationTokenSource()
        Display = New TransferDisplay()
        Finished = False
        HasFailure = False
        Preparing = False
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
            RetryFiles.Visible = HasFailure OrElse LastSnapshot.States.Any(Function(state) state = UploadFileState.Failed OrElse state = UploadFileState.Cancelled)
            RetryFiles.Enabled = MayRetry
            ContinueFiles.Visible = LastSnapshot.States.Any(Function(state) state = UploadFileState.Waiting)
        End If
        ArrangeProgressControls()
        Hide()
    End Sub

    Friend Sub ReportPreparation(path As String, Optional completed As Integer = 0, Optional total As Integer = 0)
        If Finished OrElse IsDisposed Then Return
        Preparing = True
        FileLabel.Text = If(total > 0, UiStrings.Format("UploadPreparingFolderCount", completed, total), UiStrings.GetText("UploadPreparingFolders"))
        StatusLabel.Text = If(path Is Nothing, UiStrings.GetText("UploadPreparingFolders"), UiStrings.Format("UploadPreparingFolder", path))
        SetProgress(FileBar, If(total > 0, CType(CInt(Math.Floor(completed * 100D / total)), Integer?), Nothing))
        If LastSnapshot IsNot Nothing Then UpdateTransferDetails(LastSnapshot)
        ArrangeProgressControls()
    End Sub

    Friend Sub ReportPlanning()
        If Finished OrElse IsDisposed Then Return
        Preparing = True
        FileLabel.Text = UiStrings.GetText("UploadPlanningSelection")
        StatusLabel.Text = FileLabel.Text
        ByteLabel.Text = UiStrings.GetText("UploadBytesUnknown")
        SetProgress(FileBar, Nothing)
        ArrangeProgressControls()
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
