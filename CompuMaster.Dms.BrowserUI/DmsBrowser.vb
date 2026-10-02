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

    Private Const DefaultDpi As Integer = 96
    Private Const MaximumImageListDimension As Integer = 256
    Private Const TreeIconLogicalSize As Integer = 24
    Private Const FileIconLogicalSize As Integer = 24

    ''' <summary>
    ''' A browser for DMS systems
    ''' </summary>
    ''' <remarks>Intended for designer only; please use another constructor overload</remarks>
    <Obsolete("Use overload instead")>
    <System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>
    Public Sub New()
        MyBase.New()

        ' Dieser Aufruf ist für den Designer erforderlich.
        InitializeComponent()
        ConfigureIconImageListsForDpi(Me.DeviceDpi)
        ApplyLocalizedText()
    End Sub

    Friend Sub New(dmsProvider As BaseDmsProvider)
#Disable Warning BC40000
        Me.New()
#Enable Warning BC40000
        If dmsProvider Is Nothing Then Throw New ArgumentNullException(NameOf(dmsProvider))
        Me._DmsProviderOverride = dmsProvider
    End Sub

    ''' <summary>
    ''' A browser for DMS systems
    ''' </summary>
    ''' <param name="dmsProfile"></param>
    ''' <param name="formTitle">The form title</param>
    ''' <param name="formIcon">The form icon, or <see langword="Nothing"/> to use the provider-independent default icon.</param>
    ''' <param name="initialRootFolder">The remote folder which shall be treated as root folder in browser dialog</param>
    ''' <param name="selectedFolder">The initially selected sub folder</param>
    ''' <param name="browseMode">Mode setting for browser dialog</param>
    ''' <param name="allowedActions">Allowed actions in browser dialog</param>
    ''' <param name="dialogOperationMode">Operational settings for browser dialog</param>
    ''' <param name="localParentMustFolder">Ensure that downloads/uploads from/to local system are always sub elements of this folder</param>
    ''' <param name="localDefaultFolderDownloads">Default downloads folder on local system</param>
    ''' <param name="localDefaultFolderUploads">Default uploads folder on local system</param>
    Public Sub New(dmsProfile As CompuMaster.Dms.Data.IDmsLoginProfile, formTitle As String, formIcon As System.Drawing.Icon, initialRootFolder As String, selectedFolder As String, browseMode As BrowseModes, allowedActions As FileOrFolderActions, dialogOperationMode As DialogOperationModes, localParentMustFolder As String, localDefaultFolderDownloads As String, localDefaultFolderUploads As String)
#Disable Warning BC40000
        Me.New()
#Enable Warning BC40000
        Me.DmsProfile = dmsProfile
        Me.ConfigureBrowser(formTitle, formIcon, initialRootFolder, selectedFolder, browseMode, allowedActions, dialogOperationMode, localParentMustFolder, localDefaultFolderDownloads, localDefaultFolderUploads)
    End Sub

    ''' <summary>Creates a browser that opens the default or a specified DMS instance without a startup dialog.</summary>
    ''' <param name="dmsProfile">The login profile used to authorize the provider.</param>
    ''' <param name="remoteInstance">The startup instance and optional method for later switching.</param>
    ''' <param name="formTitle">The form title.</param>
    ''' <param name="formIcon">The form icon, or <see langword="Nothing"/> to use the provider-independent default icon.</param>
    ''' <param name="initialRootFolder">The remote folder treated as the browser root.</param>
    ''' <param name="selectedFolder">The initially selected folder.</param>
    ''' <param name="browseMode">The browser mode.</param>
    ''' <param name="allowedActions">The permitted browser actions.</param>
    ''' <param name="dialogOperationMode">The dialog operation mode.</param>
    ''' <param name="localParentMustFolder">The parent required for local transfers.</param>
    ''' <param name="localDefaultFolderDownloads">The default local download folder.</param>
    ''' <param name="localDefaultFolderUploads">The default local upload folder.</param>
    ''' <exception cref="NotSupportedException">The selector requests a startup dialog; resolve it before creating the browser.</exception>
    Public Sub New(dmsProfile As IDmsLoginProfile, remoteInstance As RemoteInstanceSelector, formTitle As String, formIcon As Icon, initialRootFolder As String, selectedFolder As String, browseMode As BrowseModes, allowedActions As FileOrFolderActions, dialogOperationMode As DialogOperationModes, localParentMustFolder As String, localDefaultFolderDownloads As String, localDefaultFolderUploads As String)
#Disable Warning BC40000
        Me.New()
