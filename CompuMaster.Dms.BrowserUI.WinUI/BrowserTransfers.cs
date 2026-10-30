using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CompuMaster.Dms.BrowserUI.WinUI;

public sealed partial class DmsBrowserControl
{
    private static string TransferTitle(string key, BaseDmsProvider provider)
    {
        var target = provider.Name;
        if (provider is WebDavDmsProvider webdav && Uri.TryCreate(webdav.CustomWebApiUrl, UriKind.Absolute, out var uri))
            target = new UriBuilder(uri) { UserName = "", Password = "", Query = "", Fragment = "" }.Uri.AbsoluteUri;
        return UiText.Get(key) + " — " + target;
    }
    private static async Task<ConflictDecision> ConfirmConflictAsync(Window owner, string path)
    {
        var all = new CheckBox { Content = UiText.Get("DownloadApplyToAll") };
        DialogWindow? dialog = null;
        var replace = Dialogs.Button("DownloadReplace", () => dialog!.Close(new ConflictDecision(ConflictAction.Replace, all.IsChecked == true)));
        var skip = Dialogs.Button("DownloadSkip", () => dialog!.Close(new ConflictDecision(ConflictAction.Skip, all.IsChecked == true)));
        var cancel = Dialogs.Button("ActionCancel", () => dialog!.Close(new ConflictDecision(ConflictAction.Cancel, false)));
        var body = new StackPanel { Spacing = 12, Children = { new TextBlock { Text = UiText.Format("OverwriteFileQuestion", path), TextWrapping = TextWrapping.Wrap }, all } };
        dialog = Dialogs.Window(UiText.Get("DownloadConflictTitle"), Dialogs.Layout(body, Dialogs.Buttons(replace, skip, cancel)), 640, 320);
        return await dialog.ShowDialog<ConflictDecision?>(owner) ?? new ConflictDecision(ConflictAction.Cancel, false);
    }
    private string? DefaultLocalFolder(bool upload)
    {
        var path = (upload ? Options.LocalDefaultFolderUploads : Options.LocalDefaultFolderDownloads) ?? Options.LocalParentMustFolder;
        if (string.IsNullOrEmpty(path)) return null;
        LocalPaths.Validate(path, Options.LocalParentMustFolder); Directory.CreateDirectory(path); return Path.GetFullPath(path);
    }
    private async Task UploadFilesAsync()
    {
        Session.Require(FileOrFolderActions.AllowUploadFiles);
        var paths = await NativePickers.OpenAsync(Owner, UiText.Get("UploadTitle"), DefaultLocalFolder(true));
        if (paths.Length > 0) await UploadPathsAsync(paths, Session.SelectedNode!);
    }
    private async Task UploadFolderAsync()
    {
        Session.Require(FileOrFolderActions.AllowUploadFiles);
        var paths = await NativePickers.OpenAsync(Owner, UiText.Get("ActionUploadFolder"), DefaultLocalFolder(true), true);
        if (paths.Length > 0) await UploadPathsAsync(paths, Session.SelectedNode!);
    }
    private async Task DownloadFilesAsync()
    {
        Session.Require(FileOrFolderActions.AllowDownloadFiles);
        var selected = SelectedFiles();
        if (selected.Length == 0) throw new DmsUserErrorMessageException(UiText.Get("NoFilesSelected"));
        var downloads = new List<(DmsResourceItem Item, string Path)>();
        if (selected.Length == 1)
        {
            var path = await NativePickers.SaveAsync(Owner, UiText.Get("DownloadTitle"), DefaultLocalFolder(false), selected[0].Name);
            if (path is null) return; downloads.Add((selected[0], path));
        }
        else
        {
            var folders = await NativePickers.OpenAsync(Owner, UiText.Get("DownloadTitle"), DefaultLocalFolder(false), true);
            if (folders.Length == 0) return;
            foreach (var file in selected) downloads.Add((file, LocalPaths.SafeChild(folders[0], file.Name)));
        }
        await TransferDownloadsAsync(downloads);
    }
    private async Task DownloadFolderAsync()
    {
        Session.Require(FileOrFolderActions.AllowDownloadFiles);
        if (Session.SelectedNode is not { } node) return;
        var selected = await NativePickers.OpenAsync(Owner, UiText.Get("ActionDownloadFolder"), DefaultLocalFolder(false), true);
        if (selected.Length == 0) return;
        var parent = selected[0]; LocalPaths.Validate(parent, Options.LocalParentMustFolder);
        await Session.RunAsync(async () =>
        {
            var dialog = new TransferWindow(TransferTitle("DownloadTitle", Session.Provider), false); BrowserAppearance.UseSource(dialog, this); dialog.Show(Owner);
            try
            {
                var folder = node.Item ?? new DmsResourceItem { ItemType = DmsResourceItem.ItemTypes.Root, Name = "DMS", FullName = Session.Provider.BrowseInRootFolderName };
                var plan = await RemoteDownloadPlan.BuildAsync(Session.Provider, folder, parent, dialog.Cancellation.Token);
                foreach (var directory in plan.Directories)
                {
                    dialog.Cancellation.Token.ThrowIfCancellationRequested(); LocalPaths.Validate(directory, Options.LocalParentMustFolder);
                    AtomicDownload.Validate(directory, true); Directory.CreateDirectory(directory);
                }
                await DownloadBatchAsync(plan.Files, dialog);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ReportFailure(ex, DmsBrowserFailurePhase.Action, "DownloadFolder"); await dialog.FailureAsync(ex); }
            finally { dialog.Finish(); }
        });
    }
}
