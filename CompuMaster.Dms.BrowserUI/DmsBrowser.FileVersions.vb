Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data

Partial Public Class DmsBrowser
    Friend ReadOnly VersionHistoryButton As New ToolStripButton With {.Name = "FileVersionHistory", .Text = UiStrings.GetText("FileVersionHistory"), .Available = False}
    Friend ReadOnly VersionHistoryContext As New ToolStripMenuItem With {.Name = "FileVersionHistoryContext", .Text = UiStrings.GetText("FileVersionHistory"), .Available = False}
    Private ActiveVersionHistory As FileVersionHistoryDialog

    Private Sub InitializeFileVersions()
        VersionHistoryButton.Image = ToolStripButtonPropertiesFile.Image
        ToolStripProperties.Items.Insert(0, VersionHistoryButton)
        ContextMenuStripFile.Items.Add(VersionHistoryContext)
        AddHandler VersionHistoryButton.Click, AddressOf ShowFileVersionHistory
        AddHandler VersionHistoryContext.Click, AddressOf ShowFileVersionHistory
        AddHandler ListViewDmsFiles.SelectedIndexChanged, Sub(sender, e) UpdateFileVersionAccess()
        AddHandler ListViewDmsFiles.EnabledChanged, Sub(sender, e) UpdateFileVersionAccess()
        AddHandler ContextMenuStripFile.Opening, Sub(sender, e) UpdateFileVersionAccess()
        UpdateFileVersionAccess()
    End Sub

    Friend Function CanViewSelectedFileVersions() As Boolean
        Dim provider = If(_DmsProviderOverride, DmsProviderInstance)
        If IsDisposed OrElse Disposing OrElse TransferRunning OrElse ResourceActionRunning OrElse DragDownloadRunning OrElse
           Not ListViewDmsFiles.Enabled OrElse BrowseMode <> BrowseModes.FoldersAndFiles OrElse provider Is Nothing OrElse
           Not provider.SupportsFileVersionHistory OrElse (AllowedActions And FileOrFolderActions.AllowViewFileVersionHistory) = 0 Then Return False
        Dim selected = CurrentSelectedFiles()
        Return selected.Count = 1 AndAlso selected(0) IsNot Nothing AndAlso selected(0).ItemType = DmsResourceItem.ItemTypes.File
    End Function

    Private Sub UpdateFileVersionAccess()
        If IsDisposed OrElse Disposing Then Return
        Dim provider = If(_DmsProviderOverride, DmsProviderInstance)
        Dim visible = provider IsNot Nothing AndAlso provider.SupportsFileVersionHistory AndAlso
                      (AllowedActions And FileOrFolderActions.AllowViewFileVersionHistory) <> 0 AndAlso BrowseMode = BrowseModes.FoldersAndFiles
        VersionHistoryButton.Available = visible
        VersionHistoryContext.Available = visible
        VersionHistoryButton.Enabled = CanViewSelectedFileVersions()
        VersionHistoryContext.Enabled = VersionHistoryButton.Enabled
        ActiveVersionHistory?.UpdateAccess()
    End Sub

    Friend Function CreateFileVersionDialog() As FileVersionHistoryDialog
        If Not CanViewSelectedFileVersions() Then Throw New InvalidOperationException(UiStrings.GetText("VersionActionUnavailable"))
        Dim selected = CurrentSelectedFiles()(0)
        Dim provider = If(_DmsProviderOverride, DmsProviderInstance)
        Dim fileID = selected.ExtendedInfosFileID
        Dim filePath = selected.FullName
        Dim title = TransferWindowTitle.WithTarget(UiStrings.GetText("FileVersionHistory") & " — " & selected.Name, provider, DmsProfile)
        Return New FileVersionHistoryDialog(provider, selected, Function() AllowedActions,
            Function() Not IsDisposed AndAlso Not Disposing AndAlso Not TransferRunning AndAlso Not ResourceActionRunning AndAlso Not DragDownloadRunning AndAlso
                       provider Is If(_DmsProviderOverride, DmsProviderInstance) AndAlso
                       String.Equals(fileID, selected.ExtendedInfosFileID, StringComparison.Ordinal) AndAlso
                       String.Equals(filePath, selected.FullName, StringComparison.Ordinal),
            Icon, title, LocalParentMustFolder, LocalDefaultFolderDownloads, Sub(ex) HandleBrowserFailure(ex, showDialog:=False))
    End Function

    Private Async Sub ShowFileVersionHistory(sender As Object, e As EventArgs)
        If Not CanViewSelectedFileVersions() Then Return
        Try
            Using dialog = CreateFileVersionDialog()
                ActiveVersionHistory = dialog
                dialog.ShowDialog(Me)
                If dialog.CurrentFileChanged AndAlso Not IsDisposed AndAlso Not Disposing Then
                    Await RunTransferAsync(Function() RefreshFilesListAsync(), DmsBrowserFailurePhase.Refresh)
                End If
            End Using
        Catch ex As Exception
            HandleBrowserFailure(ex)
        Finally
            ActiveVersionHistory = Nothing
            If Not IsDisposed AndAlso Not Disposing Then UpdateFileVersionAccess()
        End Try
    End Sub
End Class
