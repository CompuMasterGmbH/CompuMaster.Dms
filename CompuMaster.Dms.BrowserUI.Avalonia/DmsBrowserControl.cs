using System.ComponentModel;
using System.Diagnostics;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Templates;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Data;
using global::Avalonia.Input;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform;
using global::Avalonia.Markup.Xaml.Styling;
using global::Avalonia.Platform.Storage;
using global::Avalonia.Styling;

namespace CompuMaster.Dms.BrowserUI.Avalonia;

/// <summary>Provides an embeddable, provider-independent Avalonia DMS browser.</summary>
/// <remarks>Call <see cref="InitializeAsync"/> after attaching the control to a window, or let the control initialize on attachment.
/// Dispose preview files with <see cref="CleanupTemporaryFiles"/> when the host no longer uses the control.</remarks>
public sealed partial class DmsBrowserControl : UserControl
{
    private BrowserSession? session;
    private readonly FileSelectionSnapshot fileSelection = new();
    internal DmsItemSharings? ActiveSharingDialog { get; private set; }
    private readonly IDmsLoginProfile? profile;
    private readonly TreeView tree = new() { Name = "DmsFolders", MinWidth = 180 };
    private readonly DataGrid files = new() { Name = "DmsFiles", AutoGenerateColumns = false, IsReadOnly = true,
        SelectionMode = DataGridSelectionMode.Extended, CanUserSortColumns = true, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal };
    private readonly Grid columns = new();
    private readonly ProgressBar progress = new() { IsIndeterminate = true, Height = 3, IsVisible = false };
    private readonly TextBlock status = new() { Margin = new Thickness(6) };
    private readonly WrapPanel fileToolbar = new();
    private readonly DockPanel filePanel = new() { Name = "FilePanel", LastChildFill = true };
    private readonly ToggleButton showFiles = new() { Name = "ActionShowFiles", Margin = new Thickness(3), Content = Dialogs.Caption("ActionShowFiles") };
    private readonly WrapPanel bottom = new() { Orientation = Orientation.Horizontal };
    private readonly List<(Control Control, Func<bool> Enabled, Func<bool> Visible)> actions = new();
    private readonly HashSet<Control> terminalButtons = new();
    private readonly List<string> temporaryDirectories = new();
    private readonly List<Exception> cleanupFailures = new();
    private bool suppressSelection;
    private bool interactionRunning;
    private bool initializing;
    private Task? initialization;
    private bool initialized;
    private DmsBrowserFailurePhase startupPhase = DmsBrowserFailurePhase.Startup;
    private bool pendingConfirmation;
    private bool pendingCancellation;
    private Button? instanceButton;

    /// <summary>Creates a browser for an authorized provider.</summary>
    /// <param name="provider">The provider; its existing instance is preserved unless options explicitly select another.</param>
    /// <param name="options">The optional browser settings.</param>
    public DmsBrowserControl(BaseDmsProvider provider, DmsBrowserOptions? options = null)
    {
        Options = options ?? new DmsBrowserOptions();
        session = new BrowserSession(provider, Options);
        BuildControls();
        AttachSession();
    }
    /// <summary>Creates a browser that authorizes a profile asynchronously.</summary>
    /// <param name="profile">The profile used to authorize the provider.</param>
    /// <param name="options">The optional browser settings.</param>
    public DmsBrowserControl(IDmsLoginProfile profile, DmsBrowserOptions? options = null)
    {
        this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Options = options ?? new DmsBrowserOptions();
        BuildControls();
    }
    /// <summary>Gets the settings supplied by the host.</summary>
    /// <remarks>Configure settings before initialization. Use <see cref="SetBrowseModeAsync"/> for later mode changes.</remarks>
    public DmsBrowserOptions Options { get; }
    /// <summary>Gets the authorized provider, or null before profile authorization completes.</summary>
    public BaseDmsProvider? DmsProvider => session?.Provider;
    /// <summary>Gets whether an operation or owned dialog prevents concurrent actions and closing.</summary>
    public bool IsBusy => initializing || interactionRunning || dragRunning || session?.IsBusy == true;
    /// <summary>Gets the current folder relative to the browser root.</summary>
    public string? SelectedFolder => session?.SelectedFolder;
    /// <summary>Gets the currently selected resources.</summary>
    public DmsBrowserSelection Selection => new(SelectedFolder, session?.SelectedNode?.Item,
        files.SelectedItems.OfType<FileRow>().Select(f => f.Item).ToArray());
    /// <summary>Gets or sets the method that opens a downloaded read-only temporary preview.</summary>
    /// <remarks>The path remains valid until cleanup. Hosts may replace the default operating-system shell launch.</remarks>
    public Func<string, Task> OpenDownloadedFileItem { get; set; } = path =>
    {
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return Task.CompletedTask;
    };
    /// <summary>Occurs when the user confirms the current selection.</summary>
    public event EventHandler? SelectionConfirmed;
    /// <summary>Occurs when the user cancels or closes the browser.</summary>
    public event EventHandler? Cancelled;
    /// <summary>Occurs after a browser operation or selection changes.</summary>
    public event EventHandler? SelectionChanged;
    /// <summary>Gets preview-cleanup failures encountered while closing the dialog.</summary>
    public IReadOnlyList<Exception> CleanupFailures => cleanupFailures;
    internal BrowserSession Session => session ?? throw new InvalidOperationException("Initialize the browser first.");
    internal TreeView FolderTree => tree;
    internal DataGrid FileGrid => files;
    private Window Owner => TopLevel.GetTopLevel(this) as Window ?? throw new InvalidOperationException("Attach the browser to an Avalonia window.");

