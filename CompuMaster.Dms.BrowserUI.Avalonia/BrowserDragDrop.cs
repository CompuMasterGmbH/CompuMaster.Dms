using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Platform.Storage;
using global::Avalonia.VisualTree;

namespace CompuMaster.Dms.BrowserUI.Avalonia;

internal sealed record RemoteDrag(BaseDmsProvider Provider, string? Instance, string? Root, DmsResourceItem[] Files);

public sealed partial class DmsBrowserControl
{
    private static readonly DataFormat<RemoteDrag> MoveFormat = DataFormat.CreateInProcessFormat<RemoteDrag>("CompuMaster.Dms.Move");
    private PointerPressedEventArgs? dragPress;
    private Point dragStart;
    private bool dragRunning;
    private RemoteDrag? activeDrag;
    private BrowserNode? droppedTarget;
    private void InitializeDragAndDrop()
    {
        foreach (var target in new Control[] { tree, files })
        {
            DragDrop.SetAllowDrop(target, true);
            target.AddHandler(DragDrop.DragOverEvent, (_, e) => DragOver(target, e));
            target.AddHandler(DragDrop.DropEvent, async (_, e) => await DropAsync(target, e));
        }
        files.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.GetCurrentPoint(files).Properties.IsLeftButtonPressed) { dragPress = e; dragStart = e.GetPosition(files); }
        }, RoutingStrategies.Tunnel);
        files.PointerMoved += async (_, e) =>
        {
            if (dragPress is null || dragRunning || IsBusy || !e.GetCurrentPoint(files).Properties.IsLeftButtonPressed ||
                Math.Abs(e.GetPosition(files).X - dragStart.X) + Math.Abs(e.GetPosition(files).Y - dragStart.Y) < 6 || !HasFiles ||
                !CanDragDownload && !CanDragMove) return;
            var press = dragPress; dragPress = null;
            await StartRemoteDragAsync(press);
        };
        files.PointerReleased += (_, _) => dragPress = null;
    }
    private BrowserNode? DropTarget(Control target, DragEventArgs e)
    {
        if (target == files) return Session.SelectedNode;
        var control = tree.InputHitTest(e.GetPosition(tree)) as Control;
        return control?.GetSelfAndVisualAncestors().OfType<TreeViewItem>().FirstOrDefault()?.DataContext as BrowserNode;
    }
    internal bool ValidMoveTarget(RemoteDrag context, BrowserNode? target)
    {
        if (!ReferenceEquals(context, activeDrag) || target is null || target.Placeholder || !Session.AllNodes().Contains(target) ||
            !CanDragMove || Session.BrowseMode != BrowseModes.FoldersAndFiles ||
            Session.Provider != context.Provider || Options.InitialFolder != context.Root ||
            (DmsProvider as IDmsInstanceProvider)?.CurrentDmsInstance?.ID != context.Instance ||
            target.Item?.ExtendedInfosCollisionDetected == true || target.Item is null && !Session.Provider.SupportsFilesInRootFolder) return false;
        var path = target.Item?.FullName ?? Session.Provider.BrowseInRootFolderName;
        try { Session.RelativePath(path); }
        catch { return false; }
        return context.Files.Length > 0 && context.Files.Select(f => f.Name).Distinct(StringComparer.Ordinal).Count() == context.Files.Length &&
            context.Files.Any(f => Session.Provider.ParentDirectoryPath(f.FullName) != path);
    }
    private void DragOver(Control target, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.None;
        if (!initialized || Session.IsBusy || interactionRunning) return;
        var node = DropTarget(target, e);
        if (e.DataTransfer.TryGetValue(MoveFormat) is { } context)
        {
            if (ValidMoveTarget(context, node)) e.DragEffects = DragDropEffects.Move;
        }
        else if (!dragRunning && Allowed(FileOrFolderActions.AllowUploadFiles) && Session.BrowseMode == BrowseModes.FoldersAndFiles &&
            node is not null && !node.Placeholder && e.DataTransfer.TryGetFiles() is { Length: > 0 } local &&
            (node.Item is not null || Session.Provider.SupportsFilesInRootFolder || local.All(f => f is IStorageFolder)) &&
            CanDropLocalPaths(local.Select(f => f.TryGetLocalPath())))
            e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }
    private async Task DropAsync(Control target, DragEventArgs e)
    {
        var node = DropTarget(target, e);
        if (e.DataTransfer.TryGetValue(MoveFormat) is { } context)
        {
            if (ValidMoveTarget(context, node)) { droppedTarget = node; e.DragEffects = DragDropEffects.Move; }
            e.Handled = true; return;
        }
        if (dragRunning || node is null || !Allowed(FileOrFolderActions.AllowUploadFiles) || Session.BrowseMode != BrowseModes.FoldersAndFiles) return;
        var storage = e.DataTransfer.TryGetFiles();
        if (storage is null) return;
        var paths = storage.Select(s => s.TryGetLocalPath()).ToArray();
        if (!CanDropLocalPaths(paths)) return;
        e.Handled = true;
        await UiAsync(() => UploadPathsAsync(paths!, node));
    }
    private async Task StartRemoteDragAsync(PointerPressedEventArgs press)
    {
        var selected = SelectedFiles();
        var context = new RemoteDrag(Session.Provider, (DmsProvider as IDmsInstanceProvider)?.CurrentDmsInstance?.ID, Options.InitialFolder,
            selected.Select(file => new DmsResourceItem { ItemType = file.ItemType, Name = file.Name, FullName = file.FullName,
                ExtendedInfosFileID = file.ExtendedInfosFileID, ExtendedInfosAssignedFolderID = file.ExtendedInfosAssignedFolderID,
                ExtendedInfosAssignedCollectionID = file.ExtendedInfosAssignedCollectionID }).ToArray());
        dragRunning = true; activeDrag = context; droppedTarget = null; UpdateControls();
        try
        {
            var transfer = new DataTransfer();
            var move = new DataTransferItem(); move.Set(MoveFormat, context); transfer.Add(move);
            var effects = CanDragMove ? DragDropEffects.Move : DragDropEffects.None;
            if (CanDragDownload)
            {
                // Avalonia exposes real storage items to native desktop targets. Stage copies rather than requiring Windows OLE streams.
                var directory = Path.Combine(Path.GetTempPath(), "CompuMaster.Dms.Avalonia", Guid.NewGuid().ToString("N"));
                if (Directory.Exists(directory)) throw new IOException("The drag staging directory already exists.");
                Directory.CreateDirectory(directory); temporaryDirectories.Add(directory);
                if (selected.Select(f => f.Name).Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Count() != selected.Length)
                    throw new DmsUserInputInvalidException(UiText.Get("DuplicateFileNameConflict"));
                await Session.RunAsync(async () =>
                {
                    foreach (var file in selected)
                    {
                        var path = LocalPaths.SafeChild(directory, file.Name);
                        await Session.Provider.DownloadFileWithProgressAsync(file, path, new CallbackProgress(_ => { }), CancellationToken.None);
                        var storage = await Owner.StorageProvider.TryGetFileFromPathAsync(new Uri(path))
                            ?? throw new IOException(UiText.Get("FileNotFound"));
                        var item = new DataTransferItem(); item.SetFile(storage); transfer.Add(item);
                    }
                });
                effects |= DragDropEffects.Copy;
            }
            press.Pointer.Capture(null);
            var result = await DragDrop.DoDragDropAsync(press, transfer, effects);
            if (result.HasFlag(DragDropEffects.Move) && droppedTarget is { } target && ValidMoveTarget(context, target))
            {
                // The native drop has finished. Commit only an authenticated in-process destination.
                dragRunning = false;
                await UiAsync(async () =>
                {
                    foreach (var file in context.Files)
                    {
                        if (!ValidMoveTarget(context, target)) throw new InvalidOperationException(UiText.Get("DestinationOutsideBrowserRoot"));
                        var parent = target.Item?.FullName ?? Session.Provider.BrowseInRootFolderName;
                        if (target.Item is { } expected)
                        {
                            var current = await Session.Provider.ListRemoteItemAsync(parent);
                            if (current is null || current.ItemType != expected.ItemType ||
                                current.ExtendedInfosFolderID != expected.ExtendedInfosFolderID ||
                                current.ExtendedInfosCollectionID != expected.ExtendedInfosCollectionID)
                                throw new InvalidOperationException(UiText.Get("DestinationOutsideBrowserRoot"));
                        }
                        if (Session.Provider.ParentDirectoryPath(file.FullName) == parent) continue;
                        await Session.ResourceActionAsync(file, Session.Provider.CombinePath(parent, file.Name), ResourceAction.Move, question => Dialogs.ConfirmAsync(Owner, question, source: this));
                    }
                });
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { await ReportFailureAsync(ex, DmsBrowserFailurePhase.Action); }
        finally { dragRunning = false; activeDrag = null; droppedTarget = null; UpdateControls(); }
    }
}
