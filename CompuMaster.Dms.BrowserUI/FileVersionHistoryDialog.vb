Imports System.Drawing
Imports System.IO
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Friend NotInheritable Class FileVersionHistoryDialog
    Inherits Form

    Private ReadOnly Provider As BaseDmsProvider
    Private ReadOnly RemoteFile As DmsResourceItem
    Private ReadOnly GetAllowedActions As Func(Of DmsBrowser.FileOrFolderActions)
    Private ReadOnly IsProviderCurrent As Func(Of Boolean)
    Private ReadOnly RequiredLocalParent As String
    Private ReadOnly DefaultDownloadFolder As String
    Private ReadOnly ReportFailure As Action(Of Exception)
    Private ReadOnly Operation As UiAsyncOperation
    Private ReadOnly ErrorTip As New ToolTip With {.AutoPopDelay = 30000}
    Private Versions As New List(Of DmsFileVersion)

    Friend ReadOnly VersionsList As New ListView With {.Dock = DockStyle.Fill, .View = View.Details, .FullRowSelect = True, .HideSelection = False, .MultiSelect = False, .ShowItemToolTips = True, .MinimumSize = New Size(0, 80)}
    Friend ReadOnly Status As New Label With {.AutoSize = True, .Dock = DockStyle.Fill, .Margin = New Padding(3, 8, 3, 8)}
    Friend ReadOnly DownloadButton As New Button With {.AutoSize = True, .Text = UiStrings.GetText("ActionDownload")}
    Friend ReadOnly RestoreButton As New Button With {.AutoSize = True, .Text = UiStrings.GetText("RestoreFileVersion")}
    Friend ReadOnly RemoveButton As New Button With {.AutoSize = True, .Text = UiStrings.GetText("RemoveFileVersion")}
    Friend ReadOnly RefreshButton As New Button With {.AutoSize = True, .Text = UiStrings.GetText("ActionRefreshFiles")}
    Friend ReadOnly CloseButton As New Button With {.AutoSize = True, .Text = UiStrings.GetText("ActionClose")}
    Friend Property Confirm As Func(Of String, Boolean)
    Friend Property CurrentFileChanged As Boolean

    Friend Sub New(provider As BaseDmsProvider, remoteFile As DmsResourceItem,
                   allowedActions As Func(Of DmsBrowser.FileOrFolderActions), isProviderCurrent As Func(Of Boolean),
                   windowIcon As Icon, title As String, requiredLocalParent As String, defaultDownloadFolder As String,
                   reportFailure As Action(Of Exception))
        Me.Provider = provider
        Me.RemoteFile = remoteFile
        Me.GetAllowedActions = allowedActions
        Me.IsProviderCurrent = isProviderCurrent
        Me.RequiredLocalParent = requiredLocalParent
        Me.DefaultDownloadFolder = defaultDownloadFolder
        Me.ReportFailure = reportFailure
        Me.Icon = windowIcon
        Me.Text = title
        Me.StartPosition = FormStartPosition.CenterParent
        Me.MinimizeBox = False
        Me.ShowInTaskbar = False
        Me.AutoScaleMode = AutoScaleMode.Font
        Me.ClientSize = New Size(850, 430)
        Me.Padding = New Padding(12)
        Operation = New UiAsyncOperation(Me)
        Confirm = Function(question) MessageBox.Show(Me, question, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) = DialogResult.Yes

        Dim name As New Label With {.AutoSize = True, .Dock = DockStyle.Fill, .Text = remoteFile.FullName, .UseMnemonic = False, .Margin = New Padding(3, 3, 3, 10)}
        For Each key In {"VersionIdentifier", "VersionTimestamp", "VersionSize", "VersionAuthor", "VersionLabel"}
            VersionsList.Columns.Add(UiStrings.GetText(key))
        Next
        Dim buttons As New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Dock = DockStyle.Fill, .WrapContents = True}
        For Each button In {DownloadButton, RestoreButton, RemoveButton, RefreshButton, CloseButton}
            button.Padding = New Padding(10, 3, 10, 3)
            buttons.Controls.Add(button)
        Next
        Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        layout.Controls.Add(name, 0, 0)
        layout.Controls.Add(VersionsList, 0, 1)
        layout.Controls.Add(Status, 0, 2)
        layout.Controls.Add(buttons, 0, 3)
        Me.Controls.Add(layout)
        LocalizedLayout.Bind(Me,
            Sub()
                Dim width = Math.Max(100, layout.ClientSize.Width - 16)
                name.MaximumSize = New Size(width, 0)
                Status.MaximumSize = New Size(width, 0)
                buttons.MaximumSize = New Size(width, 0)
                LocalizedLayout.FitButtons(Me)
                LocalizedLayout.FitColumnHeaders(VersionsList)
                Dim area = Screen.FromControl(Me).WorkingArea
                Dim minimumHeight = name.GetPreferredSize(New Size(width, 0)).Height + name.Margin.Vertical +
                                    Status.GetPreferredSize(New Size(width, 0)).Height + Status.Margin.Vertical +
                                    buttons.GetPreferredSize(New Size(width, 0)).Height + buttons.Margin.Vertical +
                                    Padding.Vertical + Height - ClientSize.Height + VersionsList.MinimumSize.Height
                MinimumSize = New Size(Math.Min(area.Width, CInt(600 * DeviceDpi / 96.0)), Math.Min(area.Height, Math.Max(CInt(300 * DeviceDpi / 96.0), minimumHeight)))
            End Sub)
        AddHandler VersionsList.SelectedIndexChanged, Sub(sender, e) UpdateAccess()
        AddHandler DownloadButton.Click, AddressOf ChooseDownload
        AddHandler RestoreButton.Click, Async Sub(sender, e) Await RunWithErrorAsync(Function() RestoreSelectedAsync())
        AddHandler RemoveButton.Click, Async Sub(sender, e) Await RunWithErrorAsync(Function() RemoveSelectedAsync())
        AddHandler RefreshButton.Click, Async Sub(sender, e) Await RunWithErrorAsync(Function() ReloadAsync())
        AddHandler CloseButton.Click, Sub(sender, e) Close()
        AddHandler Me.Shown, Async Sub(sender, e) Await RunWithErrorAsync(Function() ReloadAsync())
        Me.CancelButton = CloseButton
        UpdateAccess()
    End Sub

    Private Function Access() As FileVersionAccess
        If IsDisposed OrElse Disposing OrElse Not IsProviderCurrent() Then Return New FileVersionAccess(0, False, False, False, False)
        Return New FileVersionAccess(GetAllowedActions(), Provider.SupportsFileVersionHistory, Provider.SupportsFileVersionDownload,
                                     Provider.SupportsFileVersionRestore, Provider.SupportsFileVersionRemoval)
    End Function

    Friend Sub UpdateAccess()
        If IsDisposed OrElse Disposing Then Return
        Dim permitted = Access()
        If Not permitted.CanViewHistory AndAlso VersionsList.Items.Count > 0 Then
            Versions.Clear()
            VersionsList.Items.Clear()
            Status.Text = UiStrings.GetText("VersionActionUnavailable")
        End If
        Dim selected = SelectedVersion()
        DownloadButton.Visible = permitted.CanDownload
        RestoreButton.Visible = permitted.CanRestore
        RemoveButton.Visible = permitted.CanRemove
        Dim idle = Not Operation.IsRunning
        VersionsList.Enabled = idle AndAlso permitted.CanViewHistory
        DownloadButton.Enabled = idle AndAlso selected IsNot Nothing AndAlso permitted.CanDownload
        RestoreButton.Enabled = idle AndAlso selected IsNot Nothing AndAlso permitted.CanRestore
        RemoveButton.Enabled = idle AndAlso selected IsNot Nothing AndAlso permitted.CanRemove
        RefreshButton.Enabled = idle AndAlso permitted.CanViewHistory
        CloseButton.Enabled = idle
    End Sub

    Private Function SelectedVersion() As DmsFileVersion
        If VersionsList.SelectedItems.Count <> 1 Then Return Nothing
        Dim version = TryCast(VersionsList.SelectedItems(0).Tag, DmsFileVersion)
        Return If(version IsNot Nothing AndAlso Versions.Contains(version), version, Nothing)
    End Function

    Private Sub RequireAccess(allowed As Boolean)
        If IsDisposed OrElse Disposing OrElse Operation.IsRunning OrElse Not allowed Then
            Throw New InvalidOperationException(UiStrings.GetText("VersionActionUnavailable"))
        End If
    End Sub

    Friend Async Function ReloadAsync() As Task
        RequireAccess(Access().CanViewHistory)
        Try
            Await Operation.RunAsync(Async Function()
                                         Status.Text = UiStrings.GetText("VersionLoading")
                                         Await LoadVersionsAsync()
                                     End Function)
        Finally
            UpdateAccess()
        End Try
    End Function

    Private Async Function LoadVersionsAsync() As Task
        If IsDisposed OrElse Disposing Then Return
        If Not Access().CanViewHistory Then
            Versions.Clear()
            VersionsList.Items.Clear()
            Status.Text = UiStrings.GetText("VersionActionUnavailable")
            Return
        End If
        Dim result = Await Task.Run(Function() Provider.ListFileVersions(RemoteFile))
        If IsDisposed OrElse Disposing Then Return
        If Not Access().CanViewHistory Then
            Versions.Clear()
            VersionsList.Items.Clear()
            Status.Text = UiStrings.GetText("VersionActionUnavailable")
            Return
        End If
        If result Is Nothing OrElse result.Any(Function(version) version Is Nothing OrElse String.IsNullOrEmpty(version.VersionID)) OrElse
           result.Select(Function(version) version.VersionID).Distinct(StringComparer.Ordinal).Count() <> result.Count Then
            Throw New InvalidDataException(UiStrings.GetText("VersionInvalidHistory"))
        End If
        If result.Any(Function(version) If(String.IsNullOrEmpty(RemoteFile.ExtendedInfosFileID),
                                           Not String.Equals(RemoteFile.FullName, version.FileFullName, StringComparison.Ordinal),
                                           Not String.Equals(RemoteFile.ExtendedInfosFileID, version.FileID, StringComparison.Ordinal))) Then
            Throw New InvalidDataException(UiStrings.GetText("VersionInvalidHistory"))
        End If
        Dim selectedID = SelectedVersion()?.VersionID
        Versions = result
        VersionsList.BeginUpdate()
        Try
            VersionsList.Items.Clear()
            For Each version In Versions
                Dim item As New ListViewItem(version.VersionID) With {.Tag = version}
                Dim versionDate = If(version.VersionDateUtc, version.LastModificationUtc)
                item.SubItems.Add(If(versionDate.HasValue, versionDate.GetValueOrDefault().ToLocalTime().ToString(), UiStrings.GetText("VersionUnknown")))
                item.SubItems.Add(If(version.ContentLength.HasValue, TransferDisplay.FormatBytes(version.ContentLength.GetValueOrDefault()), UiStrings.GetText("VersionUnknown")))
                item.SubItems.Add(If(version.AuthorDisplayName, If(version.AuthorID, UiStrings.GetText("VersionUnknown"))))
                item.SubItems.Add(If(version.Label, ""))
                item.ToolTipText = version.VersionID & Environment.NewLine & If(version.AuthorDisplayName, "") & Environment.NewLine & If(version.Label, "")
                VersionsList.Items.Add(item)
                If String.Equals(version.VersionID, selectedID, StringComparison.Ordinal) Then item.Selected = True
            Next
            VersionsList.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent)
            LocalizedLayout.FitColumnHeaders(VersionsList)
        Finally
            VersionsList.EndUpdate()
        End Try
        Status.Text = If(Versions.Count = 0, UiStrings.GetText("FileVersionHistoryEmpty"), UiStrings.Format("VersionCount", Versions.Count))
        ErrorTip.SetToolTip(Status, Nothing)
    End Function

    Friend Async Function RestoreSelectedAsync() As Task
        Dim version = SelectedVersion()
        RequireAccess(version IsNot Nothing AndAlso Access().CanRestore)
        If Not Confirm(UiStrings.Format("ConfirmRestoreFileVersion", version.VersionID, RemoteFile.Name)) Then Return
        RequireAccess(version Is SelectedVersion() AndAlso Access().CanRestore)
        Try
            Await Operation.RunAsync(Async Function()
                                         Status.Text = UiStrings.GetText("VersionWorking")
                                         Await Task.Run(Sub() Provider.RestoreFileVersion(version))
                                         CurrentFileChanged = True
                                         Await LoadVersionsAsync()
                                     End Function)
        Finally
            UpdateAccess()
        End Try
    End Function

    Friend Async Function RemoveSelectedAsync() As Task
        Dim version = SelectedVersion()
        RequireAccess(version IsNot Nothing AndAlso Access().CanRemove)
        If Not Confirm(UiStrings.Format("ConfirmRemoveFileVersion", version.VersionID, RemoteFile.Name)) Then Return
        RequireAccess(version Is SelectedVersion() AndAlso Access().CanRemove)
        Try
            Await Operation.RunAsync(Async Function()
                                         Status.Text = UiStrings.GetText("VersionWorking")
                                         Await Task.Run(Sub() Provider.RemoveFileVersion(version))
                                         CurrentFileChanged = True
                                         Await LoadVersionsAsync()
                                     End Function)
        Finally
            UpdateAccess()
        End Try
    End Function

    Private Async Sub ChooseDownload(sender As Object, e As EventArgs)
        Await RunWithErrorAsync(Async Function()
                                   Dim version = SelectedVersion()
                                   RequireAccess(version IsNot Nothing AndAlso Access().CanDownload)
                                   Using picker As New SaveFileDialog With {.FileName = RemoteFile.Name, .InitialDirectory = DefaultDownloadFolder, .OverwritePrompt = False, .Filter = UiStrings.GetText("AllFilesFilter")}
                                       If picker.ShowDialog(Me) <> DialogResult.OK Then Return
                                       RequireAccess(version Is SelectedVersion() AndAlso Access().CanDownload)
                                       FileVersionDownloadDestination.Validate(picker.FileName, RequiredLocalParent)
                                       Dim replace = File.Exists(picker.FileName)
                                       If replace AndAlso Not Confirm(UiStrings.Format("OverwriteFileQuestion", picker.FileName)) Then Return
                                       RequireAccess(version Is SelectedVersion() AndAlso Access().CanDownload)
                                       Await DownloadSelectedAsync(picker.FileName, replace)
                                   End Using
                               End Function)
    End Sub

    Friend Async Function DownloadSelectedAsync(destination As String, replace As Boolean) As Task
        Dim version = SelectedVersion()
        RequireAccess(version IsNot Nothing AndAlso Access().CanDownload)
        FileVersionDownloadDestination.Validate(destination, RequiredLocalParent)
        Try
            Await Operation.RunAsync(Async Function()
                                         Status.Text = UiStrings.GetText("VersionWorking")
                                         Await Task.Run(Sub() FileVersionDownloadDestination.Save(destination, RequiredLocalParent, replace,
                                                                                                 Sub(stream) Provider.DownloadFileVersion(version, stream), version.LastModificationUtc))
                                         If Not IsDisposed AndAlso Not Disposing Then Status.Text = UiStrings.GetText("DownloadSuccessful")
                                     End Function)
        Finally
            UpdateAccess()
        End Try
    End Function

    Friend Async Function RunWithErrorAsync(work As Func(Of Task)) As Task
        Try
            Await work()
        Catch ex As Exception
            ReportFailure?.Invoke(ex)
            If Not IsDisposed AndAlso Not Disposing Then
                Dim message = ex.Message
                Dim cause = ex.GetBaseException().Message
                If Not String.Equals(message, cause, StringComparison.Ordinal) Then message &= " — " & cause
                Status.Text = UiStrings.Format("ErrorMessage", message)
                ErrorTip.SetToolTip(Status, ex.ToString())
            End If
        End Try
    End Function

    ''' <inheritdoc/>
    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then ErrorTip.Dispose()
        MyBase.Dispose(disposing)
    End Sub
End Class
