Imports System
Imports System.Windows.Forms

''' <summary>Identifies actions displayed in the toolbar in addition to their context menus.</summary>
''' <remarks>Visibility never grants permission to execute an action.</remarks>
<Flags>
Public Enum BrowserToolbarActions
    ''' <summary>Hides every toolbar action.</summary>
    None = 0
    ''' <summary>Shows the action for opening files.</summary>
    Open = 1
    ''' <summary>Shows the action for downloading files.</summary>
    Download = 2
    ''' <summary>Shows the action for downloading folders.</summary>
    DownloadFolder = 4
    ''' <summary>Shows the action for uploading files.</summary>
    Upload = 8
    ''' <summary>Shows the action for uploading folders.</summary>
    UploadFolder = 16
    ''' <summary>Shows the action for refreshing the listing.</summary>
    Refresh = 32
    ''' <summary>Shows the action for copying files.</summary>
    Copy = 64
    ''' <summary>Shows the action for moving files.</summary>
    Move = 128
    ''' <summary>Shows the action for renaming files.</summary>
    Rename = 256
    ''' <summary>Shows the action for deleting files.</summary>
    Delete = 512
    ''' <summary>Shows the action for managing file sharing.</summary>
    FileSharings = 1024
    ''' <summary>Shows the action for managing folder sharing.</summary>
    FolderSharings = 2048
    ''' <summary>Shows the action for viewing file properties.</summary>
    FileProperties = 4096
    ''' <summary>Shows the action for viewing folder properties.</summary>
    FolderProperties = 8192
    ''' <summary>Shows the action for viewing version history.</summary>
    VersionHistory = 16384
    ''' <summary>Shows the standard open, transfer and refresh actions.</summary>
    [Default] = Open Or Download Or DownloadFolder Or Upload Or UploadFolder Or Refresh
    ''' <summary>Shows every action permitted by the host and provider.</summary>
    All = 32767
End Enum

''' <summary>Selects the action performed when files are double-clicked.</summary>
Public Enum BrowserFileDoubleClickAction
    ''' <summary>Opens selected files as temporary read-only previews when downloading is permitted.</summary>
    Open
    ''' <summary>Prompts for a download destination when downloading is permitted.</summary>
    Download
    ''' <summary>Performs no action.</summary>
    None
End Enum

