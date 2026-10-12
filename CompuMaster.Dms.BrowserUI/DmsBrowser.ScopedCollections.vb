Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Partial Public Class DmsBrowser
    Private Sub InitializeScopedCollectionActions()
        AddHandler TreeViewDmsFolders.BeforeLabelEdit, AddressOf BeforeFolderLabelEdit
        AddHandler TreeViewDmsFolders.AfterLabelEdit, AddressOf CommitFolderLabelEdit
        AddHandler TreeViewDmsFolders.KeyDown, AddressOf RenameFolderWithKeyboard
        AddHandler TreeViewDmsFolders.AfterSelect, Sub(sender, e) UpdateScopedCollectionActions()
        AddHandler TreeViewDmsFolders.EnabledChanged, Sub(sender, e) UpdateScopedCollectionActions()
        AddHandler ContextMenuStripFolder.Opening, Sub(sender, e) UpdateScopedCollectionActions()
    End Sub

    Friend Function CanRenameFolderNode(node As TreeNode, Optional ownsOperation As Boolean = False) As Boolean
        Dim provider = If(_DmsProviderOverride, DmsProviderInstance)
        Dim resource = TryCast(node?.Tag, NodeTagData)?.DmsResourceItem
        If IsDisposed OrElse Disposing OrElse provider Is Nothing OrElse node Is Nothing OrElse node.TreeView IsNot TreeViewDmsFolders OrElse
           node Is RootNode OrElse resource Is Nothing OrElse String.IsNullOrWhiteSpace(resource.FullName) OrElse resource.ItemType = DmsResourceItem.ItemTypes.Root OrElse
           (resource.ItemType <> DmsResourceItem.ItemTypes.Folder AndAlso resource.ItemType <> DmsResourceItem.ItemTypes.Collection) OrElse
           (AllowedActions And FileOrFolderActions.AllowCopyRenameMoveFiles) = 0 OrElse IsCollectionManagementRestricted(resource) OrElse
           Not IsWithinBrowserRoot(resource.FullName) Then Return False
        If resource.ItemType = DmsResourceItem.ItemTypes.Collection AndAlso Not provider.SupportsCollections Then Return False
        If String.Equals(resource.FullName.Trim(provider.DirectorySeparator), If(provider.BrowseInRootFolderName, "").Trim(provider.DirectorySeparator), StringComparison.Ordinal) Then Return False
        If HasScopedBrowserRoot AndAlso String.Equals(resource.FullName.Trim(provider.DirectorySeparator), InitialFolder.Trim(provider.DirectorySeparator), StringComparison.Ordinal) Then Return False
        Return ownsOperation OrElse (TreeViewDmsFolders.Enabled AndAlso Not TransferRunning AndAlso Not ResourceActionRunning AndAlso Not DragDownloadRunning)
    End Function

    Private Sub BeforeFolderLabelEdit(sender As Object, e As NodeLabelEditEventArgs)
        e.CancelEdit = Not CanRenameFolderNode(e.Node)
    End Sub

    Private Async Sub CommitFolderLabelEdit(sender As Object, e As NodeLabelEditEventArgs)
        'The native control must never commit an unconfirmed cosmetic rename.
        e.CancelEdit = True
        If e.Label Is Nothing OrElse Not CanRenameFolderNode(e.Node) Then Return
        'Finish the native label-edit notification before refreshing/replacing nodes.
        Await Threading.Tasks.Task.Yield()
        Await RunFolderActionAsync(ResourceAction.Rename, e.Node, e.Label)
    End Sub

    Private Sub RenameFolderWithKeyboard(sender As Object, e As KeyEventArgs)
        If e.KeyCode <> Keys.F2 OrElse e.Modifiers <> Keys.None Then Return
        e.Handled = True
        e.SuppressKeyPress = True
        Dim node = TreeViewDmsFolders.SelectedNode
        If CanRenameFolderNode(node) Then node.BeginEdit()
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

    Private Function IsCollectionManagementRestricted(item As DmsResourceItem) As Boolean
        Dim provider = If(_DmsProviderOverride, DmsProviderInstance)
        'The current capability API does not describe collection placement; keep this limitation in its owning adapter family.
        If Not TypeOf provider Is CenterDeviceDmsProviderBase Then Return False
        If item Is Nothing OrElse item.ItemType <> DmsResourceItem.ItemTypes.Collection Then Return False
        If HasScopedBrowserRoot Then Return True
        'Changing the configured start path does not immediately replace the displayed tree.
        Dim visibleRoot = TryCast(RootNode?.Tag, NodeTagData)?.DmsResourceItem
        Return visibleRoot IsNot Nothing AndAlso visibleRoot.ItemType = DmsResourceItem.ItemTypes.Collection AndAlso
            String.Equals(visibleRoot.FullName, item.FullName, StringComparison.Ordinal)
    End Function

    Friend Sub EnsureCollectionActionWithinBrowserScope(item As DmsResourceItem)
        If IsCollectionManagementRestricted(item) Then Throw New DmsUserInputInvalidException(UiStrings.GetText("CollectionManagementUnavailableInScopedBrowser"))
        If item Is Nothing OrElse Not HasScopedBrowserRoot Then Return
        If Not IsWithinBrowserRoot(item.FullName) Then Throw New DmsUserInputInvalidException(UiStrings.GetText("DestinationOutsideBrowserRoot"))
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
        Dim blocked = IsCollectionManagementRestricted(item)
        Dim shareAllowed = Not blocked AndAlso provider.SupportsSharingSetup AndAlso (AllowedActions And FileOrFolderActions.AllowSharings) <> 0
        Dim enabled = TreeViewDmsFolders.Enabled AndAlso Not TransferRunning AndAlso Not ResourceActionRunning
        Me.UpdateFolderRootMutationAccess(blocked)
        ToolStripButtonSharingsFolder.Available = shareAllowed
        ToolStripButtonSharingsFolder.Enabled = shareAllowed AndAlso enabled
        ToolStripFolderContextButtonShareFolder.Available = shareAllowed
        ToolStripFolderContextButtonShareFolder.Enabled = shareAllowed AndAlso enabled
        ToolStripFolderShareActions.Visible = shareAllowed
    End Sub
End Class
