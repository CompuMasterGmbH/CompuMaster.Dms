using CompuMaster.Dms.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace CompuMaster.Dms.BrowserUI.WinUI;

public sealed partial class DmsBrowserControl
{
    private TextBox? editingFolder;
    private BrowserNode? editedNode;
    private DmsResourceItem? editedResource;
    private TreeViewNode? editedTreeNode;
    internal void BeginFolderRename()
    {
        if (IsBusy || !Allowed(FileOrFolderActions.AllowCopyRenameMoveFiles) || !MutableFolder || tree.SelectedNode is not { } treeNode) return;
        CancelFolderRename(); editedNode = Session.SelectedNode; editedResource = editedNode!.Item; editedTreeNode = treeNode;
        editingFolder = new TextBox { Name = "FolderNameEditor", Text = editedNode.Name, MinWidth = 140 };
        treeNode.Content = editingFolder;
        editingFolder.Loaded += (_, _) => { editingFolder?.Focus(FocusState.Programmatic); editingFolder?.SelectAll(); };
        editingFolder.KeyDown += async (_, e) =>
        {
            if (e.Key == VirtualKey.Escape) { e.Handled = true; CancelFolderRename(); }
            else if (e.Key == VirtualKey.Enter) { e.Handled = true; await CommitFolderRenameAsync(); }
        };
        editingFolder.LostFocus += async (_, _) => { if (editingFolder is not null) await CommitFolderRenameAsync(); };
    }
    private void CancelFolderRename()
    {
        var node = editedTreeNode; var model = editedNode;
        editingFolder = null; editedTreeNode = null; editedNode = null; editedResource = null;
        if (node is not null && model is not null) node.Content = BrowserIcons.DirectoryRow(model);
    }
    internal async Task CommitFolderRenameAsync()
    {
        var node = editedNode; var resource = editedResource; var name = editingFolder?.Text?.Trim(); CancelFolderRename();
        if (node is null || resource is null || name is null || name == resource.Name) return;
        if (IsBusy || !ReferenceEquals(Session.SelectedNode, node) || !ReferenceEquals(node.Item, resource) || !Session.AllNodes().Contains(node) || !MutableFolder) return;
        await UiAsync(async () =>
        {
            Session.ValidateName(name);
            var path = Session.Provider.CombinePath(Session.Provider.ParentDirectoryPath(resource.FullName), name);
            if (await Session.ResourceActionAsync(resource, path, ResourceAction.Rename, question => Dialogs.ConfirmAsync(Owner, question, source: this)))
                await Session.SelectPathAsync(Session.RelativePath(path));
        });
    }
}