Partial Public Class DmsBrowser
    ''' <summary>Gets or sets whether permitted resources can be moved by drag and drop inside this browser, defaulting to true.</summary>
    Public Property EnableInternalDragAndDrop As Boolean = True
    Private _ToolbarActions As BrowserToolbarActions = BrowserToolbarActions.Default
    ''' <summary>Gets or sets the toolbar actions, subject to existing permissions and provider capabilities.</summary>
    ''' <remarks>Other permitted actions remain available through context menus.</remarks>
    Public Property ToolbarActions As BrowserToolbarActions
        Get
            Return _ToolbarActions
        End Get
        Set(value As BrowserToolbarActions)
            _ToolbarActions = value
            UpdateFileToolbarLayout()
        End Set
    End Property
    Private _ShowFilesButtonVisible As Boolean = True
    ''' <summary>Gets or sets whether the permitted browse-mode switch is displayed, defaulting to true.</summary>
    Public Property ShowFilesButtonVisible As Boolean
        Get
            Return _ShowFilesButtonVisible
        End Get
        Set(value As Boolean)
            _ShowFilesButtonVisible = value
            UpdateFileToolbarLayout()
            ArrangeLocalizedBrowserControls()
        End Set
    End Property
    Private _CreateFolderButtonVisible As Boolean = True
    ''' <summary>Gets or sets whether the permitted create-folder button is displayed, defaulting to true.</summary>
    ''' <remarks>The context-menu action remains available.</remarks>
    Public Property CreateFolderButtonVisible As Boolean
        Get
            Return _CreateFolderButtonVisible
        End Get
        Set(value As Boolean)
            _CreateFolderButtonVisible = value
            UpdateFileToolbarLayout()
            ArrangeLocalizedBrowserControls()
        End Set
    End Property
    ''' <summary>Gets or sets the file double-click action, defaulting to Open in both dialog modes.</summary>
    ''' <remarks>This setting does not grant download permission or change explicit selection confirmation.</remarks>
    Public Property FileDoubleClickAction As BrowserFileDoubleClickAction = BrowserFileDoubleClickAction.Open

    Private Function ShowsToolbarAction(action As BrowserToolbarActions) As Boolean
        Return (ToolbarActions And action) <> 0
    End Function

    Friend Function IsAllowedLocalTransferPath(path As String) As Boolean
        Try
            LocalUploadPlan.ValidateLocalPath(path, LocalParentMustFolder)
            Return True
        Catch
            Return False
        End Try
    End Function

    Private Sub ApplyPresentationVisibility()
        Dim provider = If(_DmsProviderOverride, DmsProviderInstance)
        Dim download = (AllowedActions And FileOrFolderActions.AllowDownloadFiles) <> 0
        Dim upload = (AllowedActions And FileOrFolderActions.AllowUploadFiles) <> 0
        Dim modify = (AllowedActions And FileOrFolderActions.AllowCopyRenameMoveFiles) <> 0
        Dim share = provider IsNot Nothing AndAlso provider.SupportsSharingSetup AndAlso (AllowedActions And FileOrFolderActions.AllowSharings) <> 0
        ToolStripButtonOpenFile.Available = download AndAlso ShowsToolbarAction(BrowserToolbarActions.Open)
        ToolStripButtonDownloadFile.Available = download AndAlso ShowsToolbarAction(BrowserToolbarActions.Download)
        ToolStripButtonUploadFile.Available = upload AndAlso ShowsToolbarAction(BrowserToolbarActions.Upload)
        UploadFolderButton.Available = upload AndAlso BrowseMode = BrowseModes.FoldersAndFiles AndAlso ShowsToolbarAction(BrowserToolbarActions.UploadFolder)
        DownloadFolderButton.Available = download AndAlso BrowseMode = BrowseModes.FoldersAndFiles AndAlso ShowsToolbarAction(BrowserToolbarActions.DownloadFolder)
        ToolStripButtonCopyFile.Available = modify AndAlso ShowsToolbarAction(BrowserToolbarActions.Copy)
        ToolStripButtonMoveFile.Available = modify AndAlso ShowsToolbarAction(BrowserToolbarActions.Move)
        ToolStripButtonRenameFile.Available = modify AndAlso ShowsToolbarAction(BrowserToolbarActions.Rename)
        ToolStripButtonDeleteFile.Available = (AllowedActions And FileOrFolderActions.AllowDeleteFiles) <> 0 AndAlso ShowsToolbarAction(BrowserToolbarActions.Delete)
        ToolStripButtonSharingsFile.Available = share AndAlso ShowsToolbarAction(BrowserToolbarActions.FileSharings)
        ToolStripButtonSharingsFolder.Available = share AndAlso Not IsCollectionManagementRestricted(TryCast(TreeViewDmsFolders.SelectedNode?.Tag, NodeTagData)?.DmsResourceItem) AndAlso ShowsToolbarAction(BrowserToolbarActions.FolderSharings)
        ToolStripButtonPropertiesFile.Available = ShowsToolbarAction(BrowserToolbarActions.FileProperties)
        ToolStripButtonPropertiesFolder.Available = ShowsToolbarAction(BrowserToolbarActions.FolderProperties)
        ToolStripButtonRefreshFilesList.Available = ShowsToolbarAction(BrowserToolbarActions.Refresh)
        VersionHistoryButton.Available = provider IsNot Nothing AndAlso provider.SupportsFileVersionHistory AndAlso
            (AllowedActions And FileOrFolderActions.AllowViewFileVersionHistory) <> 0 AndAlso BrowseMode = BrowseModes.FoldersAndFiles AndAlso ShowsToolbarAction(BrowserToolbarActions.VersionHistory)
        ButtonShowFiles.Visible = ShowFilesButtonVisible AndAlso (AllowedActions And FileOrFolderActions.AllowSwitchBrowseMode) <> 0
        ButtonCreateNewFolder.Visible = CreateFolderButtonVisible AndAlso (AllowedActions And FileOrFolderActions.AllowCreateFolders) <> 0
        For Each strip As ToolStrip In {ToolStripFileActions, ToolStripFileShareActions, ToolStripFolderShareActions, ToolStripProperties}
            'A separator alone must never keep a toolbar alive.
            Dim seenAction As Boolean
            Dim pending As ToolStripSeparator = Nothing
            For Each item As ToolStripItem In strip.Items
                If TypeOf item Is ToolStripSeparator Then
                    item.Available = False
                    If seenAction Then pending = DirectCast(item, ToolStripSeparator)
                ElseIf item.Available Then
                    If pending IsNot Nothing Then pending.Available = True
                    pending = Nothing
                    seenAction = True
                End If
            Next
            strip.Visible = strip.Items.Cast(Of ToolStripItem)().Any(Function(item) item.Available AndAlso Not TypeOf item Is ToolStripSeparator)
        Next
        FlowLayoutPanel1.Visible = New ToolStrip() {ToolStripFileActions, ToolStripFileShareActions, ToolStripFolderShareActions, ToolStripProperties}.Any(Function(strip) strip.Items.Cast(Of ToolStripItem)().Any(Function(item) item.Available AndAlso Not TypeOf item Is ToolStripSeparator))
    End Sub

    Private Sub UpdateSortHeaders()
        Dim keys = New String() {"ColumnName", "ColumnSize", "ColumnLastModification"}
        For index As Integer = 0 To ListViewDmsFiles.Columns.Count - 1
            ListViewDmsFiles.Columns(index).Text = UiStrings.GetText(keys(index)) &
                If(index = CInt(FilesSortOrderColumn), If(FilesSortOrderDirection = ComponentModel.ListSortDirection.Descending, " ▼", " ▲"), "")
        Next
    End Sub

    Private Sub ListViewDmsFiles_MouseDoubleClick(sender As Object, e As MouseEventArgs) Handles ListViewDmsFiles.MouseDoubleClick
        If e.Button <> MouseButtons.Left OrElse ListViewDmsFiles.GetItemAt(e.X, e.Y) Is Nothing Then Return
        ActivateSelectedFiles()
    End Sub

    Friend Sub ActivateSelectedFiles()
        If TransferRunning OrElse ResourceActionRunning OrElse DragDownloadRunning OrElse
            (AllowedActions And FileOrFolderActions.AllowDownloadFiles) = 0 OrElse CurrentSelectedFiles().Count = 0 Then Return
        Select Case FileDoubleClickAction
            Case BrowserFileDoubleClickAction.Open
                ToolStripButtonOpenFile_Click(Me, EventArgs.Empty)
            Case BrowserFileDoubleClickAction.Download
                ToolStripButtonDownloadFile_Click(Me, EventArgs.Empty)
        End Select
    End Sub
End Class
