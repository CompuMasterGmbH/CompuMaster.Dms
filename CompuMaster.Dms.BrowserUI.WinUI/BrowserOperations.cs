using System.Diagnostics;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
namespace CompuMaster.Dms.BrowserUI.WinUI;
public sealed partial class DmsBrowserControl
{
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
        var selection = await picker.ShowDialogAsync(Owner);
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
    private async Task OpenFilesAsync()
    {
        Session.Require(FileOrFolderActions.AllowDownloadFiles);
        if (SelectedFiles().Length == 0) throw new DmsUserErrorMessageException(UiText.Get("NoFilesSelected"));
        await Session.RunAsync(async () =>
        {
            foreach (var file in SelectedFiles())
            {
                var directory = Path.Combine(Path.GetTempPath(), "CompuMaster.Dms.WinUI", Guid.NewGuid().ToString("N"));
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
            await dialog.ShowDialog(Owner);
            await Session.RefreshAsync();
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
