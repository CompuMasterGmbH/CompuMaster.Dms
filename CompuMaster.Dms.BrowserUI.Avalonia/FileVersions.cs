using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
#if !DMS_WINUI
using global::Avalonia.Controls;
using global::Avalonia.Data;
#endif

#if DMS_WINUI
namespace CompuMaster.Dms.BrowserUI.WinUI;
#else
namespace CompuMaster.Dms.BrowserUI.Avalonia;
#endif

internal sealed class VersionSession
{
    internal VersionSession(BaseDmsProvider provider, DmsResourceItem file, Func<FileOrFolderActions> actions)
    { Provider = provider; File = file; Actions = actions; }
    internal BaseDmsProvider Provider { get; }
    internal DmsResourceItem File { get; }
    internal Func<FileOrFolderActions> Actions { get; }
    internal bool CanView => Provider.SupportsFileVersionHistory && Actions().HasFlag(FileOrFolderActions.AllowViewFileVersionHistory);
    internal bool CanDownload => CanView && Provider.SupportsFileVersionDownload && Actions().HasFlag(FileOrFolderActions.AllowDownloadFiles);
    internal bool CanRestore => CanView && Provider.SupportsFileVersionRestore && Actions().HasFlag(FileOrFolderActions.AllowRestoreFileVersions);
    internal bool CanRemove => CanView && Provider.SupportsFileVersionRemoval && Actions().HasFlag(FileOrFolderActions.AllowRemoveFileVersions);
    internal async Task<List<DmsFileVersion>> ListAsync()
    {
        Require(CanView);
        var result = await Task.Run(() => Provider.ListFileVersions(File));
        Require(CanView);
        if (result is null || result.Any(v => v is null || string.IsNullOrEmpty(v.VersionID) ||
            (string.IsNullOrEmpty(File.ExtendedInfosFileID) ? v.FileFullName != File.FullName : v.FileID != File.ExtendedInfosFileID)) ||
            result.Select(v => v.VersionID).Distinct(StringComparer.Ordinal).Count() != result.Count)
            throw new InvalidDataException(UiText.Get("VersionInvalidHistory"));
        return result;
    }
    internal static void Require(bool allowed) { if (!allowed) throw new InvalidOperationException(UiText.Get("VersionActionUnavailable")); }
    internal async Task RestoreAsync(DmsFileVersion version)
    { Require(CanRestore); Validate(version); await Task.Run(() => { Require(CanRestore); Validate(version); Provider.RestoreFileVersion(version); }); }
    internal async Task RemoveAsync(DmsFileVersion version)
    { Require(CanRemove); Validate(version); await Task.Run(() => { Require(CanRemove); Validate(version); Provider.RemoveFileVersion(version); }); }
    private void Validate(DmsFileVersion version)
    {
        if (string.IsNullOrEmpty(File.ExtendedInfosFileID) ? version.FileFullName != File.FullName : version.FileID != File.ExtendedInfosFileID)
            throw new InvalidDataException(UiText.Get("VersionInvalidHistory"));
    }
    internal async Task DownloadAsync(DmsFileVersion version, string destination, string? requiredParent, bool replace)
    {
        Require(CanDownload); Validate(version); LocalPaths.Validate(destination, requiredParent);
        await AtomicDownload.SaveAsync(destination, replace, async temporary =>
        {
            await Task.Run(() =>
            {
                Require(CanDownload); Validate(version);
                using var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None);
                Provider.DownloadFileVersion(version, stream);
            });
            if (version.LastModificationUtc.HasValue) System.IO.File.SetLastWriteTimeUtc(temporary, version.LastModificationUtc.Value);
        });
    }
}
internal sealed record VersionRow(DmsFileVersion Version)
{
    public string Identifier => Version.VersionID;
    public string Timestamp => (Version.VersionDateUtc ?? Version.LastModificationUtc)?.ToLocalTime().ToString() ?? UiText.Get("VersionUnknown");
    public string Size => Version.ContentLength.HasValue ? UiText.ByteSize(Version.ContentLength.Value) : UiText.Get("VersionUnknown");
    public string Author => Version.AuthorDisplayName ?? Version.AuthorID ?? UiText.Get("VersionUnknown");
    public string Label => Version.Label ?? "";
    public override string ToString() => string.Join(" | ", Version.VersionID,
        (Version.VersionDateUtc ?? Version.LastModificationUtc)?.ToLocalTime().ToString() ?? UiText.Get("VersionUnknown"),
        Version.ContentLength.HasValue ? UiText.ByteSize(Version.ContentLength.Value) : UiText.Get("VersionUnknown"),
        Version.AuthorDisplayName ?? Version.AuthorID ?? UiText.Get("VersionUnknown"), Version.Label ?? "");
}
#if !DMS_WINUI
internal sealed class FileVersionHistoryDialog : OperationWindow
{
    private readonly VersionSession session;
    private readonly DmsBrowserOptions options;
    internal readonly DataGrid List = new() { Name = "Versions", IsReadOnly = true, AutoGenerateColumns = false,
        SelectionMode = DataGridSelectionMode.Single, CanUserSortColumns = false, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal };
    private readonly TextBlock status = new();
    internal bool CurrentFileChanged { get; private set; }
    internal FileVersionHistoryDialog(BaseDmsProvider provider, DmsResourceItem file, DmsBrowserOptions options)
    {
        this.options = options;
        session = new VersionSession(provider, file, () => options.AllowedActions);
        Title = UiText.Get("FileVersionHistory") + " — " + file.Name;
        Width = 900; Height = 500;
        foreach (var (key, binding) in new[] { ("VersionIdentifier", "Identifier"), ("VersionTimestamp", "Timestamp"),
            ("VersionSize", "Size"), ("VersionAuthor", "Author"), ("VersionLabel", "Label") })
            List.Columns.Add(new DataGridTextColumn { Header = UiText.Get(key), Binding = new Binding(binding), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        var download = Dialogs.Button("ActionDownload", async () => await RunAsync(ChooseDownloadAsync));
        var restore = Dialogs.Button("RestoreFileVersion", async () =>
        {
            if (Selected() is not { } version) return;
            if (!await Dialogs.ConfirmAsync(this, UiText.Format("ConfirmRestoreFileVersion", version.VersionID, file.Name))) return;
            await RunAsync(async () => { await session.RestoreAsync(version); CurrentFileChanged = true; await RefreshAsync(); });
        });
        var remove = Dialogs.Button("RemoveFileVersion", async () =>
        {
            if (Selected() is not { } version) return;
            if (!await Dialogs.ConfirmAsync(this, UiText.Format("ConfirmRemoveFileVersion", version.VersionID, file.Name))) return;
            await RunAsync(async () => { await session.RemoveAsync(version); CurrentFileChanged = true; await RefreshAsync(); });
        });
        void Update()
        {
            download.IsEnabled = Selected() is not null && session.CanDownload;
            restore.IsEnabled = Selected() is not null && session.CanRestore;
            remove.IsEnabled = Selected() is not null && session.CanRemove;
        }
        List.SelectionChanged += (_, _) => Update();
        var body = new DockPanel();
        var heading = new TextBlock { Text = file.FullName };
        DockPanel.SetDock(heading, Dock.Top); body.Children.Add(heading);
        DockPanel.SetDock(status, Dock.Bottom); body.Children.Add(status); body.Children.Add(List);
        Content = Dialogs.Layout(body, Dialogs.Buttons(download, restore, remove,
            Dialogs.Button("ActionRefreshFiles", async () => await RunAsync(RefreshAsync)), Dialogs.Button("ActionClose", Close)));
        Opened += async (_, _) => { await RunAsync(RefreshAsync); Update(); };
    }
    private DmsFileVersion? Selected() => (List.SelectedItem as VersionRow)?.Version;
    private async Task RefreshAsync()
    {
        List.ItemsSource = (await session.ListAsync()).Select(v => new VersionRow(v)).ToArray();
        var count = List.ItemsSource.Cast<VersionRow>().Count();
        status.Text = count == 0 ? UiText.Get("FileVersionHistoryEmpty") : UiText.Format("VersionCount", count);
    }
    private async Task ChooseDownloadAsync()
    {
        if (Selected() is not { } version) return;
        VersionSession.Require(session.CanDownload);
        var file = await StorageProvider.SaveFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerSaveOptions { SuggestedFileName = version.FileName, ShowOverwritePrompt = true });
        if (file is null) return;
        var path = global::Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(file)
            ?? throw new NotSupportedException("A local filesystem path is required.");
        await session.DownloadAsync(version, path, options.LocalParentMustFolder, true);
        status.Text = UiText.Get("DownloadSuccessful");
    }
}
public sealed partial class DmsBrowserControl
{
    private async Task ShowVersionsAsync()
    {
        Session.Require(FileOrFolderActions.AllowViewFileVersionHistory);
        var selected = SelectedFiles().Single();
        var dialog = new FileVersionHistoryDialog(Session.Provider, selected, Options);
        await dialog.ShowDialog(Owner);
        if (dialog.CurrentFileChanged) await Session.RefreshAsync();
    }
}

#endif