    private void BuildControls()
    {
        FlowDirection = UiText.LayoutDirection;
        Styles.Add(new StyleInclude(new Uri("avares://CompuMaster.Dms.BrowserUI.Avalonia/"))
            { Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml") });
        var layout = new DockPanel { Margin = new Thickness(10), LastChildFill = true };
        var top = new StackPanel { Children = { progress } };
        DockPanel.SetDock(top, Dock.Top); layout.Children.Add(top);
        var footer = new StackPanel { Children = { status, bottom } };
        DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
        columns.ColumnDefinitions = new ColumnDefinitions("3*,6,7*");
        columns.Children.Add(tree);
        var splitter = new GridSplitter { Width = 6, HorizontalAlignment = HorizontalAlignment.Stretch };
        Grid.SetColumn(splitter, 1); columns.Children.Add(splitter);
        DockPanel.SetDock(fileToolbar, Dock.Top); filePanel.Children.Add(fileToolbar); filePanel.Children.Add(files);
        Grid.SetColumn(filePanel, 2); columns.Children.Add(filePanel);
        layout.Children.Add(columns); Content = layout;

        tree.ItemTemplate = new FuncTreeDataTemplate<BrowserNode>((node, _) =>
        {
            var image = new Image { Width = 24, Height = 24, Margin = new Thickness(0, 0, 6, 0) };
            image.Bind(Image.SourceProperty, new Binding("Icon"));
            var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            text.Bind(TextBlock.TextProperty, new Binding("Name"));
            var editor = new TextBox { Name = "FolderNameEditor", IsVisible = false, MinWidth = 140, DataContext = node };
            editor.KeyDown += async (_, e) =>
            {
                if (e.Key is not (Key.Enter or Key.Escape)) return;
                e.Handled = true;
                if (e.Key == Key.Escape) CancelFolderRename();
                else await CommitFolderRenameAsync();
            };
            editor.LostFocus += async (_, _) => { if (ReferenceEquals(editingFolder, editor)) await CommitFolderRenameAsync(); };
            return new StackPanel { Orientation = Orientation.Horizontal, Children = { image, new Grid { Children = { text, editor } } } };
        }, node => node.Children);
        tree.Styles.Add(new global::Avalonia.Styling.Style(x => x.OfType<TreeViewItem>())
        {
            Setters =
            {
                new global::Avalonia.Styling.Setter(TreeViewItem.IsExpandedProperty, new Binding("IsExpanded") { Mode = BindingMode.TwoWay }),
                // Keep a child for Avalonia's expander, but never lay out the lazy-load sentinel as a directory.
                new global::Avalonia.Styling.Setter(TreeViewItem.IsVisibleProperty,
                    new Binding(nameof(BrowserNode.Placeholder)) { Converter = global::Avalonia.Data.Converters.BoolConverters.Not })
            }
        });
        tree.AddHandler(TreeViewItem.ExpandedEvent, async (_, e) =>
        {
            if (suppressSelection || e.Source is not TreeViewItem { DataContext: BrowserNode node } || node.Loaded || node.Placeholder) return;
            await UiAsync(() => Session.ExpandAsync(node), DmsBrowserFailurePhase.ChildListing, "ExpandFolder");
        });
        tree.SelectionChanged += async (_, _) =>
        {
            if (suppressSelection || tree.SelectedItem is not BrowserNode node || node.Placeholder) return;
            await UiAsync(() => Session.SelectAsync(node), DmsBrowserFailurePhase.Action, "SelectFolder");
        };
        tree.DoubleTapped += (_, _) => { if (editingFolder is null && !IsBusy && Options.DialogOperationMode == DialogOperationModes.ReturnSelectedItems) ConfirmSelection(); };
        files.DoubleTapped += async (_, _) =>
        {
            if (files.SelectedItems.Count == 0) return;
            if (Options.DialogOperationMode == DialogOperationModes.ReturnSelectedItems) ConfirmSelection();
            else await UiAsync(OpenFilesAsync);
        };
        files.SelectionChanged += (_, _) =>
        {
            if (suppressSelection || SessionBusy) return;
            if (session is not null) fileSelection.Capture(Session, Selection.Files);
            UpdateControls(); SelectionChanged?.Invoke(this, EventArgs.Empty);
        };
        var nameColumn = new DataGridTemplateColumn
        {
            Header = UiText.Get("ColumnName"), SortMemberPath = "Name", Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            CellTemplate = new FuncDataTemplate<FileRow>((row, _) =>
            {
                var icon = new Image { Source = BrowserIcons.File(row?.Shared == true), Width = 24, Height = 24, Margin = new Thickness(4) };
                if (row is not null) PlatformFileIcons.Attach(icon, row.Name, row.Shared);
                var name = new TextBlock { Text = row?.Name, VerticalAlignment = VerticalAlignment.Center };
                if (row?.Duplicate == true) name.Foreground = Brushes.White;
                var container = new Border { Background = row?.Duplicate == true ? Brushes.Firebrick : null,
                    Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { icon, name } } };
                if (row?.Duplicate == true) ToolTip.SetTip(container, UiText.Get("DuplicateFileNameConflict"));
                else if (row?.Shared == true) ToolTip.SetTip(container, Dialogs.Caption("ActionSharings"));
                return container;
            })
        };
        files.Columns.Add(nameColumn);
        files.Columns.Add(new DataGridTextColumn { Header = UiText.Get("ColumnSize"), Binding = new Binding("Size"), SortMemberPath = "ContentLength" });
        files.Columns.Add(new DataGridTextColumn { Header = UiText.Get("ColumnLastModification"), Binding = new Binding("LastModified"),
            SortMemberPath = "LastModified", MinWidth = 180, Width = new DataGridLength(190) });
        var folderMenu = new ContextMenu();
        var fileMenu = new ContextMenu();
        tree.ContextMenu = folderMenu; files.ContextMenu = fileMenu;
        folderMenu.Opening += (_, e) => { if (IsBusy || session?.SelectedNode is null) e.Cancel = true; };
        fileMenu.Opening += (_, e) => { if (IsBusy || files.SelectedItems.Count == 0) e.Cancel = true; };
        AddAction("ActionUpload", UploadFilesAsync, FileOrFolderActions.AllowUploadFiles, () => HasFolderForFiles, fileMenu);
        AddAction("ActionUploadFolder", UploadFolderAsync, FileOrFolderActions.AllowUploadFiles, () => initialized && Session.SelectedNode is not null, fileMenu);
        AddAction("ActionDownload", DownloadFilesAsync, FileOrFolderActions.AllowDownloadFiles, () => HasFiles, fileMenu);
        AddAction("ActionDownloadFolder", DownloadFolderAsync, FileOrFolderActions.AllowDownloadFiles, () => initialized && Session.SelectedNode is not null, folderMenu);
        AddAction("ActionOpen", OpenFilesAsync, FileOrFolderActions.AllowDownloadFiles, () => HasFiles, fileMenu);
        foreach (var (key, action) in new[] { ("ActionCopy", ResourceAction.Copy), ("ActionRename", ResourceAction.Rename), ("ActionMove", ResourceAction.Move) })
        {
            AddAction(key, () => FileActionAsync(action), FileOrFolderActions.AllowCopyRenameMoveFiles,
                () => HasFiles && (action != ResourceAction.Rename || files.SelectedItems.Count == 1), fileMenu);
            AddMenu(folderMenu, key, () => FolderActionAsync(action), FileOrFolderActions.AllowCopyRenameMoveFiles, () => MutableFolder);
        }
        AddAction("ActionDelete", DeleteFilesAsync, FileOrFolderActions.AllowDeleteFiles, () => HasFiles, fileMenu);
        AddAction("ActionFileSharings", () => SharingAsync(true), FileOrFolderActions.AllowSharings, () => HasFiles, fileMenu, sharing: true);
        AddAction("FileProperties", () => PropertiesAsync(true), null, () => files.SelectedItems.Count == 1, fileMenu);
        AddAction("FileVersionHistory", ShowVersionsAsync, FileOrFolderActions.AllowViewFileVersionHistory,
            () => HasFiles && files.SelectedItems.Count == 1 && DmsProvider?.SupportsFileVersionHistory == true, fileMenu);
        AddAction("ActionFolderSharings", () => SharingAsync(false), FileOrFolderActions.AllowSharings, () => Session.SelectedNode?.Item is not null, folderMenu, sharing: true);
        AddAction("FolderProperties", () => PropertiesAsync(false), null, () => Session.SelectedNode is not null, folderMenu);
        AddAction("ActionRefreshFiles", () => Session.RefreshAsync(), null, () => initialized, fileMenu);
        AddMenu(folderMenu, "ActionRefreshFiles", () => Session.RefreshAsync(), null, () => initialized);
        AddMenu(folderMenu, "ActionDelete", DeleteFolderAsync, FileOrFolderActions.AllowDeleteFiles, () => MutableFolder);
        AddMenu(folderMenu, "ActionCreateFolder", CreateFolderAsync, FileOrFolderActions.AllowCreateFolders, () => Session.SelectedNode is not null);

        AddBottom("ActionCreateFolder", CreateFolderAsync, FileOrFolderActions.AllowCreateFolders);
        bottom.Children.Add(showFiles);
        actions.Add((showFiles, () => true, () => Allowed(FileOrFolderActions.AllowSwitchBrowseMode)));
        showFiles.IsCheckedChanged += async (_, _) =>
        {
            if (suppressSelection || !initialized || IsBusy) return;
            var mode = showFiles.IsChecked == true ? BrowseModes.FoldersAndFiles : BrowseModes.Folders;
            await UiAsync(() => SetBrowseModeAsync(mode));
        };
        instanceButton = AddBottom("ChangeDmsInstance", ChangeInstanceAsync, FileOrFolderActions.AllowSwitchDmsInstance,
            () => Options.RemoteInstance.SelectionDialog is not null && DmsProvider is IDmsInstanceProvider && availableInstanceCount > 1);
        var okay = AddBottom("ActionOkay", () => { pendingConfirmation = true; return Task.CompletedTask; }, null,
            () => Options.DialogOperationMode == DialogOperationModes.ReturnSelectedItems);
        okay.IsDefault = true;
        AddBottom("ActionCancel", () => { pendingCancellation = true; return Task.CompletedTask; }, null,
            () => Options.DialogOperationMode == DialogOperationModes.ReturnSelectedItems);
        AddBottom("ActionClose", () => { pendingCancellation = true; return Task.CompletedTask; }, null,
            () => Options.DialogOperationMode == DialogOperationModes.NoResults);
        KeyDown += async (_, e) =>
        {
            if (IsBusy) return;
            if (e.Key == Key.F5) { e.Handled = true; await UiAsync(() => Session.RefreshAsync()); }
            else if (e.Key == Key.F2 && Allowed(FileOrFolderActions.AllowCopyRenameMoveFiles))
            {
                e.Handled = true;
                if (files.IsKeyboardFocusWithin) await UiAsync(() => FileActionAsync(ResourceAction.Rename));
                else BeginFolderRename();
            }
            else if (e.Key == Key.Delete && Allowed(FileOrFolderActions.AllowDeleteFiles))
            { e.Handled = true; await UiAsync(files.IsKeyboardFocusWithin ? DeleteFilesAsync : DeleteFolderAsync); }
        };
        AttachedToVisualTree += async (_, _) => { await UiAsync(InitializeAsync); };
        InitializeDragAndDrop();
        UpdateControls();
    }

    private bool HasFiles => initialized && files.SelectedItems.Count > 0 && Session.BrowseMode == BrowseModes.FoldersAndFiles;
    private bool HasFolderForFiles => initialized && Session.SelectedNode is not null && (Session.SelectedNode.Item is not null || Session.Provider.SupportsFilesInRootFolder);
    private bool MutableFolder => initialized && Session.SelectedNode?.Parent is not null && Session.SelectedNode.Item is not null &&
        !Session.CollectionManagementRestricted(Session.SelectedNode.Item);
    private bool Allowed(FileOrFolderActions flag) => (Options.AllowedActions & flag) == flag;
    private bool Visible(FileOrFolderActions? flag, bool sharing = false) =>
        (!flag.HasValue || Allowed(flag.Value)) && (!sharing || DmsProvider?.SupportsSharingSetup == true);
    private void AddAction(string key, Func<Task> action, FileOrFolderActions? flag, Func<bool> enabled, ContextMenu menu, bool sharing = false)
    {
        var button = Dialogs.Button(key, async () => await UiAsync(action, key == "ActionRefreshFiles" ? DmsBrowserFailurePhase.Refresh : DmsBrowserFailurePhase.Action, key)); button.Name = key;
        BrowserActionIcons.Apply(button, key);
        fileToolbar.Children.Add(button);
        actions.Add((button, () => (!flag.HasValue || Allowed(flag.Value)) &&
            (key == "FileVersionHistory" || key == "ActionDownloadFolder" ? enabled() : true),
            () => ToolbarActionVisible(key, flag, sharing)));
        AddMenu(menu, key, action, flag, enabled, sharing);
    }
    private void AddMenu(ContextMenu menu, string key, Func<Task> action, FileOrFolderActions? flag, Func<bool> enabled, bool sharing = false)
    {
        var item = new MenuItem { Name = key, Header = Dialogs.Caption(key),
            Icon = BrowserActionIcons.Create(key, folderMenu: menu == tree.ContextMenu, menu: true) };
        item.Click += async (_, _) => await UiAsync(action, key == "ActionRefreshFiles" ? DmsBrowserFailurePhase.Refresh : DmsBrowserFailurePhase.Action, key);
        menu.Items.Add(item); actions.Add((item, () => (!flag.HasValue || Allowed(flag.Value)) &&
            (key is "FileVersionHistory" or "ActionDownloadFolder" ? enabled() : true),
            () => Visible(flag, sharing) && (menu != tree.ContextMenu || FolderActionVisible(key)) && (key != "FileVersionHistory" ||
            DmsProvider?.SupportsFileVersionHistory == true && session?.BrowseMode == BrowseModes.FoldersAndFiles)));
    }
    private bool FolderActionVisible(string key) => key switch
    {
        "ActionCopy" or "ActionRename" or "ActionMove" or "ActionDelete" => MutableFolder,
        "ActionFolderSharings" => session?.SelectedNode?.Item is not { } item || !Session.CollectionManagementRestricted(item),
        _ => true
    };
    private bool ToolbarActionVisible(string key, FileOrFolderActions? flag, bool sharing) => key switch
    {
        "ActionUpload" or "ActionUploadFolder" or "ActionDownload" or "ActionOpen" or "ActionDelete" => true,
        "ActionFolderSharings" => Visible(flag, sharing) && FolderActionVisible(key),
        "FileVersionHistory" => Visible(flag) && DmsProvider?.SupportsFileVersionHistory == true && session?.BrowseMode == BrowseModes.FoldersAndFiles,
        _ => Visible(flag, sharing)
    };
    private Button AddBottom(string key, Func<Task> action, FileOrFolderActions? flag, Func<bool>? visible = null)
    {
        var button = Dialogs.Button(key, async () => await UiAsync(action, key == "ActionRefreshFiles" ? DmsBrowserFailurePhase.Refresh : DmsBrowserFailurePhase.Action, key)); button.Name = key;
        bottom.Children.Add(button);
        if (key is "ActionCancel" or "ActionClose") terminalButtons.Add(button);
        actions.Add((button, () => (!flag.HasValue || Allowed(flag.Value)) && initialized,
            () => (key == "ActionCreateFolder" || Visible(flag)) && (visible?.Invoke() ?? true)));
        return button;
    }
    private void AttachSession()
    {
        tree.ItemsSource = Session.Roots; files.ItemsSource = Session.Files;
        Session.Changed += Synchronize;
    }
    private void Synchronize()
    {
        suppressSelection = true;
        try
        {
            tree.SelectedItem = Session.SelectedNode;
            if (!SessionBusy)
            {
                var retained = fileSelection.Retained(Session);
                foreach (var selected in files.SelectedItems.Cast<FileRow>().Where(row => !retained.Contains(row)).ToArray()) files.SelectedItems.Remove(selected);
                foreach (var row in retained) if (!files.SelectedItems.Contains(row)) files.SelectedItems.Add(row);
                fileSelection.Capture(Session, Selection.Files);
            }
        }
        finally { suppressSelection = false; }
        UpdateControls(); SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
    private void UpdateControls()
    {
        var busy = IsBusy;
        tree.IsEnabled = (!busy || dragRunning && !SessionBusy) && initialized;
        files.IsEnabled = (!busy || dragRunning && !SessionBusy) && initialized;
        progress.IsVisible = busy && ActiveSharingDialog is null;
        status.Text = busy && ActiveSharingDialog is null ? UiText.Extra("Working…", "Wird ausgeführt…") : session?.CurrentPath ?? "";
        columns.ColumnDefinitions[2].Width = session?.BrowseMode == BrowseModes.Folders ? new GridLength(0) : new GridLength(7, GridUnitType.Star);
        columns.Children[1].IsVisible = session?.BrowseMode != BrowseModes.Folders;
        files.IsVisible = session?.BrowseMode != BrowseModes.Folders;
        filePanel.IsVisible = files.IsVisible;
        var previousSuppression = suppressSelection;
        suppressSelection = true;
        try { showFiles.IsChecked = (session?.BrowseMode ?? Options.BrowseMode) == BrowseModes.FoldersAndFiles; }
        finally { suppressSelection = previousSuppression; }
        foreach (var action in actions)
        {
            action.Control.IsVisible = action.Visible();
            action.Control.IsEnabled = !busy && (terminalButtons.Contains(action.Control) || initialized && action.Enabled());
        }
        if (instanceButton is { IsVisible: true } && DmsProvider is IDmsInstanceProvider instances && instances.CurrentDmsInstance is { } current)
            instanceButton.Content = UiText.Format("CurrentDmsInstance", current.DisplayName);
    }
    private int availableInstanceCount;
    /// <summary>Authorizes the provider, selects the startup instance and loads the initial directory.</summary>
    /// <returns>A task that completes when initialization succeeds.</returns>
    /// <exception cref="OperationCanceledException">The user cancels startup instance selection.</exception>
    public Task InitializeAsync() => initialization ??= InitializeCoreAsync();
    private async Task InitializeCoreAsync()
    {
        initializing = true; UpdateControls();
        try
        {
            if (session is null)
            {
                startupPhase = DmsBrowserFailurePhase.Authentication;
                session = new BrowserSession(await DmsFactory.CreateAuthorizedDmsProviderInstanceAsync(profile), Options);
                AttachSession();
            }
            startupPhase = DmsBrowserFailurePhase.InstanceSelection;
            if (!await Options.RemoteInstance.SelectAsync(Session.Provider, Owner)) throw new OperationCanceledException();
            if (DmsProvider is IDmsInstanceProvider instances && Options.RemoteInstance.SelectionDialog is not null)
                availableInstanceCount = (await Task.Run(instances.ListAvailableDmsInstances)).Count;
            startupPhase = DmsBrowserFailurePhase.Startup;
            await Session.InitializeAsync();
            initialized = true;
            Synchronize();
        }
        catch { initialization = null; throw; }
        finally { initializing = false; UpdateControls(); }
    }
    /// <summary>Navigates to a directory relative to the configured browser root.</summary>
    /// <param name="relativePath">The relative directory, or null for the root.</param>
    /// <param name="cancellationToken">Cancels provider access when supported.</param>
    /// <returns>A task that completes after the file list is updated.</returns>
    public Task SelectFolderAsync(string? relativePath, CancellationToken cancellationToken = default) => Session.SelectPathAsync(relativePath, cancellationToken);
    /// <summary>Refreshes loaded directories and the current file list.</summary>
    /// <param name="cancellationToken">Cancels provider access when supported.</param>
    /// <returns>A task that completes after refresh.</returns>
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Session.RefreshAsync(cancellationToken);
    /// <summary>Changes whether files are displayed.</summary>
    /// <param name="mode">The desired content mode.</param>
    /// <returns>A task that completes after updating the file list.</returns>
    public async Task SetBrowseModeAsync(BrowseModes mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        Session.Require(FileOrFolderActions.AllowSwitchBrowseMode);
        Session.BrowseMode = mode;
        await Session.SelectPathAsync(SelectedFolder);
    }
    private void ConfirmSelection()
    {
        if (!IsBusy && initialized && Session.SelectedNode is not null)
            SelectionConfirmed?.Invoke(this, EventArgs.Empty);
    }
    private async Task UiAsync(Func<Task> action, DmsBrowserFailurePhase? phase = null, [System.Runtime.CompilerServices.CallerMemberName] string operation = "")
    {
        if (interactionRunning || SessionBusy || dragRunning) return;
        interactionRunning = true; UpdateControls();
        try { await action(); }
        catch (OperationCanceledException) { if (!initialized) pendingCancellation = true; }
        catch (Data.DirectoryNotFoundException ex) { session?.RemoveMissing(ex.RemotePath); await ReportFailureAsync(ex, phase ?? DmsBrowserFailurePhase.Action, operation); if (!initialized) pendingCancellation = true; }
        catch (Data.FileNotFoundException ex) { session?.RemoveMissing(ex.RemotePath); await ReportFailureAsync(ex, phase ?? DmsBrowserFailurePhase.Action, operation); if (!initialized) pendingCancellation = true; }
        catch (Data.RessourceNotFoundException ex) { session?.RemoveMissing(ex.RemotePath); await ReportFailureAsync(ex, phase ?? DmsBrowserFailurePhase.Action, operation); }
        catch (Exception ex)
        {
            await ReportFailureAsync(ex, phase ?? (initialized ? DmsBrowserFailurePhase.Action :
                startupPhase == DmsBrowserFailurePhase.Startup ? session?.FailurePhase ?? startupPhase : startupPhase), operation);
            if (!initialized) pendingCancellation = true;
        }
        finally
        {
            interactionRunning = false; UpdateControls();
            if (pendingConfirmation) { pendingConfirmation = false; ConfirmSelection(); }
            if (pendingCancellation) { pendingCancellation = false; Cancelled?.Invoke(this, EventArgs.Empty); }
        }
    }
    private bool SessionBusy => session?.IsBusy == true;

    private async Task CreateFolderAsync()
    {
        var name = await Dialogs.InputAsync(Owner, UiText.Format("NewFolderPrompt", Session.CurrentPath));
        if (name is not null) await Session.CreateDirectoryAsync(name.Trim());
    }
    private DmsResourceItem[] SelectedFiles() => Selection.Files.ToArray();
    private async Task DeleteFilesAsync()
    {
        var selected = SelectedFiles();
        if (selected.Length == 0) throw new DmsUserErrorMessageException(UiText.Get("NoFileSelected"));
        if (await Dialogs.ConfirmAsync(Owner, UiText.Get("DeleteFilesQuestion") + "\n\n" + string.Join("\n", selected.Select(f => f.Name))))
            await Session.DeleteAsync(selected);
    }
    private async Task DeleteFolderAsync()
    {
        if (!MutableFolder) throw new DmsUserInputInvalidException(UiText.Get("RootFolderCannotBeDeleted"));
        var item = Session.SelectedNode!.Item!;
        if (await Dialogs.ConfirmAsync(Owner, UiText.Get("DeleteFolderQuestion") + "\n\n" + item.Name)) await Session.DeleteAsync(new[] { item });
    }
    private async Task<string?> DestinationAsync()
    {
        var picker = new DmsBrowser(Session.Provider, new DmsBrowserOptions
        {
            FormTitle = UiText.Get("SelectDestinationFolder"), InitialFolder = Options.InitialFolder, SelectedFolder = SelectedFolder,
            BrowseMode = BrowseModes.Folders, AllowedActions = Options.AllowedActions & FileOrFolderActions.AllowCreateFolders
        });
        var selection = await picker.ShowDialog<DmsBrowserSelection?>(Owner);
        return selection is null ? null : selection.Folder?.FullName ?? Session.Provider.BrowseInRootFolderName ?? "";
    }
    private async Task FileActionAsync(ResourceAction action)
    {
        var sources = SelectedFiles();
        if (sources.Length == 0) throw new DmsUserInputInvalidException(UiText.Get("NoFileSelected"));
        if (action == ResourceAction.Rename && sources.Length != 1) throw new DmsUserInputInvalidException(UiText.Get("ExactlyOneItemRequired"));
        var destination = action == ResourceAction.Rename ? null : await DestinationAsync();
        if (action != ResourceAction.Rename && destination is null) return;
        var completed = 0;
        try
        {
            foreach (var source in sources)
            {
                var name = source.Name;
                if (sources.Length == 1)
                {
                    name = await Dialogs.InputAsync(Owner, UiText.Format(action == ResourceAction.Rename ? "RenameItemPrompt" : "DestinationNamePrompt", source.Name), name);
                    if (name is null) return; name = name.Trim(); Session.ValidateName(name);
                }
                var path = Session.Provider.CombinePath(action == ResourceAction.Rename ? Session.Provider.ParentDirectoryPath(source.FullName) : destination, name);
                if (!await Session.ResourceActionAsync(source, path, action, question => Dialogs.ConfirmAsync(Owner, question)))
                {
                    if (completed > 0) await Dialogs.MessageAsync(Owner, UiText.Format("ActionStoppedAfter", completed, sources.Length));
                    return;
                }
                completed++;
            }
        }
        catch (Exception ex) when (completed > 0) { throw BrowserSession.PartialFailure(completed, sources.Length, ex); }
    }
    private async Task FolderActionAsync(ResourceAction action)
    {
        if (!MutableFolder) throw new DmsUserInputInvalidException(UiText.Get("RootFolderCannotBeChanged"));
        var source = Session.SelectedNode!.Item!;
        var destination = action == ResourceAction.Rename ? Session.Provider.ParentDirectoryPath(source.FullName) : await DestinationAsync();
        if (destination is null) return;
        var name = await Dialogs.InputAsync(Owner, UiText.Format(action == ResourceAction.Rename ? "RenameItemPrompt" : "DestinationNamePrompt", source.Name), source.Name);
        if (name is null) return;
        Session.ValidateName(name.Trim());
        var path = Session.Provider.CombinePath(destination, name.Trim());
        if (await Session.ResourceActionAsync(source, path, action, question => Dialogs.ConfirmAsync(Owner, question)))
            await Session.SelectPathAsync(Session.RelativePath(path));
    }
    private async Task<IStorageFolder?> DefaultLocalFolderAsync(bool upload)
    {
        var path = (upload ? Options.LocalDefaultFolderUploads : Options.LocalDefaultFolderDownloads) ?? Options.LocalParentMustFolder;
        if (string.IsNullOrEmpty(path)) return null;
        LocalPaths.Validate(path, Options.LocalParentMustFolder);
        Directory.CreateDirectory(path);
        return await Owner.StorageProvider.TryGetFolderFromPathAsync(new Uri(Path.GetFullPath(path)));
    }
    private async Task UploadFilesAsync()
    {
        Session.Require(FileOrFolderActions.AllowUploadFiles);
        if (!Owner.StorageProvider.CanOpen) throw new NotSupportedException("The host does not support local file selection.");
        var selected = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        { Title = UiText.Get("UploadTitle"), AllowMultiple = true, SuggestedStartLocation = await DefaultLocalFolderAsync(true) });
        var paths = selected.Select(f => f.TryGetLocalPath() ?? throw new NotSupportedException("A local filesystem path is required.")).ToArray();
        if (paths.Length > 0)
        {
            await UploadPathsAsync(paths, Session.SelectedNode!);
        }
    }
    private async Task DownloadFilesAsync()
    {
        Session.Require(FileOrFolderActions.AllowDownloadFiles);
        var selected = SelectedFiles();
        if (selected.Length == 0) throw new DmsUserErrorMessageException(UiText.Get("NoFilesSelected"));
        var start = await DefaultLocalFolderAsync(false);
        List<(DmsResourceItem Item, string Path)> downloads = new();
        if (selected.Length == 1)
        {
            if (!Owner.StorageProvider.CanSave) throw new NotSupportedException("The host does not support saving local files.");
            var file = await Owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            { Title = UiText.Get("DownloadTitle"), SuggestedFileName = selected[0].Name, SuggestedStartLocation = start, ShowOverwritePrompt = false });
            if (file is null) return;
            downloads.Add((selected[0], file.TryGetLocalPath() ?? throw new NotSupportedException("A local filesystem path is required.")));
        }
        else
        {
            if (!Owner.StorageProvider.CanPickFolder) throw new NotSupportedException("The host does not support local folder selection.");
            var folders = await Owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            { Title = UiText.Get("DownloadTitle"), SuggestedStartLocation = start });
            if (folders.Count == 0) return;
            var directory = folders[0].TryGetLocalPath() ?? throw new NotSupportedException("A local filesystem path is required.");
            foreach (var item in selected) downloads.Add((item, LocalPaths.SafeChild(directory, item.Name)));
        }
        await TransferDownloadsAsync(downloads);
    }
    private async Task OpenFilesAsync()
    {
        Session.Require(FileOrFolderActions.AllowDownloadFiles);
        if (SelectedFiles().Length == 0) throw new DmsUserErrorMessageException(UiText.Get("NoFilesSelected"));
        await Session.RunAsync(async () =>
        {
            foreach (var file in SelectedFiles())
            {
                var directory = Path.Combine(Path.GetTempPath(), "CompuMaster.Dms.Avalonia", Guid.NewGuid().ToString("N"));
                if (Directory.Exists(directory)) throw new IOException("The preview directory already exists.");
                Directory.CreateDirectory(directory); temporaryDirectories.Add(directory);
                var path = LocalPaths.SafeChild(directory, file.Name);
                await Session.Provider.DownloadFileAsync(file, path);
                File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
                await OpenDownloadedFileItem(path);
            }
        });
    }
    /// <summary>Removes temporary preview files owned by this browser.</summary>
    /// <exception cref="IOException">One or more preview files cannot be removed; their paths remain tracked for retry.</exception>
    public void CleanupTemporaryFiles()
    {
        List<Exception> failures = new();
        foreach (var directory in temporaryDirectories.ToArray())
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    foreach (var file in Directory.EnumerateFiles(directory)) { File.SetAttributes(file, FileAttributes.Normal); File.Delete(file); }
                    Directory.Delete(directory, false);
                }
                temporaryDirectories.Remove(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failures.Add(ex); }
        }
        if (failures.Count > 0) throw new IOException("Some preview files could not be removed.", new AggregateException(failures));
    }
    internal void ReportCleanupFailure(Exception ex) { cleanupFailures.Add(ex); ReportFailure(ex, DmsBrowserFailurePhase.Action, nameof(CleanupTemporaryFiles)); }
    private async Task SharingAsync(bool selectedFiles)
    {
        Session.Require(FileOrFolderActions.AllowSharings);
        if (!Session.Provider.SupportsSharingSetup) throw new NotSupportedException("The provider does not support sharing.");
        var items = selectedFiles ? SelectedFiles() : new[] { Session.SelectedNode?.Item ?? throw new DmsUserInputInvalidException(UiText.Get("RootSharingUnsupported")) };
        if (items.Length == 0) throw new DmsUserErrorMessageException(UiText.Get("NoFileSelected"));
        foreach (var item in items)
        {
            if (Session.CollectionManagementRestricted(item)) throw new DmsUserInputInvalidException(UiText.Get("CollectionManagementUnavailableInScopedBrowser"));
            DmsResourceItem details = item;
            await Session.RunAsync(async () => details = await ResourceDetails.LoadAsync(Session.Provider, item));
            var dialog = new DmsItemSharings(Session.Provider, details);
            ActiveSharingDialog = dialog; UpdateControls();
            try { await dialog.ShowDialog(Owner); }
            finally { ActiveSharingDialog = null; UpdateControls(); }
            if (dialog.HasChanges) await Session.RefreshAsync();
        }
    }
    private async Task PropertiesAsync(bool selectedFile)
    {
        if (selectedFile && SelectedFiles().Length != 1) throw new DmsUserInputInvalidException(UiText.Get("ExactlyOneItemRequired"));
        var item = selectedFile ? SelectedFiles().Single() : Session.SelectedNode?.Item;
        if (item is null)
        {
            var instance = (DmsProvider as IDmsInstanceProvider)?.CurrentDmsInstance;
            await Dialogs.MessageAsync(Owner, UiText.Get("RootValue") + (instance is null ? "" : "\n\n" +
                UiText.Get("PropertiesInstanceName") + instance.DisplayName + "\n" + UiText.Get("PropertiesInstanceId") + instance.ID));
            return;
        }
        string details = "";
        await Session.RunAsync(async () => details = await ItemProperties.FormatAsync(Session.Provider,
            await ResourceDetails.LoadAsync(Session.Provider, item)));
        await Dialogs.MessageAsync(Owner, details, UiText.Format("PropertiesTitle", item.Name));
    }
    private async Task ChangeInstanceAsync()
    {
        Session.Require(FileOrFolderActions.AllowSwitchDmsInstance);
        if (Session.Provider is not IDmsInstanceProvider instances || Options.RemoteInstance.SelectionDialog is null) return;
        var available = await Task.Run(instances.ListAvailableDmsInstances);
        if (available.Count <= 1) return;
        var id = await Options.RemoteInstance.SelectionDialog(Owner, available);
        if (id is null) return;
        RemoteInstanceSelector.Validate(available, id);
        if (instances.CurrentDmsInstance?.ID == id) return;
        try
        {
            await Session.RunAsync(() => Task.Run(() => instances.SelectDmsInstance(id)));
            Options.SelectedFolder = null;
            await Session.InitializeAsync();
        }
        catch { initialized = false; Session.Roots.Clear(); Session.Files.Clear(); throw; }
    }
}

internal static class BrowserIcons
{
    private static readonly Dictionary<string, IImage> Cache = new();
    private static IImage? sharedFile;
    internal static IImage Load(string name)
    {
        if (!Cache.TryGetValue(name, out var image))
        {
            image = new Bitmap(AssetLoader.Open(new Uri("avares://CompuMaster.Dms.BrowserUI.Avalonia/Assets/" + name + ".png")));
            Cache.Add(name, image);
        }
        return image;
    }
    internal static IImage Directory(BrowserNode node)
    {
        var name = node.Parent is null ? "iconfinder_Home-ui-ux-mobile-web_4960719" :
            node.Item?.ItemType == DmsResourceItem.ItemTypes.Collection ? "iconfinder_bookmark-ui-ux-mobile-web_4960727" : "iconfinder_Folder-ui-ux-mobile-web_4960713";
        return Load(name + (node.Shared ? " - Shared" : ""));
    }
    internal static IImage File(bool shared)
    {
        var document = Load("iconfinder_Document-ui-ux-mobile-web-office-microsoftofficeico_4960706");
        return shared ? sharedFile ??= PlatformFileIcons.Decorate(document, true) : document;
    }
}