#Enable Warning BC40000
        If dmsProfile Is Nothing Then Throw New ArgumentNullException(NameOf(dmsProfile))
        If remoteInstance Is Nothing Then Throw New ArgumentNullException(NameOf(remoteInstance))
        If remoteInstance.SelectedStartupInstance = RemoteInstanceSelector.StartupInstance.SelectionDialog Then
            Throw New NotSupportedException("Resolve the startup selection before creating the browser.")
        End If
        Me.DmsProfile = dmsProfile
        Me.InstanceSelectionDialog = remoteInstance.SelectionDialog
        remoteInstance.SelectProviderInstance(Me.DmsProvider, Nothing)
        Me.ConfigureBrowser(formTitle, formIcon, initialRootFolder, selectedFolder, browseMode, allowedActions, dialogOperationMode, localParentMustFolder, localDefaultFolderDownloads, localDefaultFolderUploads)
    End Sub

    ''' <summary>Creates a browser for an already authorized provider with its startup instance already selected.</summary>
    ''' <param name="dmsProvider">The authorized provider bound to the instance to browse.</param>
    ''' <param name="selectionDialog">The optional method for later switching.</param>
    ''' <param name="formTitle">The form title.</param>
    ''' <param name="formIcon">The form icon, or <see langword="Nothing"/> to use the provider-independent default icon.</param>
    ''' <param name="initialRootFolder">The remote folder treated as the browser root.</param>
    ''' <param name="selectedFolder">The initially selected folder.</param>
    ''' <param name="browseMode">The browser mode.</param>
    ''' <param name="allowedActions">The permitted browser actions.</param>
    ''' <param name="dialogOperationMode">The dialog operation mode.</param>
    ''' <param name="localParentMustFolder">The parent required for local transfers.</param>
    ''' <param name="localDefaultFolderDownloads">The default local download folder.</param>
    ''' <param name="localDefaultFolderUploads">The default local upload folder.</param>
    Public Sub New(dmsProvider As BaseDmsProvider, selectionDialog As RemoteInstanceSelector.SelectionDialogMethod, formTitle As String, formIcon As Icon, initialRootFolder As String, selectedFolder As String, browseMode As BrowseModes, allowedActions As FileOrFolderActions, dialogOperationMode As DialogOperationModes, localParentMustFolder As String, localDefaultFolderDownloads As String, localDefaultFolderUploads As String)
#Disable Warning BC40000
        Me.New()
#Enable Warning BC40000
        If dmsProvider Is Nothing Then Throw New ArgumentNullException(NameOf(dmsProvider))
        Me._DmsProviderOverride = dmsProvider
        Me.InstanceSelectionDialog = selectionDialog
        Me.ConfigureBrowser(formTitle, formIcon, initialRootFolder, selectedFolder, browseMode, allowedActions, dialogOperationMode, localParentMustFolder, localDefaultFolderDownloads, localDefaultFolderUploads)
    End Sub

    Private Sub ConfigureBrowser(formTitle As String, formIcon As Icon, initialRootFolder As String, selectedFolder As String, browseMode As BrowseModes, allowedActions As FileOrFolderActions, dialogOperationMode As DialogOperationModes, localParentMustFolder As String, localDefaultFolderDownloads As String, localDefaultFolderUploads As String)
        Me.Text = formTitle
        If formIcon IsNot Nothing Then Me.Icon = formIcon
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

    Private Sub ApplyLocalizedText()
        Me.Text = UiStrings.GetText("BrowseTitle")
        Me.ButtonCancel.Text = UiStrings.GetText("ActionCancel")
        Me.ButtonOkay.Text = UiStrings.GetText("ActionOkay")
        Me.ButtonClose.Text = UiStrings.GetText("ActionClose")
        Me.ButtonCreateNewFolder.Text = UiStrings.GetText("ActionCreateFolder")
        Me.ButtonShowFiles.Text = UiStrings.GetText("ActionShowFiles")
        Me.ColumnHeaderFileName.Text = UiStrings.GetText("ColumnName")
        Me.ColumnHeaderSize.Text = UiStrings.GetText("ColumnSize")
        Me.ColumnHeaderLastModifiedOn.Text = UiStrings.GetText("ColumnLastModification")
        Me.ToolStripFolderContextButtonNewFolder.Text = UiStrings.GetText("ActionCreateFolder")
        Me.ToolStripFolderContextButtonCopyFolder.Text = UiStrings.GetText("ActionCopy")
        Me.ToolStripFolderContextButtonRenameFolder.Text = UiStrings.GetText("ActionRename")
        Me.ToolStripFolderContextButtonMoveFolder.Text = UiStrings.GetText("ActionMove")
        Me.ToolStripFolderContextButtonDeleteFolder.Text = UiStrings.GetText("ActionDelete")
        Me.ToolStripFolderContextButtonShareFolder.Text = UiStrings.GetText("ActionSharings")
        Me.ToolStripFolderContextButtonRefreshFilesList.Text = UiStrings.GetText("ActionRefreshFiles")
        Me.ToolStripFolderContextButtonProperties.Text = UiStrings.GetText("ActionProperties")
        Me.ToolStripFileContextButtonUploadFile.Text = UiStrings.GetText("ActionUpload")
        Me.ToolStripFileContextButtonDownloadFile.Text = UiStrings.GetText("ActionDownload")
        Me.ToolStripFileContextButtonOpenPreviewFile.Text = UiStrings.GetText("ActionOpen")
        Me.ToolStripFileContextButtonCopyFile.Text = UiStrings.GetText("ActionCopy")
        Me.ToolStripFileContextButtonRenameFile.Text = UiStrings.GetText("ActionRename")
        Me.ToolStripFileContextButtonMoveFile.Text = UiStrings.GetText("ActionMove")
        Me.ToolStripFileContextButtonDeleteFile.Text = UiStrings.GetText("ActionDelete")
        Me.ToolStripFileContextButtonShareFile.Text = UiStrings.GetText("ActionSharings")
        Me.ToolStripFileContextButtonProperties.Text = UiStrings.GetText("ActionProperties")
        Me.ToolStripButtonUploadFile.Text = UiStrings.GetText("ActionUpload")
        Me.ToolStripButtonDownloadFile.Text = UiStrings.GetText("ActionDownload")
        Me.ToolStripButtonOpenFile.Text = UiStrings.GetText("ActionOpen")
        Me.ToolStripButtonDeleteFile.Text = UiStrings.GetText("ActionDelete")
        Me.ToolStripButtonCopyFile.Text = UiStrings.GetText("ActionCopy")
        Me.ToolStripButtonRenameFile.Text = UiStrings.GetText("ActionRename")
        Me.ToolStripButtonMoveFile.Text = UiStrings.GetText("ActionMove")
        Me.ToolStripButtonSharingsFile.Text = UiStrings.GetText("ActionFileSharings")
        Me.ToolStripButtonSharingsFolder.Text = UiStrings.GetText("ActionFolderSharings")
        Me.ToolStripButtonPropertiesFile.Text = UiStrings.GetText("FileProperties")
        Me.ToolStripButtonPropertiesFolder.Text = UiStrings.GetText("FolderProperties")
        Me.ToolStripButtonRefreshFilesList.Text = UiStrings.GetText("ActionRefreshFiles")
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
            ElseIf Me.DmsProfile Is Nothing AndAlso Me._DmsProviderOverride Is Nothing Then
                Return True
            Else
                Return False
            End If
        End Get
    End Property

    Public Property DmsProfile As CompuMaster.Dms.Data.IDmsLoginProfile

    ''' <summary>
    ''' Gets or sets a value indicating whether the browser offers DMS instance selection when the provider supports it.
    ''' </summary>
    ''' <remarks>The default is <see langword="False"/> to preserve the existing browser behavior.</remarks>
    Public Property EnableDmsInstanceSelection As Boolean

    ''' <summary>
    ''' Gets or sets the instance identifier to select without prompting when instance selection is enabled.
    ''' </summary>
    ''' <remarks>Leave this value empty to prompt when multiple instances are available.</remarks>
    Public Property PreselectedDmsInstanceID As String

    Private InstanceButton As Button
    Private InstanceSelectionDialog As RemoteInstanceSelector.SelectionDialogMethod

    Private _DmsProviderOverride As CompuMaster.Dms.Providers.BaseDmsProvider
    Public ReadOnly Property DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider
        Get
            If Me._DmsProviderOverride IsNot Nothing Then Return Me._DmsProviderOverride
            Static DmsProviderInstance As CompuMaster.Dms.Providers.BaseDmsProvider
            If DmsProviderInstance Is Nothing Then
                DmsProviderInstance = CompuMaster.Dms.Providers.CreateAuthorizedDmsProviderInstance(DmsProfile)
            End If
            Return DmsProviderInstance
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
        ''' <summary>Allows switching between DMS instances when a selection method and multiple instances are available.</summary>
        AllowSwitchDmsInstance = 128
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
            Me.UpdateFileToolbarLayout()
            If Me.InstanceButton IsNot Nothing OrElse Me.IsHandleCreated Then Me.InitializeDmsInstanceSwitching()
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
            Me.UpdateFileToolbarLayout()
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

    Private Sub ConfigureIconImageListsForDpi(deviceDpi As Integer)
        Dim treeIconSize As Integer = ScaleLogicalPixels(TreeIconLogicalSize, deviceDpi)
        Dim fileIconSize As Integer = ScaleLogicalPixels(FileIconLogicalSize, deviceDpi)
        Dim treeIconSizeChanged As Boolean = Me.ImageListTreeIcons.ImageSize <> New Size(treeIconSize, treeIconSize)
        Dim fileIconSizeChanged As Boolean = Me.ImageListFileIcons.ImageSize <> New Size(fileIconSize, fileIconSize)

        If treeIconSizeChanged Then ConfigureTreeIconImageList(treeIconSize)
        If fileIconSizeChanged Then
            Me._FileIcons = Nothing
            ConfigureFileIconImageList(fileIconSize)
            RefreshCurrentFileIconIndices()
        End If
        Me.TreeViewDmsFolders.ItemHeight = Math.Max(treeIconSize, Me.TreeViewDmsFolders.Font.Height + ScaleLogicalPixels(4, deviceDpi))
    End Sub

    Protected Overrides Sub OnDpiChanged(e As DpiChangedEventArgs)
        MyBase.OnDpiChanged(e)
        ConfigureIconImageListsForDpi(e.DeviceDpiNew)
        Me.UpdateFileToolbarIconSize(e.DeviceDpiNew)
    End Sub

    Private Shared Function ScaleLogicalPixels(logicalPixels As Integer, deviceDpi As Integer) As Integer
        Return Math.Min(MaximumImageListDimension, CInt(Math.Round(CDbl(logicalPixels) * CDbl(deviceDpi) / DefaultDpi, MidpointRounding.AwayFromZero)))
    End Function

    Private Sub ConfigureTreeIconImageList(iconSize As Integer)
        ConfigureImageList(Me.ImageListTreeIcons, iconSize)
        Me.ImageListTreeIcons.Images.Add("iconfinder_Home-ui-ux-mobile-web_4960719.png", Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Home_ui_ux_mobile_web_4960719)
        Me.ImageListTreeIcons.Images.Add("iconfinder_bookmark-ui-ux-mobile-web_4960727.png", Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_bookmark_ui_ux_mobile_web_4960727)
        Me.ImageListTreeIcons.Images.Add("iconfinder_Folder-ui-ux-mobile-web_4960713.png", Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Folder_ui_ux_mobile_web_4960713)
        Me.ImageListTreeIcons.Images.Add("iconfinder_Home-ui-ux-mobile-web_4960719 - Shared.png", Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Home_ui_ux_mobile_web_4960719___Shared)
        Me.ImageListTreeIcons.Images.Add("iconfinder_bookmark-ui-ux-mobile-web_4960727 - Shared.png", Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_bookmark_ui_ux_mobile_web_4960727___Shared)
        Me.ImageListTreeIcons.Images.Add("iconfinder_Folder-ui-ux-mobile-web_4960713 - Shared.png", Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Folder_ui_ux_mobile_web_4960713___Shared)
    End Sub

    Private Sub ConfigureFileIconImageList(iconSize As Integer)
        ConfigureImageList(Me.ImageListFileIcons, iconSize)
        Me.ImageListFileIcons.Images.Add("iconfinder_Home-ui-ux-mobile-web_4960719.png", Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Home_ui_ux_mobile_web_4960719)
        Me.ImageListFileIcons.Images.Add("iconfinder_bookmark-ui-ux-mobile-web_4960727.png", Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_bookmark_ui_ux_mobile_web_4960727)
        Me.ImageListFileIcons.Images.Add("iconfinder_Folder-ui-ux-mobile-web_4960713.png", Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Folder_ui_ux_mobile_web_4960713)
        Me.ImageListFileIcons.Images.Add("iconfinder_Home-ui-ux-mobile-web_4960719 - Shared.png", Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Home_ui_ux_mobile_web_4960719___Shared)
        Me.ImageListFileIcons.Images.Add("iconfinder_bookmark-ui-ux-mobile-web_4960727 - Shared.png", Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_bookmark_ui_ux_mobile_web_4960727___Shared)
        Me.ImageListFileIcons.Images.Add("iconfinder_Folder-ui-ux-mobile-web_4960713 - Shared.png", Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Folder_ui_ux_mobile_web_4960713___Shared)
        Me.ImageListFileIcons.Images.Add("iconfinder_Document-ui-ux-mobile-web-office-microsoftofficeico_4960706.png", Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Document_ui_ux_mobile_web_office_microsoftofficeico_4960706)
        Me.ImageListFileIcons.Images.Add("iconfinder_Document-ui-ux-mobile-web-office-microsoftofficeico_4960706 - Shared.png", Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Document_ui_ux_mobile_web_office_microsoftofficeico_4960706)
    End Sub

    Private Shared Sub ConfigureImageList(imageList As ImageList, iconSize As Integer)
        imageList.Images.Clear()
        imageList.ColorDepth = ColorDepth.Depth32Bit
        imageList.ImageSize = New Size(iconSize, iconSize)
        imageList.TransparentColor = Color.Transparent
    End Sub

    Private Sub RefreshCurrentFileIconIndices()
        Me.ListViewDmsFiles.BeginUpdate()
        Try
            For Each item As ListViewItem In Me.ListViewDmsFiles.Items
                Dim file As DmsResourceItem = TryCast(item.Tag, DmsResourceItem)
                If file IsNot Nothing Then
                    item.ImageIndex = Me.FileIcons.GetSIImageListIndexForFileExtension(GetFileExtension(file.Name), file.ExtendedInfosIsShared)
                End If
            Next
        Finally
            Me.ListViewDmsFiles.EndUpdate()
        End Try
    End Sub

    Private Shared Function GetFileExtension(fileName As String) As String
        Try
            Return System.IO.Path.GetExtension(fileName)
        Catch ex As Exception
            Return ""
        End Try
    End Function

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
    ''' Files must be downloaded into a folder below of this folder (e.g. a customer base directory)
    ''' </summary>
    ''' <returns></returns>
    Public Property LocalParentMustFolder As String

    Private Sub BrowseDmsFolders_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        If Me.IsDesignMode Then Return 'no loading in design mode
        If Me.LocalDefaultFolderDownloads = Nothing Then LocalDefaultFolderDownloads = Me.LocalParentMustFolder
        If Me.LocalDefaultFolderUploads = Nothing Then LocalDefaultFolderUploads = Me.LocalParentMustFolder
        Try
            If Me.EnableDmsInstanceSelection AndAlso Not Me.InitializeDmsInstanceSelection() Then
                Me.DialogResult = DialogResult.Cancel
                Me.Close()
                Return
            End If
            Me.InitializeDmsInstanceSwitching()
            Me.LoadTree()
        Catch ex As CompuMaster.Dms.Data.DirectoryNotFoundException
            MessageBox.Show(Me, ex.Message, UiStrings.Format("DmsFolderNotFound", ex.RemotePath), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Me.DialogResult = DialogResult.Cancel
            Me.Close()
            Return
        Catch ex As Exception
            If System.Diagnostics.Debugger.IsAttached Then
                MessageBox.Show(Me, ex.ToString, UiStrings.GetText("CredentialsOrServerError"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Else
                MessageBox.Show(Me, ex.Message, UiStrings.GetText("CredentialsOrServerError"), MessageBoxButtons.OK, MessageBoxIcon.Error)
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
            MessageBox.Show(Me, UiStrings.Format("InvalidFolderMessage", Me.SelectedFolder), UiStrings.GetText("InvalidFolderTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Friend Function InitializeDmsInstanceSelection() As Boolean
        Dim InstanceProvider As IDmsInstanceProvider = TryCast(Me.DmsProvider, IDmsInstanceProvider)
        If InstanceProvider Is Nothing Then Return True

        Dim Instances As IReadOnlyList(Of DmsInstanceInfo) = InstanceProvider.ListAvailableDmsInstances()
        If Instances.Count = 0 Then Throw New InvalidOperationException(UiStrings.GetText("NoDmsInstances"))

        If Not String.IsNullOrWhiteSpace(Me.PreselectedDmsInstanceID) Then
            InstanceProvider.SelectDmsInstance(Me.PreselectedDmsInstanceID)
        ElseIf Instances.Count > 1 Then
            Using Picker As New DmsInstanceSelectionDialog(Instances, Me.Icon)
                If Picker.ShowDialog(Me) <> DialogResult.OK Then Return False
                InstanceProvider.SelectDmsInstance(Picker.SelectedInstance.ID)
            End Using
        ElseIf InstanceProvider.CurrentDmsInstance Is Nothing OrElse
               Not String.Equals(InstanceProvider.CurrentDmsInstance.ID, Instances(0).ID, StringComparison.Ordinal) Then
            InstanceProvider.SelectDmsInstance(Instances(0).ID)
        End If

        Return True
    End Function

    Friend Sub InitializeDmsInstanceSwitching()
        If (Me.AllowedActions And FileOrFolderActions.AllowSwitchDmsInstance) <> FileOrFolderActions.AllowSwitchDmsInstance OrElse
           Me.InstanceSelectionDialog Is Nothing Then
            If Me.InstanceButton IsNot Nothing Then Me.InstanceButton.Visible = False
            Return
        End If

        Dim instanceProvider As IDmsInstanceProvider = TryCast(Me.DmsProvider, IDmsInstanceProvider)
        If instanceProvider Is Nothing OrElse instanceProvider.ListAvailableDmsInstances().Count <= 1 Then
            If Me.InstanceButton IsNot Nothing Then Me.InstanceButton.Visible = False
            Return
        End If

        If Me.InstanceButton Is Nothing Then
            Me.InstanceButton = New Button With {
                .Name = "ButtonDmsInstance",
                .Text = UiStrings.GetText("ChangeDmsInstance"),
                .AutoEllipsis = True,
                .Location = New Point(Me.ButtonShowFiles.Right + 8, Me.ButtonShowFiles.Top),
                .Size = New Size(250, Me.ButtonCreateNewFolder.Height),
                .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left
            }
            AddHandler Me.InstanceButton.Click, AddressOf Me.ChangeDmsInstance_Click
            Me.Controls.Add(Me.InstanceButton)
        End If
        Me.InstanceButton.Visible = True
        Me.UpdateDmsInstanceButton()
    End Sub

    Private Sub UpdateDmsInstanceButton()
        If Me.InstanceButton Is Nothing Then Return
        Dim InstanceProvider As IDmsInstanceProvider = CType(Me.DmsProvider, IDmsInstanceProvider)
        Dim CurrentInstance As DmsInstanceInfo = InstanceProvider.CurrentDmsInstance
        Me.InstanceButton.Text = If(CurrentInstance Is Nothing, UiStrings.GetText("ChangeDmsInstance"), UiStrings.Format("CurrentDmsInstance", CurrentInstance.DisplayName))
    End Sub

    Friend Sub ChangeDmsInstance_Click(sender As Object, e As EventArgs)
        Dim InstanceChangeStarted As Boolean = False
        Try
            Dim InstanceProvider As IDmsInstanceProvider = CType(Me.DmsProvider, IDmsInstanceProvider)
            Dim Instances As IReadOnlyList(Of DmsInstanceInfo) = InstanceProvider.ListAvailableDmsInstances()
            If Instances.Count <= 1 Then
                Me.InstanceButton.Visible = False
                Return
            End If
            Dim selectedID As String = Me.InstanceSelectionDialog(Me, Instances)
            If selectedID Is Nothing Then Return
            If String.IsNullOrWhiteSpace(selectedID) OrElse
               Not Instances.Any(Function(instance) String.Equals(instance.ID, selectedID, StringComparison.Ordinal)) Then
                Throw New ArgumentOutOfRangeException(NameOf(selectedID), "The selected DMS instance is not available.")
            End If
            Dim CurrentInstance As DmsInstanceInfo = InstanceProvider.CurrentDmsInstance
            If CurrentInstance IsNot Nothing AndAlso String.Equals(CurrentInstance.ID, selectedID, StringComparison.Ordinal) Then Return
            InstanceChangeStarted = True
            InstanceProvider.SelectDmsInstance(selectedID)

            Me.ReloadDmsInstanceView()
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, UiStrings.GetText("ChangeDmsInstanceFailed"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            If InstanceChangeStarted Then Me.Close()
        End Try
    End Sub

    Friend Sub ReloadDmsInstanceView()
        Me.TreeViewDmsFolders.BeginUpdate()
        Try
            Me.ListViewDmsFiles.Items.Clear()
            Me.ListViewDmsFiles.Tag = Nothing
            Me.LastFileListFolderPath = Nothing
            Me.SelectedFolder = Nothing
            Me.LoadTree()
            Me.SelectFolderPath(Nothing)
        Finally
            Me.TreeViewDmsFolders.EndUpdate()
        End Try
        Me.UpdateDmsInstanceButton()
    End Sub

    Friend Sub LoadTree()
        Me.TreeViewDmsFolders.Nodes.Clear()
        If Me.InitialFolder <> Nothing Then
            Dim Folder As DmsResourceItem = Me.DmsProvider.ListRemoteItem(Me.InitialFolder)
            If Folder Is Nothing Then Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(Me.InitialFolder)
            Me.RootNode = Me.TreeViewDmsFolders.Nodes.Add("", Me.InitialFolder)
            Me.RootNode.Tag = New NodeTagData(Folder)
            If Folder.ExtendedInfosHasGroupSharings OrElse Folder.ExtendedInfosHasUserSharings OrElse Folder.ExtendedInfosIsShared OrElse Folder.ExtendedInfosHasLinks Then
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
        Me.TreeViewDmsFolders.Sort()
        Me.RootNode.Expand()
    End Sub

    Private Class NodeTagData
        Public Sub New(dmsResourceItem As DmsResourceItem)
            Me.DmsResourceItem = dmsResourceItem
        End Sub
        Public ChildrenLoaded As Boolean
        Public DmsResourceItem As DmsResourceItem
    End Class

    Private Shared Function HasKnownChildDirectories(item As DmsResourceItem) As Boolean?
        If item Is Nothing Then Return Nothing
        If item.ItemType = DmsResourceItem.ItemTypes.File Then Return False
        If item.ChildDirectoryCount.HasValue Then Return item.ChildDirectoryCount.Value > 0
        Return item.HasChildDirectories
    End Function

    Private Shared Sub AddExpansionPlaceholderIfRequired(node As TreeNode)
        Dim Item As DmsResourceItem = CType(node.Tag, NodeTagData).DmsResourceItem
        Dim HasChildren As Boolean? = HasKnownChildDirectories(Item)
        If Not HasChildren.HasValue OrElse HasChildren.Value Then
            node.Nodes.Add(New TreeNode With {.Tag = Nothing})
        End If
    End Sub

    Private Sub AddDirectoryTreeNode(parentNode As TreeNode, item As DmsResourceItem)
        Dim Node As TreeNode = CreateDirectoryTreeNode(item)
        AddExpansionPlaceholderIfRequired(Node)
        parentNode.Nodes.Add(Node)
    End Sub

    Friend Sub AddTreeChildren(parentNode As TreeNode)
        Dim ParentData As NodeTagData = CType(parentNode.Tag, NodeTagData)
        If ParentData.ChildrenLoaded Then Return

        Dim ParentPath As String = If(ParentData.DmsResourceItem?.FullName, Me.DmsProvider.BrowseInRootFolderName)
        Dim ChildDirectories As List(Of DmsResourceItem) = Me.DmsProvider.ListAllDirectoryItems(ParentPath)
        parentNode.Nodes.Clear()
        For Each ChildDirectory As DmsResourceItem In ChildDirectories
            Dim CollectionAllowed As Boolean = ChildDirectory.ItemType <> DmsResourceItem.ItemTypes.Collection OrElse
                (Me.DmsProvider.SupportsCollections AndAlso
                 (ParentData.DmsResourceItem Is Nothing OrElse ParentData.DmsResourceItem.ItemType = DmsResourceItem.ItemTypes.Collection))
            If CollectionAllowed Then Me.AddDirectoryTreeNode(parentNode, ChildDirectory)
        Next
        ParentData.ChildrenLoaded = True
        UpdateChildDirectoryMetadata(parentNode)
    End Sub

    Private Shared Sub UpdateChildDirectoryMetadata(parentNode As TreeNode)
        Dim ParentData As NodeTagData = CType(parentNode.Tag, NodeTagData)
        If ParentData.DmsResourceItem IsNot Nothing Then
            Dim ChildCount As Integer = parentNode.Nodes.Cast(Of TreeNode)().Count(Function(node) node.Tag IsNot Nothing)
            ParentData.DmsResourceItem.ChildDirectoryCount = ChildCount
            ParentData.DmsResourceItem.HasChildDirectories = ChildCount > 0
        End If
    End Sub

    Private Shared Sub RecordChildDirectoryCreated(parentNode As TreeNode)
        Dim ParentItem As DmsResourceItem = CType(parentNode.Tag, NodeTagData).DmsResourceItem
        If ParentItem Is Nothing Then Return
        If ParentItem.ChildDirectoryCount.HasValue Then ParentItem.ChildDirectoryCount += 1
        ParentItem.HasChildDirectories = True
    End Sub

    Private Shared Sub RecordChildDirectoryDeleted(parentNode As TreeNode)
        Dim ParentData As NodeTagData = CType(parentNode.Tag, NodeTagData)
        If ParentData.DmsResourceItem Is Nothing Then Return
        If ParentData.ChildrenLoaded Then
            UpdateChildDirectoryMetadata(parentNode)
        ElseIf ParentData.DmsResourceItem.ChildDirectoryCount.HasValue Then
            ParentData.DmsResourceItem.ChildDirectoryCount = Math.Max(0, ParentData.DmsResourceItem.ChildDirectoryCount.Value - 1)
            ParentData.DmsResourceItem.HasChildDirectories = ParentData.DmsResourceItem.ChildDirectoryCount.Value > 0
        Else
            ParentData.DmsResourceItem.HasChildDirectories = Nothing
        End If
    End Sub

    Friend Sub RefreshTreeNode(node As TreeNode)
        Dim NodeData As NodeTagData = CType(node.Tag, NodeTagData)
        Dim NodePath As String = If(NodeData.DmsResourceItem?.FullName, Me.DmsProvider.BrowseInRootFolderName)
        Me.DmsProvider.ResetCachesForRemoteItems(NodePath, BaseDmsProvider.SearchItemType.AllItems)
        NodeData.ChildrenLoaded = False
        node.Nodes.Clear()
        Me.AddTreeChildren(node)
        Me.TreeViewDmsFolders.Sort()
    End Sub

    Friend Shared Function CreateDirectoryTreeNode(directory As DmsResourceItem) As TreeNode
        If directory Is Nothing Then Throw New ArgumentNullException(NameOf(directory))

        Dim imageIndex As Integer
        Select Case directory.ItemType
            Case DmsResourceItem.ItemTypes.Collection
                If directory.ExtendedInfosHasGroupSharings OrElse directory.ExtendedInfosHasUserSharings OrElse directory.ExtendedInfosIsShared OrElse directory.ExtendedInfosHasLinks Then
                    imageIndex = 4
                Else
                    imageIndex = 1
                End If
            Case DmsResourceItem.ItemTypes.Folder
                If directory.ExtendedInfosHasGroupSharings OrElse directory.ExtendedInfosHasUserSharings OrElse directory.ExtendedInfosIsShared OrElse directory.ExtendedInfosHasLinks Then
                    imageIndex = 5
                Else
                    imageIndex = 2
                End If
            Case Else
                Throw New ArgumentException("A collection or folder is required.", NameOf(directory))
        End Select

        Return New TreeNode(directory.Name) With {
            .Tag = New NodeTagData(directory),
            .ImageIndex = imageIndex,
            .SelectedImageIndex = imageIndex
        }
    End Function

    Private Sub SelectFolderPath(path As String)
        If path = Nothing Then
            Me.TreeViewDmsFolders.SelectedNode = Me.RootNode
        Else
            Dim FolderHierarchy As New List(Of String)(path.Split(Me.DmsProvider.DirectorySeparator))
            Dim LastMatch As TreeNode = Me.RootNode
            For MyCounter As Integer = 0 To FolderHierarchy.Count - 1
                Me.AddTreeChildren(LastMatch)
                Dim NewMatch As TreeNode = Me.FindChildNode(LastMatch, FolderHierarchy(MyCounter))
                If NewMatch Is Nothing Then
                    Exit For 'Path has been found partially, select as far as possible
                Else
                    LastMatch = NewMatch
                End If
            Next
            Me.TreeViewDmsFolders.SelectedNode = LastMatch
        End If
        Me.SelectedFolder = Me.SelectedFolderPath()
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
        If Me.SuppressSelectionRefresh OrElse Me.TreeViewDmsFolders.SelectedNode Is Nothing Then Return
        Me.SelectedFolder = Me.SelectedFolderPath
        Me.RefreshFilesList()
    End Sub

    Private Sub TreeViewDmsFolders_BeforeExpand(sender As Object, e As TreeViewCancelEventArgs) Handles TreeViewDmsFolders.BeforeExpand
        Try
            Me.AddTreeChildren(e.Node)
        Catch ex As Data.DirectoryNotFoundException
            e.Cancel = True
            Me.ShowMissingDirectory(e.Node, ex.RemotePath)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub TreeViewDmsFolders_DoubleClick(sender As Object, e As EventArgs) Handles TreeViewDmsFolders.DoubleClick
        Me.ButtonOkay_Click(sender, e)
    End Sub

    Private Sub ButtonCreateNewFolder_Click(sender As Object, e As EventArgs) Handles ButtonCreateNewFolder.Click
        Try
            Dim NewFolderName As String = Nothing
            If Not UITools.TryInputBox(UiStrings.Format("NewFolderPrompt", Me.SelectedFolderPath), UiStrings.GetText("NewFolderTitle"), "", NewFolderName) Then Return
            NewFolderName = NewFolderName.Trim()
            If Not IsValidResourceName(NewFolderName, Me.DmsProvider.DirectorySeparator) Then Throw New DmsUserInputInvalidException(UiStrings.GetText("InvalidDestinationName"))
            Dim n As TreeNode = Me.CreateNewDirectoryTreeNode(Me.TreeViewDmsFolders.SelectedNode, NewFolderName)
            Me.TreeViewDmsFolders.SelectedNode = n
            n.TreeView.Focus()
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Friend Function CreateNewDirectoryTreeNode(parentNode As TreeNode, folderName As String) As TreeNode
        Dim ParentData As NodeTagData = CType(parentNode.Tag, NodeTagData)
        If Not ParentData.ChildrenLoaded Then
            If HasKnownChildDirectories(ParentData.DmsResourceItem) = False Then
                ParentData.ChildrenLoaded = True
            Else
                Me.AddTreeChildren(parentNode)
            End If
        End If
        Dim NewFolderPath As String = Me.DmsProvider.CombinePath(CType(parentNode.Tag, NodeTagData).DmsResourceItem?.FullName, folderName)
        Me.DmsProvider.CreateDirectory(NewFolderPath)
        Dim Folder As DmsResourceItem = Me.DmsProvider.ListRemoteItem(NewFolderPath)
        'A newly created directory is empty even when the provider omits child metadata.
        If Not Folder.ChildDirectoryCount.HasValue AndAlso Not Folder.HasChildDirectories.HasValue Then
            Folder.ChildDirectoryCount = 0
            Folder.HasChildDirectories = False
        End If
        Me.AddDirectoryTreeNode(parentNode, Folder)
        RecordChildDirectoryCreated(parentNode)
        Dim NewNode As TreeNode = parentNode.Nodes.Cast(Of TreeNode)().Single(Function(node) node.Tag IsNot Nothing AndAlso CType(node.Tag, NodeTagData).DmsResourceItem Is Folder)
        'The empty child list is already known; expansion must not replace newly inserted children.
        If HasKnownChildDirectories(Folder) = False Then CType(NewNode.Tag, NodeTagData).ChildrenLoaded = True
        Return NewNode
    End Function

    Private Enum FilesListingColumn As Integer
        FileName = 0
        Size = 1
        LastModified = 2
    End Enum
    Private FilesSortOrderColumn As FilesListingColumn = FilesListingColumn.LastModified
    Private FilesSortOrderDirection As ListSortDirection = ListSortDirection.Descending
    Private LastFileListFolderPath As String
    Private SuppressSelectionRefresh As Boolean

    Friend Sub RemoveMissingDirectory(node As TreeNode)
        Me.ListViewDmsFiles.Items.Clear()
        Me.ListViewDmsFiles.Tag = Nothing
        Me.LastFileListFolderPath = Nothing

        If node Is Nothing Then Return
        Dim parent As TreeNode = node.Parent
        Me.SuppressSelectionRefresh = True
        Try
            If parent Is Nothing Then
                Me.TreeViewDmsFolders.Nodes.Clear()
                Me.RootNode = Nothing
                Me.TreeViewDmsFolders.SelectedNode = Nothing
            Else
                node.Remove()
                RecordChildDirectoryDeleted(parent)
                Me.TreeViewDmsFolders.SelectedNode = parent
            End If
            Me.SelectedFolder = Me.SelectedFolderPath()
        Finally
            Me.SuppressSelectionRefresh = False
        End Try
    End Sub

    Private Sub ShowMissingDirectory(node As TreeNode, remotePath As String)
        Me.RemoveMissingDirectory(node)
        MessageBox.Show(Me, UiStrings.Format("DeletedFolderMessage", remotePath), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Function FindDirectoryNodeByPath(remotePath As String) As TreeNode
        If Me.RootNode Is Nothing Then Return Nothing
        Dim rootData As NodeTagData = TryCast(Me.RootNode.Tag, NodeTagData)
        If rootData IsNot Nothing AndAlso rootData.DmsResourceItem Is Nothing AndAlso String.Equals(remotePath, Me.DmsProvider.BrowseInRootFolderName, StringComparison.Ordinal) Then Return Me.RootNode
        Return FindDirectoryNodeByPath(Me.RootNode, remotePath)
    End Function

    Private Shared Function FindDirectoryNodeByPath(node As TreeNode, remotePath As String) As TreeNode
        Dim data As NodeTagData = TryCast(node.Tag, NodeTagData)
        If data IsNot Nothing AndAlso data.DmsResourceItem IsNot Nothing AndAlso String.Equals(data.DmsResourceItem.FullName, remotePath, StringComparison.Ordinal) Then Return node
        For Each child As TreeNode In node.Nodes
            Dim match As TreeNode = FindDirectoryNodeByPath(child, remotePath)
            If match IsNot Nothing Then Return match
        Next
        Return Nothing
    End Function

    Friend Sub RemoveMissingFile(remotePath As String)
        For Each item As ListViewItem In Me.ListViewDmsFiles.Items.Cast(Of ListViewItem)().ToArray()
            Dim file As DmsResourceItem = TryCast(item.Tag, DmsResourceItem)
            If file IsNot Nothing AndAlso String.Equals(file.FullName, remotePath, StringComparison.Ordinal) Then Me.ListViewDmsFiles.Items.Remove(item)
        Next
        Dim cachedFiles As List(Of DmsResourceItem) = TryCast(Me.ListViewDmsFiles.Tag, List(Of DmsResourceItem))
        If cachedFiles IsNot Nothing Then cachedFiles.RemoveAll(Function(file) String.Equals(file.FullName, remotePath, StringComparison.Ordinal))
    End Sub

    Private Sub ShowMissingFile(remotePath As String)
        Me.RemoveMissingFile(remotePath)
        MessageBox.Show(Me, UiStrings.Format("DeletedFileMessage", remotePath), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

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
        Me.ListViewDmsFiles.Tag = Nothing
        If Me.SplitContainer.Panel2Collapsed = False Then
            Dim CurrentFolder As NodeTagData = TryCast(Me.TreeViewDmsFolders.SelectedNode?.Tag, NodeTagData)
            Dim currentFolderPath As String
            If CurrentFolder IsNot Nothing Then
                currentFolderPath = CurrentFolder.DmsResourceItem?.FullName
                If currentFolderPath Is Nothing AndAlso Me.DmsProvider.SupportsFilesInRootFolder Then
                    currentFolderPath = Me.DmsProvider.BrowseInRootFolderName
                End If
                Me.LastFileListFolderPath = currentFolderPath
            Else
                currentFolderPath = Me.LastFileListFolderPath
                If currentFolderPath Is Nothing AndAlso Me.SelectedFolder IsNot Nothing Then
                    currentFolderPath = Me.DmsProvider.CombinePath(Me.InitialFolder, Me.SelectedFolder)
                End If
            End If
            Dim Files As List(Of DmsResourceItem)
            If currentFolderPath IsNot Nothing Then
                Me.DmsProvider.ResetCachesForRemoteItems(currentFolderPath, Providers.BaseDmsProvider.SearchItemType.Files)
                Try
                    Files = Me.DmsProvider.ListAllFileItems(currentFolderPath)
                Catch ex As Data.DirectoryNotFoundException
                    Me.ShowMissingDirectory(Me.FindDirectoryNodeByPath(ex.RemotePath), ex.RemotePath)
                    Return
                End Try
            Else
                Files = New List(Of DmsResourceItem)
            End If
            Me.ListViewDmsFiles.Sorting = SortOrder.None
            Files = ApplyFilesSortOrder(Files)
            Me.ListViewDmsFiles.Tag = Files
            Dim AllFileNameHashes As List(Of Integer) = Files.ConvertAll(Of Integer)(Function(file) file.Name.GetHashCode)
            For Each file As DmsResourceItem In Files
                Dim FileExtension As String = GetFileExtension(file.Name)
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
                    Item.ToolTipText = UiStrings.GetText("DuplicateFileNameConflict")
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
            Return Me.TreeViewDmsFolders.SelectedNode.Parent
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
            If Me.LocalDefaultFolderUploads <> Nothing AndAlso System.IO.Directory.Exists(Me.LocalDefaultFolderUploads) = False Then System.IO.Directory.CreateDirectory(Me.LocalDefaultFolderUploads)
            Dim DialogUserResult As DialogResult = DialogResult.None
            Dim f As New System.Windows.Forms.OpenFileDialog
            f.CheckFileExists = False
            f.InitialDirectory = Me.LocalDefaultFolderUploads
            f.Title = UiStrings.GetText("UploadTitle")
            f.AddExtension = False
            f.CheckFileExists = True
            f.CheckPathExists = True
            f.Multiselect = True
            f.Filter = UiStrings.GetText("AllFilesFilter")
            DialogUserResult = f.ShowDialog()
            If DialogUserResult = DialogResult.OK Then
                If f.FileNames.Length > 0 Then
                    For MyCounter As Integer = 0 To f.FileNames.Length - 1
                        If System.IO.File.Exists(f.FileNames(MyCounter)) = True Then
                            Dim TargetFile As String = Me.DmsProvider.CombinePath(CType(Me.TreeViewDmsFolders.SelectedNode.Tag, NodeTagData).DmsResourceItem.FullName, System.IO.Path.GetFileName(f.FileNames(MyCounter)))
                            Dim LocalFile As String = f.FileNames(MyCounter)
                            Me.RunWithWaitCursor(Sub() Me.DmsProvider.UploadFile(TargetFile, LocalFile))
                        Else
                            System.Windows.Forms.MessageBox.Show(Me, UiStrings.GetText("FileNotFound"), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                        End If
                    Next
                    System.Windows.Forms.MessageBox.Show(Me, UiStrings.GetText("UploadSuccessful"), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Me.RefreshFilesList()
                Else
                    System.Windows.Forms.MessageBox.Show(Me, UiStrings.GetText("NoFileSelected"), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                End If
            Else
                System.Windows.Forms.MessageBox.Show(Me, UiStrings.GetText("OperationCancelled"), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
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
                System.Windows.Forms.MessageBox.Show(Me, UiStrings.GetText("NoFilesSelected"), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            ElseIf SelectedFiles.Count = 1 Then
                If Me.LocalDefaultFolderDownloads <> Nothing AndAlso System.IO.Directory.Exists(Me.LocalDefaultFolderDownloads) = False Then System.IO.Directory.CreateDirectory(Me.LocalDefaultFolderDownloads)
                Dim DialogUserResult As DialogResult = DialogResult.None
                Dim f As New System.Windows.Forms.SaveFileDialog
                f.CheckFileExists = False
                f.InitialDirectory = Me.LocalDefaultFolderDownloads
                f.FileName = SelectedFiles(0).Name
                f.Title = UiStrings.GetText("DownloadTitle")
                f.AddExtension = False
                f.CheckPathExists = True
                f.OverwritePrompt = True
                f.Filter = UiStrings.GetText("AllFilesFilter")
                DialogUserResult = f.ShowDialog()
                If DialogUserResult = DialogResult.OK Then
                    If Me.LocalParentMustFolder = Nothing OrElse f.FileName.StartsWith(Me.LocalParentMustFolder) Then
                        Dim TargetFile As String = f.FileName
                        Me.RunWithWaitCursor(Sub() DownloadFile(Me.DmsProvider, SelectedFiles(0), TargetFile))
                        System.Windows.Forms.MessageBox.Show(Me, UiStrings.GetText("DownloadSuccessful"), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Else
                        System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("OutsideRequiredFolder", Me.LocalParentMustFolder), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                    End If
                Else
                    System.Windows.Forms.MessageBox.Show(Me, UiStrings.GetText("OperationCancelled"), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                End If
            Else
                If Me.LocalDefaultFolderDownloads <> Nothing AndAlso System.IO.Directory.Exists(Me.LocalDefaultFolderDownloads) = False Then System.IO.Directory.CreateDirectory(Me.LocalDefaultFolderDownloads)
                Dim DialogUserResult As DialogResult = DialogResult.None
                Dim f As New System.Windows.Forms.FolderBrowserDialog
                f.SelectedPath = Me.LocalDefaultFolderDownloads
                f.Description = UiStrings.GetText("DownloadTitle")
                f.ShowNewFolderButton = True
                DialogUserResult = f.ShowDialog()
                If DialogUserResult = DialogResult.OK Then
                    If Me.LocalParentMustFolder = Nothing OrElse f.SelectedPath.StartsWith(Me.LocalParentMustFolder) Then
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
                            Select Case MessageBox.Show(Me, UiStrings.GetText("FilesAlreadyExist"), UiStrings.Format("DownloadToTitle", f.SelectedPath), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question)
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
                            Dim RemoteFile As DmsResourceItem = SelectedFiles(MyCounter)
                            Dim TargetFile As String = System.IO.Path.Combine(f.SelectedPath, SelectedFiles(MyCounter).Name)
                            If System.IO.File.Exists(TargetFile) Then
                                If OverwriteLocalFiles Then
                                    Me.RunWithWaitCursor(Sub() DownloadFile(Me.DmsProvider, RemoteFile, TargetFile))
                                End If
                            Else
                                Me.RunWithWaitCursor(Sub() DownloadFile(Me.DmsProvider, RemoteFile, TargetFile))
                            End If
                        Next
                        System.Windows.Forms.MessageBox.Show(Me, UiStrings.GetText("DownloadSuccessful"), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Else
                        System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("OutsideRequiredFolder", Me.LocalParentMustFolder), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                    End If
                Else
                    System.Windows.Forms.MessageBox.Show(Me, UiStrings.GetText("OperationCancelled"), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                End If
            End If
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Data.FileNotFoundException
            Me.ShowMissingFile(ex.RemotePath)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripButtonDeleteFile_Click(sender As Object, e As EventArgs) Handles ToolStripButtonDeleteFile.Click
        Try
            Dim SelectedFiles As List(Of DmsResourceItem) = Me.CurrentSelectedFiles
            If SelectedFiles.Count = 0 Then Throw New Data.DmsUserErrorMessageException(UiStrings.GetText("NoFileSelected"))
            If InfoBox.InformationBox.Show(UiStrings.GetText("DeleteFilesQuestion") & System.Environment.NewLine & System.Environment.NewLine & Strings.Join(SelectedFiles.ConvertAll(Of String)(Function(item) item.Name).ToArray, System.Environment.NewLine), title:=UiStrings.GetText("DeleteFilesTitle"), buttons:=InfoBox.InformationBoxButtons.YesNoCancel, icon:=InformationBoxIcon.Question) = InformationBoxResult.Yes Then
                For MyCounter As Integer = 0 To SelectedFiles.Count - 1
                    Me.DmsProvider.DeleteRemoteItem(SelectedFiles(MyCounter))
                Next
            End If
            Me.RefreshFilesList()
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Data.RessourceNotFoundException
            Me.ShowMissingFile(ex.RemotePath)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripButtonCopyFile_Click(sender As Object, e As EventArgs) Handles ToolStripButtonCopyFile.Click
        Me.RunFileAction(ResourceAction.Copy)
    End Sub

    Private Sub ToolStripButtonRenameFile_Click(sender As Object, e As EventArgs) Handles ToolStripButtonRenameFile.Click
        Me.RunFileAction(ResourceAction.Rename)
    End Sub

    Private Sub ToolStripButtonMoveFile_Click(sender As Object, e As EventArgs) Handles ToolStripButtonMoveFile.Click
        Me.RunFileAction(ResourceAction.Move)
    End Sub

    Friend Enum ResourceAction
        Copy
        Rename
        Move
    End Enum

    Private Sub RunFileAction(action As ResourceAction)
        Try
            Dim selectedFiles As List(Of DmsResourceItem) = Me.CurrentSelectedFiles()
            If selectedFiles.Count = 0 Then Throw New DmsUserInputInvalidException(UiStrings.GetText("NoFileSelected"))
            If action = ResourceAction.Rename AndAlso selectedFiles.Count <> 1 Then Throw New DmsUserInputInvalidException(UiStrings.GetText("ExactlyOneItemRequired"))

            Dim destinationDirectory As String = Nothing
            If action <> ResourceAction.Rename AndAlso Not Me.TrySelectDestinationDirectory(destinationDirectory) Then Return

            Dim completed As Integer = 0
            Try
                For Each source As DmsResourceItem In selectedFiles
                    Dim parentPath As String = If(action = ResourceAction.Rename, Me.DmsProvider.ParentDirectoryPath(source.FullName), destinationDirectory)
                    Dim targetName As String = source.Name
                    If action = ResourceAction.Rename OrElse selectedFiles.Count = 1 Then
                        If Not Me.TryGetDestinationName(action, source, targetName) Then Return
                    End If
                    Dim destinationPath As String = Me.DmsProvider.CombinePath(parentPath, targetName)
                    If Not Me.ExecuteResourceAction(source, destinationPath, action) Then
                        If completed > 0 Then MessageBox.Show(Me, UiStrings.Format("ActionStoppedAfter", completed, selectedFiles.Count), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Information)
                        Return
                    End If
                    completed += 1
                Next
            Catch ex As Exception
                If completed > 0 Then
                    Throw New InvalidOperationException(UiStrings.Format("ActionPartlyCompleted", completed, selectedFiles.Count, ex.Message), ex)
                End If
                Throw
            End Try
        Catch ex As Exception
            Me.ShowResourceActionError(ex)
        Finally
            Try
                If Me.TreeViewDmsFolders.SelectedNode IsNot Nothing Then Me.RefreshFilesList()
            Catch ex As Exception
                Me.ShowResourceActionError(ex)
            End Try
        End Try
    End Sub

    Private Sub RunFolderAction(action As ResourceAction)
        Dim refreshRequired As Boolean = False
        Dim refreshPath As String = Nothing
        Try
            Dim source As DmsResourceItem = Me.CurrentSelectedFolder()
            If source Is Nothing Then Throw New DmsUserInputInvalidException(UiStrings.GetText("NoFolderSelected"))
            If source.ItemType = DmsResourceItem.ItemTypes.Root OrElse Me.CurrentSelectedFolderNode() Is Me.RootNode Then
                Throw New DmsUserInputInvalidException(UiStrings.GetText("RootFolderCannotBeChanged"))
            End If

            Dim parentPath As String = Me.DmsProvider.ParentDirectoryPath(source.FullName)
            If action <> ResourceAction.Rename AndAlso Not Me.TrySelectDestinationDirectory(parentPath) Then Return
            Dim targetName As String = source.Name
            If Not Me.TryGetDestinationName(action, source, targetName) Then Return
            Dim destinationPath As String = Me.DmsProvider.CombinePath(parentPath, targetName)
            refreshPath = source.FullName
            refreshRequired = True
            If Not Me.ExecuteResourceAction(source, destinationPath, action) Then Return
            refreshPath = destinationPath
        Catch ex As Exception
            Me.ShowResourceActionError(ex)
        Finally
            If refreshRequired Then
                Try
                    Me.LoadTree()
                    Me.SelectFolderPath(Me.PathRelativeToBrowserRoot(refreshPath))
                Catch ex As Exception
                    Me.ShowResourceActionError(ex)
                End Try
            End If
        End Try
    End Sub

    Private Function TrySelectDestinationDirectory(ByRef directoryPath As String) As Boolean
        Using picker As DmsBrowser = Me.CreateDestinationPicker()
            If picker.ShowDialog(Me) <> DialogResult.OK Then Return False
            directoryPath = If(picker.CurrentSelectedFolder()?.FullName, "")
            Return True
        End Using
    End Function

    Friend Function CreateDestinationPicker() As DmsBrowser
        Dim picker As New DmsBrowser(Me.DmsProvider)
        picker.DmsProfile = Me.DmsProfile
        picker.Text = UiStrings.GetText("SelectDestinationFolder")
        picker.InitialFolder = Me.InitialFolder
        picker.SelectedFolder = Me.SelectedFolderPath()
        picker.BrowseMode = BrowseModes.Folders
        picker.AllowedActions = Me.AllowedActions And FileOrFolderActions.AllowCreateFolders
        picker.DialogOperationModeInternal = DialogOperationModes.ReturnSelectedItems
        Return picker
    End Function

    Private Function TryGetDestinationName(action As ResourceAction, source As DmsResourceItem, ByRef name As String) As Boolean
        Dim promptKey As String = If(action = ResourceAction.Rename, "RenameItemPrompt", "DestinationNamePrompt")
        If Not UITools.TryInputBox(UiStrings.Format(promptKey, source.Name), UiStrings.GetText("DestinationNameTitle"), source.Name, name) Then Return False
        name = name.Trim()
        If Not IsValidResourceName(name, Me.DmsProvider.DirectorySeparator) Then
            Throw New DmsUserInputInvalidException(UiStrings.GetText("InvalidDestinationName"))
        End If
        Return True
    End Function

    Private Shared Function IsValidResourceName(name As String, separator As Char) As Boolean
        Return Not String.IsNullOrWhiteSpace(name) AndAlso name <> "." AndAlso name <> ".." AndAlso
            name.IndexOf(separator) < 0 AndAlso name.IndexOf("/"c) < 0 AndAlso name.IndexOf("\"c) < 0
    End Function

    Friend Function ExecuteResourceAction(source As DmsResourceItem, destinationPath As String, action As ResourceAction) As Boolean
        If String.Equals(source.FullName.TrimEnd(Me.DmsProvider.DirectorySeparator), destinationPath.TrimEnd(Me.DmsProvider.DirectorySeparator), StringComparison.Ordinal) Then
            Throw New DmsUserInputInvalidException(UiStrings.GetText("SourceEqualsDestination"))
        End If

        Dim allowOverwrite As Boolean = False
        Dim existing As DmsResourceItem = Me.DmsProvider.ListRemoteItem(destinationPath)
        If existing IsNot Nothing Then
            Dim questionKey As String = If(source.ItemType = DmsResourceItem.ItemTypes.File, "OverwriteFileQuestion", "MergeFolderQuestion")
            If MessageBox.Show(Me, UiStrings.Format(questionKey, destinationPath), Me.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then Return False
            allowOverwrite = True
        End If
        Select Case action
            Case ResourceAction.Copy
                Me.DmsProvider.Copy(source, destinationPath, allowOverwrite, False)
            Case ResourceAction.Rename, ResourceAction.Move
                Me.DmsProvider.Move(source, destinationPath, allowOverwrite, False)
        End Select
        Return True
    End Function

    Private Function PathRelativeToBrowserRoot(path As String) As String
        Dim rootPath As String = Me.InitialFolder
        If String.IsNullOrEmpty(rootPath) Then Return path.TrimStart(Me.DmsProvider.DirectorySeparator)
        Dim prefix As String = rootPath.TrimEnd(Me.DmsProvider.DirectorySeparator) & Me.DmsProvider.DirectorySeparator
        If Not path.StartsWith(prefix, StringComparison.Ordinal) Then Throw New InvalidOperationException(UiStrings.GetText("DestinationOutsideBrowserRoot"))
        Return path.Substring(prefix.Length)
    End Function

    Private Sub ShowResourceActionError(ex As Exception)
        If TypeOf ex Is Data.FileNotFoundException Then
            Me.ShowMissingFile(CType(ex, Data.FileNotFoundException).RemotePath)
            Return
        ElseIf TypeOf ex Is Data.DirectoryNotFoundException Then
            Dim missingPath As String = CType(ex, Data.DirectoryNotFoundException).RemotePath
            Dim missingNode As TreeNode = Me.FindDirectoryNodeByPath(missingPath)
            If missingNode IsNot Nothing Then
                Me.ShowMissingDirectory(missingNode, missingPath)
                Return
            End If
        ElseIf TypeOf ex Is Data.RessourceNotFoundException Then
            Dim missingPath As String = CType(ex, Data.RessourceNotFoundException).RemotePath
            If Me.CurrentSelectedFiles().Any(Function(file) String.Equals(file.FullName, missingPath, StringComparison.Ordinal)) Then
                Me.ShowMissingFile(missingPath)
                Return
            End If
            Dim missingNode As TreeNode = Me.FindDirectoryNodeByPath(missingPath)
            If missingNode IsNot Nothing Then
                Me.ShowMissingDirectory(missingNode, missingPath)
                Return
            End If
        End If
        Dim message As String = If(System.Diagnostics.Debugger.IsAttached, ex.ToString(), ex.Message)
        MessageBox.Show(Me, message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
    End Sub

    Private Sub ButtonClose_Click(sender As Object, e As EventArgs) Handles ButtonClose.Click
        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub

    Private Sub ToolStripButtonSharingsFolder_Click(sender As Object, e As EventArgs) Handles ToolStripButtonSharingsFolder.Click
        Try
            If CurrentSelectedFolder() Is Nothing OrElse CurrentSelectedFolder.ItemType = DmsResourceItem.ItemTypes.Root Then Throw New Data.DmsUserErrorMessageException(UiStrings.GetText("RootSharingUnsupported"))
            Dim DmsShareForm As New DmsItemSharings()
            DmsShareForm.DmsItem = CurrentSelectedFolder()
            DmsShareForm.DmsProvider = Me.DmsProvider
            AddHandler DmsShareForm.SharingsChanged, AddressOf Me.DmsShareForm_SharingsChanged
            DmsShareForm.Show(Me)
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripButtonSharingsFile_Click(sender As Object, e As EventArgs) Handles ToolStripButtonSharingsFile.Click
        Try
            Dim SelectedFiles As List(Of DmsResourceItem) = Me.CurrentSelectedFiles
            If SelectedFiles.Count = 0 Then Throw New Data.DmsUserErrorMessageException(UiStrings.GetText("NoFileSelected"))
            For MyCounter As Integer = 0 To SelectedFiles.Count - 1
                Dim DmsShareForm As New DmsItemSharings()
                DmsShareForm.DmsItem = SelectedFiles(MyCounter)
                DmsShareForm.DmsProvider = Me.DmsProvider
                AddHandler DmsShareForm.SharingsChanged, AddressOf Me.DmsShareForm_SharingsChanged
                DmsShareForm.Show(Me)
            Next
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub DmsShareForm_SharingsChanged(sender As Object, e As EventArgs)
        Me.RefreshSharingVisuals(DirectCast(sender, DmsItemSharings).DmsItem, Me.DmsProvider)
    End Sub

    Friend Sub RefreshSharingVisuals(ChangedItem As DmsResourceItem, provider As BaseDmsProvider)
        Dim ParentPath As String = provider.ParentDirectoryPath(ChangedItem.FullName)
        If ChangedItem.ItemType = DmsResourceItem.ItemTypes.File Then
            provider.ResetCachesForRemoteItems(ParentPath, BaseDmsProvider.SearchItemType.Files)
            Dim RefreshedFile As DmsResourceItem = provider.ListAllFileItems(ParentPath).Find(Function(item) SameSharingItem(item, ChangedItem))
            If RefreshedFile Is Nothing Then Return
            For Each FileItem As ListViewItem In Me.ListViewDmsFiles.Items
                Dim ListedFile As DmsResourceItem = DirectCast(FileItem.Tag, DmsResourceItem)
                If SameSharingItem(ListedFile, ChangedItem) Then
                    ListedFile.ExtendedInfosIsShared = RefreshedFile.ExtendedInfosIsShared OrElse RefreshedFile.ExtendedInfosHasLinks
                    FileItem.ImageIndex = Me.FileIcons.GetSIImageListIndexForFileExtension(System.IO.Path.GetExtension(ListedFile.Name), ListedFile.ExtendedInfosIsShared)
                End If
            Next
            Return
        End If

        Dim RefreshedItem As DmsResourceItem
        Select Case ChangedItem.ItemType
            Case DmsResourceItem.ItemTypes.Collection
                provider.ResetCachesForRemoteItems(ParentPath, BaseDmsProvider.SearchItemType.Collections)
                RefreshedItem = provider.ListAllCollectionItems(ParentPath).Find(Function(item) SameSharingItem(item, ChangedItem))
            Case DmsResourceItem.ItemTypes.Folder
                provider.ResetCachesForRemoteItems(ParentPath, BaseDmsProvider.SearchItemType.Folders)
                RefreshedItem = provider.ListAllFolderItems(ParentPath).Find(Function(item) SameSharingItem(item, ChangedItem))
            Case Else
                Return
        End Select
        If RefreshedItem Is Nothing Then Return

        Me.UpdateSharingTreeIcons(Me.TreeViewDmsFolders.Nodes, ChangedItem, RefreshedItem)
        If Me.TreeViewDmsFolders.SelectedNode IsNot Nothing Then Me.RefreshFilesList()
    End Sub

    Private Shared Function SameSharingItem(item As DmsResourceItem, changedItem As DmsResourceItem) As Boolean
        If item.ItemType <> changedItem.ItemType Then Return False
        Select Case changedItem.ItemType
            Case DmsResourceItem.ItemTypes.File
                If changedItem.ExtendedInfosFileID <> Nothing Then Return item.ExtendedInfosFileID = changedItem.ExtendedInfosFileID
            Case DmsResourceItem.ItemTypes.Folder
                If changedItem.ExtendedInfosFolderID <> Nothing Then Return item.ExtendedInfosFolderID = changedItem.ExtendedInfosFolderID
            Case DmsResourceItem.ItemTypes.Collection
                If changedItem.ExtendedInfosCollectionID <> Nothing Then Return item.ExtendedInfosCollectionID = changedItem.ExtendedInfosCollectionID
        End Select
        Return String.Equals(item.FullName, changedItem.FullName, StringComparison.Ordinal)
    End Function

    Private Sub UpdateSharingTreeIcons(nodes As TreeNodeCollection, changedItem As DmsResourceItem, refreshedItem As DmsResourceItem)
        For Each node As TreeNode In nodes
            Dim nodeData As NodeTagData = TryCast(node.Tag, NodeTagData)
            If nodeData IsNot Nothing AndAlso nodeData.DmsResourceItem IsNot Nothing Then
                Dim nodeItem As DmsResourceItem = nodeData.DmsResourceItem
                If Object.ReferenceEquals(nodeItem, changedItem) OrElse SameSharingItem(nodeItem, changedItem) Then
                    Dim IsShared As Boolean = refreshedItem.ExtendedInfosIsShared OrElse refreshedItem.ExtendedInfosHasLinks OrElse refreshedItem.ExtendedInfosHasGroupSharings OrElse refreshedItem.ExtendedInfosHasUserSharings
                    nodeItem.ExtendedInfosIsShared = IsShared
                    If node Is Me.RootNode Then
                        node.ImageIndex = If(IsShared, 3, 0)
                    ElseIf refreshedItem.ItemType = DmsResourceItem.ItemTypes.Collection Then
                        node.ImageIndex = If(IsShared, 4, 1)
                    Else
                        node.ImageIndex = If(IsShared, 5, 2)
                    End If
                    node.SelectedImageIndex = node.ImageIndex
                End If
            End If
            Me.UpdateSharingTreeIcons(node.Nodes, changedItem, refreshedItem)
        Next
    End Sub

    Private Sub ToolStripButtonPropertiesFile_Click(sender As Object, e As EventArgs) Handles ToolStripButtonPropertiesFile.Click
        Try
            Dim SelectedFiles As List(Of DmsResourceItem) = Me.CurrentSelectedFiles
            If SelectedFiles.Count <> 1 Then Throw New DmsUserInputInvalidException(UiStrings.GetText("ExactlyOneItemRequired"))
            Dim SelectedFile As DmsResourceItem = SelectedFiles(0)
            InfoBox.InformationBox.Show(Me.PropertiesDetails(SelectedFile), title:=UiStrings.Format("PropertiesTitle", SelectedFile.Name), buttons:=InfoBox.InformationBoxButtons.OK, icon:=InformationBoxIcon.Information)
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As DmsUserInputInvalidException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Friend Function PropertiesDetails(dmsItem As DmsResourceItem) As String
        Dim Message As String = UiStrings.GetText("PropertiesFullPath") & dmsItem.FullName & System.Environment.NewLine &
                UiStrings.GetText("PropertiesParentFolder") & dmsItem.Folder & System.Environment.NewLine &
                UiStrings.GetText("PropertiesParentCollection") & dmsItem.Collection & System.Environment.NewLine
        If dmsItem.ItemType = DmsResourceItem.ItemTypes.File Then
            Message &= UiStrings.GetText("PropertiesFileSize") & dmsItem.ContentLength.ToString("#,##0") & " Bytes" & System.Environment.NewLine
        End If
        Message &= System.Environment.NewLine &
                UiStrings.GetText("PropertiesDetails") & System.Environment.NewLine &
                "- " & UiStrings.GetText("PropertiesOwner") & dmsItem.ExtendedInfosOwner.ToString & System.Environment.NewLine &
                "- " & UiStrings.GetText("PropertiesVersionNumber") & dmsItem.ExtendedInfosVersion & System.Environment.NewLine
        If dmsItem.ExtendedInfosVersionDateLocalTime.HasValue Then Message &= "- " & UiStrings.GetText("PropertiesVersionDate") & dmsItem.ExtendedInfosVersionDateLocalTime & System.Environment.NewLine
        If dmsItem.ExtendedInfosArchivedDateLocalTime.HasValue Then Message &= "- " & UiStrings.GetText("PropertiesArchivedDate") & dmsItem.ExtendedInfosArchivedDateLocalTime & System.Environment.NewLine
        If dmsItem.ExtendedInfosLockedByUser.ToString <> Nothing Then
            Message &= "- " & UiStrings.GetText("PropertiesLockedBy") & dmsItem.ExtendedInfosLockedByUser.ToString & System.Environment.NewLine
        End If
        If dmsItem.ExtendedInfosLocks IsNot Nothing AndAlso dmsItem.ExtendedInfosLocks.Count <> 0 Then
            Message &= "- " & UiStrings.GetText("PropertiesLocks") & System.Environment.NewLine
            For Each Lock As String In dmsItem.ExtendedInfosLocks
                Message &= "  - " & Lock & System.Environment.NewLine
            Next
        End If
        If dmsItem.ExtendedInfosCollisionDetected Then
            Message &= "- " & UiStrings.GetText("PropertiesCollisionWarning") & System.Environment.NewLine
        End If
        Message &= System.Environment.NewLine &
                UiStrings.GetText("PropertiesSharings") & System.Environment.NewLine &
                "- IsShared: " & dmsItem.ExtendedInfosIsShared.ToString & System.Environment.NewLine
        If dmsItem.ItemType = DmsResourceItem.ItemTypes.Collection Then
            Message &= "- IsPublicCollection: " & dmsItem.ExtendedInfosIsPublicCollection.ToString & System.Environment.NewLine
        End If
        Message &= "- HasHiddenGroupSharings: " & dmsItem.ExtendedInfosHasHiddenGroupSharings & System.Environment.NewLine &
                "- HasGroupSharings: " & dmsItem.ExtendedInfosHasGroupSharings & System.Environment.NewLine
        If dmsItem.ExtendedInfosGroupSharings IsNot Nothing Then
            For Each Sharing As DmsShareForGroup In dmsItem.ExtendedInfosGroupSharings
                Message &= "  - " & Sharing.ToString & System.Environment.NewLine
            Next
        End If
        Message &= "- HasHiddenUserSharings: " & dmsItem.ExtendedInfosHasHiddenUserSharings & System.Environment.NewLine &
                "- HasUserSharings: " & dmsItem.ExtendedInfosHasUserSharings & System.Environment.NewLine
        If dmsItem.ExtendedInfosUserSharings IsNot Nothing Then
            For Each Sharing As DmsShareForUser In dmsItem.ExtendedInfosUserSharings
                Message &= "  - " & Sharing.ToString & System.Environment.NewLine
            Next
        End If
        If dmsItem.ExtendedInfosLinks IsNot Nothing Then
            For Each ViewLink As DmsLink In dmsItem.ExtendedInfosLinks
                If ViewLink.ID <> Nothing Then
                    ViewLink.Refresh()
                    Message &= "- Link: " & ViewLink.ID & System.Environment.NewLine &
                "  - WebUrl: " & ViewLink.WebUrl & System.Environment.NewLine &
                "  - DownloadUrl: " & ViewLink.DownloadUrl & System.Environment.NewLine &
                "  - Password: " & ViewLink.Password & System.Environment.NewLine &
                "  - ExpiresOn: " & ViewLink.ExpiryDateLocalTime & System.Environment.NewLine &
                "  - MaxDownloads: " & ViewLink.MaxDownloads & System.Environment.NewLine &
                "  - MaxBytes: " & ViewLink.MaxBytes & System.Environment.NewLine &
                "  - MaxUploads: " & ViewLink.MaxUploads & System.Environment.NewLine &
                "  - UploadsCount: " & ViewLink.UploadsCount & System.Environment.NewLine &
                "  - UploadedBytes: " & ViewLink.UploadedBytes & System.Environment.NewLine &
                "  - AllowView: " & ViewLink.AllowView & System.Environment.NewLine &
                "  - AllowDownload: " & ViewLink.AllowDownload & System.Environment.NewLine &
                "  - AllowUpload: " & ViewLink.AllowUpload & System.Environment.NewLine &
                "  - AllowEdit: " & ViewLink.AllowEdit & System.Environment.NewLine &
                "  - AllowDelete: " & ViewLink.AllowDelete & System.Environment.NewLine
                End If
            Next
        End If
        Message &= System.Environment.NewLine &
                UiStrings.GetText("PropertiesExtendedInformation") & System.Environment.NewLine &
                "- " & UiStrings.GetText("PropertiesLastModification") & dmsItem.LastModificationOnLocalTime.ToString & System.Environment.NewLine
        Dim HasChildren As Boolean? = HasKnownChildDirectories(dmsItem)
        Message &= "- HasChildDirectories: " & If(HasChildren.HasValue, HasChildren.Value.ToString(), "") & System.Environment.NewLine &
                "- IsIntelligent: " & dmsItem.ExtendedInfosIsIntelligent.ToString & System.Environment.NewLine &
                "- IsAuditing: " & dmsItem.ExtendedInfosIsAuditing.ToString & System.Environment.NewLine &
                "- Hash/ETag: " & dmsItem.ProviderSpecificHashOrETag & System.Environment.NewLine &
                "- File ID: " & dmsItem.ExtendedInfosFileID & System.Environment.NewLine &
                "- Folder ID: " & dmsItem.ExtendedInfosFolderID & System.Environment.NewLine &
                "- Collection ID: " & dmsItem.ExtendedInfosCollectionID & System.Environment.NewLine &
                "- Assigned Collection ID: " & dmsItem.ExtendedInfosAssignedCollectionID & System.Environment.NewLine &
                "- Assigned Folder ID: " & dmsItem.ExtendedInfosAssignedFolderID & System.Environment.NewLine
        If dmsItem.ExtendedInfosReferencedFromCollectionIDs IsNot Nothing Then
            Message &= "- " & UiStrings.GetText("PropertiesReferencedCollectionIds") & System.Environment.NewLine
            For Each item As String In dmsItem.ExtendedInfosReferencedFromCollectionIDs
                Message &= "  - " & Me.LookupCollectionNameForUI(item, dmsItem) & System.Environment.NewLine
            Next
        End If
        If dmsItem.ExtendedInfosReferencedFromFolderIDs IsNot Nothing Then
            Message &= "- " & UiStrings.GetText("PropertiesReferencedFolderIds") & System.Environment.NewLine
            For Each item As String In dmsItem.ExtendedInfosReferencedFromFolderIDs
                Message &= "  - " & Me.LookupFolderNameForUI(item, dmsItem) & System.Environment.NewLine
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
                    Try
                        Dim collection As DmsResourceItem = Me.DmsProvider.FindCollectionById(id)
                        If collection IsNot Nothing Then Return collection.Name & " (" & id & ")"
                    Catch ex As Exception
                        'A failed optional name lookup must not hide the file's other properties.
                    End Try
                    Return id
                End If
            Case Else
                Return id
        End Select
    End Function

    Private Function LookupFolderNameForUI(id As String, currentDmsItem As DmsResourceItem) As String
        Select Case Me.DmsProvider.DmsProviderID
            Case Providers.BaseDmsProvider.DmsProviders.CenterDevice, Providers.BaseDmsProvider.DmsProviders.Scopevisio
                If currentDmsItem IsNot Nothing AndAlso currentDmsItem.ExtendedInfosFolderID = id Then
                    Return currentDmsItem.FullName & " (" & id & ")"
                Else
                    Try
                        Dim folder As DmsResourceItem = Me.DmsProvider.FindFolderById(id)
                        If folder IsNot Nothing Then Return folder.Name & " (" & id & ")"
                    Catch ex As Exception
                        'A failed optional name lookup must not hide the file's other properties.
                    End Try
                    Return id
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
                InfoBox.InformationBox.Show(Me.PropertiesDetails(SelectedFolder), title:=UiStrings.Format("PropertiesTitle", SelectedFolder.Name), buttons:=InfoBox.InformationBoxButtons.OK, icon:=InformationBoxIcon.Information)
            Else
                InfoBox.InformationBox.Show("Root", title:=UiStrings.Format("PropertiesTitle", "/"), buttons:=InfoBox.InformationBoxButtons.OK, icon:=InformationBoxIcon.Information)
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
            Me.RefreshCurrentFolderAndFiles()
        Catch ex As Data.DirectoryNotFoundException
            Me.ShowMissingDirectory(Me.TreeViewDmsFolders.SelectedNode, ex.RemotePath)
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As DmsUserInputInvalidException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Friend Sub RefreshCurrentFolderAndFiles()
        Dim selectedNode As TreeNode = Me.TreeViewDmsFolders.SelectedNode
        If selectedNode Is Nothing AndAlso Me.LastFileListFolderPath IsNot Nothing Then
            Me.SelectFolderPath(Me.PathRelativeToBrowserRoot(Me.LastFileListFolderPath))
            selectedNode = Me.TreeViewDmsFolders.SelectedNode
        ElseIf selectedNode Is Nothing AndAlso Me.SelectedFolder IsNot Nothing Then
            Me.SelectFolderPath(Me.SelectedFolder)
            selectedNode = Me.TreeViewDmsFolders.SelectedNode
        End If
        If selectedNode Is Nothing Then selectedNode = Me.RootNode
        Dim selectionAncestors As New List(Of TreeNode)
        While selectedNode IsNot Nothing
            selectionAncestors.Add(selectedNode)
            selectedNode = selectedNode.Parent
        End While
        Me.SuppressSelectionRefresh = True
        Me.TreeViewDmsFolders.BeginUpdate()
        Try
            If Me.RootNode IsNot Nothing Then Me.RefreshLoadedTree(Me.RootNode, selectionAncestors.FirstOrDefault())
            Me.TreeViewDmsFolders.Sort()
            Me.TreeViewDmsFolders.SelectedNode = selectionAncestors.FirstOrDefault(Function(node) node.TreeView Is Me.TreeViewDmsFolders)
            If Me.TreeViewDmsFolders.SelectedNode Is Nothing Then Me.TreeViewDmsFolders.SelectedNode = Me.RootNode
            Me.SelectedFolder = Me.SelectedFolderPath()
        Finally
            Me.TreeViewDmsFolders.EndUpdate()
            Me.SuppressSelectionRefresh = False
        End Try
        Me.RefreshFilesList()
    End Sub

    Private Sub RefreshLoadedTree(node As TreeNode, selectedNode As TreeNode)
        Dim data As NodeTagData = DirectCast(node.Tag, NodeTagData)
        If Not data.ChildrenLoaded AndAlso node IsNot selectedNode Then Return
        Dim path As String = If(data.DmsResourceItem?.FullName, Me.DmsProvider.BrowseInRootFolderName)
        Me.DmsProvider.ResetCachesForRemoteItems(path, BaseDmsProvider.SearchItemType.AllItems)
        Dim children As List(Of DmsResourceItem) = Me.DmsProvider.ListAllDirectoryItems(path)
        Dim previousNodes As List(Of TreeNode) = node.Nodes.Cast(Of TreeNode)().Where(Function(child) child.Tag IsNot Nothing).ToList()
        For Each child As TreeNode In node.Nodes.Cast(Of TreeNode)().Where(Function(item) item.Tag Is Nothing).ToList()
            child.Remove()
        Next
        For Each item As DmsResourceItem In children
            If item.ItemType = DmsResourceItem.ItemTypes.Collection AndAlso
                (Not Me.DmsProvider.SupportsCollections OrElse
                 (data.DmsResourceItem IsNot Nothing AndAlso data.DmsResourceItem.ItemType <> DmsResourceItem.ItemTypes.Collection)) Then Continue For
            Dim existing As TreeNode = previousNodes.FirstOrDefault(Function(child)
                                                                       Dim resource = DirectCast(child.Tag, NodeTagData).DmsResourceItem
                                                                       Return resource.FullName = item.FullName AndAlso resource.ItemType = item.ItemType
                                                                   End Function)
            If existing Is Nothing Then
                Me.AddDirectoryTreeNode(node, item)
            Else
                previousNodes.Remove(existing)
                DirectCast(existing.Tag, NodeTagData).DmsResourceItem = item
                existing.Text = item.Name
                Dim appearance As TreeNode = CreateDirectoryTreeNode(item)
                existing.ImageIndex = appearance.ImageIndex
                existing.SelectedImageIndex = appearance.SelectedImageIndex
                If Not DirectCast(existing.Tag, NodeTagData).ChildrenLoaded AndAlso existing IsNot selectedNode Then
                    existing.Nodes.Clear()
                    AddExpansionPlaceholderIfRequired(existing)
                Else
                    Me.RefreshLoadedTree(existing, selectedNode)
                End If
            End If
        Next
        For Each removed As TreeNode In previousNodes
            removed.Remove()
        Next
        data.ChildrenLoaded = True
        UpdateChildDirectoryMetadata(node)
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
        Me.RunFolderAction(ResourceAction.Copy)
    End Sub

    Private Sub ToolStripFolderContextButtonRenameFolder_Click(sender As Object, e As EventArgs) Handles ToolStripFolderContextButtonRenameFolder.Click
        Me.RunFolderAction(ResourceAction.Rename)
    End Sub

    Private Sub ToolStripFolderContextButtonMoveFolder_Click(sender As Object, e As EventArgs) Handles ToolStripFolderContextButtonMoveFolder.Click
        Me.RunFolderAction(ResourceAction.Move)
    End Sub

    Private Sub ToolStripFolderContextButtonDeleteFolder_Click(sender As Object, e As EventArgs) Handles ToolStripFolderContextButtonDeleteFolder.Click
        Try
            Dim SelectedFolderNode As TreeNode = Me.CurrentSelectedFolderNode
            Dim SelectedFolder As DmsResourceItem = Me.CurrentSelectedFolder
            If SelectedFolderNode Is Nothing AndAlso SelectedFolder Is Nothing Then Throw New Data.DmsUserErrorMessageException(UiStrings.GetText("NoFolderSelected"))
            If SelectedFolderNode IsNot Nothing AndAlso (SelectedFolder Is Nothing OrElse SelectedFolder.ItemType = DmsResourceItem.ItemTypes.Root) Then Throw New Data.DmsUserErrorMessageException(UiStrings.GetText("RootFolderCannotBeDeleted"))
            If InfoBox.InformationBox.Show(UiStrings.GetText("DeleteFolderQuestion") & System.Environment.NewLine & System.Environment.NewLine & SelectedFolder.Name, title:=UiStrings.GetText("DeleteFolderTitle"), buttons:=InfoBox.InformationBoxButtons.YesNoCancel, icon:=InformationBoxIcon.Question) = InformationBoxResult.Yes Then
                Dim ParentFolderNode As TreeNode = Me.CurrentParentOfSelectedFolderNode
                Dim ParentFolder As DmsResourceItem = Me.CurrentParentOfSelectedFolder
                Me.DmsProvider.DeleteRemoteItem(SelectedFolder)
                If ParentFolderNode IsNot Nothing Then
                    ParentFolderNode.Nodes.Remove(CurrentSelectedFolderNode)
                    RecordChildDirectoryDeleted(ParentFolderNode)
                End If
            End If
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Data.RessourceNotFoundException
            Me.ShowMissingDirectory(Me.TreeViewDmsFolders.SelectedNode, ex.RemotePath)
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
                System.Windows.Forms.MessageBox.Show(Me, UiStrings.GetText("NoFilesSelected"), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            Else
                For MyCounter As Integer = 0 To SelectedFiles.Count - 1
                    Dim TargetFile As New CompuMaster.IO.TemporaryFile(CompuMaster.IO.TemporaryFile.TempFileCleanupEvent.OnApplicationExit,
                                                                       System.IO.Path.GetRandomFileName,
                                                                       System.IO.Path.GetFileNameWithoutExtension(SelectedFiles(MyCounter).Name),
                                                                       System.IO.Path.GetExtension(SelectedFiles(MyCounter).Name))
                    Dim RemoteFile As DmsResourceItem = SelectedFiles(MyCounter)
                    Me.RunWithWaitCursor(Sub() DownloadFile(Me.DmsProvider, RemoteFile, TargetFile.FilePath))
                    System.IO.File.SetAttributes(TargetFile.FilePath, System.IO.FileAttributes.ReadOnly Or System.IO.FileAttributes.Temporary)
                    OpenDownloadedFileItem.Invoke(TargetFile)
                Next
            End If
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Data.FileNotFoundException
            Me.ShowMissingFile(ex.RemotePath)
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

    Friend Sub RunWithWaitCursor(operation As Action)
        Dim PreviousUseWaitCursor As Boolean = Me.UseWaitCursor
        Dim PreviousCursor As Cursor = Cursor.Current
        Try
            Me.UseWaitCursor = True
            Cursor.Current = Cursors.WaitCursor
            operation()
        Finally
            Me.UseWaitCursor = PreviousUseWaitCursor
            Cursor.Current = PreviousCursor
        End Try
    End Sub

    Friend Shared Sub DownloadFile(provider As BaseDmsProvider, remoteFile As DmsResourceItem, localFilePath As String)
        provider.DownloadFile(remoteFile, localFilePath)
    End Sub

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
