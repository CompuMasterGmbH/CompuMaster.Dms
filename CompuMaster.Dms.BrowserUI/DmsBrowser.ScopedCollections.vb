Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Partial Public Class DmsBrowser
    Private Sub InitializeScopedCollectionActions()
        AddHandler TreeViewDmsFolders.AfterSelect, Sub(sender, e) UpdateScopedCollectionActions()
        AddHandler TreeViewDmsFolders.EnabledChanged, Sub(sender, e) UpdateScopedCollectionActions()
        AddHandler ContextMenuStripFolder.Opening, Sub(sender, e) UpdateScopedCollectionActions()
    End Sub

    Private ReadOnly Property HasScopedBrowserRoot As Boolean
        Get
            Dim provider = If(_DmsProviderOverride, DmsProviderInstance)
            Return provider IsNot Nothing AndAlso Not String.IsNullOrEmpty(InitialFolder) AndAlso
                Not String.Equals(InitialFolder.Trim(provider.DirectorySeparator), If(provider.BrowseInRootFolderName, "").Trim(provider.DirectorySeparator), StringComparison.Ordinal)
        End Get
    End Property

    Private Function IsWithinBrowserRoot(path As String) As Boolean
        If Not HasScopedBrowserRoot Then Return True
        Dim root = InitialFolder.Trim(DmsProvider.DirectorySeparator)
        Dim candidate = If(path, "").Trim(DmsProvider.DirectorySeparator)
        Return String.Equals(candidate, root, StringComparison.Ordinal) OrElse candidate.StartsWith(root & DmsProvider.DirectorySeparator, StringComparison.Ordinal)
    End Function

    Friend Sub EnsureCollectionActionWithinBrowserScope(item As DmsResourceItem)
        If item Is Nothing OrElse Not HasScopedBrowserRoot Then Return
        If Not IsWithinBrowserRoot(item.FullName) Then Throw New DmsUserInputInvalidException(UiStrings.GetText("DestinationOutsideBrowserRoot"))
        If item.ItemType = DmsResourceItem.ItemTypes.Collection Then Throw New DmsUserInputInvalidException(UiStrings.GetText("CollectionManagementUnavailableInScopedBrowser"))
    End Sub

    Private Function NewDirectoryPathWithinBrowser(parentNode As TreeNode, folderName As String) As String
        If parentNode Is Nothing OrElse parentNode.TreeView IsNot TreeViewDmsFolders Then Throw New DmsUserInputInvalidException(UiStrings.GetText("NoFolderSelected"))
        If Not IsValidResourceName(folderName, DmsProvider.DirectorySeparator) Then Throw New DmsUserInputInvalidException(UiStrings.GetText("InvalidDestinationName"))
        Dim data = TryCast(parentNode.Tag, NodeTagData)
        If data Is Nothing Then Throw New DmsUserInputInvalidException(UiStrings.GetText("NoFolderSelected"))
        Dim parentPath = If(data.DmsResourceItem?.FullName, DmsProvider.BrowseInRootFolderName)
        If Not IsWithinBrowserRoot(parentPath) Then Throw New DmsUserInputInvalidException(UiStrings.GetText("DestinationOutsideBrowserRoot"))
        Return DmsProvider.CombinePath(parentPath, folderName)
    End Function

    Private Sub UpdateScopedCollectionActions()
        Dim provider = If(_DmsProviderOverride, DmsProviderInstance)
        If provider Is Nothing Then Return
        Dim item = TryCast(TreeViewDmsFolders.SelectedNode?.Tag, NodeTagData)?.DmsResourceItem
        Dim blocked = HasScopedBrowserRoot AndAlso item IsNot Nothing AndAlso item.ItemType = DmsResourceItem.ItemTypes.Collection
        Dim copyAllowed = Not blocked AndAlso (AllowedActions And FileOrFolderActions.AllowCopyRenameMoveFiles) <> 0
        Dim deleteAllowed = Not blocked AndAlso (AllowedActions And FileOrFolderActions.AllowDeleteFiles) <> 0
        Dim shareAllowed = Not blocked AndAlso provider.SupportsSharingSetup AndAlso (AllowedActions And FileOrFolderActions.AllowSharings) <> 0
        Dim enabled = TreeViewDmsFolders.Enabled AndAlso Not TransferRunning AndAlso Not ResourceActionRunning
        For Each control In New ToolStripItem() {ToolStripFolderContextButtonCopyFolder, ToolStripFolderContextButtonRenameFolder, ToolStripFolderContextButtonMoveFolder}
            control.Available = copyAllowed
            control.Enabled = copyAllowed AndAlso enabled
        Next
        ToolStripFolderContextButtonDeleteFolder.Available = deleteAllowed
        ToolStripFolderContextButtonDeleteFolder.Enabled = deleteAllowed AndAlso enabled
        ToolStripButtonSharingsFolder.Available = shareAllowed
        ToolStripButtonSharingsFolder.Enabled = shareAllowed AndAlso enabled
        ToolStripFolderContextButtonShareFolder.Available = shareAllowed
        ToolStripFolderContextButtonShareFolder.Enabled = shareAllowed AndAlso enabled
        ToolStripFolderShareActions.Visible = shareAllowed
    End Sub
End Class
