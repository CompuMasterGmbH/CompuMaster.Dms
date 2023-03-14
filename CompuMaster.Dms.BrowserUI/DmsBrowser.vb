Imports System.Windows.Forms
Imports System.Drawing
Imports System.ComponentModel
Imports CompuMaster.Dms.Providers
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms
Imports CompuMaster.VisualBasicCompatibility
Imports CompuMaster.VisualBasicCompatibility.Information
Imports InfoBox

Public Class DmsBrowser

    ''' <summary>
    ''' A browser for DMS systems
    ''' </summary>
    ''' <remarks>Intended for designer only; please use another constructor overload</remarks>
    <System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>
    Public Sub New()
        MyBase.New()

        ' Dieser Aufruf ist für den Designer erforderlich.
        InitializeComponent()
    End Sub

    ''' <summary>
    ''' A browser for DMS systems
    ''' </summary>
    ''' <param name="dmsProfile"></param>
    ''' <param name="formTitle">The form title</param>
    ''' <param name="formIcon">The form icon</param>
    ''' <param name="initialRootFolder">The remote folder which shall be treated as root folder in browser dialog</param>
    ''' <param name="selectedFolder">The initially selected sub folder</param>
    ''' <param name="browseMode">Mode setting for browser dialog</param>
    ''' <param name="allowedActions">Allowed actions in browser dialog</param>
    ''' <param name="dialogOperationMode">Operational settings for browser dialog</param>
    ''' <param name="localParentMustFolder">Ensure that downloads/uploads from/to local system are always sub elements of this folder</param>
    ''' <param name="localDefaultFolderDownloads">Default downloads folder on local system</param>
    ''' <param name="localDefaultFolderUploads">Default uploads folder on local system</param>
    Public Sub New(dmsProfile As CompuMaster.Dms.Data.IDmsLoginProfile, formTitle As String, formIcon As System.Drawing.Icon, initialRootFolder As String, selectedFolder As String, browseMode As BrowseModes, allowedActions As FileOrFolderActions, dialogOperationMode As DialogOperationModes, localParentMustFolder As String, localDefaultFolderDownloads As String, localDefaultFolderUploads As String)

        ' Dieser Aufruf ist für den Designer erforderlich.
        InitializeComponent()

        ' Fügen Sie Initialisierungen nach dem InitializeComponent()-Aufruf hinzu.
        Me.Text = formTitle
        Me.Icon = formIcon
        Me.DmsProfile = dmsProfile
        Me.InitialFolder = initialRootFolder
        Me.SelectedFolder = selectedFolder
        Me.BrowseMode = browseMode
        Me.AllowedActions = allowedActions
        Me.DialogOperationModeInternal = dialogOperationMode
        Me.LocalParentMustFolder = localParentMustFolder
        If localParentMustFolder <> Nothing Then
            If Tools.IsParentDirectory(localParentMustFolder, localDefaultFolderDownloads) = False Then
                Throw New ArgumentException("Local default downloads folder """ & localDefaultFolderDownloads & """ must be a sub folder of directory """ & localParentMustFolder & "", NameOf(localDefaultFolderDownloads))
            End If
            If Tools.IsParentDirectory(localParentMustFolder, localDefaultFolderUploads) = False Then
                Throw New ArgumentException("Local default uploads folder """ & localDefaultFolderUploads & """ must be a sub folder of directory """ & localParentMustFolder & "", NameOf(localDefaultFolderUploads))
            End If
        End If
        Me.LocalDefaultFolderDownloads = localDefaultFolderDownloads
        Me.LocalDefaultFolderUploads = localDefaultFolderUploads
    End Sub

    Private ReadOnly Property IsDesignMode As Boolean
        Get
            If Me.DesignMode Then
                Return True
            ElseIf System.ComponentModel.LicenseManager.UsageMode = LicenseUsageMode.Designtime Then
                Return True
                'ElseIf System.Reflection.Assembly.GetEntryAssembly Is Nothing Then
                '    'Visual Studio IDE, sometimes causing reload timer to run !?!
                '    Return True
            ElseIf Me.DmsProfile Is Nothing Then
                Return True
            Else
                Return False
            End If
        End Get
    End Property

    Public Property DmsProfile As CompuMaster.Dms.Data.IDmsLoginProfile

    Public ReadOnly Property DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider
        Get
            Static _DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider
            If _DmsProvider Is Nothing Then
                _DmsProvider = CompuMaster.Dms.Providers.CreateAuthorizedDmsProviderInstance(DmsProfile)
            End If
            Return _DmsProvider
        End Get
    End Property

    Public Enum DialogOperationModes As Byte
        ReturnSelectedItems = 1
        NoResults = 2
    End Enum

    Private _DialogOperationModeInternal As DialogOperationModes
    Private Property DialogOperationModeInternal As DialogOperationModes
        Get
            Return _DialogOperationModeInternal
        End Get
        Set(value As DialogOperationModes)
            Select Case value
                Case DialogOperationModes.NoResults
                    Me.ButtonOkay.Visible = False
                    Me.ButtonCancel.Visible = False
                    Me.ButtonClose.Visible = True
                Case DialogOperationModes.ReturnSelectedItems
                    Me.ButtonOkay.Visible = True
                    Me.ButtonCancel.Visible = True
                    Me.ButtonClose.Visible = False
                Case Else
                    Throw New ArgumentOutOfRangeException(NameOf(value), "Invalid value: " & value.ToString)
            End Select
            _DialogOperationModeInternal = value
        End Set
    End Property

    Public ReadOnly Property DialogOperationMode As DialogOperationModes
        Get
            Return Me.DialogOperationModeInternal
        End Get
    End Property

    <Flags> Public Enum FileOrFolderActions As Integer
        AllowSelectOnly = 0
        AllowSwitchBrowseMode = 1
        AllowCreateFolders = 2
        AllowUploadFiles = 4
        AllowDownloadFiles = 8
        AllowDeleteFiles = 16
        AllowCopyRenameMoveFiles = 32
        AllowSharings = 64
    End Enum
    Private _AllowedActions As FileOrFolderActions
    Public Property AllowedActions As FileOrFolderActions
        Get
            Return _AllowedActions
        End Get
        Set(value As FileOrFolderActions)
            _AllowedActions = value
            Me.ToolStripButtonUploadFile.Enabled = ((value And FileOrFolderActions.AllowUploadFiles) = FileOrFolderActions.AllowUploadFiles)
            Me.ToolStripButtonDownloadFile.Enabled = ((value And FileOrFolderActions.AllowDownloadFiles) = FileOrFolderActions.AllowDownloadFiles)
            Me.ToolStripButtonOpenFile.Enabled = ((value And FileOrFolderActions.AllowDownloadFiles) = FileOrFolderActions.AllowDownloadFiles)
            Me.ToolStripButtonDeleteFile.Enabled = ((value And FileOrFolderActions.AllowDeleteFiles) = FileOrFolderActions.AllowDeleteFiles)
            Me.ButtonCreateNewFolder.Enabled = ((value And FileOrFolderActions.AllowCreateFolders) = FileOrFolderActions.AllowCreateFolders)
            Me.ButtonShowFiles.Visible = ((value And FileOrFolderActions.AllowSwitchBrowseMode) = FileOrFolderActions.AllowSwitchBrowseMode)
            Me.ToolStripSeparatorBeforeCopyRenameMove.Visible = ((value And FileOrFolderActions.AllowCopyRenameMoveFiles) = FileOrFolderActions.AllowCopyRenameMoveFiles)
            Me.ToolStripButtonCopyFile.Visible = ((value And FileOrFolderActions.AllowCopyRenameMoveFiles) = FileOrFolderActions.AllowCopyRenameMoveFiles)
            Me.ToolStripButtonRenameFile.Visible = ((value And FileOrFolderActions.AllowCopyRenameMoveFiles) = FileOrFolderActions.AllowCopyRenameMoveFiles)
            Me.ToolStripButtonMoveFile.Visible = ((value And FileOrFolderActions.AllowCopyRenameMoveFiles) = FileOrFolderActions.AllowCopyRenameMoveFiles)
            Me.ToolStripButtonSharingsFile.Visible = Me.DmsProvider.SupportsSharingSetup AndAlso ((value And FileOrFolderActions.AllowSharings) = FileOrFolderActions.AllowSharings)
            Me.ToolStripButtonSharingsFolder.Visible = Me.DmsProvider.SupportsSharingSetup AndAlso ((value And FileOrFolderActions.AllowSharings) = FileOrFolderActions.AllowSharings)
            Me.ToolStripFileShareActions.Visible = Me.DmsProvider.SupportsSharingSetup AndAlso ((value And FileOrFolderActions.AllowSharings) = FileOrFolderActions.AllowSharings)
            Me.ToolStripFolderShareActions.Visible = Me.DmsProvider.SupportsSharingSetup AndAlso ((value And FileOrFolderActions.AllowSharings) = FileOrFolderActions.AllowSharings)
            Me.ToolStripButtonPropertiesFile.Visible = True
            UITools.SwitchToolStripVisibility(Me.ToolStripFolderContextButtonNewFolder, ((value And FileOrFolderActions.AllowCreateFolders) = FileOrFolderActions.AllowCreateFolders), False)
            UITools.SwitchToolStripVisibility(Me.ToolStripFolderContextButtonCopyFolder, ((value And FileOrFolderActions.AllowCopyRenameMoveFiles) = FileOrFolderActions.AllowCopyRenameMoveFiles), False)
            UITools.SwitchToolStripVisibility(Me.ToolStripFolderContextButtonMoveFolder, ((value And FileOrFolderActions.AllowCopyRenameMoveFiles) = FileOrFolderActions.AllowCopyRenameMoveFiles), False)
            UITools.SwitchToolStripVisibility(Me.ToolStripFolderContextButtonRenameFolder, ((value And FileOrFolderActions.AllowCopyRenameMoveFiles) = FileOrFolderActions.AllowCopyRenameMoveFiles), False)
            UITools.SwitchToolStripVisibility(Me.ToolStripFolderContextButtonDeleteFolder, ((value And FileOrFolderActions.AllowDeleteFiles) = FileOrFolderActions.AllowDeleteFiles), False)
            UITools.SwitchToolStripVisibility(Me.ToolStripFolderContextButtonRefreshFilesList, True, False)
            UITools.SwitchToolStripVisibility(Me.ToolStripFolderContextButtonShareFolder, Me.DmsProvider.SupportsSharingSetup AndAlso ((value And FileOrFolderActions.AllowSharings) = FileOrFolderActions.AllowSharings), False)
            UITools.SwitchToolStripVisibility(Me.ToolStripFolderContextButtonProperties, True, False)
            UITools.SwitchToolStripVisibility(Me.ToolStripFileContextButtonUploadFile, ((value And FileOrFolderActions.AllowUploadFiles) = FileOrFolderActions.AllowUploadFiles), False)
            UITools.SwitchToolStripVisibility(Me.ToolStripFileContextButtonDownloadFile, ((value And FileOrFolderActions.AllowDownloadFiles) = FileOrFolderActions.AllowDownloadFiles), False)
            UITools.SwitchToolStripVisibility(Me.ToolStripFileContextButtonOpenPreviewFile, ((value And FileOrFolderActions.AllowDownloadFiles) = FileOrFolderActions.AllowDownloadFiles), False)
            UITools.SwitchToolStripVisibility(Me.ToolStripFileContextButtonCopyFile, ((value And FileOrFolderActions.AllowCopyRenameMoveFiles) = FileOrFolderActions.AllowCopyRenameMoveFiles), False)
            UITools.SwitchToolStripVisibility(Me.ToolStripFileContextButtonMoveFile, ((value And FileOrFolderActions.AllowCopyRenameMoveFiles) = FileOrFolderActions.AllowCopyRenameMoveFiles), False)
            UITools.SwitchToolStripVisibility(Me.ToolStripFileContextButtonRenameFile, ((value And FileOrFolderActions.AllowCopyRenameMoveFiles) = FileOrFolderActions.AllowCopyRenameMoveFiles), False)
            UITools.SwitchToolStripVisibility(Me.ToolStripFileContextButtonDeleteFile, ((value And FileOrFolderActions.AllowDeleteFiles) = FileOrFolderActions.AllowDeleteFiles), False)
            UITools.SwitchToolStripVisibility(Me.ToolStripFileContextButtonShareFile, Me.DmsProvider.SupportsSharingSetup AndAlso ((value And FileOrFolderActions.AllowSharings) = FileOrFolderActions.AllowSharings), False)
            UITools.SwitchToolStripVisibility(Me.ToolStripFileContextButtonProperties, True, False)
        End Set
    End Property

    Public Enum BrowseModes As Byte
        Folders = 0
        FoldersAndFiles = 1
    End Enum
    Private _BrowseMode As BrowseModes
    Public Property BrowseMode As BrowseModes
        Get
            Return _BrowseMode
        End Get
        Set(value As BrowseModes)
            _BrowseMode = value
            Select Case value
                Case BrowseModes.Folders
                    Me.SplitContainer.Panel2Collapsed = True
                    Me.ToolStripButtonUploadFile.Visible = False
                    Me.ToolStripButtonDownloadFile.Visible = False
                    Me.ToolStripButtonDeleteFile.Visible = False
                    Me.ButtonShowFiles.Checked = False
                Case BrowseModes.FoldersAndFiles
                    Me.SplitContainer.Panel2Collapsed = False
                    Me.ToolStripButtonUploadFile.Visible = True
                    Me.ToolStripButtonDownloadFile.Visible = True
                    Me.ToolStripButtonDeleteFile.Visible = True
                    Me.ButtonShowFiles.Checked = True
            End Select
        End Set
    End Property

    Public Property InitialFolder As String
    Public Property SelectedFolder As String
    Private RootNode As TreeNode
    Private _FileIcons As SystemIconsImageListWrapper = Nothing
    Private ReadOnly Property FileIcons As SystemIconsImageListWrapper
        Get
            If _FileIcons Is Nothing Then
                _FileIcons = New SystemIconsImageListWrapper(Me.ImageListFileIcons, 6, 7)
            End If
            Return _FileIcons
        End Get
    End Property

    ''' <summary>
    ''' When starting downloads, automatically open this local folder
    ''' </summary>
    ''' <returns></returns>
    Public Property LocalDefaultFolderDownloads As String

    ''' <summary>
    ''' When starting uploads, automatically open this local folder
    ''' </summary>
    ''' <returns></returns>
    Public Property LocalDefaultFolderUploads As String

    ''' <summary>
    ''' Files must be downloaded into a folder below of this folder (usually the customer base directory)
    ''' </summary>
    ''' <returns></returns>
    Public Property LocalParentMustFolder As String

    Private Sub BrowseDmsFolders_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        If Me.IsDesignMode Then Return 'no loading in design mode
        If Me.LocalDefaultFolderDownloads = Nothing Then LocalDefaultFolderDownloads = Me.LocalParentMustFolder
        If Me.LocalDefaultFolderUploads = Nothing Then LocalDefaultFolderUploads = Me.LocalParentMustFolder
        Try
            Me.LoadTree()
        Catch ex As CompuMaster.Dms.Data.DirectoryNotFoundException
            MessageBox.Show(Me, ex.Message, "DMS folder not found: " & ex.RemotePath, MessageBoxButtons.OK, MessageBoxIcon.Error)
            Me.DialogResult = DialogResult.Cancel
            Me.Close()
            Return
        Catch ex As Exception
            If System.Diagnostics.Debugger.IsAttached Then
                MessageBox.Show(Me, ex.ToString, "Zugangsdaten ungültig, DMS-Server-Instanz-Fehler oder Netzwerkfehler", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Else
                MessageBox.Show(Me, ex.Message, "Zugangsdaten ungültig, DMS-Server-Instanz-Fehler oder Netzwerkfehler", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If
            Me.DialogResult = DialogResult.Cancel
            Me.Close()
            Return
        End Try
        Try
            Me.SelectFolderPath(Me.SelectedFolder)
            Select Case Me.BrowseMode
                Case BrowseModes.Folders
                    Me.TreeViewDmsFolders.Select()
                Case Else
                    Me.ListViewDmsFiles.Select()
            End Select
        Catch ex As Exception
            MessageBox.Show(Me, "Ungültiger Ordner: " & Me.SelectedFolder, "DMS Ordner nicht oder nicht mehr gültig", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub LoadTree()
        Me.TreeViewDmsFolders.Nodes.Clear()
        If Me.InitialFolder <> Nothing Then
            Dim Folder As DmsResourceItem = Me.DmsProvider.ListRemoteItem(Me.InitialFolder)
            If Folder Is Nothing Then Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(Me.InitialFolder)
            Me.RootNode = Me.TreeViewDmsFolders.Nodes.Add("", Me.InitialFolder)
            Me.RootNode.Tag = New NodeTagData(Folder)
            If Folder.ExtendedInfosHasGroupSharings OrElse Folder.ExtendedInfosHasUserSharings Then
                Me.RootNode.ImageIndex = 3
                Me.RootNode.SelectedImageIndex = 3
            Else
                Me.RootNode.ImageIndex = 0
                Me.RootNode.SelectedImageIndex = 0
            End If
        Else
            Me.RootNode = Me.TreeViewDmsFolders.Nodes.Add("", Tools.NotEmptyOrAlternativeValue(Me.DmsProvider.BrowseInRootFolderName, "/"))
            Me.RootNode.Tag = New NodeTagData(Nothing)
            Me.RootNode.ImageIndex = 0
            Me.RootNode.SelectedImageIndex = 0
        End If
        Me.AddTreeChildren(Me.RootNode)
        Me.AddTreeGrandChildren(Me.RootNode)
        Me.TreeViewDmsFolders.Sort()
        Me.RootNode.Expand()
    End Sub

    Private Class NodeTagData
        Public Sub New(dmsResourceItem As DmsResourceItem)
            Me.DmsResourceItem = dmsResourceItem
        End Sub
        Public HasFolders As TriState = TriState.UseDefault
        Public DmsResourceItem As DmsResourceItem
    End Class

    Private Sub AddTreeGrandChildren(grandParentNode As TreeNode)
        If grandParentNode Is Nothing Then Return
        For Each ChildNode As TreeNode In grandParentNode.Nodes
            If ChildNode.Tag Is Nothing Then
                ChildNode.BackColor = Color.Red
            Else
                Dim ChildData As NodeTagData = CType(ChildNode.Tag, NodeTagData)
                Me.AddTreeChildren(ChildNode)
            End If
        Next
    End Sub

    Private Sub AddTreeChildren(parentNode As TreeNode)
        Dim ParentData As NodeTagData = CType(parentNode.Tag, NodeTagData)
        If ParentData.HasFolders = TriState.UseDefault Then
            'Search for collections
            If Me.DmsProvider.SupportsCollections AndAlso (ParentData.DmsResourceItem Is Nothing OrElse ParentData.DmsResourceItem.ItemType = DmsResourceItem.ItemTypes.Collection) Then
                Dim SubCollections As List(Of DmsResourceItem)
                If ParentData.DmsResourceItem Is Nothing Then
                    SubCollections = Me.DmsProvider.ListAllCollectionItems(Me.DmsProvider.BrowseInRootFolderName)
                Else
                    SubCollections = Me.DmsProvider.ListAllCollectionItems(ParentData.DmsResourceItem.FullName)
                End If
                For Each collection As DmsResourceItem In SubCollections
                    Dim n As New TreeNode(collection.Name)
                    n.Tag = New NodeTagData(collection)
                    If collection.ExtendedInfosHasGroupSharings OrElse collection.ExtendedInfosHasUserSharings OrElse collection.ExtendedInfosIsShared Then
                        n.ImageIndex = 4
                        n.SelectedImageIndex = 4
                    Else
                        n.ImageIndex = 1
                        n.SelectedImageIndex = 1
                    End If
                    Me.RootNode.Nodes.Add(n)
                Next
                If SubCollections.Count = 0 Then
                    ParentData.HasFolders = TriState.False
                Else
                    ParentData.HasFolders = TriState.True
                End If
            End If
            'Search for folders
            Dim SubFolders As List(Of DmsResourceItem)
            If ParentData.DmsResourceItem Is Nothing Then
                SubFolders = Me.DmsProvider.ListAllFolderItems(Me.DmsProvider.BrowseInRootFolderName)
            Else
                SubFolders = Me.DmsProvider.ListAllFolderItems(ParentData.DmsResourceItem.FullName)
            End If
            If ParentData.HasFolders <> TriState.True Then
                If SubFolders.Count = 0 Then
                    ParentData.HasFolders = TriState.False
                Else
                    ParentData.HasFolders = TriState.True
                End If
            End If
            For Each folder As DmsResourceItem In SubFolders
                Dim n As New TreeNode(folder.Name)
                n.Tag = New NodeTagData(folder)
                If folder.ExtendedInfosHasGroupSharings OrElse folder.ExtendedInfosHasUserSharings Then
                    n.ImageIndex = 5
                    n.SelectedImageIndex = 5
                Else
                    n.ImageIndex = 2
                    n.SelectedImageIndex = 2
                End If
                parentNode.Nodes.Add(n)
            Next
        End If
    End Sub

    Private Sub SelectFolderPath(path As String)
        If path = Nothing Then
            Me.TreeViewDmsFolders.SelectedNode = Me.RootNode
        Else
            Dim FolderHierarchy As New List(Of String)(path.Split(Me.DmsProvider.DirectorySeparator))
            Dim LastMatch As TreeNode = Me.RootNode
            For MyCounter As Integer = 0 To FolderHierarchy.Count - 1
                Dim NewMatch As TreeNode = Me.FindChildNode(LastMatch, FolderHierarchy(MyCounter))
                If NewMatch Is Nothing Then
                    Exit For 'Path has been found partially, select as far as possible
                Else
                    LastMatch = NewMatch
                End If
                Me.AddTreeGrandChildren(LastMatch)
            Next
            Me.TreeViewDmsFolders.SelectedNode = LastMatch
        End If
        Me.RefreshFilesList()
    End Sub

    Private Function FindChildNode(parentNode As TreeNode, folderName As String) As TreeNode
        For Each ChildNode As TreeNode In parentNode.Nodes
            Dim ChildData As NodeTagData = CType(ChildNode.Tag, NodeTagData)
            If ChildData.DmsResourceItem.Name = folderName Then
                Return ChildNode
            End If
        Next
        Return Nothing
    End Function

    Private Function SelectedFolderPath() As String
        If Me.TreeViewDmsFolders.SelectedNode Is Nothing Then
            Return Nothing
        Else
            Dim SelectedNodeFolderPath As String = CType(Me.TreeViewDmsFolders.SelectedNode.Tag, NodeTagData).DmsResourceItem?.FullName
            If SelectedNodeFolderPath = Nothing Then Return Nothing
            Dim RootNodeFolderPath As String = Tools.NotEmptyOrAlternativeValue(CType(Me.RootNode.Tag, NodeTagData).DmsResourceItem?.FullName, "")
            If SelectedNodeFolderPath = RootNodeFolderPath Then
                Return Nothing
            ElseIf RootNodeFolderPath = Nothing Then
                If SelectedNodeFolderPath.StartsWith(Me.DmsProvider.DirectorySeparator) Then
                    Return SelectedNodeFolderPath.Substring(Me.DmsProvider.DirectorySeparator.ToString.Length)
                Else
                    Return SelectedNodeFolderPath
                End If
            ElseIf SelectedNodeFolderPath.StartsWith(RootNodeFolderPath) = False Then
                Throw New InvalidOperationException("SelectedNodeFolderPath """ & SelectedNodeFolderPath & """ expected to start with RootNodeFolderPath """ & RootNodeFolderPath & """")
            Else
                Return SelectedNodeFolderPath.Substring((RootNodeFolderPath & Me.DmsProvider.DirectorySeparator).Length)
            End If
        End If
    End Function

    Private Sub ButtonOkay_Click(sender As Object, e As EventArgs) Handles ButtonOkay.Click
        If Me.ButtonOkay.Visible = False Then Return 'Cancel DoubleClick events referencing ButtonOkay.Click
        Me.DialogResult = DialogResult.OK
        Me.SelectedFolder = Me.SelectedFolderPath()
        Me.Close()
    End Sub

    Private Sub ButtonCancel_Click(sender As Object, e As EventArgs) Handles ButtonCancel.Click
        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub

    Private Sub TreeViewDmsFolders_AfterSelect(sender As Object, e As TreeViewEventArgs) Handles TreeViewDmsFolders.AfterSelect
        Me.SelectedFolder = Me.SelectedFolderPath
        Me.RefreshFilesList()
    End Sub

    Private Sub TreeViewDmsFolders_BeforeExpand(sender As Object, e As TreeViewCancelEventArgs) Handles TreeViewDmsFolders.BeforeExpand
        Try
            Me.AddTreeGrandChildren(e.Node)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub TreeViewDmsFolders_DoubleClick(sender As Object, e As EventArgs) Handles TreeViewDmsFolders.DoubleClick
        Me.ButtonOkay_Click(sender, e)
    End Sub

    Private Sub ButtonCreateNewFolder_Click(sender As Object, e As EventArgs) Handles ButtonCreateNewFolder.Click
        Try
            Dim NewFolderName As String = InputBox("Wie soll der neue Ordner unterhalb von """ & Me.SelectedFolderPath & """ heißen?", "Neuer Ordner")
            If NewFolderName = Nothing Then Return
            Dim NewFolderPath As String = Me.DmsProvider.CombinePath(CType(Me.TreeViewDmsFolders.SelectedNode.Tag, NodeTagData).DmsResourceItem?.FullName, NewFolderName)
            Me.DmsProvider.CreateDirectory(NewFolderPath)
            Dim Folder As DmsResourceItem = Me.DmsProvider.ListRemoteItem(NewFolderPath)
            Dim n As New TreeNode(Folder.Name)
            n.Tag = New NodeTagData(Folder)
            Me.TreeViewDmsFolders.SelectedNode.Nodes.Add(n)
            Me.TreeViewDmsFolders.SelectedNode = n
            n.TreeView.Focus()
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Enum FilesListingColumn As Integer
        FileName = 0
        Size = 1
        LastModified = 2
    End Enum
    Private FilesSortOrderColumn As FilesListingColumn = FilesListingColumn.LastModified
    Private FilesSortOrderDirection As ListSortDirection = ListSortDirection.Descending
    Private Function ApplyFilesSortOrder(list As List(Of DmsResourceItem)) As List(Of DmsResourceItem)
        Select Case Me.FilesSortOrderDirection
            Case ListSortDirection.Descending
                Select Case Me.FilesSortOrderColumn
                    Case FilesListingColumn.FileName
                        Return New List(Of DmsResourceItem)(list.OrderBy(Of String)(Function(item) item.Name))
                    Case FilesListingColumn.Size
                        Return New List(Of DmsResourceItem)(list.OrderBy(Of Long)(Function(item) item.ContentLength))
                    Case FilesListingColumn.LastModified
                        Return New List(Of DmsResourceItem)(list.OrderBy(Of DateTime?)(Function(item) item.LastModificationOnLocalTime))
                    Case Else
                        Throw New NotImplementedException
                End Select
            Case Else
                Select Case Me.FilesSortOrderColumn
                    Case FilesListingColumn.FileName
                        Return New List(Of DmsResourceItem)(list.OrderByDescending(Of String)(Function(item) item.Name))
                    Case FilesListingColumn.Size
                        Return New List(Of DmsResourceItem)(list.OrderByDescending(Of Long)(Function(item) item.ContentLength))
                    Case FilesListingColumn.LastModified
                        Return New List(Of DmsResourceItem)(list.OrderByDescending(Of DateTime?)(Function(item) item.LastModificationOnLocalTime))
                    Case Else
                        Throw New NotImplementedException
                End Select
        End Select
    End Function

    Private Sub RefreshFilesList()
        Me.ListViewDmsFiles.Items.Clear()
        If Me.SplitContainer.Panel2Collapsed = False Then
            Dim CurrentFolder As NodeTagData = CType(Me.TreeViewDmsFolders.SelectedNode.Tag, NodeTagData)
            Dim Files As List(Of DmsResourceItem)
            If CurrentFolder IsNot Nothing AndAlso CurrentFolder.DmsResourceItem IsNot Nothing Then
                Me.DmsProvider.ResetCachesForRemoteItems(CurrentFolder.DmsResourceItem.FullName, Providers.BaseDmsProvider.SearchItemType.Files)
                Files = Me.DmsProvider.ListAllFileItems(CurrentFolder.DmsResourceItem.FullName)
            Else
                Files = New List(Of DmsResourceItem)
            End If
            Me.ListViewDmsFiles.Sorting = SortOrder.None
            Files = ApplyFilesSortOrder(Files)
            Me.ListViewDmsFiles.Tag = Files
            Dim AllFileNameHashes As List(Of Integer) = Files.ConvertAll(Of Integer)(Function(file) file.Name.GetHashCode)
            For Each file As DmsResourceItem In Files
                Dim FileExtension As String
                Try
                    FileExtension = System.IO.Path.GetExtension(file.Name)
                Catch ex As Exception
                    FileExtension = ""
                End Try
                Dim Item As New ListViewItem(file.Name, Me.FileIcons.GetSIImageListIndexForFileExtension(FileExtension, file.ExtendedInfosIsShared))
                Item.Tag = file
                Dim SubItems As ListViewItem.ListViewSubItem() = New ListViewItem.ListViewSubItem() {
                        New ListViewItem.ListViewSubItem(Item, Tools.ByteSizeToUIDisplayText(file.ContentLength)),
                        New ListViewItem.ListViewSubItem(Item, file.LastModificationOnLocalTime.ToString())
                        }
                Dim FileNameHash As Integer = file.Name.GetHashCode
                If AllFileNameHashes.FindAll(Function(hashItem As Integer)
                                                 Return hashItem = FileNameHash
                                             End Function).Count > 1 Then
                    Item.BackColor = Color.Red
                    Item.ForeColor = Color.White
                    Item.ToolTipText = "Konflikt: Mehrere Dateien mit gleichem Dateinamen vorhanden"
                End If
                Item.SubItems.AddRange(SubItems)
                Me.ListViewDmsFiles.Items.Add(Item)
            Next
            Me.ListViewDmsFiles.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent)
            Me.ListViewDmsFiles.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize)
        End If
    End Sub

    Private Sub ButtonShowFiles_CheckedChanged(sender As Object, e As EventArgs) Handles ButtonShowFiles.CheckedChanged
        If Me.ButtonShowFiles.Checked Then
            Me.BrowseMode = BrowseModes.FoldersAndFiles
        Else
            Me.BrowseMode = BrowseModes.Folders
        End If
        If Me.TreeViewDmsFolders.SelectedNode IsNot Nothing Then
            Me.RefreshFilesList()
        End If
    End Sub

    Private Function CurrentSelectedFiles() As List(Of DmsResourceItem)
        If Me.ListViewDmsFiles.SelectedItems.Count = 0 Then
            Return New List(Of DmsResourceItem)
        Else
            Dim Result As New List(Of DmsResourceItem)
            For Each Item In Me.ListViewDmsFiles.SelectedItems
                Result.Add(CType(CType(Item, ListViewItem).Tag, DmsResourceItem))
            Next
            Return Result
        End If
    End Function

    Private Function CurrentSelectedFolder() As DmsResourceItem
        If Me.TreeViewDmsFolders.SelectedNode Is Nothing Then
            Return Nothing
        Else
            Return CType(Me.TreeViewDmsFolders.SelectedNode.Tag, NodeTagData).DmsResourceItem
        End If
    End Function

    Private Function CurrentSelectedFolderNode() As TreeNode
        If Me.TreeViewDmsFolders.SelectedNode Is Nothing Then
            Return Nothing
        Else
            Return Me.TreeViewDmsFolders.SelectedNode
        End If
    End Function

    Private Function CurrentParentOfSelectedFolder() As DmsResourceItem
        If Me.TreeViewDmsFolders.SelectedNode Is Nothing OrElse Me.TreeViewDmsFolders.SelectedNode.Parent Is Nothing Then
            Return Nothing
        Else
            Return CType(Me.TreeViewDmsFolders.SelectedNode.Parent.Tag, NodeTagData).DmsResourceItem
        End If
    End Function

    Private Function CurrentParentOfSelectedFolderNode() As TreeNode
        If Me.TreeViewDmsFolders.SelectedNode Is Nothing OrElse Me.TreeViewDmsFolders.SelectedNode.Parent Is Nothing Then
            Return Nothing
        Else
            Return Me.TreeViewDmsFolders.SelectedNode
        End If
    End Function

    Private Sub ListViewDmsFiles_ColumnClick(sender As Object, e As ColumnClickEventArgs) Handles ListViewDmsFiles.ColumnClick
        If Me.FilesSortOrderColumn = CType(e.Column, FilesListingColumn) Then
            If Me.FilesSortOrderDirection = ListSortDirection.Descending Then
                Me.FilesSortOrderDirection = ListSortDirection.Ascending
            Else
                Me.FilesSortOrderDirection = ListSortDirection.Descending
            End If
        Else
            Me.FilesSortOrderDirection = ListSortDirection.Ascending
        End If
        Me.FilesSortOrderColumn = CType(e.Column, FilesListingColumn)
        Me.RefreshFilesList()
    End Sub

    Private Sub ToolStripButtonUploadFile_Click(sender As Object, e As EventArgs) Handles ToolStripButtonUploadFile.Click
        Try
            Dim DialogUserResult As DialogResult = DialogResult.None
            Dim f As New System.Windows.Forms.OpenFileDialog
            f.CheckFileExists = False
            f.InitialDirectory = Me.LocalDefaultFolderUploads
            f.Title = "Remote DMS - Datei-Upload"
            f.AddExtension = False
            f.CheckFileExists = True
            f.CheckPathExists = True
            f.Multiselect = True
            f.Filter = "Alle Dateien (*.*)|*.*"
            DialogUserResult = f.ShowDialog()
            If DialogUserResult = DialogResult.OK Then
                If f.FileNames.Length > 0 Then
                    For MyCounter As Integer = 0 To f.FileNames.Length - 1
                        If System.IO.File.Exists(f.FileNames(MyCounter)) = True Then
                            Dim TargetFile As String = Me.DmsProvider.CombinePath(CType(Me.TreeViewDmsFolders.SelectedNode.Tag, NodeTagData).DmsResourceItem.FullName, System.IO.Path.GetFileName(f.FileNames(MyCounter)))
                            Me.DmsProvider.UploadFile(TargetFile, f.FileNames(MyCounter))
                        Else
                            System.Windows.Forms.MessageBox.Show(Me, "Datei nicht gefunden", Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                        End If
                    Next
                    System.Windows.Forms.MessageBox.Show(Me, "Upload erfolgreich", Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Me.RefreshFilesList()
                Else
                    System.Windows.Forms.MessageBox.Show(Me, "Keine Datei ausgewählt", Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                End If
            Else
                System.Windows.Forms.MessageBox.Show(Me, "Vorgang durch Benutzer abgebrochen", Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            End If
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripButtonDownloadFile_Click(sender As Object, e As EventArgs) Handles ToolStripButtonDownloadFile.Click
        Try
            Dim SelectedFiles As List(Of DmsResourceItem) = Me.CurrentSelectedFiles
            If SelectedFiles.Count = 0 Then
                System.Windows.Forms.MessageBox.Show(Me, "Keine Datei(en) ausgewählt", Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            ElseIf SelectedFiles.Count = 1 Then
                Dim DialogUserResult As DialogResult = DialogResult.None
                Dim f As New System.Windows.Forms.SaveFileDialog
                f.CheckFileExists = False
                f.InitialDirectory = Me.LocalDefaultFolderDownloads
                f.FileName = SelectedFiles(0).Name
                f.Title = "Remote DMS - Datei-Download"
                f.AddExtension = False
                f.CheckPathExists = True
                f.OverwritePrompt = True
                f.Filter = "Alle Dateien (*.*)|*.*"
                DialogUserResult = f.ShowDialog()
                If DialogUserResult = DialogResult.OK Then
                    If f.FileName.StartsWith(Me.LocalParentMustFolder) Then
                        Dim TargetFile As String = f.FileName
                        Me.DmsProvider.DownloadFile(SelectedFiles(0).FullName, TargetFile, SelectedFiles(0).LastModificationOnLocalTime)
                        System.Windows.Forms.MessageBox.Show(Me, "Download erfolgreich", Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Else
                        System.Windows.Forms.MessageBox.Show(Me, "Bereitstellung außerhalb des Ordners """ & Me.LocalParentMustFolder & """ ist nicht unterstützt", Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                    End If
                Else
                    System.Windows.Forms.MessageBox.Show(Me, "Vorgang durch Benutzer abgebrochen", Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                End If
            Else
                Dim DialogUserResult As DialogResult = DialogResult.None
                Dim f As New System.Windows.Forms.FolderBrowserDialog
                f.SelectedPath = Me.LocalDefaultFolderDownloads
                f.Description = "Remote DMS - Datei-Download"
                f.ShowNewFolderButton = True
                DialogUserResult = f.ShowDialog()
                If DialogUserResult = DialogResult.OK Then
                    If f.SelectedPath.StartsWith(Me.LocalParentMustFolder) Then
                        'Overwrite pre-checks
                        Dim OverwriteWarning As New System.Text.StringBuilder()
                        For MyCounter As Integer = 0 To SelectedFiles.Count - 1
                            Dim TargetFile As String = System.IO.Path.Combine(f.SelectedPath, SelectedFiles(MyCounter).Name)
                            If System.IO.File.Exists(TargetFile) Then
                                If OverwriteWarning.Length <> 0 Then
                                    OverwriteWarning.AppendLine()
                                End If
                                OverwriteWarning.Append("- ")
                                OverwriteWarning.Append(SelectedFiles(MyCounter).Name)
                            End If
                        Next
                        Dim OverwriteLocalFiles As Boolean = False
                        If OverwriteWarning.Length <> 0 Then
                            Select Case MessageBox.Show(Me, "Die folgenden Dateien existieren bereits. Sollen diese überschrieben werden?", "Download nach " & f.SelectedPath, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question)
                                Case DialogResult.Yes
                                    OverwriteLocalFiles = True
                                Case DialogResult.No
                                    OverwriteLocalFiles = False
                                Case Else
                                    'exit loop and method
                                    Return
                            End Select
                        End If
                        'Save to disk
                        For MyCounter As Integer = 0 To SelectedFiles.Count - 1
                            Dim TargetFile As String = System.IO.Path.Combine(f.SelectedPath, SelectedFiles(MyCounter).Name)
                            If System.IO.File.Exists(TargetFile) Then
                                If OverwriteLocalFiles Then
                                    Me.DmsProvider.DownloadFile(SelectedFiles(MyCounter).FullName, TargetFile, SelectedFiles(MyCounter).LastModificationOnLocalTime)
                                End If
                            Else
                                Me.DmsProvider.DownloadFile(SelectedFiles(MyCounter).FullName, TargetFile, SelectedFiles(MyCounter).LastModificationOnLocalTime)
                            End If
                        Next
                        System.Windows.Forms.MessageBox.Show(Me, "Download erfolgreich", Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Else
                        System.Windows.Forms.MessageBox.Show(Me, "Bereitstellung außerhalb des Ordners """ & Me.LocalParentMustFolder & """ ist nicht unterstützt", Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                    End If
                Else
                    System.Windows.Forms.MessageBox.Show(Me, "Vorgang durch Benutzer abgebrochen", Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                End If
            End If
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripButtonDeleteFile_Click(sender As Object, e As EventArgs) Handles ToolStripButtonDeleteFile.Click
        Try
            Dim SelectedFiles As List(Of DmsResourceItem) = Me.CurrentSelectedFiles
            If SelectedFiles.Count = 0 Then Throw New Data.DmsUserErrorMessageException("No selected file")
            If InfoBox.InformationBox.Show("Möchten Sie wirklich folgende Dateien löschen?" & System.Environment.NewLine & System.Environment.NewLine & Strings.Join(SelectedFiles.ConvertAll(Of String)(Function(item) item.Name).ToArray, System.Environment.NewLine), title:="Remote DMS - Löschen von Dateien", buttons:=InfoBox.InformationBoxButtons.YesNoCancel, icon:=InformationBoxIcon.Question) = InformationBoxResult.Yes Then
                For MyCounter As Integer = 0 To SelectedFiles.Count - 1
                    Me.DmsProvider.DeleteRemoteItem(SelectedFiles(MyCounter))
                Next
            End If
            Me.RefreshFilesList()
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripButtonCopyFile_Click(sender As Object, e As EventArgs) Handles ToolStripButtonCopyFile.Click
        Try
            Throw New NotImplementedException
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripButtonRenameFile_Click(sender As Object, e As EventArgs) Handles ToolStripButtonRenameFile.Click
        Try
            Throw New NotImplementedException
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripButtonMoveFile_Click(sender As Object, e As EventArgs) Handles ToolStripButtonMoveFile.Click
        Try
            Throw New NotImplementedException
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ButtonClose_Click(sender As Object, e As EventArgs) Handles ButtonClose.Click
        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub

    Private Sub ToolStripButtonSharingsFolder_Click(sender As Object, e As EventArgs) Handles ToolStripButtonSharingsFolder.Click
        Try
            If CurrentSelectedFolder() Is Nothing OrElse CurrentSelectedFolder.ItemType = DmsResourceItem.ItemTypes.Root Then Throw New Data.DmsUserErrorMessageException("Sharing für Root-Ordner nicht unterstützt")
            Dim DmsShareForm As New DmsItemSharings()
            DmsShareForm.DmsItem = CurrentSelectedFolder()
            DmsShareForm.DmsProvider = Me.DmsProvider
            DmsShareForm.Show()
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripButtonSharingsFile_Click(sender As Object, e As EventArgs) Handles ToolStripButtonSharingsFile.Click
        Try
            Dim SelectedFiles As List(Of DmsResourceItem) = Me.CurrentSelectedFiles
            If SelectedFiles.Count = 0 Then Throw New Data.DmsUserErrorMessageException("No selected file")
            For MyCounter As Integer = 0 To SelectedFiles.Count - 1
                Dim DmsShareForm As New DmsItemSharings()
                DmsShareForm.DmsItem = SelectedFiles(MyCounter)
                DmsShareForm.DmsProvider = Me.DmsProvider
                DmsShareForm.Show()
            Next
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripButtonPropertiesFile_Click(sender As Object, e As EventArgs) Handles ToolStripButtonPropertiesFile.Click
        Try
            Dim SelectedFiles As List(Of DmsResourceItem) = Me.CurrentSelectedFiles
            If SelectedFiles.Count <> 1 Then Throw New DmsUserInputInvalidException("Es muss exakt 1 Eintrag ausgewählt sein")
            Dim SelectedFile As DmsResourceItem = SelectedFiles(0)
            InfoBox.InformationBox.Show(Me.PropertiesDetails(SelectedFile), title:="Eigenschaften " & SelectedFile.Name, buttons:=InfoBox.InformationBoxButtons.OK, icon:=InformationBoxIcon.Information)
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As DmsUserInputInvalidException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function PropertiesDetails(dmsItem As DmsResourceItem) As String
        Dim Message As String = "Full path: " & dmsItem.FullName & System.Environment.NewLine &
                "Parent Folder: " & dmsItem.Folder & System.Environment.NewLine &
                "Parent Collection: " & dmsItem.Collection & System.Environment.NewLine
        If dmsItem.ItemType = DmsResourceItem.ItemTypes.File Then
            Message &= "Datei-Größe: " & dmsItem.ContentLength.ToString("#,##0") & " Bytes" & System.Environment.NewLine
        End If
        Message &= System.Environment.NewLine &
                "Details" & System.Environment.NewLine &
                "Besitzer: " & dmsItem.ExtendedInfosOwner.ToString & System.Environment.NewLine &
                "Versions-Nr.: " & dmsItem.ExtendedInfosVersion & System.Environment.NewLine
        If dmsItem.ExtendedInfosVersionDateLocalTime.HasValue Then Message &= "Versions-Datum: " & dmsItem.ExtendedInfosVersionDateLocalTime & System.Environment.NewLine
        If dmsItem.ExtendedInfosArchivedDateLocalTime.HasValue Then Message &= "Archivierungs-Datum: " & dmsItem.ExtendedInfosArchivedDateLocalTime & System.Environment.NewLine
        If dmsItem.ExtendedInfosLockedByUser.ToString <> Nothing Then
            Message &= "Gesperrt durch: " & dmsItem.ExtendedInfosLockedByUser.ToString & System.Environment.NewLine
        End If
        If dmsItem.ExtendedInfosLocks IsNot Nothing AndAlso dmsItem.ExtendedInfosLocks.Count <> 0 Then
            Message &= "Sperrungen: " & System.Environment.NewLine
            For Each Lock As String In dmsItem.ExtendedInfosLocks
                Message &= "- " & Lock & System.Environment.NewLine
            Next
        End If
        If dmsItem.ExtendedInfosCollisionDetected Then
            Message &= "WARNUNG: Kollission mit anderer Datei gleichen Namens erkannt" & System.Environment.NewLine
        End If
        Message &= System.Environment.NewLine &
                "Freigaben" & System.Environment.NewLine &
                "IsShared: " & dmsItem.ExtendedInfosIsShared.ToString & System.Environment.NewLine
        If dmsItem.ItemType = DmsResourceItem.ItemTypes.Collection Then
            Message &= "IsPublicCollection: " & dmsItem.ExtendedInfosIsPublicCollection.ToString & System.Environment.NewLine
        End If
        Message &= "HasHiddenGroupSharings: " & dmsItem.ExtendedInfosHasHiddenGroupSharings & System.Environment.NewLine &
                "HasGroupSharings: " & dmsItem.ExtendedInfosHasGroupSharings & System.Environment.NewLine
        If dmsItem.ExtendedInfosGroupSharings IsNot Nothing Then
            For Each Sharing As DmsShareForGroup In dmsItem.ExtendedInfosGroupSharings
                Message &= "- " & Sharing.ToString & System.Environment.NewLine
            Next
        End If
        Message &= "HasHiddenUserSharings: " & dmsItem.ExtendedInfosHasHiddenUserSharings & System.Environment.NewLine &
                "HasUserSharings: " & dmsItem.ExtendedInfosHasUserSharings & System.Environment.NewLine
        If dmsItem.ExtendedInfosUserSharings IsNot Nothing Then
            For Each Sharing As DmsShareForUser In dmsItem.ExtendedInfosUserSharings
                Message &= "- " & Sharing.ToString & System.Environment.NewLine
            Next
        End If
        If dmsItem.ExtendedInfosLinks IsNot Nothing Then
            For Each ViewLink As DmsLink In dmsItem.ExtendedInfosLinks
                If ViewLink.ID <> Nothing Then
                    ViewLink.Refresh()
                    Message &= "Link: " & ViewLink.ID & System.Environment.NewLine &
                "- WebUrl: " & ViewLink.WebUrl & System.Environment.NewLine &
                "- DownloadUrl: " & ViewLink.DownloadUrl & System.Environment.NewLine &
                "- Password: " & ViewLink.Password & System.Environment.NewLine &
                "- ExpiresOn: " & ViewLink.ExpiryDateLocalTime & System.Environment.NewLine &
                "- MaxDownloads: " & ViewLink.MaxDownloads & System.Environment.NewLine &
                "- MaxBytes: " & ViewLink.MaxBytes & System.Environment.NewLine &
                "- MaxUploads: " & ViewLink.MaxUploads & System.Environment.NewLine &
                "- UploadsCount: " & ViewLink.UploadsCount & System.Environment.NewLine &
                "- UploadedBytes: " & ViewLink.UploadedBytes & System.Environment.NewLine &
                "- AllowView: " & ViewLink.AllowView & System.Environment.NewLine &
                "- AllowDownload: " & ViewLink.AllowDownload & System.Environment.NewLine &
                "- AllowUpload: " & ViewLink.AllowUpload & System.Environment.NewLine &
                "- AllowEdit: " & ViewLink.AllowEdit & System.Environment.NewLine &
                "- AllowDelete: " & ViewLink.AllowDelete & System.Environment.NewLine
                End If
            Next
        End If
        Message &= System.Environment.NewLine &
                "Erweiterte Infos" & System.Environment.NewLine &
                "Letzte Änderung: " & dmsItem.LastModificationOnLocalTime.ToString & System.Environment.NewLine &
                "IsIntelligent: " & dmsItem.ExtendedInfosIsIntelligent.ToString & System.Environment.NewLine &
                "IsAuditing: " & dmsItem.ExtendedInfosIsAuditing.ToString & System.Environment.NewLine &
                "Hash/ETag: " & dmsItem.ProviderSpecificHashOrETag & System.Environment.NewLine &
                "File ID: " & dmsItem.ExtendedInfosFileID & System.Environment.NewLine &
                "Folder ID: " & dmsItem.ExtendedInfosFolderID & System.Environment.NewLine &
                "Collection ID: " & dmsItem.ExtendedInfosCollectionID & System.Environment.NewLine &
                "Assigned Collection ID: " & dmsItem.ExtendedInfosAssignedCollectionID & System.Environment.NewLine &
                "Assigned Folder ID: " & dmsItem.ExtendedInfosAssignedFolderID & System.Environment.NewLine
        If dmsItem.ExtendedInfosReferencedFromCollectionIDs IsNot Nothing Then
            Message &= "Referenced from Collecton IDs" & System.Environment.NewLine
            For Each item As String In dmsItem.ExtendedInfosReferencedFromCollectionIDs
                Message &= "- " & Me.LookupCollectionNameForUI(item, dmsItem) & System.Environment.NewLine
            Next
        End If
        If dmsItem.ExtendedInfosReferencedFromFolderIDs IsNot Nothing Then
            Message &= "Referenced from Folder IDs" & System.Environment.NewLine
            For Each item As String In dmsItem.ExtendedInfosReferencedFromFolderIDs
                Message &= "- " & Me.LookupFolderNameForUI(item, dmsItem) & System.Environment.NewLine
            Next
        End If
        Return Message
    End Function

    Private Function LookupCollectionNameForUI(id As String, currentDmsItem As DmsResourceItem) As String
        Select Case Me.DmsProvider.DmsProviderID
            Case Providers.BaseDmsProvider.DmsProviders.CenterDevice, Providers.BaseDmsProvider.DmsProviders.Scopevisio
                If currentDmsItem IsNot Nothing AndAlso currentDmsItem.ExtendedInfosCollectionID = id Then
                    Return currentDmsItem.FullName & " (" & id & ")"
                Else
                    Return Me.DmsProvider.FindCollectionById(id).Name & " (" & id & ")"
                End If
            Case Else
                Return id
        End Select
    End Function

    Private Function LookupFolderNameForUI(id As String, currentDmsItem As DmsResourceItem) As String
        Select Case Me.DmsProvider.DmsProviderID
            Case Providers.BaseDmsProvider.DmsProviders.CenterDevice, Providers.BaseDmsProvider.DmsProviders.Scopevisio
                If currentDmsItem IsNot Nothing AndAlso currentDmsItem.ExtendedInfosCollectionID = id Then
                    Return currentDmsItem.FullName & " (" & id & ")"
                Else
                    Return Me.DmsProvider.FindFolderById(id).Name & " (" & id & ")"
                End If
            Case Else
                Return id
        End Select
    End Function

    Private Function LookupFileNameForUI(id As String) As String
        Select Case Me.DmsProvider.DmsProviderID
            Case Providers.BaseDmsProvider.DmsProviders.CenterDevice, Providers.BaseDmsProvider.DmsProviders.Scopevisio
                Return Me.DmsProvider.FindFileById(id).Name & " (" & id & ")"
            Case Else
                Return id
        End Select
    End Function

    Private Sub ToolStripButtonPropertiesFolder_Click(sender As Object, e As EventArgs) Handles ToolStripButtonPropertiesFolder.Click
        Try
            Dim SelectedFolder As DmsResourceItem = CType(Me.TreeViewDmsFolders.SelectedNode.Tag, NodeTagData).DmsResourceItem
            If SelectedFolder IsNot Nothing Then
                InfoBox.InformationBox.Show(Me.PropertiesDetails(SelectedFolder), title:="Eigenschaften " & SelectedFolder.Name, buttons:=InfoBox.InformationBoxButtons.OK, icon:=InformationBoxIcon.Information)
            Else
                InfoBox.InformationBox.Show("Root", title:="Eigenschaften /", buttons:=InfoBox.InformationBoxButtons.OK, icon:=InformationBoxIcon.Information)
            End If
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As DmsUserInputInvalidException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripButtonRefreshFilesList_Click(sender As Object, e As EventArgs) Handles ToolStripButtonRefreshFilesList.Click
        Try
            Me.RefreshFilesList()
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As DmsUserInputInvalidException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ContextMenuStripFolder_Opening(sender As Object, e As CancelEventArgs) Handles ContextMenuStripFolder.Opening
        If Me.CurrentSelectedFolderNode Is Nothing Then
            e.Cancel = True
            Return
        End If
        UITools.SwitchSeparatorLinesVisibility(Me.ContextMenuStripFolder.Items)
    End Sub

    Private Sub ToolStripFolderContextButtonNewFolder_Click(sender As Object, e As EventArgs) Handles ToolStripFolderContextButtonNewFolder.Click
        Me.ButtonCreateNewFolder_Click(sender, e)
    End Sub

    Private Sub ToolStripFolderContextButtonCopyFolder_Click(sender As Object, e As EventArgs) Handles ToolStripFolderContextButtonCopyFolder.Click
        Try
            Throw New NotImplementedException
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripFolderContextButtonRenameFolder_Click(sender As Object, e As EventArgs) Handles ToolStripFolderContextButtonRenameFolder.Click
        Try
            Throw New NotImplementedException
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripFolderContextButtonMoveFolder_Click(sender As Object, e As EventArgs) Handles ToolStripFolderContextButtonMoveFolder.Click
        Try
            Throw New NotImplementedException
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripFolderContextButtonDeleteFolder_Click(sender As Object, e As EventArgs) Handles ToolStripFolderContextButtonDeleteFolder.Click
        Try
            Dim SelectedFolderNode As TreeNode = Me.CurrentSelectedFolderNode
            Dim SelectedFolder As DmsResourceItem = Me.CurrentSelectedFolder
            If SelectedFolderNode Is Nothing AndAlso SelectedFolder Is Nothing Then Throw New Data.DmsUserErrorMessageException("No selected folder")
            If SelectedFolderNode IsNot Nothing AndAlso (SelectedFolder Is Nothing OrElse SelectedFolder.ItemType = DmsResourceItem.ItemTypes.Root) Then Throw New Data.DmsUserErrorMessageException("Root folder can't be deleted")
            If InfoBox.InformationBox.Show("Möchten Sie wirklich folgenden Ordner löschen?" & System.Environment.NewLine & System.Environment.NewLine & SelectedFolder.Name, title:="Remote DMS - Löschen von Ordnern", buttons:=InfoBox.InformationBoxButtons.YesNoCancel, icon:=InformationBoxIcon.Question) = InformationBoxResult.Yes Then
                Dim ParentFolderNode As TreeNode = Me.CurrentParentOfSelectedFolderNode
                Dim ParentFolder As DmsResourceItem = Me.CurrentParentOfSelectedFolder
                Me.DmsProvider.DeleteRemoteItem(SelectedFolder)
                If ParentFolderNode IsNot Nothing Then
                    ParentFolderNode.Nodes.Remove(CurrentSelectedFolderNode)
                End If
            End If
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripFolderContextButtonShareFolder_Click(sender As Object, e As EventArgs) Handles ToolStripFolderContextButtonShareFolder.Click
        Me.ToolStripButtonSharingsFolder_Click(sender, e)
    End Sub

    Private Sub ToolStripFolderContextButtonRefreshFilesList_Click(sender As Object, e As EventArgs) Handles ToolStripFolderContextButtonRefreshFilesList.Click
        Me.ToolStripButtonRefreshFilesList_Click(sender, e)
    End Sub

    Private Sub ToolStripFolderContextButtonProperties_Click(sender As Object, e As EventArgs) Handles ToolStripFolderContextButtonProperties.Click
        Me.ToolStripButtonPropertiesFolder_Click(sender, e)
    End Sub

    Private Sub ToolStripFileContextButtonUploadFile_Click(sender As Object, e As EventArgs) Handles ToolStripFileContextButtonUploadFile.Click
        Me.ToolStripButtonUploadFile_Click(sender, e)
    End Sub

    Private Sub ToolStripFileContextButtonDownloadFile_Click(sender As Object, e As EventArgs) Handles ToolStripFileContextButtonDownloadFile.Click
        Me.ToolStripButtonDownloadFile_Click(sender, e)
    End Sub

    Private Sub ToolStripFileContextButtonCopyFile_Click(sender As Object, e As EventArgs) Handles ToolStripFileContextButtonCopyFile.Click
        Me.ToolStripButtonCopyFile_Click(sender, e)
    End Sub

    Private Sub ToolStripFileContextButtonRenameFile_Click(sender As Object, e As EventArgs) Handles ToolStripFileContextButtonRenameFile.Click
        Me.ToolStripButtonRenameFile_Click(sender, e)
    End Sub

    Private Sub ToolStripFileContextButtonMoveFile_Click(sender As Object, e As EventArgs) Handles ToolStripFileContextButtonMoveFile.Click
        Me.ToolStripButtonMoveFile_Click(sender, e)
    End Sub

    Private Sub ToolStripFileContextButtonDeleteFile_Click(sender As Object, e As EventArgs) Handles ToolStripFileContextButtonDeleteFile.Click
        Me.ToolStripButtonDeleteFile_Click(sender, e)
    End Sub

    Private Sub ToolStripFileContextButtonShareFile_Click(sender As Object, e As EventArgs) Handles ToolStripFileContextButtonShareFile.Click
        Me.ToolStripButtonSharingsFile_Click(sender, e)
    End Sub

    Private Sub ToolStripFileContextButtonProperties_Click(sender As Object, e As EventArgs) Handles ToolStripFileContextButtonProperties.Click
        Me.ToolStripButtonPropertiesFile_Click(sender, e)
    End Sub

    Private Sub ContextMenuStripFile_Opening(sender As Object, e As CancelEventArgs) Handles ContextMenuStripFile.Opening
        If Me.CurrentSelectedFiles.Count = 0 Then
            e.Cancel = True
            Return
        End If
        UITools.SwitchSeparatorLinesVisibility(Me.ContextMenuStripFile.Items)
    End Sub

    ''' <summary>
    ''' Select the node under the mouse if not yet selected
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    Private Sub TreeViewDmsFolders_MouseUp(sender As Object, e As MouseEventArgs) Handles TreeViewDmsFolders.MouseUp
        If e.Button = MouseButtons.Right Then
            Me.TreeViewDmsFolders.SelectedNode = Me.TreeViewDmsFolders.GetNodeAt(e.X, e.Y)
            If Me.TreeViewDmsFolders.GetNodeAt(e.X, e.Y) Is Nothing Then
                Me.ContextMenuStripFolder.Close()
            End If
        End If
    End Sub

    ''' <summary>
    ''' Select the item under the mouse if not yet selected
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    Private Sub ListViewDmsFiles_MouseUp(sender As Object, e As MouseEventArgs) Handles ListViewDmsFiles.MouseUp
        If e.Button = MouseButtons.Right Then
            Dim SelectedOverItem As ListViewItem = Me.ListViewDmsFiles.GetItemAt(e.X, e.Y)
            If SelectedOverItem IsNot Nothing Then
                If Me.CurrentSelectedFiles.Contains(CType(SelectedOverItem.Tag, DmsResourceItem)) = False Then
                    Me.ListViewDmsFiles.SelectedItems.Clear()
                    SelectedOverItem.Selected = True
                End If
            End If
        End If
    End Sub

    ''' <summary>
    ''' Handle ESC key to cancel dialog
    ''' </summary>
    ''' <param name="keyData"></param>
    ''' <returns></returns>
    Protected Overrides Function ProcessDialogKey(keyData As Keys) As Boolean
        If Form.ModifierKeys = Keys.None AndAlso keyData = Keys.Escape Then
            Me.Close()
            Return True
        End If
        Return MyBase.ProcessDialogKey(keyData)
    End Function

    ''' <summary>
    ''' Download to a temporary location and open this file
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    Private Sub ToolStripButtonOpenFile_Click(sender As Object, e As EventArgs) Handles ToolStripButtonOpenFile.Click
        Try
            Dim SelectedFiles As List(Of DmsResourceItem) = Me.CurrentSelectedFiles
            If SelectedFiles.Count = 0 Then
                System.Windows.Forms.MessageBox.Show(Me, "Keine Datei(en) ausgewählt", Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            Else
                For MyCounter As Integer = 0 To SelectedFiles.Count - 1
                    Dim TargetFile As New CompuMaster.IO.TemporaryFile(CompuMaster.IO.TemporaryFile.TempFileCleanupEvent.OnApplicationExit,
                                                                       System.IO.Path.GetRandomFileName,
                                                                       System.IO.Path.GetFileNameWithoutExtension(SelectedFiles(MyCounter).Name),
                                                                       System.IO.Path.GetExtension(SelectedFiles(MyCounter).Name))
                    Me.DmsProvider.DownloadFile(SelectedFiles(MyCounter).FullName, TargetFile.FilePath, SelectedFiles(MyCounter).LastModificationOnLocalTime)
                    System.IO.File.SetAttributes(TargetFile.FilePath, System.IO.FileAttributes.ReadOnly Or System.IO.FileAttributes.Temporary)
                    OpenDownloadedFileItem.Invoke(TargetFile)
                Next
            End If
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ''' <summary>
    ''' Open/preview a remote file, downloaded into a temporary file
    ''' </summary>
    ''' <param name="localTemporaryFile">The remote file downloaded into a temporary file on local disk</param>
    ''' <returns>Process of started file</returns>
    Public Delegate Function OpenDownloadedFileAction(localTemporaryFile As CompuMaster.IO.TemporaryFile) As System.Diagnostics.Process

    Public Property OpenDownloadedFileItem As OpenDownloadedFileAction = AddressOf _OpenDownloadedFile_Default

    Private Function _OpenDownloadedFile_Default(localTemporaryFile As CompuMaster.IO.TemporaryFile) As System.Diagnostics.Process
        Return System.Diagnostics.Process.Start(New ProcessStartInfo(localTemporaryFile.FilePath) With {.UseShellExecute = True})
    End Function

    Private Sub DmsBrowser_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        Me.CleanupTemporaryFiles()
    End Sub

    ''' <summary>
    ''' Cleanup previewed/downloaded temporary files
    ''' </summary>
    ''' <remarks>Run a CleanupOnApplicationExit for component CompuMaster.IO.TemporaryFile</remarks>
    Public Overridable Sub CleanupTemporaryFiles()
        CompuMaster.IO.TemporaryFile.CleanupOnApplicationExit()
    End Sub

    Private Sub ToolStripFileContextButtonOpenPreviewFile_Click(sender As Object, e As EventArgs) Handles ToolStripFileContextButtonOpenPreviewFile.Click
        Me.ToolStripButtonOpenFile_Click(sender, e)
    End Sub

End Class