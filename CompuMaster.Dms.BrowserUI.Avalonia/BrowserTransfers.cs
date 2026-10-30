using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;

namespace CompuMaster.Dms.BrowserUI.Avalonia;

public sealed partial class DmsBrowserControl
{
    private static string TransferTitle(string key, BaseDmsProvider provider)
    {
        var target = provider.Name;
        if (provider is WebDavDmsProvider webdav && Uri.TryCreate(webdav.CustomWebApiUrl, UriKind.Absolute, out var uri))
            target = new UriBuilder(uri) { UserName = "", Password = "", Query = "", Fragment = "" }.Uri.AbsoluteUri;
        return UiText.Get(key) + " — " + target;
    }
    private static Task<ConflictDecision> ConfirmConflictAsync(Window owner, string path)
    {
        var all = new CheckBox { Content = UiText.Get("DownloadApplyToAll") };
        Window? dialog = null;
        var replace = Dialogs.Button("DownloadReplace", () => dialog!.Close(new ConflictDecision(ConflictAction.Replace, all.IsChecked == true)));
        var skip = Dialogs.Button("DownloadSkip", () => dialog!.Close(new ConflictDecision(ConflictAction.Skip, all.IsChecked == true)));
        var cancel = Dialogs.Button("ActionCancel", () => dialog!.Close(new ConflictDecision(ConflictAction.Cancel, false)));
        var body = new StackPanel { Spacing = 12, Children = { new TextBlock { Text = UiText.Format("OverwriteFileQuestion", path), TextWrapping = global::Avalonia.Media.TextWrapping.Wrap }, all } };
        dialog = Dialogs.Window(UiText.Get("DownloadConflictTitle"), Dialogs.Layout(body, Dialogs.Buttons(replace, skip, cancel)), 640, 300);
        return AwaitDecision();
        async Task<ConflictDecision> AwaitDecision() =>
            await BrowserAppearance.ShowDialog<ConflictDecision?>(dialog, owner) ?? new ConflictDecision(ConflictAction.Cancel, false);
    }
    private async Task UploadFolderAsync()
    {
        Session.Require(FileOrFolderActions.AllowUploadFiles);
        var selected = await Owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        { Title = UiText.Get("ActionUploadFolder"), AllowMultiple = true, SuggestedStartLocation = await DefaultLocalFolderAsync(true) });
        if (selected.Count == 0) return;
        var paths = selected.Select(p => p.TryGetLocalPath() ?? throw new NotSupportedException("A local filesystem path is required.")).ToArray();
        await UploadPathsAsync(paths, Session.SelectedNode!);
    }
    private async Task UploadPathsAsync(IReadOnlyList<string> paths, BrowserNode target)
    {
        Session.Require(FileOrFolderActions.AllowUploadFiles);
        var provider = Session.Provider;
        var root = Options.InitialFolder;
        var instance = (provider as IDmsInstanceProvider)?.CurrentDmsInstance?.ID;
        var parent = target.Item?.FullName ?? provider.BrowseInRootFolderName;
        var folderID = target.Item?.ExtendedInfosFolderID;
        var collectionID = target.Item?.ExtendedInfosCollectionID;
        bool CanWrite() => Session.Provider == provider && Options.InitialFolder == root &&
            (provider as IDmsInstanceProvider)?.CurrentDmsInstance?.ID == instance &&
            Allowed(FileOrFolderActions.AllowUploadFiles) && Session.AllNodes().Contains(target) &&
            (target.Item?.FullName ?? provider.BrowseInRootFolderName) == parent &&
            target.Item?.ExtendedInfosFolderID == folderID && target.Item?.ExtendedInfosCollectionID == collectionID;
        await Session.RunAsync(async () =>
        {
            var dialog = new TransferWindow(TransferTitle("UploadTitle", provider), !provider.SupportsNonUniqueRemoteItems);
            BrowserAppearance.Inherit(dialog, Owner, this); dialog.Show(Owner);
            Exception? failure = null;
            try
            {
                var plan = await Task.Run(() => LocalUploadPlan.Build(paths, Options.LocalParentMustFolder, dialog.Cancellation.Token));
                if (!CanWrite()) throw new UnauthorizedAccessException();
                if (target.Item is null && !provider.SupportsFilesInRootFolder && plan.Files.Any(f => !f.Relative.Contains('/')))
                    throw new DmsUserInputInvalidException(UiText.Get("RootFileDropUnavailable"));
                var batch = new TransferBatch(plan.Files.Select(f => f.Relative), plan.Files.Select(f => (long?)new FileInfo(f.Source).Length).ToArray());
                var conflicts = new TransferConflicts();
                var retry = true;
                do
                {
                    failure = null;
                    try
                    {
                        foreach (var directory in plan.Directories)
                        {
                            dialog.Cancellation.Token.ThrowIfCancellationRequested();
                            if (!CanWrite()) throw new UnauthorizedAccessException();
                            var destination = LocalUploadPlan.Destination(provider, parent, directory);
                            Session.RelativePath(destination);
                            var existing = await provider.ListRemoteItemAsync(destination, dialog.Cancellation.Token);
                            if (existing?.ExtendedInfosCollisionDetected == true) throw new RemotePathNotUniqueException(destination);
                            if (existing is null) await provider.CreateDirectoryAsync(destination, dialog.Cancellation.Token);
                            else if (existing.ItemType == DmsResourceItem.ItemTypes.File) throw new IOException(UiText.Format("UploadSelectionCollision", destination));
                        }
                        await batch.RunAsync(async (index, progress, token) =>
                        {
                            if (!CanWrite()) throw new UnauthorizedAccessException();
                            var file = plan.Files[index];
                            LocalPaths.Validate(file.Source, Options.LocalParentMustFolder);
                            var destination = LocalUploadPlan.Destination(provider, parent, file.Relative);
                            Session.RelativePath(destination);
                            await provider.ResetCachesForRemoteItemsAsync(provider.ParentDirectoryPath(destination), BaseDmsProvider.SearchItemType.AllItems, token);
                            var existing = await provider.ListRemoteItemAsync(destination, token);
                            if (existing is not null)
                            {
                                if (existing.ItemType != DmsResourceItem.ItemTypes.File || existing.ExtendedInfosCollisionDetected)
                                    throw new RemotePathNotUniqueException(destination);
                                var decision = await conflicts.ResolveAsync(destination, existing.ExtendedInfosFileID, p => ConfirmConflictAsync(dialog, p));
                                if (decision == ConflictAction.Cancel) throw new OperationCanceledException();
                                if (decision == ConflictAction.Skip) return false;
                                await provider.ResetCachesForRemoteItemsAsync(provider.ParentDirectoryPath(destination), BaseDmsProvider.SearchItemType.AllItems, token);
                                var current = await provider.ListRemoteItemAsync(destination, token);
                                if (current is null || current.ItemType != DmsResourceItem.ItemTypes.File || current.ExtendedInfosFileID != existing.ExtendedInfosFileID)
                                    throw new InvalidOperationException(UiText.Format("UploadDestinationChanged", destination));
                                if (!CanWrite()) throw new UnauthorizedAccessException();
                                await provider.ReplaceFileWithProgressAsync(current, file.Source, progress, token);
                            }
                            else
                            {
                                conflicts.ValidateAbsent(destination);
                                if (!CanWrite()) throw new UnauthorizedAccessException();
                                await provider.UploadFileWithProgressAsync(destination, file.Source, progress, token);
                            }
                            return true;
                        }, () => dialog.Update(batch), retry, dialog.Cancellation.Token);
                    }
                    catch (Exception ex) { failure = ex; }
                    try { await Session.RefreshCoreAsync(CancellationToken.None); }
                    catch (Exception ex) { if (failure is null) failure = ex; else failure.Data["RefreshFailure"] = ex; }
                    if (failure is null) break;
                    if (failure is not OperationCanceledException) ReportFailure(failure, DmsBrowserFailurePhase.Action, "Upload");
                    var recovery = await dialog.FailureAsync(failure);
                    if (recovery == TransferRecovery.Close) return;
                    retry = recovery == TransferRecovery.RetryFailed; dialog.Restart();
                } while (true);
            }
            finally { dialog.Finish(); }
        });
    }
    private async Task DownloadFolderAsync()
    {
        Session.Require(FileOrFolderActions.AllowDownloadFiles);
        if (Session.SelectedNode is not { } node) return;
        var selected = await Owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        { Title = UiText.Get("ActionDownloadFolder"), SuggestedStartLocation = await DefaultLocalFolderAsync(false) });
        if (selected.Count == 0) return;
        var parent = selected[0].TryGetLocalPath() ?? throw new NotSupportedException("A local filesystem path is required.");
        LocalPaths.Validate(parent, Options.LocalParentMustFolder);
        await Session.RunAsync(async () =>
        {
            var dialog = new TransferWindow(TransferTitle("DownloadTitle", Session.Provider), false);
            BrowserAppearance.Inherit(dialog, Owner, this); dialog.Show(Owner);
            try
            {
                var folder = node.Item ?? new DmsResourceItem { ItemType = DmsResourceItem.ItemTypes.Root, Name = "DMS", FullName = Session.Provider.BrowseInRootFolderName };
                var plan = await RemoteDownloadPlan.BuildAsync(Session.Provider, folder, parent, dialog.Cancellation.Token);
                foreach (var directory in plan.Directories)
                {
                    dialog.Cancellation.Token.ThrowIfCancellationRequested();
                    LocalPaths.Validate(directory, Options.LocalParentMustFolder); AtomicDownload.Validate(directory, true);
                    Directory.CreateDirectory(directory);
                }
                await DownloadBatchAsync(plan.Files, dialog);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ReportFailure(ex, DmsBrowserFailurePhase.Action, "DownloadFolder"); await dialog.FailureAsync(ex); }
            finally { dialog.Finish(); }
        });
    }
    private Task TransferDownloadsAsync(IReadOnlyList<(DmsResourceItem Item, string Path)> downloads) => Session.RunAsync(async () =>
    {
        Session.Require(FileOrFolderActions.AllowDownloadFiles);
        foreach (var download in downloads) { Session.ValidateSource(download.Item); LocalPaths.Validate(download.Path, Options.LocalParentMustFolder); }
        if (downloads.Select(d => Path.GetFullPath(d.Path)).Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Count() != downloads.Count)
            throw new DmsUserInputInvalidException(UiText.Get("DuplicateFileNameConflict"));
        var dialog = new TransferWindow(TransferTitle("DownloadTitle", Session.Provider), false); BrowserAppearance.Inherit(dialog, Owner, this); dialog.Show(Owner);
        try { await DownloadBatchAsync(downloads.Select(d => (d.Item, d.Path)).ToList(), dialog); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ReportFailure(ex, DmsBrowserFailurePhase.Action, "Download"); await dialog.FailureAsync(ex); }
        finally { dialog.Finish(); }
    });
    private async Task DownloadBatchAsync(IReadOnlyList<(DmsResourceItem Item, string Destination)> downloads, TransferWindow dialog)
    {
        var batch = new TransferBatch(downloads.Select(d => d.Item.Name), downloads.Select(d => (long?)d.Item.ContentLength).ToArray());
        var conflicts = new TransferConflicts();
        await batch.RunAsync(async (index, progress, token) =>
        {
            Session.Require(FileOrFolderActions.AllowDownloadFiles);
            var file = downloads[index]; LocalPaths.Validate(file.Destination, Options.LocalParentMustFolder);
            var replace = false;
            if (File.Exists(file.Destination))
            {
                var decision = await conflicts.ResolveAsync(file.Destination, null, p => ConfirmConflictAsync(dialog, p));
                if (decision == ConflictAction.Cancel) throw new OperationCanceledException();
                if (decision == ConflictAction.Skip) return false;
                replace = true;
            }
            await AtomicDownload.SaveAsync(file.Destination, replace, async path =>
            {
                await Session.Provider.DownloadFileWithProgressAsync(file.Item, path, progress, token);
                token.ThrowIfCancellationRequested();
            });
            return true;
        }, () => dialog.Update(batch), true, dialog.Cancellation.Token);
    }
}
