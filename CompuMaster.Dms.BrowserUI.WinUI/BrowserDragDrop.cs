using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace CompuMaster.Dms.BrowserUI.WinUI;

internal sealed record RemoteDrag(BaseDmsProvider Provider, string? Instance, string? Root, DmsResourceItem[] Files);
public sealed partial class DmsBrowserControl
{
    private const string MoveFormat = "CompuMaster.Dms.WinUI.Move";
    private RemoteDrag? activeDrag;
    private string? dragNonce;
    private BrowserNode? droppedTarget;
    private Exception? dragFailure;
    private void InitializeDragAndDrop()
    {
        tree.AllowDrop = files.AllowDrop = true; files.CanDragItems = true;
        foreach (var target in new Control[] { tree, files })
        {
            target.DragOver += async (_, e) => await DragOverAsync(target, e);
            target.Drop += async (_, e) => await DropAsync(target, e);
        }
        files.DragItemsStarting += (_, e) =>
        {
            if (IsBusy || !HasFiles || !Allowed(FileOrFolderActions.AllowDownloadFiles) && !Allowed(FileOrFolderActions.AllowCopyRenameMoveFiles)) { e.Cancel = true; return; }
            var selected = SelectedFiles();
            activeDrag = new RemoteDrag(Session.Provider, (DmsProvider as IDmsInstanceProvider)?.CurrentDmsInstance?.ID, Options.InitialFolder,
                selected.Select(file => new DmsResourceItem { ItemType = file.ItemType, Name = file.Name, FullName = file.FullName,
                    ExtendedInfosFileID = file.ExtendedInfosFileID, ExtendedInfosAssignedFolderID = file.ExtendedInfosAssignedFolderID, ExtendedInfosAssignedCollectionID = file.ExtendedInfosAssignedCollectionID }).ToArray());
            var context = activeDrag;
            dragNonce = Guid.NewGuid().ToString("N"); droppedTarget = null; dragFailure = null; dragRunning = true;
            e.Data.SetData(MoveFormat, dragNonce);
            e.Data.RequestedOperation = Allowed(FileOrFolderActions.AllowCopyRenameMoveFiles) ? DataPackageOperation.Move : DataPackageOperation.None;
            if (Allowed(FileOrFolderActions.AllowDownloadFiles))
            {
                e.Data.RequestedOperation |= DataPackageOperation.Copy;
                e.Data.SetDataProvider(StandardDataFormats.StorageItems, request =>
                {
                    var deferral = request.GetDeferral();
                    async void Prepare()
                    {
                        try
                        {
                            if (!ReferenceEquals(activeDrag, context) || Session.Provider != context.Provider ||
                                (DmsProvider as IDmsInstanceProvider)?.CurrentDmsInstance?.ID != context.Instance || !Allowed(FileOrFolderActions.AllowDownloadFiles)) throw new UnauthorizedAccessException();
                            if (selected.Select(file => file.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != selected.Length)
                                throw new DmsUserInputInvalidException(UiText.Get("DuplicateFileNameConflict"));
                            var directory = Path.Combine(Path.GetTempPath(), "CompuMaster.Dms.WinUI", Guid.NewGuid().ToString("N"));
                            if (Directory.Exists(directory)) throw new IOException("The drag staging directory already exists.");
                            Directory.CreateDirectory(directory); temporaryDirectories.Add(directory);
                            var storage = new List<IStorageItem>();
                            await Session.RunAsync(async () =>
                            {
                                foreach (var file in selected)
                                {
                                    var path = LocalPaths.SafeChild(directory, file.Name);
                                    await Session.Provider.DownloadFileWithProgressAsync(file, path, new CallbackProgress(_ => { }), CancellationToken.None);
                                    storage.Add(await StorageFile.GetFileFromPathAsync(path));
                                }
                            });
                            request.SetData(storage);
                        }
                        catch (Exception ex) { dragFailure = ex; request.SetData(Array.Empty<IStorageItem>()); }
                        finally { deferral.Complete(); }
                    }
                    if (DispatcherQueue.HasThreadAccess) Prepare();
                    else if (!DispatcherQueue.TryEnqueue(Prepare)) deferral.Complete();
                });
            }
            UpdateControls();
        };
        files.DragItemsCompleted += async (_, e) =>
        {
            var context = activeDrag; var target = droppedTarget;
            dragRunning = false;
            try
            {
                if (dragFailure is { } failure) await ReportFailureAsync(failure, DmsBrowserFailurePhase.Action);
                if (e.DropResult.HasFlag(DataPackageOperation.Move) && context is not null && target is not null && ValidMoveTarget(context, target))
                    await UiAsync(async () =>
                    {
                        foreach (var file in context.Files)
                        {
                            if (!ValidMoveTarget(context, target)) throw new InvalidOperationException(UiText.Get("DestinationOutsideBrowserRoot"));
                            var parent = target.Item?.FullName ?? Session.Provider.BrowseInRootFolderName;
                            if (target.Item is { } expected)
                            {
                                var current = await Session.Provider.ListRemoteItemAsync(parent);
                                if (current is null || current.ItemType != expected.ItemType || current.ExtendedInfosFolderID != expected.ExtendedInfosFolderID || current.ExtendedInfosCollectionID != expected.ExtendedInfosCollectionID)
                                    throw new InvalidOperationException(UiText.Get("DestinationOutsideBrowserRoot"));
                            }
                            if (Session.Provider.ParentDirectoryPath(file.FullName) == parent) continue;
                            await Session.ResourceActionAsync(file, Session.Provider.CombinePath(parent, file.Name), ResourceAction.Move, question => Dialogs.ConfirmAsync(Owner, question));
                        }
                    });
            }
            finally { activeDrag = null; dragNonce = null; droppedTarget = null; dragFailure = null; UpdateControls(); }
        };
    }
    internal bool ValidMoveTarget(RemoteDrag context, BrowserNode? target)
    {
        if (!ReferenceEquals(context, activeDrag) || target is null || target.Placeholder || !Session.AllNodes().Contains(target) ||
            !Allowed(FileOrFolderActions.AllowCopyRenameMoveFiles) || Session.BrowseMode != BrowseModes.FoldersAndFiles ||
            Session.Provider != context.Provider || Options.InitialFolder != context.Root || (DmsProvider as IDmsInstanceProvider)?.CurrentDmsInstance?.ID != context.Instance ||
            target.Item?.ExtendedInfosCollisionDetected == true || target.Item is null && !Session.Provider.SupportsFilesInRootFolder) return false;
        var path = target.Item?.FullName ?? Session.Provider.BrowseInRootFolderName;
        try { Session.RelativePath(path); } catch { return false; }
        return context.Files.Length > 0 && context.Files.Select(file => file.Name).Distinct(StringComparer.Ordinal).Count() == context.Files.Length &&
            context.Files.Any(file => Session.Provider.ParentDirectoryPath(file.FullName) != path);
    }
    private BrowserNode? DropTarget(Control target, DragEventArgs e)
    {
        if (target == files) return Session.SelectedNode;
        var source = e.OriginalSource as DependencyObject;
        while (source is not null)
        {
            if (source is TreeViewItem item && tree.NodeFromContainer(item) is { } node && nodes.TryGetValue(node, out var model)) return model;
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }
    private async Task<bool> IsInternalAsync(DataPackageView data) => activeDrag is not null && data.Contains(MoveFormat) && await data.GetDataAsync(MoveFormat) is string nonce && nonce == dragNonce;
    private async Task DragOverAsync(Control target, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            e.AcceptedOperation = DataPackageOperation.None;
            if (!initialized || Session.IsBusy || interactionRunning) return;
            var node = DropTarget(target, e);
            if (await IsInternalAsync(e.DataView))
            { if (ValidMoveTarget(activeDrag!, node)) e.AcceptedOperation = DataPackageOperation.Move; }
            else if (!dragRunning && Allowed(FileOrFolderActions.AllowUploadFiles) && Session.BrowseMode == BrowseModes.FoldersAndFiles && node is not null && e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0 && (node.Item is not null || Session.Provider.SupportsFilesInRootFolder || items.All(item => item is StorageFolder)))
                    e.AcceptedOperation = DataPackageOperation.Copy;
            }
            e.Handled = true;
        }
        catch { e.AcceptedOperation = DataPackageOperation.None; }
        finally { deferral.Complete(); }
    }
    private async Task DropAsync(Control target, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            var node = DropTarget(target, e);
            if (await IsInternalAsync(e.DataView))
            {
                if (ValidMoveTarget(activeDrag!, node)) { droppedTarget = node; e.AcceptedOperation = DataPackageOperation.Move; }
                e.Handled = true; return;
            }
            if (dragRunning || node is null || !Allowed(FileOrFolderActions.AllowUploadFiles) || Session.BrowseMode != BrowseModes.FoldersAndFiles || !e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            var items = await e.DataView.GetStorageItemsAsync(); var paths = items.Select(item => item.Path).ToArray();
            if (paths.Length == 0 || paths.Any(string.IsNullOrEmpty)) return;
            e.Handled = true; await UiAsync(() => UploadPathsAsync(paths, node));
        }
        catch (Exception ex) { await ReportFailureAsync(ex, DmsBrowserFailurePhase.Action); }
        finally { deferral.Complete(); }
    }
}
