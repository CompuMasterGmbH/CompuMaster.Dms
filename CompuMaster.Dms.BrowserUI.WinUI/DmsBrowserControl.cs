using System.Diagnostics;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.System;

namespace CompuMaster.Dms.BrowserUI.WinUI;

/// <summary>Provides an embeddable native WinUI 3 DMS browser.</summary>
/// <remarks>Call AttachToWindow before initialization. The host must initialize WinUI and its XamlControlsResources.</remarks>
public sealed partial class DmsBrowserControl : UserControl
{
    private BrowserSession? session;
    private readonly FileSelectionSnapshot fileSelection = new();
    internal DmsItemSharings? ActiveSharingDialog { get; private set; }
    private readonly IDmsLoginProfile? profile;
    private Window? owner;
    private readonly TreeView tree = new() { Name = "DmsFolders", MinWidth = 180, SelectionMode = TreeViewSelectionMode.Single, CanDragItems = false, CanReorderItems = false };
    private readonly ListView files = new() { Name = "DmsFiles", SelectionMode = ListViewSelectionMode.Extended, IsItemClickEnabled = false };
    private readonly Grid columns = new() { ColumnSpacing = 6 };
    private readonly Grid filePanel = new();
    private readonly ProgressBar progress = new() { IsIndeterminate = true, Height = 3, Visibility = Visibility.Collapsed };
    private readonly TextBlock status = new() { Margin = new Thickness(6) };
    private readonly WrapPanel fileToolbar = new();
    private readonly WrapPanel bottom = new();
    private readonly ToggleButton showFiles = new() { Name = "ActionShowFiles", Content = Dialogs.Caption("ActionShowFiles"), Margin = new Thickness(3) };
    private readonly List<(Control Control, Func<bool> Enabled, Func<bool> Visible)> actions = new();
    private readonly HashSet<Control> terminalButtons = new();
    private readonly Dictionary<TreeViewNode, BrowserNode> nodes = new();
    private readonly Dictionary<TreeViewNode, (string Name, bool Shared)> nodeLabels = new();
    private FileRow[] renderedFiles = Array.Empty<FileRow>();
    private readonly List<string> temporaryDirectories = new();
    private readonly List<Exception> cleanupFailures = new();
    private bool suppressSelection, interactionRunning, initializing, initialized, pendingConfirmation, pendingCancellation, dragRunning;
    private Task? initialization;
    private DmsBrowserFailurePhase startupPhase = DmsBrowserFailurePhase.Startup;
    private Button? instanceButton;
    private string sort = "Modified";
    private bool descending = true;
    /// <summary>Creates a browser for an authorized provider.</summary>
    /// <param name="provider">The provider whose current instance is preserved by default.</param>
    /// <param name="options">The optional browser settings.</param>
    public DmsBrowserControl(BaseDmsProvider provider, DmsBrowserOptions? options = null)
    { Options = options ?? new(); session = new BrowserSession(provider, Options); BuildControls(); AttachSession(); }
    /// <summary>Creates a browser that authorizes a profile asynchronously.</summary>
    /// <param name="profile">The login profile.</param>
    /// <param name="options">The optional browser settings.</param>
    public DmsBrowserControl(IDmsLoginProfile profile, DmsBrowserOptions? options = null)
    { this.profile = profile ?? throw new ArgumentNullException(nameof(profile)); Options = options ?? new(); BuildControls(); }
    /// <summary>Gets the host settings, which should be configured before initialization.</summary>
    public DmsBrowserOptions Options { get; }
    /// <summary>Gets the authorized provider, or null before authorization.</summary>
    public BaseDmsProvider? DmsProvider => session?.Provider;
    /// <summary>Gets whether an operation or owned dialog prevents concurrent actions.</summary>
    public bool IsBusy => initializing || interactionRunning || dragRunning || session?.IsBusy == true;
    /// <summary>Gets the selected directory relative to the configured root.</summary>
    public string? SelectedFolder => session?.SelectedFolder;
    /// <summary>Gets the selected remote resources.</summary>
    public DmsBrowserSelection Selection => new(SelectedFolder, session?.SelectedNode?.Item,
        files.SelectedItems.OfType<ListViewItem>().Select(i => ((FileRow)i.Tag).Item).ToArray());
    /// <summary>Gets or sets the method opening a downloaded temporary read-only preview.</summary>
    public Func<string, Task> OpenDownloadedFileItem { get; set; } = path =>
    { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); return Task.CompletedTask; };
    /// <summary>Occurs when the user confirms a selection.</summary>
    public event EventHandler? SelectionConfirmed;
    /// <summary>Occurs when the user cancels or closes the browser.</summary>
    public event EventHandler? Cancelled;
    /// <summary>Occurs when the selection or browser state changes.</summary>
    public event EventHandler? SelectionChanged;
    /// <summary>Gets failures encountered while removing owned previews.</summary>
    public IReadOnlyList<Exception> CleanupFailures => cleanupFailures;
    /// <summary>Associates this embedded control with its owning WinUI window.</summary>
    /// <param name="window">The owning window used for modal dialogs and native pickers.</param>
    public void AttachToWindow(Window window) => owner = window ?? throw new ArgumentNullException(nameof(window));
    internal BrowserSession Session => session ?? throw new InvalidOperationException("Initialize the browser first.");
    internal TreeView FolderTree => tree;
    internal ListView FileGrid => files;
    private Window Owner => owner ?? throw new InvalidOperationException("Call AttachToWindow before initializing the WinUI browser.");
    private bool HasFiles => initialized && files.SelectedItems.Count > 0 && Session.BrowseMode == BrowseModes.FoldersAndFiles;
    private bool HasFolderForFiles => initialized && Session.SelectedNode is not null && (Session.SelectedNode.Item is not null || Session.Provider.SupportsFilesInRootFolder);
    private bool MutableFolder => initialized && Session.SelectedNode?.Parent is not null && Session.SelectedNode.Item is not null && !Session.CollectionManagementRestricted(Session.SelectedNode.Item);
    private bool Allowed(FileOrFolderActions flag) => (Options.AllowedActions & flag) == flag;
    private bool Visible(FileOrFolderActions? flag, bool sharing = false) => (!flag.HasValue || Allowed(flag.Value)) && (!sharing || DmsProvider?.SupportsSharingSetup == true);
    private bool FolderActionVisible(string key) => key switch
    {
        "ActionCopy" or "ActionRename" or "ActionMove" or "ActionDelete" => MutableFolder,
        "ActionFolderSharings" => session?.SelectedNode?.Item is not { } item || !Session.CollectionManagementRestricted(item), _ => true
    };
    private void BuildControls()
    {
        FlowDirection = UiText.LayoutDirection;
        tree.ItemTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><ContentPresenter Content='{Binding Content}' HorizontalAlignment='Stretch'/></DataTemplate>");
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7, GridUnitType.Star) });
        columns.Children.Add(tree);
        var splitter = new Thumb { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        splitter.DragDelta += (_, e) => columns.ColumnDefinitions[0].Width = new GridLength(Math.Max(180, columns.ColumnDefinitions[0].ActualWidth + e.HorizontalChange));
        Grid.SetColumn(splitter, 1); columns.Children.Add(splitter);
        filePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        filePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        filePanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        filePanel.Children.Add(fileToolbar);
        var headers = FileColumns();
        foreach (var (key, field, index) in new[] { ("ColumnName", "Name", 0), ("ColumnSize", "Size", 1), ("ColumnLastModification", "Modified", 2) })
        {
            var button = Dialogs.Button(key, () => { descending = sort == field && !descending; sort = field; Synchronize(); });
            Grid.SetColumn(button, index); headers.Children.Add(button);
        }
        Grid.SetRow(headers, 1); filePanel.Children.Add(headers);
        Grid.SetRow(files, 2); filePanel.Children.Add(files); Grid.SetColumn(filePanel, 2); columns.Children.Add(filePanel);
        var footer = new StackPanel { Children = { status, bottom } };
        var content = new Grid { Margin = new Thickness(10), RowSpacing = 4 };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.Children.Add(progress); Grid.SetRow(columns, 1); content.Children.Add(columns); Grid.SetRow(footer, 2); content.Children.Add(footer); Content = content;
        tree.Expanding += async (_, e) =>
        {
            if (suppressSelection || !nodes.TryGetValue(e.Node, out var node) || node.Loaded) return;
            node.IsExpanded = true; await UiAsync(() => Session.ExpandAsync(node), DmsBrowserFailurePhase.ChildListing, "ExpandFolder");
        };
        tree.Collapsed += (_, e) => { if (!suppressSelection && nodes.TryGetValue(e.Node, out var node)) node.IsExpanded = false; };
        tree.SelectionChanged += async (_, _) =>
        { if (!suppressSelection && !SessionBusy && tree.SelectedNode is { } selected && nodes.TryGetValue(selected, out var node) && !ReferenceEquals(node, Session.SelectedNode)) await UiAsync(() => Session.SelectAsync(node), DmsBrowserFailurePhase.Action, "SelectFolder"); };
        tree.DoubleTapped += (_, _) => { if (Options.DialogOperationMode == DialogOperationModes.ReturnSelectedItems) ConfirmSelection(); };
        files.SelectionChanged += (_, _) =>
        {
            if (suppressSelection || SessionBusy) return;
            if (session is not null) fileSelection.Capture(Session, Selection.Files);
            UpdateControls(); SelectionChanged?.Invoke(this, EventArgs.Empty);
        };
        files.DoubleTapped += async (_, _) =>
        { if (Options.DialogOperationMode == DialogOperationModes.ReturnSelectedItems) ConfirmSelection(); else await UiAsync(OpenFilesAsync); };
        BuildActions();
        KeyDown += async (_, e) =>
        {
            if (IsBusy || !initialized) return;
            if (e.Key == VirtualKey.F5) { e.Handled = true; await UiAsync(() => Session.RefreshAsync()); }
            else if (e.Key == VirtualKey.F2 && Allowed(FileOrFolderActions.AllowCopyRenameMoveFiles))
            { e.Handled = true; if (FileHasFocus()) await UiAsync(() => FileActionAsync(ResourceAction.Rename)); else BeginFolderRename(); }
            else if (e.Key == VirtualKey.Delete && Allowed(FileOrFolderActions.AllowDeleteFiles))
            { e.Handled = true; await UiAsync(FileHasFocus() ? DeleteFilesAsync : DeleteFolderAsync); }
            else if (e.Key == VirtualKey.Escape) Cancelled?.Invoke(this, EventArgs.Empty);
            else if (e.Key == VirtualKey.Enter && Options.DialogOperationMode == DialogOperationModes.ReturnSelectedItems) { e.Handled = true; ConfirmSelection(); }
        };
        Loaded += async (_, _) => { if (owner is not null) await UiAsync(InitializeAsync); };
        InitializeDragAndDrop(); UpdateControls();
    }
    private bool FileHasFocus()
    {
        var focused = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        while (focused is not null) { if (ReferenceEquals(focused, files)) return true; focused = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(focused); }
        return false;
    }
    private static Grid FileColumns()
    {
        var grid = new Grid { ColumnSpacing = 10, HorizontalAlignment = HorizontalAlignment.Stretch };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(185) }); return grid;
    }
    private void BuildActions()
    {
        var fileMenu = new MenuFlyout(); var folderMenu = new MenuFlyout(); files.ContextFlyout = fileMenu; tree.ContextFlyout = folderMenu;
        files.ContextRequested += (_, e) =>
        {
            e.Handled = true;
            if (IsBusy || !initialized) return;
            var source = e.OriginalSource as DependencyObject;
            while (source is not null && source is not ListViewItem) source = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(source);
            if (source is ListViewItem item && !files.SelectedItems.Contains(item)) { files.SelectedItems.Clear(); files.SelectedItems.Add(item); }
            if (files.SelectedItems.Count == 0) return;
            if (e.TryGetPosition(files, out var point)) fileMenu.ShowAt(files, new FlyoutShowOptions { Position = point }); else fileMenu.ShowAt(files);
        };
        tree.ContextRequested += async (_, e) =>
        {
            e.Handled = true;
            if (IsBusy || !initialized) return;
            var source = e.OriginalSource as DependencyObject;
            while (source is not null && source is not TreeViewItem) source = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(source);
            var model = source is TreeViewItem item && tree.NodeFromContainer(item) is { } node && nodes.TryGetValue(node, out var found) ? found : Session.SelectedNode;
            var positioned = e.TryGetPosition(tree, out var point);
            if (model is null) return;
            if (!ReferenceEquals(Session.SelectedNode, model)) await UiAsync(() => Session.SelectAsync(model));
            if (IsBusy || !ReferenceEquals(Session.SelectedNode, model)) return;
            if (positioned) folderMenu.ShowAt(tree, new FlyoutShowOptions { Position = point }); else folderMenu.ShowAt(tree);
        };
        void Action(string key, Func<Task> action, FileOrFolderActions? flag, Func<bool> enabled, MenuFlyout menu, bool sharing = false)
        {
            var button = Dialogs.Button(key, async () => await UiAsync(action, key == "ActionRefreshFiles" ? DmsBrowserFailurePhase.Refresh : DmsBrowserFailurePhase.Action, key)); BrowserActionIcons.Apply(button, key); fileToolbar.Children.Add(button);
            var mandatory = key is "ActionUpload" or "ActionUploadFolder" or "ActionDownload" or "ActionOpen" or "ActionDelete";
            actions.Add((button, () => (!flag.HasValue || Allowed(flag.Value)) && (key is "FileVersionHistory" or "ActionDownloadFolder" ? enabled() : true),
                () => mandatory || Visible(flag, sharing) && (key != "ActionFolderSharings" || FolderActionVisible(key)) && (key != "FileVersionHistory" || DmsProvider?.SupportsFileVersionHistory == true)));
            Menu(menu, key, action, flag, enabled, sharing);
        }
        void Menu(MenuFlyout menu, string key, Func<Task> action, FileOrFolderActions? flag, Func<bool> enabled, bool sharing = false)
        {
            var item = new MenuFlyoutItem { Name = key, Text = Dialogs.Caption(key) };
            if (BrowserActionIcons.Create(key, folderMenu: menu == folderMenu, menu: true) is { } icon) item.Icon = new ImageIcon { Source = icon.Source };
            item.Click += async (_, _) => await UiAsync(action, key == "ActionRefreshFiles" ? DmsBrowserFailurePhase.Refresh : DmsBrowserFailurePhase.Action, key); menu.Items.Add(item);
            actions.Add((item, () => (!flag.HasValue || Allowed(flag.Value)) && (key is "FileVersionHistory" or "ActionDownloadFolder" ? enabled() : true),
                () => Visible(flag, sharing) && (menu != folderMenu || FolderActionVisible(key)) && (key != "FileVersionHistory" || DmsProvider?.SupportsFileVersionHistory == true && session?.BrowseMode == BrowseModes.FoldersAndFiles)));
        }
        Action("ActionUpload", UploadFilesAsync, FileOrFolderActions.AllowUploadFiles, () => HasFolderForFiles, fileMenu);
        Action("ActionUploadFolder", UploadFolderAsync, FileOrFolderActions.AllowUploadFiles, () => initialized, fileMenu);
        Action("ActionDownload", DownloadFilesAsync, FileOrFolderActions.AllowDownloadFiles, () => HasFiles, fileMenu);
        Action("ActionDownloadFolder", DownloadFolderAsync, FileOrFolderActions.AllowDownloadFiles, () => initialized, folderMenu);
        Action("ActionOpen", OpenFilesAsync, FileOrFolderActions.AllowDownloadFiles, () => HasFiles, fileMenu);
        foreach (var (key, kind) in new[] { ("ActionCopy", ResourceAction.Copy), ("ActionRename", ResourceAction.Rename), ("ActionMove", ResourceAction.Move) })
        { Action(key, () => FileActionAsync(kind), FileOrFolderActions.AllowCopyRenameMoveFiles, () => HasFiles, fileMenu); Menu(folderMenu, key, () => FolderActionAsync(kind), FileOrFolderActions.AllowCopyRenameMoveFiles, () => MutableFolder); }
        Action("ActionDelete", DeleteFilesAsync, FileOrFolderActions.AllowDeleteFiles, () => HasFiles, fileMenu);
        Action("ActionFileSharings", () => SharingAsync(true), FileOrFolderActions.AllowSharings, () => HasFiles, fileMenu, true);
        Action("FileProperties", () => PropertiesAsync(true), null, () => files.SelectedItems.Count == 1, fileMenu);
        Action("FileVersionHistory", ShowVersionsAsync, FileOrFolderActions.AllowViewFileVersionHistory, () => files.SelectedItems.Count == 1 && DmsProvider?.SupportsFileVersionHistory == true, fileMenu);
        Action("ActionFolderSharings", () => SharingAsync(false), FileOrFolderActions.AllowSharings, () => Session.SelectedNode?.Item is not null, folderMenu, true);
        Action("FolderProperties", () => PropertiesAsync(false), null, () => initialized, folderMenu);
        Action("ActionRefreshFiles", () => Session.RefreshAsync(), null, () => initialized, fileMenu);
        Menu(folderMenu, "ActionRefreshFiles", () => Session.RefreshAsync(), null, () => initialized);
        Menu(folderMenu, "ActionDelete", DeleteFolderAsync, FileOrFolderActions.AllowDeleteFiles, () => MutableFolder);
        Menu(folderMenu, "ActionCreateFolder", CreateFolderAsync, FileOrFolderActions.AllowCreateFolders, () => initialized);
        AddBottom("ActionCreateFolder", CreateFolderAsync, FileOrFolderActions.AllowCreateFolders);
        bottom.Children.Add(showFiles); actions.Add((showFiles, () => true, () => Allowed(FileOrFolderActions.AllowSwitchBrowseMode)));
        showFiles.Checked += async (_, _) => { if (!suppressSelection && initialized && !IsBusy) await UiAsync(() => SetBrowseModeAsync(BrowseModes.FoldersAndFiles)); };
        showFiles.Unchecked += async (_, _) => { if (!suppressSelection && initialized && !IsBusy) await UiAsync(() => SetBrowseModeAsync(BrowseModes.Folders)); };
        instanceButton = AddBottom("ChangeDmsInstance", ChangeInstanceAsync, FileOrFolderActions.AllowSwitchDmsInstance, () => Options.RemoteInstance.SelectionDialog is not null && DmsProvider is IDmsInstanceProvider && availableInstanceCount > 1);
        AddBottom("ActionOkay", () => { pendingConfirmation = true; return Task.CompletedTask; }, null, () => Options.DialogOperationMode == DialogOperationModes.ReturnSelectedItems);
        AddBottom("ActionCancel", () => { pendingCancellation = true; return Task.CompletedTask; }, null, () => Options.DialogOperationMode == DialogOperationModes.ReturnSelectedItems);
        AddBottom("ActionClose", () => { pendingCancellation = true; return Task.CompletedTask; }, null, () => Options.DialogOperationMode == DialogOperationModes.NoResults);
    }
    private Button AddBottom(string key, Func<Task> action, FileOrFolderActions? flag, Func<bool>? visible = null)
    {
        var button = Dialogs.Button(key, async () => await UiAsync(action, key == "ActionRefreshFiles" ? DmsBrowserFailurePhase.Refresh : DmsBrowserFailurePhase.Action, key)); bottom.Children.Add(button);
        if (key is "ActionCancel" or "ActionClose") terminalButtons.Add(button);
        actions.Add((button, () => !flag.HasValue || Allowed(flag.Value), () => (key == "ActionCreateFolder" || Visible(flag)) && (visible?.Invoke() ?? true))); return button;
    }
    private void AttachSession() => Session.Changed += Synchronize;
    private void Synchronize()
    {
        suppressSelection = true;
        try
        {
            var retainedNodes = new HashSet<TreeViewNode>();
            void Reconcile(IList<TreeViewNode> target, IEnumerable<BrowserNode> children)
            {
                var models = children.Where(child => !child.Placeholder).ToArray();
                for (var index = 0; index < models.Length; index++)
                {
                    var model = models[index];
                    TreeViewNode node;
                    if (index < target.Count && nodes.TryGetValue(target[index], out var existing) && ReferenceEquals(existing, model)) node = target[index];
                    else
                    {
                        node = new TreeViewNode(); nodes[node] = model;
                        target.Insert(index, node);
                    }
                    retainedNodes.Add(node);
                    var label = (model.Name, model.Shared);
                    if (!nodeLabels.TryGetValue(node, out var previous) || previous != label)
                    { node.Content = BrowserIcons.DirectoryRow(model); nodeLabels[node] = label; }
                    Reconcile(node.Children, model.Children);
                    node.HasUnrealizedChildren = !model.Loaded && BrowserNode.HasChildren(model.Item) != false;
                    if (node.IsExpanded != model.IsExpanded) node.IsExpanded = model.IsExpanded;
                }
                while (target.Count > models.Length) target.RemoveAt(target.Count - 1);
            }
            Reconcile(tree.RootNodes, Session.Roots);
            foreach (var key in nodes.Keys.Where(key => !retainedNodes.Contains(key)).ToArray()) { nodes.Remove(key); nodeLabels.Remove(key); }
            var selectedNode = nodes.FirstOrDefault(pair => ReferenceEquals(pair.Value, Session.SelectedNode)).Key;
            if (!ReferenceEquals(tree.SelectedNode, selectedNode)) tree.SelectedNode = selectedNode;
            IEnumerable<FileRow> ordered = sort switch { "Size" => Session.Files.OrderBy(r => r.ContentLength), "Modified" => Session.Files.OrderBy(r => r.LastModified), _ => Session.Files.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase) };
            if (descending) ordered = ordered.Reverse();
            var rows = ordered.ToArray();
            if (!renderedFiles.SequenceEqual(rows))
            {
              files.Items.Clear(); renderedFiles = rows;
              foreach (var row in rows)
              {
                var grid = FileColumns(); grid.Children.Add(BrowserIcons.FileRow(row));
                var size = new TextBlock { Text = row.Size, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(size, 1); grid.Children.Add(size);
                var date = new TextBlock { Text = row.LastModified?.ToString("g"), VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(date, 2); grid.Children.Add(date);
                var item = new ListViewItem { Content = grid, Tag = row, HorizontalContentAlignment = HorizontalAlignment.Stretch };
                files.Items.Add(item);
              }
            }
            if (!SessionBusy)
            {
                var retained = fileSelection.Retained(Session);
                foreach (var selected in files.SelectedItems.Cast<ListViewItem>().Where(item => !retained.Contains((FileRow)item.Tag)).ToArray()) files.SelectedItems.Remove(selected);
                foreach (var row in retained)
                {
                    var item = files.Items.Cast<ListViewItem>().Single(candidate => ReferenceEquals(candidate.Tag, row));
                    if (!files.SelectedItems.Contains(item)) files.SelectedItems.Add(item);
                }
                fileSelection.Capture(Session, Selection.Files);
            }
        }
        finally { suppressSelection = false; }
        UpdateControls(); SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
    private void UpdateControls()
    {
        var busy = IsBusy; tree.IsEnabled = files.IsEnabled = (!busy || dragRunning && !SessionBusy) && initialized;
        progress.Visibility = busy && ActiveSharingDialog is null ? Visibility.Visible : Visibility.Collapsed;
        status.Text = busy && ActiveSharingDialog is null ? UiText.Extra("Working…", "Wird ausgeführt…") : session?.CurrentPath ?? "";
        var fileMode = (session?.BrowseMode ?? Options.BrowseMode) == BrowseModes.FoldersAndFiles;
        filePanel.Visibility = fileMode ? Visibility.Visible : Visibility.Collapsed;
        columns.ColumnDefinitions[2].Width = new GridLength(fileMode ? 7 : 0, GridUnitType.Star);
        suppressSelection = true; try { showFiles.IsChecked = fileMode; } finally { suppressSelection = false; }
        foreach (var action in actions)
        { action.Control.Visibility = action.Visible() ? Visibility.Visible : Visibility.Collapsed; action.Control.IsEnabled = !busy && (terminalButtons.Contains(action.Control) || initialized && action.Enabled()); }
        if (instanceButton?.Visibility == Visibility.Visible && DmsProvider is IDmsInstanceProvider provider && provider.CurrentDmsInstance is { } instance)
            instanceButton.Content = UiText.Format("CurrentDmsInstance", instance.DisplayName);
    }
}
