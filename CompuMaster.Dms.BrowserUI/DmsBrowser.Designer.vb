Imports System.Windows.Forms

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class DmsBrowser
    Inherits System.Windows.Forms.Form

    'Das Formular überschreibt den Löschvorgang, um die Komponentenliste zu bereinigen.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Wird vom Windows Form-Designer benötigt.
    Private components As System.ComponentModel.IContainer

    'Hinweis: Die folgende Prozedur ist für den Windows Form-Designer erforderlich.
    'Das Bearbeiten ist mit dem Windows Form-Designer möglich.  
    'Das Bearbeiten mit dem Code-Editor ist nicht möglich.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.components = New System.ComponentModel.Container()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(DmsBrowser))
        Me.ButtonCancel = New System.Windows.Forms.Button()
        Me.ButtonOkay = New System.Windows.Forms.Button()
        Me.TreeViewDmsFolders = New System.Windows.Forms.TreeView()
        Me.ContextMenuStripFolder = New System.Windows.Forms.ContextMenuStrip(Me.components)
        Me.ToolStripFolderContextButtonNewFolder = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripFolderContextButtonCopyFolder = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripFolderContextButtonRenameFolder = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripFolderContextButtonMoveFolder = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripFolderContextButtonDeleteFolder = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripSeparator3 = New System.Windows.Forms.ToolStripSeparator()
        Me.ToolStripFolderContextButtonShareFolder = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripFolderContextButtonRefreshFilesList = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripFolderContextButtonProperties = New System.Windows.Forms.ToolStripButton()
        Me.ImageListFileIcons = New System.Windows.Forms.ImageList(Me.components)
        Me.ButtonCreateNewFolder = New System.Windows.Forms.Button()
        Me.SplitContainer = New System.Windows.Forms.SplitContainer()
        Me.ListViewDmsFiles = New System.Windows.Forms.ListView()
        Me.ColumnHeaderFileName = New System.Windows.Forms.ColumnHeader()
        Me.ColumnHeaderSize = New System.Windows.Forms.ColumnHeader()
        Me.ColumnHeaderLastModifiedOn = New System.Windows.Forms.ColumnHeader()
        Me.ContextMenuStripFile = New System.Windows.Forms.ContextMenuStrip(Me.components)
        Me.ToolStripFileContextButtonUploadFile = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripFileContextButtonDownloadFile = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripFileContextButtonOpenPreviewFile = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripSeparator2 = New System.Windows.Forms.ToolStripSeparator()
        Me.ToolStripFileContextButtonCopyFile = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripFileContextButtonRenameFile = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripFileContextButtonMoveFile = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripFileContextButtonDeleteFile = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripSeparator1 = New System.Windows.Forms.ToolStripSeparator()
        Me.ToolStripFileContextButtonShareFile = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripFileContextButtonProperties = New System.Windows.Forms.ToolStripButton()
        Me.FlowLayoutPanel1 = New System.Windows.Forms.FlowLayoutPanel()
        Me.ToolStripFileActions = New System.Windows.Forms.ToolStrip()
        Me.ToolStripButtonUploadFile = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripButtonDownloadFile = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripButtonOpenFile = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripButtonDeleteFile = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripSeparatorBeforeCopyRenameMove = New System.Windows.Forms.ToolStripSeparator()
        Me.ToolStripButtonCopyFile = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripButtonRenameFile = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripButtonMoveFile = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripFileShareActions = New System.Windows.Forms.ToolStrip()
        Me.ToolStripButtonSharingsFile = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripFolderShareActions = New System.Windows.Forms.ToolStrip()
        Me.ToolStripButtonSharingsFolder = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripProperties = New System.Windows.Forms.ToolStrip()
        Me.ToolStripButtonPropertiesFile = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripButtonPropertiesFolder = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripButtonRefreshFilesList = New System.Windows.Forms.ToolStripButton()
        Me.ButtonShowFiles = New System.Windows.Forms.CheckBox()
        Me.BottomToolStripPanel = New System.Windows.Forms.ToolStripPanel()
        Me.TopToolStripPanel = New System.Windows.Forms.ToolStripPanel()
        Me.RightToolStripPanel = New System.Windows.Forms.ToolStripPanel()
        Me.LeftToolStripPanel = New System.Windows.Forms.ToolStripPanel()
        Me.ContentPanel = New System.Windows.Forms.ToolStripContentPanel()
        Me.ButtonClose = New System.Windows.Forms.Button()
        Me.ToolTipFileSystemItems = New System.Windows.Forms.ToolTip(Me.components)
        Me.ContextMenuStripFolder.SuspendLayout()
        CType(Me.SplitContainer, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SplitContainer.Panel1.SuspendLayout()
        Me.SplitContainer.Panel2.SuspendLayout()
        Me.SplitContainer.SuspendLayout()
        Me.ContextMenuStripFile.SuspendLayout()
        Me.FlowLayoutPanel1.SuspendLayout()
        Me.ToolStripFileActions.SuspendLayout()
        Me.ToolStripFileShareActions.SuspendLayout()
        Me.ToolStripFolderShareActions.SuspendLayout()
        Me.ToolStripProperties.SuspendLayout()
        Me.SuspendLayout()
        '
        'ButtonCancel
        '
        Me.ButtonCancel.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.ButtonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel
        Me.ButtonCancel.Location = New System.Drawing.Point(959, 503)
        Me.ButtonCancel.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.ButtonCancel.Name = "ButtonCancel"
        Me.ButtonCancel.Size = New System.Drawing.Size(88, 27)
        Me.ButtonCancel.TabIndex = 11
        Me.ButtonCancel.Text = "&Abbrechen"
        Me.ButtonCancel.UseVisualStyleBackColor = True
        '
        'ButtonOkay
        '
        Me.ButtonOkay.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.ButtonOkay.DialogResult = System.Windows.Forms.DialogResult.OK
        Me.ButtonOkay.Location = New System.Drawing.Point(864, 503)
        Me.ButtonOkay.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.ButtonOkay.Name = "ButtonOkay"
        Me.ButtonOkay.Size = New System.Drawing.Size(88, 27)
        Me.ButtonOkay.TabIndex = 10
        Me.ButtonOkay.Text = "&Okay"
        Me.ButtonOkay.UseVisualStyleBackColor = True
        '
        'TreeViewDmsFolders
        '
        Me.TreeViewDmsFolders.ContextMenuStrip = Me.ContextMenuStripFolder
        Me.TreeViewDmsFolders.Dock = System.Windows.Forms.DockStyle.Fill
        Me.TreeViewDmsFolders.ImageIndex = 2
        Me.TreeViewDmsFolders.ImageList = Me.ImageListFileIcons
        Me.TreeViewDmsFolders.Indent = 27
        Me.TreeViewDmsFolders.ItemHeight = 24
        Me.TreeViewDmsFolders.LabelEdit = True
        Me.TreeViewDmsFolders.Location = New System.Drawing.Point(0, 0)
        Me.TreeViewDmsFolders.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.TreeViewDmsFolders.Name = "TreeViewDmsFolders"
        Me.TreeViewDmsFolders.SelectedImageIndex = 2
        Me.TreeViewDmsFolders.ShowNodeToolTips = True
        Me.TreeViewDmsFolders.Size = New System.Drawing.Size(324, 482)
        Me.TreeViewDmsFolders.TabIndex = 1
        '
        'ContextMenuStripFolder
        '
        Me.ContextMenuStripFolder.ImageScalingSize = New System.Drawing.Size(20, 20)
        Me.ContextMenuStripFolder.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.ToolStripFolderContextButtonNewFolder, Me.ToolStripFolderContextButtonCopyFolder, Me.ToolStripFolderContextButtonRenameFolder, Me.ToolStripFolderContextButtonMoveFolder, Me.ToolStripFolderContextButtonDeleteFolder, Me.ToolStripSeparator3, Me.ToolStripFolderContextButtonShareFolder, Me.ToolStripFolderContextButtonRefreshFilesList, Me.ToolStripFolderContextButtonProperties})
        Me.ContextMenuStripFolder.Name = "ContextMenuStripFolder"
        Me.ContextMenuStripFolder.Size = New System.Drawing.Size(217, 226)
        '
        'ToolStripFolderContextButtonNewFolder
        '
        Me.ToolStripFolderContextButtonNewFolder.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Folder_ui_ux_mobile_web_new
        Me.ToolStripFolderContextButtonNewFolder.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripFolderContextButtonNewFolder.Name = "ToolStripFolderContextButtonNewFolder"
        Me.ToolStripFolderContextButtonNewFolder.Size = New System.Drawing.Size(153, 24)
        Me.ToolStripFolderContextButtonNewFolder.Text = "&Neuen Ordner erstellen"
        '
        'ToolStripFolderContextButtonCopyFolder
        '
        Me.ToolStripFolderContextButtonCopyFolder.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Duplicate_ui_ux_mobile_web_4960708
        Me.ToolStripFolderContextButtonCopyFolder.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripFolderContextButtonCopyFolder.Name = "ToolStripFolderContextButtonCopyFolder"
        Me.ToolStripFolderContextButtonCopyFolder.Size = New System.Drawing.Size(78, 24)
        Me.ToolStripFolderContextButtonCopyFolder.Text = "&Kopieren"
        '
        'ToolStripFolderContextButtonRenameFolder
        '
        Me.ToolStripFolderContextButtonRenameFolder.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Rename_ui_ux_mobile_web_4960746
        Me.ToolStripFolderContextButtonRenameFolder.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripFolderContextButtonRenameFolder.Name = "ToolStripFolderContextButtonRenameFolder"
        Me.ToolStripFolderContextButtonRenameFolder.Size = New System.Drawing.Size(103, 24)
        Me.ToolStripFolderContextButtonRenameFolder.Text = "&Umbenennen"
        '
        'ToolStripFolderContextButtonMoveFolder
        '
        Me.ToolStripFolderContextButtonMoveFolder.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Arrow_ui_ux_mobile_web_4960722
        Me.ToolStripFolderContextButtonMoveFolder.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripFolderContextButtonMoveFolder.Name = "ToolStripFolderContextButtonMoveFolder"
        Me.ToolStripFolderContextButtonMoveFolder.Size = New System.Drawing.Size(94, 24)
        Me.ToolStripFolderContextButtonMoveFolder.Text = "&Verschieben"
        '
        'ToolStripFolderContextButtonDeleteFolder
        '
        Me.ToolStripFolderContextButtonDeleteFolder.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Trash_ui_ux_mobile_web_4960750
        Me.ToolStripFolderContextButtonDeleteFolder.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripFolderContextButtonDeleteFolder.Name = "ToolStripFolderContextButtonDeleteFolder"
        Me.ToolStripFolderContextButtonDeleteFolder.Size = New System.Drawing.Size(75, 24)
        Me.ToolStripFolderContextButtonDeleteFolder.Text = "&Löschen"
        '
        'ToolStripSeparator3
        '
        Me.ToolStripSeparator3.Name = "ToolStripSeparator3"
        Me.ToolStripSeparator3.Size = New System.Drawing.Size(213, 6)
        '
        'ToolStripFolderContextButtonShareFolder
        '
        Me.ToolStripFolderContextButtonShareFolder.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Group_ui_ux_mobile_web_4960717
        Me.ToolStripFolderContextButtonShareFolder.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripFolderContextButtonShareFolder.Name = "ToolStripFolderContextButtonShareFolder"
        Me.ToolStripFolderContextButtonShareFolder.Size = New System.Drawing.Size(83, 24)
        Me.ToolStripFolderContextButtonShareFolder.Text = "&Freigaben"
        '
        'ToolStripFolderContextButtonRefreshFilesList
        '
        Me.ToolStripFolderContextButtonRefreshFilesList.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_View_ui_ux_mobile_web_4960757
        Me.ToolStripFolderContextButtonRefreshFilesList.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripFolderContextButtonRefreshFilesList.Name = "ToolStripFolderContextButtonRefreshFilesList"
        Me.ToolStripFolderContextButtonRefreshFilesList.Size = New System.Drawing.Size(156, 24)
        Me.ToolStripFolderContextButtonRefreshFilesList.Text = "Datei-Liste aktualisieren"
        '
        'ToolStripFolderContextButtonProperties
        '
        Me.ToolStripFolderContextButtonProperties.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_setting_ui_ux_mobile_web_4960747
        Me.ToolStripFolderContextButtonProperties.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripFolderContextButtonProperties.Name = "ToolStripFolderContextButtonProperties"
        Me.ToolStripFolderContextButtonProperties.Size = New System.Drawing.Size(105, 24)
        Me.ToolStripFolderContextButtonProperties.Text = "Eigenschaften"
        '
        'ImageListFileIcons
        '
        Me.ImageListFileIcons.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit
        Me.ImageListFileIcons.ImageStream = CType(resources.GetObject("ImageListFileIcons.ImageStream"), System.Windows.Forms.ImageListStreamer)
        Me.ImageListFileIcons.TransparentColor = System.Drawing.Color.Transparent
        Me.ImageListFileIcons.Images.SetKeyName(0, "iconfinder_Home-ui-ux-mobile-web_4960719.png")
        Me.ImageListFileIcons.Images.SetKeyName(1, "iconfinder_bookmark-ui-ux-mobile-web_4960727.png")
        Me.ImageListFileIcons.Images.SetKeyName(2, "iconfinder_Folder-ui-ux-mobile-web_4960713.png")
        Me.ImageListFileIcons.Images.SetKeyName(3, "iconfinder_Home-ui-ux-mobile-web_4960719 - Shared.png")
        Me.ImageListFileIcons.Images.SetKeyName(4, "iconfinder_bookmark-ui-ux-mobile-web_4960727 - Shared.png")
        Me.ImageListFileIcons.Images.SetKeyName(5, "iconfinder_Folder-ui-ux-mobile-web_4960713 - Shared.png")
        Me.ImageListFileIcons.Images.SetKeyName(6, "iconfinder_Document-ui-ux-mobile-web-office-microsoftofficeico_4960706.png")
        Me.ImageListFileIcons.Images.SetKeyName(7, "iconfinder_Document-ui-ux-mobile-web-office-microsoftofficeico_4960706 - Shared.p" &
        "ng")
        '
        'ButtonCreateNewFolder
        '
        Me.ButtonCreateNewFolder.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.ButtonCreateNewFolder.Location = New System.Drawing.Point(14, 503)
        Me.ButtonCreateNewFolder.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.ButtonCreateNewFolder.Name = "ButtonCreateNewFolder"
        Me.ButtonCreateNewFolder.Size = New System.Drawing.Size(161, 27)
        Me.ButtonCreateNewFolder.TabIndex = 12
        Me.ButtonCreateNewFolder.Text = "&Neuen Ordner erstellen"
        Me.ButtonCreateNewFolder.UseVisualStyleBackColor = True
        '
        'SplitContainer
        '
        Me.SplitContainer.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.SplitContainer.Location = New System.Drawing.Point(14, 14)
        Me.SplitContainer.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.SplitContainer.Name = "SplitContainer"
        '
        'SplitContainer.Panel1
        '
        Me.SplitContainer.Panel1.Controls.Add(Me.TreeViewDmsFolders)
        Me.SplitContainer.Panel1MinSize = 100
        '
        'SplitContainer.Panel2
        '
        Me.SplitContainer.Panel2.Controls.Add(Me.ListViewDmsFiles)
        Me.SplitContainer.Panel2.Controls.Add(Me.FlowLayoutPanel1)
        Me.SplitContainer.Panel2MinSize = 100
        Me.SplitContainer.Size = New System.Drawing.Size(1032, 482)
        Me.SplitContainer.SplitterDistance = 324
        Me.SplitContainer.SplitterWidth = 5
        Me.SplitContainer.TabIndex = 13
        '
        'ListViewDmsFiles
        '
        Me.ListViewDmsFiles.Columns.AddRange(New System.Windows.Forms.ColumnHeader() {Me.ColumnHeaderFileName, Me.ColumnHeaderSize, Me.ColumnHeaderLastModifiedOn})
        Me.ListViewDmsFiles.ContextMenuStrip = Me.ContextMenuStripFile
        Me.ListViewDmsFiles.Dock = System.Windows.Forms.DockStyle.Fill
        Me.ListViewDmsFiles.FullRowSelect = True
        Me.ListViewDmsFiles.HideSelection = False
        Me.ListViewDmsFiles.LargeImageList = Me.ImageListFileIcons
        Me.ListViewDmsFiles.Location = New System.Drawing.Point(0, 92)
        Me.ListViewDmsFiles.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.ListViewDmsFiles.Name = "ListViewDmsFiles"
        Me.ListViewDmsFiles.ShowItemToolTips = True
        Me.ListViewDmsFiles.Size = New System.Drawing.Size(703, 390)
        Me.ListViewDmsFiles.SmallImageList = Me.ImageListFileIcons
        Me.ListViewDmsFiles.TabIndex = 2
        Me.ListViewDmsFiles.UseCompatibleStateImageBehavior = False
        Me.ListViewDmsFiles.View = System.Windows.Forms.View.Details
        '
        'ColumnHeaderFileName
        '
        Me.ColumnHeaderFileName.Text = "Name"
        '
        'ColumnHeaderSize
        '
        Me.ColumnHeaderSize.Text = "Size"
        '
        'ColumnHeaderLastModifiedOn
        '
        Me.ColumnHeaderLastModifiedOn.Text = "Last modification"
        '
        'ContextMenuStripFile
        '
        Me.ContextMenuStripFile.ImageScalingSize = New System.Drawing.Size(20, 20)
        Me.ContextMenuStripFile.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.ToolStripFileContextButtonUploadFile, Me.ToolStripFileContextButtonDownloadFile, Me.ToolStripFileContextButtonOpenPreviewFile, Me.ToolStripSeparator2, Me.ToolStripFileContextButtonCopyFile, Me.ToolStripFileContextButtonRenameFile, Me.ToolStripFileContextButtonMoveFile, Me.ToolStripFileContextButtonDeleteFile, Me.ToolStripSeparator1, Me.ToolStripFileContextButtonShareFile, Me.ToolStripFileContextButtonProperties})
        Me.ContextMenuStripFile.Name = "ContextMenuStripFile"
        Me.ContextMenuStripFile.Size = New System.Drawing.Size(166, 259)
        '
        'ToolStripFileContextButtonUploadFile
        '
        Me.ToolStripFileContextButtonUploadFile.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Upload_ui_ux_mobile_web_4960752
        Me.ToolStripFileContextButtonUploadFile.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripFileContextButtonUploadFile.Name = "ToolStripFileContextButtonUploadFile"
        Me.ToolStripFileContextButtonUploadFile.Size = New System.Drawing.Size(69, 24)
        Me.ToolStripFileContextButtonUploadFile.Text = "&Upload"
        '
        'ToolStripFileContextButtonDownloadFile
        '
        Me.ToolStripFileContextButtonDownloadFile.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Download_ui_ux_mobile_web_4960707
        Me.ToolStripFileContextButtonDownloadFile.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripFileContextButtonDownloadFile.Name = "ToolStripFileContextButtonDownloadFile"
        Me.ToolStripFileContextButtonDownloadFile.Size = New System.Drawing.Size(85, 24)
        Me.ToolStripFileContextButtonDownloadFile.Text = "&Download"
        '
        'ToolStripFileContextButtonOpenPreviewFile
        '
        Me.ToolStripFileContextButtonOpenPreviewFile.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_View_ui_ux_mobile_web_4960757
        Me.ToolStripFileContextButtonOpenPreviewFile.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripFileContextButtonOpenPreviewFile.Name = "ToolStripFileContextButtonOpenPreviewFile"
        Me.ToolStripFileContextButtonOpenPreviewFile.Size = New System.Drawing.Size(68, 24)
        Me.ToolStripFileContextButtonOpenPreviewFile.Text = "&Öffnen"
        '
        'ToolStripSeparator2
        '
        Me.ToolStripSeparator2.Name = "ToolStripSeparator2"
        Me.ToolStripSeparator2.Size = New System.Drawing.Size(162, 6)
        '
        'ToolStripFileContextButtonCopyFile
        '
        Me.ToolStripFileContextButtonCopyFile.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Duplicate_ui_ux_mobile_web_4960708
        Me.ToolStripFileContextButtonCopyFile.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripFileContextButtonCopyFile.Name = "ToolStripFileContextButtonCopyFile"
        Me.ToolStripFileContextButtonCopyFile.Size = New System.Drawing.Size(78, 24)
        Me.ToolStripFileContextButtonCopyFile.Text = "&Kopieren"
        '
        'ToolStripFileContextButtonRenameFile
        '
        Me.ToolStripFileContextButtonRenameFile.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Rename_ui_ux_mobile_web_4960746
        Me.ToolStripFileContextButtonRenameFile.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripFileContextButtonRenameFile.Name = "ToolStripFileContextButtonRenameFile"
        Me.ToolStripFileContextButtonRenameFile.Size = New System.Drawing.Size(103, 24)
        Me.ToolStripFileContextButtonRenameFile.Text = "&Umbenennen"
        '
        'ToolStripFileContextButtonMoveFile
        '
        Me.ToolStripFileContextButtonMoveFile.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Arrow_ui_ux_mobile_web_4960722
        Me.ToolStripFileContextButtonMoveFile.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripFileContextButtonMoveFile.Name = "ToolStripFileContextButtonMoveFile"
        Me.ToolStripFileContextButtonMoveFile.Size = New System.Drawing.Size(94, 24)
        Me.ToolStripFileContextButtonMoveFile.Text = "&Verschieben"
        '
        'ToolStripFileContextButtonDeleteFile
        '
        Me.ToolStripFileContextButtonDeleteFile.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Trash_ui_ux_mobile_web_4960750
        Me.ToolStripFileContextButtonDeleteFile.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripFileContextButtonDeleteFile.Name = "ToolStripFileContextButtonDeleteFile"
        Me.ToolStripFileContextButtonDeleteFile.Size = New System.Drawing.Size(75, 24)
        Me.ToolStripFileContextButtonDeleteFile.Text = "&Löschen"
        '
        'ToolStripSeparator1
        '
        Me.ToolStripSeparator1.Name = "ToolStripSeparator1"
        Me.ToolStripSeparator1.Size = New System.Drawing.Size(162, 6)
        '
        'ToolStripFileContextButtonShareFile
        '
        Me.ToolStripFileContextButtonShareFile.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Group_ui_ux_mobile_web_4960717
        Me.ToolStripFileContextButtonShareFile.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripFileContextButtonShareFile.Name = "ToolStripFileContextButtonShareFile"
        Me.ToolStripFileContextButtonShareFile.Size = New System.Drawing.Size(83, 24)
        Me.ToolStripFileContextButtonShareFile.Text = "&Freigaben"
        '
        'ToolStripFileContextButtonProperties
        '
        Me.ToolStripFileContextButtonProperties.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_setting_ui_ux_mobile_web_4960747
        Me.ToolStripFileContextButtonProperties.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripFileContextButtonProperties.Name = "ToolStripFileContextButtonProperties"
        Me.ToolStripFileContextButtonProperties.Size = New System.Drawing.Size(105, 24)
        Me.ToolStripFileContextButtonProperties.Text = "Eigenschaften"
        '
        'FlowLayoutPanel1
        '
        Me.FlowLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.FlowLayoutPanel1.Controls.Add(Me.ToolStripFileActions)
        Me.FlowLayoutPanel1.Controls.Add(Me.ToolStripFileShareActions)
        Me.FlowLayoutPanel1.Controls.Add(Me.ToolStripFolderShareActions)
        Me.FlowLayoutPanel1.Controls.Add(Me.ToolStripProperties)
        Me.FlowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top
        Me.FlowLayoutPanel1.Location = New System.Drawing.Point(0, 0)
        Me.FlowLayoutPanel1.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.FlowLayoutPanel1.Name = "FlowLayoutPanel1"
        Me.FlowLayoutPanel1.Size = New System.Drawing.Size(703, 92)
        Me.FlowLayoutPanel1.TabIndex = 0
        '
        'ToolStripFileActions
        '
        Me.ToolStripFileActions.Dock = System.Windows.Forms.DockStyle.None
        Me.ToolStripFileActions.ImageScalingSize = New System.Drawing.Size(19, 19)
        Me.ToolStripFileActions.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.ToolStripButtonUploadFile, Me.ToolStripButtonDownloadFile, Me.ToolStripButtonOpenFile, Me.ToolStripButtonDeleteFile, Me.ToolStripSeparatorBeforeCopyRenameMove, Me.ToolStripButtonCopyFile, Me.ToolStripButtonRenameFile, Me.ToolStripButtonMoveFile})
        Me.ToolStripFileActions.Location = New System.Drawing.Point(0, 0)
        Me.ToolStripFileActions.Name = "ToolStripFileActions"
        Me.ToolStripFileActions.Padding = New System.Windows.Forms.Padding(0, 0, 2, 0)
        Me.ToolStripFileActions.Size = New System.Drawing.Size(584, 26)
        Me.ToolStripFileActions.TabIndex = 19
        '
        'ToolStripButtonUploadFile
        '
        Me.ToolStripButtonUploadFile.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Upload_ui_ux_mobile_web_4960752
        Me.ToolStripButtonUploadFile.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonUploadFile.Name = "ToolStripButtonUploadFile"
        Me.ToolStripButtonUploadFile.Size = New System.Drawing.Size(68, 23)
        Me.ToolStripButtonUploadFile.Text = "&Upload"
        '
        'ToolStripButtonDownloadFile
        '
        Me.ToolStripButtonDownloadFile.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Download_ui_ux_mobile_web_4960707
        Me.ToolStripButtonDownloadFile.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonDownloadFile.Name = "ToolStripButtonDownloadFile"
        Me.ToolStripButtonDownloadFile.Size = New System.Drawing.Size(84, 23)
        Me.ToolStripButtonDownloadFile.Text = "&Download"
        '
        'ToolStripButtonOpenFile
        '
        Me.ToolStripButtonOpenFile.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_View_ui_ux_mobile_web_4960757
        Me.ToolStripButtonOpenFile.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonOpenFile.Name = "ToolStripButtonOpenFile"
        Me.ToolStripButtonOpenFile.Size = New System.Drawing.Size(67, 23)
        Me.ToolStripButtonOpenFile.Text = "&Öffnen"
        '
        'ToolStripButtonDeleteFile
        '
        Me.ToolStripButtonDeleteFile.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Trash_ui_ux_mobile_web_4960750
        Me.ToolStripButtonDeleteFile.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonDeleteFile.Name = "ToolStripButtonDeleteFile"
        Me.ToolStripButtonDeleteFile.Size = New System.Drawing.Size(74, 23)
        Me.ToolStripButtonDeleteFile.Text = "&Löschen"
        '
        'ToolStripSeparatorBeforeCopyRenameMove
        '
        Me.ToolStripSeparatorBeforeCopyRenameMove.Name = "ToolStripSeparatorBeforeCopyRenameMove"
        Me.ToolStripSeparatorBeforeCopyRenameMove.Size = New System.Drawing.Size(6, 26)
        '
        'ToolStripButtonCopyFile
        '
        Me.ToolStripButtonCopyFile.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Duplicate_ui_ux_mobile_web_4960708
        Me.ToolStripButtonCopyFile.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonCopyFile.Name = "ToolStripButtonCopyFile"
        Me.ToolStripButtonCopyFile.Size = New System.Drawing.Size(77, 23)
        Me.ToolStripButtonCopyFile.Text = "&Kopieren"
        '
        'ToolStripButtonRenameFile
        '
        Me.ToolStripButtonRenameFile.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Rename_ui_ux_mobile_web_4960746
        Me.ToolStripButtonRenameFile.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonRenameFile.Name = "ToolStripButtonRenameFile"
        Me.ToolStripButtonRenameFile.Size = New System.Drawing.Size(102, 23)
        Me.ToolStripButtonRenameFile.Text = "&Umbenennen"
        '
        'ToolStripButtonMoveFile
        '
        Me.ToolStripButtonMoveFile.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Arrow_ui_ux_mobile_web_4960722
        Me.ToolStripButtonMoveFile.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonMoveFile.Name = "ToolStripButtonMoveFile"
        Me.ToolStripButtonMoveFile.Size = New System.Drawing.Size(93, 23)
        Me.ToolStripButtonMoveFile.Text = "&Verschieben"
        '
        'ToolStripFileShareActions
        '
        Me.ToolStripFileShareActions.Dock = System.Windows.Forms.DockStyle.None
        Me.ToolStripFileShareActions.ImageScalingSize = New System.Drawing.Size(19, 19)
        Me.ToolStripFileShareActions.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.ToolStripButtonSharingsFile})
        Me.ToolStripFileShareActions.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow
        Me.ToolStripFileShareActions.Location = New System.Drawing.Point(0, 26)
        Me.ToolStripFileShareActions.Name = "ToolStripFileShareActions"
        Me.ToolStripFileShareActions.Padding = New System.Windows.Forms.Padding(0, 0, 2, 0)
        Me.ToolStripFileShareActions.Size = New System.Drawing.Size(145, 26)
        Me.ToolStripFileShareActions.TabIndex = 22
        '
        'ToolStripButtonSharingsFile
        '
        Me.ToolStripButtonSharingsFile.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Group_ui_ux_mobile_web_4960717
        Me.ToolStripButtonSharingsFile.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonSharingsFile.Name = "ToolStripButtonSharingsFile"
        Me.ToolStripButtonSharingsFile.Size = New System.Drawing.Size(132, 23)
        Me.ToolStripButtonSharingsFile.Text = "&Freigaben der Datei"
        '
        'ToolStripFolderShareActions
        '
        Me.ToolStripFolderShareActions.Dock = System.Windows.Forms.DockStyle.None
        Me.ToolStripFolderShareActions.ImageScalingSize = New System.Drawing.Size(19, 19)
        Me.ToolStripFolderShareActions.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.ToolStripButtonSharingsFolder})
        Me.ToolStripFolderShareActions.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow
        Me.ToolStripFolderShareActions.Location = New System.Drawing.Point(145, 26)
        Me.ToolStripFolderShareActions.Name = "ToolStripFolderShareActions"
        Me.ToolStripFolderShareActions.Padding = New System.Windows.Forms.Padding(0, 0, 2, 0)
        Me.ToolStripFolderShareActions.Size = New System.Drawing.Size(161, 26)
        Me.ToolStripFolderShareActions.TabIndex = 20
        '
        'ToolStripButtonSharingsFolder
        '
        Me.ToolStripButtonSharingsFolder.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Group_ui_ux_mobile_web_4960717
        Me.ToolStripButtonSharingsFolder.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonSharingsFolder.Name = "ToolStripButtonSharingsFolder"
        Me.ToolStripButtonSharingsFolder.Size = New System.Drawing.Size(148, 23)
        Me.ToolStripButtonSharingsFolder.Text = "&Freigaben des Ordners"
        '
        'ToolStripProperties
        '
        Me.ToolStripProperties.Dock = System.Windows.Forms.DockStyle.None
        Me.ToolStripProperties.ImageScalingSize = New System.Drawing.Size(19, 19)
        Me.ToolStripProperties.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.ToolStripButtonPropertiesFile, Me.ToolStripButtonPropertiesFolder, Me.ToolStripButtonRefreshFilesList})
        Me.ToolStripProperties.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow
        Me.ToolStripProperties.Location = New System.Drawing.Point(0, 52)
        Me.ToolStripProperties.Name = "ToolStripProperties"
        Me.ToolStripProperties.Padding = New System.Windows.Forms.Padding(0, 0, 2, 0)
        Me.ToolStripProperties.Size = New System.Drawing.Size(492, 26)
        Me.ToolStripProperties.TabIndex = 21
        '
        'ToolStripButtonPropertiesFile
        '
        Me.ToolStripButtonPropertiesFile.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_setting_ui_ux_mobile_web_4960747
        Me.ToolStripButtonPropertiesFile.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonPropertiesFile.Name = "ToolStripButtonPropertiesFile"
        Me.ToolStripButtonPropertiesFile.Size = New System.Drawing.Size(154, 23)
        Me.ToolStripButtonPropertiesFile.Text = "Eigenschaften der Datei"
        '
        'ToolStripButtonPropertiesFolder
        '
        Me.ToolStripButtonPropertiesFolder.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_setting_ui_ux_mobile_web_4960747
        Me.ToolStripButtonPropertiesFolder.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonPropertiesFolder.Name = "ToolStripButtonPropertiesFolder"
        Me.ToolStripButtonPropertiesFolder.Size = New System.Drawing.Size(170, 23)
        Me.ToolStripButtonPropertiesFolder.Text = "Eigenschaften des Ordners"
        '
        'ToolStripButtonRefreshFilesList
        '
        Me.ToolStripButtonRefreshFilesList.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.Refresh
        Me.ToolStripButtonRefreshFilesList.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonRefreshFilesList.Name = "ToolStripButtonRefreshFilesList"
        Me.ToolStripButtonRefreshFilesList.Size = New System.Drawing.Size(155, 23)
        Me.ToolStripButtonRefreshFilesList.Text = "Datei-Liste aktualisieren"
        '
        'ButtonShowFiles
        '
        Me.ButtonShowFiles.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.ButtonShowFiles.Appearance = System.Windows.Forms.Appearance.Button
        Me.ButtonShowFiles.AutoSize = True
        Me.ButtonShowFiles.Location = New System.Drawing.Point(182, 504)
        Me.ButtonShowFiles.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.ButtonShowFiles.Name = "ButtonShowFiles"
        Me.ButtonShowFiles.Size = New System.Drawing.Size(107, 25)
        Me.ButtonShowFiles.TabIndex = 18
        Me.ButtonShowFiles.Text = "Dateien an&zeigen"
        Me.ButtonShowFiles.UseVisualStyleBackColor = True
        '
        'BottomToolStripPanel
        '
        Me.BottomToolStripPanel.Location = New System.Drawing.Point(0, 0)
        Me.BottomToolStripPanel.Name = "BottomToolStripPanel"
        Me.BottomToolStripPanel.Orientation = System.Windows.Forms.Orientation.Horizontal
        Me.BottomToolStripPanel.RowMargin = New System.Windows.Forms.Padding(3, 0, 0, 0)
        Me.BottomToolStripPanel.Size = New System.Drawing.Size(0, 0)
        '
        'TopToolStripPanel
        '
        Me.TopToolStripPanel.Location = New System.Drawing.Point(0, 0)
        Me.TopToolStripPanel.Name = "TopToolStripPanel"
        Me.TopToolStripPanel.Orientation = System.Windows.Forms.Orientation.Horizontal
        Me.TopToolStripPanel.RowMargin = New System.Windows.Forms.Padding(3, 0, 0, 0)
        Me.TopToolStripPanel.Size = New System.Drawing.Size(0, 0)
        '
        'RightToolStripPanel
        '
        Me.RightToolStripPanel.Location = New System.Drawing.Point(0, 0)
        Me.RightToolStripPanel.Name = "RightToolStripPanel"
        Me.RightToolStripPanel.Orientation = System.Windows.Forms.Orientation.Horizontal
        Me.RightToolStripPanel.RowMargin = New System.Windows.Forms.Padding(3, 0, 0, 0)
        Me.RightToolStripPanel.Size = New System.Drawing.Size(0, 0)
        '
        'LeftToolStripPanel
        '
        Me.LeftToolStripPanel.Location = New System.Drawing.Point(0, 0)
        Me.LeftToolStripPanel.Name = "LeftToolStripPanel"
        Me.LeftToolStripPanel.Orientation = System.Windows.Forms.Orientation.Horizontal
        Me.LeftToolStripPanel.RowMargin = New System.Windows.Forms.Padding(3, 0, 0, 0)
        Me.LeftToolStripPanel.Size = New System.Drawing.Size(0, 0)
        '
        'ContentPanel
        '
        Me.ContentPanel.Size = New System.Drawing.Size(100, 125)
        '
        'ButtonClose
        '
        Me.ButtonClose.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.ButtonClose.Location = New System.Drawing.Point(959, 503)
        Me.ButtonClose.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.ButtonClose.Name = "ButtonClose"
        Me.ButtonClose.Size = New System.Drawing.Size(88, 27)
        Me.ButtonClose.TabIndex = 19
        Me.ButtonClose.Text = "&Schließen"
        Me.ButtonClose.UseVisualStyleBackColor = True
        '
        'ToolTipFileSystemItems
        '
        Me.ToolTipFileSystemItems.ToolTipIcon = System.Windows.Forms.ToolTipIcon.Warning
        Me.ToolTipFileSystemItems.ToolTipTitle = "Konflikt"
        '
        'DmsBrowser
        '
        Me.AcceptButton = Me.ButtonOkay
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.CancelButton = Me.ButtonCancel
        Me.ClientSize = New System.Drawing.Size(1060, 543)
        Me.Controls.Add(Me.ButtonClose)
        Me.Controls.Add(Me.ButtonShowFiles)
        Me.Controls.Add(Me.SplitContainer)
        Me.Controls.Add(Me.ButtonCreateNewFolder)
        Me.Controls.Add(Me.ButtonCancel)
        Me.Controls.Add(Me.ButtonOkay)
        Me.Icon = CType(resources.GetObject("$this.Icon"), System.Drawing.Icon)
        Me.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.MinimumSize = New System.Drawing.Size(575, 263)
        Me.Name = "DmsBrowser"
        Me.Text = "DMS Ordner durchsuchen"
        Me.ContextMenuStripFolder.ResumeLayout(False)
        Me.SplitContainer.Panel1.ResumeLayout(False)
        Me.SplitContainer.Panel2.ResumeLayout(False)
        CType(Me.SplitContainer, System.ComponentModel.ISupportInitialize).EndInit()
        Me.SplitContainer.ResumeLayout(False)
        Me.ContextMenuStripFile.ResumeLayout(False)
        Me.FlowLayoutPanel1.ResumeLayout(False)
        Me.FlowLayoutPanel1.PerformLayout()
        Me.ToolStripFileActions.ResumeLayout(False)
        Me.ToolStripFileActions.PerformLayout()
        Me.ToolStripFileShareActions.ResumeLayout(False)
        Me.ToolStripFileShareActions.PerformLayout()
        Me.ToolStripFolderShareActions.ResumeLayout(False)
        Me.ToolStripFolderShareActions.PerformLayout()
        Me.ToolStripProperties.ResumeLayout(False)
        Me.ToolStripProperties.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents ButtonCancel As Button
    Friend WithEvents ButtonOkay As Button
    Friend WithEvents TreeViewDmsFolders As TreeView
    Friend WithEvents ButtonCreateNewFolder As Button
    Friend WithEvents SplitContainer As SplitContainer
    Friend WithEvents ListViewDmsFiles As ListView
    Friend WithEvents ColumnHeaderFileName As ColumnHeader
    Friend WithEvents ColumnHeaderSize As ColumnHeader
    Friend WithEvents ColumnHeaderLastModifiedOn As ColumnHeader
    Friend WithEvents ImageListFileIcons As ImageList
    Friend WithEvents ButtonShowFiles As CheckBox
    Friend WithEvents ToolStripFileActions As ToolStrip
    Friend WithEvents ToolStripButtonUploadFile As ToolStripButton
    Friend WithEvents ToolStripButtonDownloadFile As ToolStripButton
    Friend WithEvents ToolStripButtonDeleteFile As ToolStripButton
    Friend WithEvents ToolStripButtonCopyFile As ToolStripButton
    Friend WithEvents ToolStripButtonRenameFile As ToolStripButton
    Friend WithEvents ToolStripButtonMoveFile As ToolStripButton
    Friend WithEvents ToolStripSeparatorBeforeCopyRenameMove As ToolStripSeparator
    Friend WithEvents ButtonClose As Button
    Friend WithEvents ToolTipFileSystemItems As ToolTip
    Friend WithEvents FlowLayoutPanel1 As FlowLayoutPanel
    Friend WithEvents ToolStripFolderShareActions As ToolStrip
    Friend WithEvents ToolStripButtonSharingsFolder As ToolStripButton
    Friend WithEvents BottomToolStripPanel As ToolStripPanel
    Friend WithEvents TopToolStripPanel As ToolStripPanel
    Friend WithEvents RightToolStripPanel As ToolStripPanel
    Friend WithEvents LeftToolStripPanel As ToolStripPanel
    Friend WithEvents ContentPanel As ToolStripContentPanel
    Friend WithEvents ToolStripProperties As ToolStrip
    Friend WithEvents ToolStripButtonPropertiesFile As ToolStripButton
    Friend WithEvents ToolStripFileShareActions As ToolStrip
    Friend WithEvents ToolStripButtonSharingsFile As ToolStripButton
    Friend WithEvents ToolStripButtonPropertiesFolder As ToolStripButton
    Friend WithEvents ToolStripButtonRefreshFilesList As ToolStripButton
    Friend WithEvents ContextMenuStripFolder As ContextMenuStrip
    Friend WithEvents ContextMenuStripFile As ContextMenuStrip
    Friend WithEvents ToolStripFolderContextButtonShareFolder As ToolStripButton
    Friend WithEvents ToolStripFolderContextButtonRefreshFilesList As ToolStripButton
    Friend WithEvents ToolStripFolderContextButtonProperties As ToolStripButton
    Friend WithEvents ToolStripFolderContextButtonCopyFolder As ToolStripButton
    Friend WithEvents ToolStripFolderContextButtonRenameFolder As ToolStripButton
    Friend WithEvents ToolStripFolderContextButtonMoveFolder As ToolStripButton
    Friend WithEvents ToolStripFolderContextButtonDeleteFolder As ToolStripButton
    Friend WithEvents ToolStripFolderContextButtonNewFolder As ToolStripButton
    Friend WithEvents ToolStripSeparator3 As ToolStripSeparator
    Friend WithEvents ToolStripFileContextButtonUploadFile As ToolStripButton
    Friend WithEvents ToolStripFileContextButtonDownloadFile As ToolStripButton
    Friend WithEvents ToolStripSeparator2 As ToolStripSeparator
    Friend WithEvents ToolStripFileContextButtonCopyFile As ToolStripButton
    Friend WithEvents ToolStripFileContextButtonRenameFile As ToolStripButton
    Friend WithEvents ToolStripFileContextButtonMoveFile As ToolStripButton
    Friend WithEvents ToolStripFileContextButtonDeleteFile As ToolStripButton
    Friend WithEvents ToolStripSeparator1 As ToolStripSeparator
    Friend WithEvents ToolStripFileContextButtonShareFile As ToolStripButton
    Friend WithEvents ToolStripFileContextButtonProperties As ToolStripButton
    Friend WithEvents ToolStripButtonOpenFile As ToolStripButton
    Friend WithEvents ToolStripFileContextButtonOpenPreviewFile As ToolStripButton
End Class
