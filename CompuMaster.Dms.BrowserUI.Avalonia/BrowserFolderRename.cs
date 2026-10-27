using CompuMaster.Dms.Data;
using global::Avalonia.Controls;
using global::Avalonia.VisualTree;

namespace CompuMaster.Dms.BrowserUI.Avalonia;

public sealed partial class DmsBrowserControl
{
    private TextBox? editingFolder;
    private BrowserNode? editedNode;
    private DmsResourceItem? editedResource;
    internal void BeginFolderRename()
    {
        if (IsBusy || !Allowed(FileOrFolderActions.AllowCopyRenameMoveFiles) || !MutableFolder) return;
        CancelFolderRename();
        var node = Session.SelectedNode;
        var editor = tree.GetVisualDescendants().OfType<TextBox>()
            .FirstOrDefault(input => input.Name == "FolderNameEditor" && ReferenceEquals(input.DataContext, node));
        if (editor is null) return;
        editedNode = node; editedResource = node!.Item;
        editingFolder = editor; editor.Text = node.Name; editor.IsVisible = true;
        if (editor.Parent is Grid panel) panel.Children.OfType<TextBlock>().Single().IsVisible = false;
        editor.Focus(); editor.SelectAll();
    }
    private void CancelFolderRename()
    {
        var editor = editingFolder;
        editingFolder = null; editedNode = null; editedResource = null;
        if (editor is null) return;
        editor.IsVisible = false;
        if (editor.Parent is Grid panel) panel.Children.OfType<TextBlock>().Single().IsVisible = true;
    }
    internal async Task CommitFolderRenameAsync()
    {
        var node = editedNode; var resource = editedResource;
        var name = editingFolder?.Text?.Trim();
        CancelFolderRename();
        if (node is null || resource is null || name is null || name == resource.Name) return;
        if (IsBusy || !ReferenceEquals(Session.SelectedNode, node) || !ReferenceEquals(node.Item, resource) ||
            !Session.AllNodes().Contains(node) || !MutableFolder) return;
        await UiAsync(async () =>
        {
            Session.ValidateName(name);
            var path = Session.Provider.CombinePath(Session.Provider.ParentDirectoryPath(resource.FullName), name);
            if (await Session.ResourceActionAsync(resource, path, ResourceAction.Rename, question => Dialogs.ConfirmAsync(Owner, question)))
                await Session.SelectPathAsync(Session.RelativePath(path));
        });
    }
}
