using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace CompuMaster.Dms.BrowserUI.WinUI;

internal sealed class FileVersionHistoryDialog : OperationWindow
{
    private readonly VersionSession session;
    private readonly DmsBrowserOptions options;
    internal readonly DataGrid List = new() { Name = "Versions", IsReadOnly = true, AutoGenerateColumns = false, SelectionMode = DataGridSelectionMode.Single, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal };
    private readonly TextBlock status = new();
    internal bool CurrentFileChanged { get; private set; }
    internal FileVersionHistoryDialog(BaseDmsProvider provider, DmsResourceItem file, DmsBrowserOptions options)
    {
        this.options = options; session = new VersionSession(provider, file, () => options.AllowedActions);
        Title = UiText.Get("FileVersionHistory") + " — " + file.Name; Width = 950; Height = 560;
        foreach (var (key, binding) in new[] { ("VersionIdentifier", "Identifier"), ("VersionTimestamp", "Timestamp"), ("VersionSize", "Size"), ("VersionAuthor", "Author"), ("VersionLabel", "Label") })
            List.Columns.Add(new DataGridTextColumn { Header = UiText.Get(key), Binding = new Binding { Path = new PropertyPath(binding) }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        var download = Dialogs.Button("ActionDownload", async () => await RunAsync(ChooseDownloadAsync));
        var restore = Dialogs.Button("RestoreFileVersion", async () =>
        {
            if (Selected() is not { } version || !await Dialogs.ConfirmAsync(this, UiText.Format("ConfirmRestoreFileVersion", version.VersionID, file.Name))) return;
            await RunAsync(async () => { await session.RestoreAsync(version); CurrentFileChanged = true; await RefreshAsync(); });
        });
        var remove = Dialogs.Button("RemoveFileVersion", async () =>
        {
            if (Selected() is not { } version || !await Dialogs.ConfirmAsync(this, UiText.Format("ConfirmRemoveFileVersion", version.VersionID, file.Name))) return;
            await RunAsync(async () => { await session.RemoveAsync(version); CurrentFileChanged = true; await RefreshAsync(); });
        });
        void Update()
        {
            download.IsEnabled = Selected() is not null && session.CanDownload;
            restore.IsEnabled = Selected() is not null && session.CanRestore;
            remove.IsEnabled = Selected() is not null && session.CanRemove;
        }
        List.SelectionChanged += (_, _) => Update();
        var body = new Grid { RowSpacing = 6 };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.Children.Add(new TextBlock { Text = file.FullName }); Grid.SetRow(List, 1); body.Children.Add(List); Grid.SetRow(status, 2); body.Children.Add(status);
        Content = Dialogs.Layout(body, Dialogs.Buttons(download, restore, remove, Dialogs.Button("ActionRefreshFiles", async () => await RunAsync(RefreshAsync)), Dialogs.Button("ActionClose", Close)));
        Opened += async (_, _) => { await RunAsync(RefreshAsync); Update(); };
    }
    private DmsFileVersion? Selected() => (List.SelectedItem as VersionRow)?.Version;
    private async Task RefreshAsync()
    {
        var versions = await session.ListAsync(); List.ItemsSource = versions.Select(v => new VersionRow(v)).ToArray();
        status.Text = versions.Count == 0 ? UiText.Get("FileVersionHistoryEmpty") : UiText.Format("VersionCount", versions.Count);
    }
    private async Task ChooseDownloadAsync()
    {
        if (Selected() is not { } version) return;
        VersionSession.Require(session.CanDownload);
        var path = await NativePickers.SaveAsync(this, UiText.Get("DownloadTitle"), options.LocalDefaultFolderDownloads, version.FileName);
        if (path is null) return;
        var replace = File.Exists(path);
        if (replace && !await Dialogs.ConfirmAsync(this, UiText.Format("OverwriteFileQuestion", path))) return;
        await session.DownloadAsync(version, path, options.LocalParentMustFolder, replace);
        status.Text = UiText.Get("DownloadSuccessful");
    }
}
public sealed partial class DmsBrowserControl
{
    private async Task ShowVersionsAsync()
    {
        Session.Require(FileOrFolderActions.AllowViewFileVersionHistory);
        if (SelectedFiles().Length != 1) throw new DmsUserInputInvalidException(UiText.Get("ExactlyOneItemRequired"));
        var dialog = new FileVersionHistoryDialog(Session.Provider, SelectedFiles().Single(), Options);
        await BrowserAppearance.ShowDialog(dialog, Owner, this); if (dialog.CurrentFileChanged) await Session.RefreshAsync();
    }
}
