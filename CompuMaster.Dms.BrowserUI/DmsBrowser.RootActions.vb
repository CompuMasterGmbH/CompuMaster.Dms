Imports System.Windows.Forms
Imports CompuMaster.Dms.Data

Partial Public Class DmsBrowser
    Private Sub UpdateFolderRootMutationAccess()
        Dim node = CurrentSelectedFolderNode()
        Dim item = TryCast(node?.Tag, NodeTagData)?.DmsResourceItem
        'A scoped root remains the browser root even when backed by an ordinary folder or collection.
        Dim mutable = node IsNot Nothing AndAlso node IsNot RootNode AndAlso item IsNot Nothing AndAlso item.ItemType <> DmsResourceItem.ItemTypes.Root
        Dim copyAllowed = mutable AndAlso (AllowedActions And FileOrFolderActions.AllowCopyRenameMoveFiles) <> 0
        Dim deleteAllowed = mutable AndAlso (AllowedActions And FileOrFolderActions.AllowDeleteFiles) <> 0
        Dim enabled = TreeViewDmsFolders.Enabled AndAlso Not TransferRunning AndAlso Not ResourceActionRunning
        For Each button As ToolStripItem In {ToolStripFolderContextButtonCopyFolder, ToolStripFolderContextButtonRenameFolder, ToolStripFolderContextButtonMoveFolder}
            button.Available = copyAllowed
            button.Enabled = copyAllowed AndAlso enabled
        Next
        ToolStripFolderContextButtonDeleteFolder.Available = deleteAllowed
        ToolStripFolderContextButtonDeleteFolder.Enabled = deleteAllowed AndAlso enabled
    End Sub
End Class
