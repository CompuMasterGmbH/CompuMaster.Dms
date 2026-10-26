using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;

namespace CompuMaster.Dms.BrowserUI.Avalonia;

internal sealed class BrowserNode : INotifyPropertyChanged
{
    internal BrowserNode(DmsResourceItem? item, BrowserNode? parent = null, string? rootName = null, bool placeholder = false)
    {
        Item = item; Parent = parent; RootName = rootName;
        Placeholder = placeholder;
        if (!placeholder) SetPlaceholder();
    }
    internal DmsResourceItem? Item { get; set; }
    internal BrowserNode? Parent { get; }
    internal string? RootName { get; }
    internal bool Loaded { get; set; }
    internal Task? Loading { get; set; }
    internal bool Placeholder { get; init; }
    public string Name => Item?.Name ?? RootName ?? "/";
    public global::Avalonia.Media.IImage Icon => BrowserIcons.Directory(this);
    public bool Shared => Item is not null && (Item.ExtendedInfosIsShared || Item.ExtendedInfosHasLinks ||
        Item.ExtendedInfosHasUserSharings || Item.ExtendedInfosHasGroupSharings);
    public ObservableCollection<BrowserNode> Children { get; } = new();
    private bool expanded;
    public bool IsExpanded { get => expanded; set { expanded = value; Notify(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    internal void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    internal static bool? HasChildren(DmsResourceItem? item) => item is null ? null :
        item.ItemType == DmsResourceItem.ItemTypes.File ? false :
        item.ChildDirectoryCount.HasValue ? item.ChildDirectoryCount > 0 : item.HasChildDirectories;
    internal void SetPlaceholder()
    {
        if (!Loaded)
        {
            Children.Clear();
            if (HasChildren(Item) != false) Children.Add(new BrowserNode(null, placeholder: true));
        }
    }
}

internal sealed class FileRow
{
    internal FileRow(DmsResourceItem item, bool duplicate) { Item = item; Duplicate = duplicate; }
    internal DmsResourceItem Item { get; }
    public string Name => Item.Name;
    public long ContentLength => Item.ContentLength;
    public string Size => UiText.ByteSize(Item.ContentLength);
    public DateTime? LastModified => Item.LastModificationOnLocalTime;
    public bool Duplicate { get; }
    public bool Shared => Item.ExtendedInfosIsShared || Item.ExtendedInfosHasLinks ||
        Item.ExtendedInfosHasUserSharings || Item.ExtendedInfosHasGroupSharings;
}

internal enum ResourceAction { Copy, Rename, Move }

internal sealed partial class BrowserSession
{
    internal BrowserSession(BaseDmsProvider provider, DmsBrowserOptions options)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        if (!Enum.IsDefined(options.BrowseMode) || !Enum.IsDefined(options.DialogOperationMode))
            throw new ArgumentOutOfRangeException(nameof(options));
        foreach (var local in new[] { options.LocalDefaultFolderDownloads, options.LocalDefaultFolderUploads })
            if (!string.IsNullOrEmpty(local)) LocalPaths.Validate(local, options.LocalParentMustFolder);
        BrowseMode = options.BrowseMode;
    }
    internal BaseDmsProvider Provider { get; }
    internal DmsBrowserOptions Options { get; }
    internal ObservableCollection<BrowserNode> Roots { get; } = new();
    internal ObservableCollection<FileRow> Files { get; } = new();
    internal BrowserNode? SelectedNode { get; private set; }
    internal BrowseModes BrowseMode { get; set; }
    internal bool IsBusy { get; private set; }
    internal DmsBrowserFailurePhase FailurePhase { get; private set; } = DmsBrowserFailurePhase.Startup;
    internal event Action? Changed;
    internal string? CurrentPath => SelectedNode?.Item?.FullName ?? Options.InitialFolder ?? Provider.BrowseInRootFolderName;
    internal string? SelectedFolder => RelativePath(CurrentPath);
    internal void Require(FileOrFolderActions action)
    {
        if ((Options.AllowedActions & action) != action) throw new InvalidOperationException("This operation is not permitted by the host.");
    }
    internal async Task RunAsync(Func<Task> operation)
    {
        if (IsBusy) throw new InvalidOperationException("A browser operation is already running.");
        IsBusy = true; Changed?.Invoke();
        try { await operation(); }
        finally { IsBusy = false; Changed?.Invoke(); }
    }
    internal Task InitializeAsync(CancellationToken token = default) => RunAsync(async () =>
    {
        await LoadRootAsync(token);
        await SelectPathCoreAsync(Options.SelectedFolder, token);
    });
    private async Task LoadRootAsync(CancellationToken token)
    {
        DmsResourceItem? item = null;
        if (!string.IsNullOrEmpty(Options.InitialFolder) && Options.InitialFolder.Trim(Provider.DirectorySeparator) !=
            (Provider.BrowseInRootFolderName ?? "").Trim(Provider.DirectorySeparator))
        {
            FailurePhase = DmsBrowserFailurePhase.ScopedRootLookup;
            item = await Provider.ListRemoteItemAsync(Options.InitialFolder, token)
                ?? throw new Data.DirectoryNotFoundException(Options.InitialFolder);
            if (item.ItemType == DmsResourceItem.ItemTypes.File) throw new ArgumentException("The browser root must be a directory.");
        }
        var root = new BrowserNode(item, rootName: Options.InitialFolder ?? Provider.BrowseInRootFolderName ?? "/");
        Roots.Clear(); Roots.Add(root); SelectedNode = root; Files.Clear();
        await LoadChildrenCoreAsync(root, token);
        root.IsExpanded = true;
    }
    internal Task ExpandAsync(BrowserNode node, CancellationToken token = default) => RunAsync(() => LoadChildrenCoreAsync(node, token));
    private Task LoadChildrenCoreAsync(BrowserNode node, CancellationToken token)
    {
        if (node.Loaded) return Task.CompletedTask;
        if (BrowserNode.HasChildren(node.Item) == false) { ApplyChildren(node, new()); return Task.CompletedTask; }
        return node.Loading ??= LoadChildrenRequestAsync(node, token);
    }
    private async Task LoadChildrenRequestAsync(BrowserNode node, CancellationToken token)
    {
        try
        {
            FailurePhase = DmsBrowserFailurePhase.ChildListing;
            var children = await Provider.ListDirectoryEntriesAsync(node.Item?.FullName ?? Provider.BrowseInRootFolderName, token);
            ApplyChildren(node, children);
        }
        finally { node.Loading = null; }
    }
    private void ApplyChildren(BrowserNode node, List<DmsResourceItem> items)
    {
        var old = node.Children.Where(n => !n.Placeholder).ToList();
        node.Children.Clear();
        foreach (var item in items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            if (item.ItemType == DmsResourceItem.ItemTypes.Collection &&
                (!Provider.SupportsCollections || node.Item is not null && node.Item.ItemType != DmsResourceItem.ItemTypes.Collection)) continue;
            var child = old.FirstOrDefault(n => SameItem(n.Item, item));
            if (child is null) child = new BrowserNode(item, node);
            else { child.Item = item; child.SetPlaceholder(); child.Notify(); }
            node.Children.Add(child);
        }
        node.Loaded = true;
        if (node.Item is not null)
        {
            node.Item.ChildDirectoryCount = node.Children.Count;
            node.Item.HasChildDirectories = node.Children.Count > 0;
        }
        node.Notify();
    }
    internal static bool SameItem(DmsResourceItem? a, DmsResourceItem b) => a?.FullName == b.FullName && a.ItemType == b.ItemType;
    internal Task SelectAsync(BrowserNode node, CancellationToken token = default) => RunAsync(async () =>
    {
        if (node.Placeholder || !AllNodes().Contains(node)) throw new ArgumentException("Select a current directory.");
        SelectedNode = node;
        await RefreshFilesCoreAsync(token);
    });
    internal Task SelectPathAsync(string? relativePath, CancellationToken token = default) =>
        RunAsync(() => SelectPathCoreAsync(relativePath, token));
    private async Task SelectPathCoreAsync(string? relativePath, CancellationToken token)
    {
        FailurePhase = DmsBrowserFailurePhase.InitialSelection;
        var node = Roots.FirstOrDefault();
        if (node is null) return;
        foreach (var segment in (relativePath ?? "").Split(Provider.DirectorySeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            ValidateName(segment);
            await LoadChildrenCoreAsync(node, token);
            var child = node.Children.FirstOrDefault(n => !n.Placeholder && n.Name == segment);
            if (child is null) break; // Preserve the WinForms nearest-existing-ancestor behavior.
            node.IsExpanded = true;
            node = child;
        }
        SelectedNode = node;
        await RefreshFilesCoreAsync(token);
    }
    internal string? RelativePath(string? fullPath)
    {
        var separator = Provider.DirectorySeparator;
        if ((fullPath ?? "").Split(separator).Any(p => p is "." or ".." || p.Contains('\0')))
            throw new DmsUserInputInvalidException(UiText.Get("DestinationOutsideBrowserRoot"));
        var root = (Options.InitialFolder ?? Provider.BrowseInRootFolderName ?? "").TrimEnd(separator);
        var path = (fullPath ?? "").TrimEnd(separator);
        if (path == root) return null;
        if (root.Length == 0) return path.TrimStart(separator);
        if (!path.StartsWith(root + separator, StringComparison.Ordinal))
            throw new InvalidOperationException(UiText.Get("DestinationOutsideBrowserRoot"));
        return path[(root.Length + 1)..];
    }
    private async Task RefreshFilesCoreAsync(CancellationToken token)
    {
        Files.Clear();
        if (SelectedNode is null || BrowseMode == BrowseModes.Folders ||
            SelectedNode.Item is null && !Provider.SupportsFilesInRootFolder) return;
        await Provider.ResetCachesForRemoteItemsAsync(CurrentPath, BaseDmsProvider.SearchItemType.Files, token);
        List<DmsResourceItem> files;
        try { files = await Provider.ListFileEntriesAsync(CurrentPath, token); }
        catch (Data.DirectoryNotFoundException) { RemoveMissing(CurrentPath); throw; }
        var duplicates = files.GroupBy(f => f.Name, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        foreach (var file in files.OrderByDescending(f => f.LastModificationOnLocalTime))
            Files.Add(new FileRow(file, duplicates.Contains(file.Name)));
    }
    internal Task RefreshAsync(CancellationToken token = default) => RunAsync(() => RefreshCoreAsync(token));
    internal async Task RefreshCoreAsync(CancellationToken token)
    {
        FailurePhase = DmsBrowserFailurePhase.Refresh;
        var oldSelection = SelectedNode;
        if (Roots.Count == 0) { await LoadRootAsync(token); }
        else await RefreshLoadedAsync(Roots[0], token);
        while (oldSelection is not null && !AllNodes().Contains(oldSelection)) oldSelection = oldSelection.Parent;
        SelectedNode = oldSelection ?? Roots.FirstOrDefault();
        await RefreshFilesCoreAsync(token);
    }
    private async Task RefreshLoadedAsync(BrowserNode node, CancellationToken token)
    {
        if (!node.Loaded && node != SelectedNode) return;
        var path = node.Item?.FullName ?? Provider.BrowseInRootFolderName;
        await Provider.ResetCachesForRemoteItemsAsync(path, BaseDmsProvider.SearchItemType.AllItems, token);
        List<DmsResourceItem> children;
        try { children = await Provider.ListDirectoryEntriesAsync(path, token); }
        catch (Data.DirectoryNotFoundException) { RemoveMissing(path); throw; }
        ApplyChildren(node, children);
        foreach (var child in node.Children.ToArray()) await RefreshLoadedAsync(child, token);
    }
    internal IEnumerable<BrowserNode> AllNodes() => Roots.SelectMany(Descendants);
    private static IEnumerable<BrowserNode> Descendants(BrowserNode node) =>
        new[] { node }.Concat(node.Children.Where(n => !n.Placeholder).SelectMany(Descendants));
    internal void RemoveMissing(string? path)
    {
        var node = AllNodes().FirstOrDefault(n => (n.Item?.FullName ?? Provider.BrowseInRootFolderName) == path);
        if (node is not null)
        {
            if (node.Parent is null) Roots.Clear(); else node.Parent.Children.Remove(node);
            SelectedNode = node.Parent; Files.Clear();
            if (node.Parent?.Item is { } parent && node.Parent.Loaded)
            { parent.ChildDirectoryCount = node.Parent.Children.Count; parent.HasChildDirectories = parent.ChildDirectoryCount > 0; }
        }
        foreach (var row in Files.Where(f => f.Item.FullName == path).ToArray()) Files.Remove(row);
        Changed?.Invoke();
    }
    internal void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." ||
            name.IndexOfAny(new[] { '/', '\\', Provider.DirectorySeparator, '\0' }) >= 0)
            throw new DmsUserInputInvalidException(UiText.Get("InvalidDestinationName"));
    }
    internal Task CreateDirectoryAsync(string name, CancellationToken token = default) => RunAsync(async () =>
    {
        Require(FileOrFolderActions.AllowCreateFolders); ValidateName(name);
        if (SelectedNode is null) throw new InvalidOperationException(UiText.Get("NoFolderSelected"));
        var parent = SelectedNode;
        var path = Provider.CombinePath(CurrentPath, name);
        await MutateAndRefreshAsync(() => Provider.CreateDirectoryAsync(path, token), token);
        var child = parent.Children.FirstOrDefault(n => n.Item?.FullName == path);
        if (child is not null) { SelectedNode = child; await RefreshFilesCoreAsync(token); }
    });
    internal Task DeleteAsync(IReadOnlyList<DmsResourceItem> items, CancellationToken token = default) => RunAsync(async () =>
    {
        Require(FileOrFolderActions.AllowDeleteFiles);
        foreach (var item in items) ValidateSource(item, protectRoot: true);
        await MutateAndRefreshAsync(async () =>
        {
            var completed = 0;
            try { foreach (var item in items) { await Provider.DeleteRemoteItemAsync(item, token); completed++; } }
            catch (Exception ex) when (completed > 0) { throw PartialFailure(completed, items.Count, ex); }
        }, token);
    });
    internal Task<bool> ResourceActionAsync(DmsResourceItem source, string destination, ResourceAction action,
        Func<string, Task<bool>> confirmOverwrite, CancellationToken token = default)
    {
        return RunWithResultAsync(async () =>
        {
            Require(FileOrFolderActions.AllowCopyRenameMoveFiles);
            ValidateSource(source, protectRoot: true);
            RelativePath(destination);
            ValidateName(Provider.ItemName(destination));
            if (source.FullName.TrimEnd(Provider.DirectorySeparator) == destination.TrimEnd(Provider.DirectorySeparator))
                throw new DmsUserInputInvalidException(UiText.Get("SourceEqualsDestination"));
            var existing = await Provider.ListRemoteItemAsync(destination, token);
            var overwrite = existing is not null;
            if (overwrite && !await confirmOverwrite(UiText.Format(source.ItemType == DmsResourceItem.ItemTypes.File ?
                "OverwriteFileQuestion" : "MergeFolderQuestion", destination))) return false;
            await MutateAndRefreshAsync(() => action == ResourceAction.Copy ?
                Provider.CopyAsync(source, destination, overwrite, false, token) :
                Provider.MoveAsync(source, destination, overwrite, false, token), token);
            return true;
        });
    }
    private async Task<T> RunWithResultAsync<T>(Func<Task<T>> operation)
    { T result = default!; await RunAsync(async () => result = await operation()); return result; }
    internal void ValidateSource(DmsResourceItem item, bool protectRoot = false)
    {
        RelativePath(item.FullName);
        if (protectRoot && (item.ItemType == DmsResourceItem.ItemTypes.Root || RelativePath(item.FullName) is null))
            throw new DmsUserInputInvalidException(UiText.Get("RootFolderCannotBeChanged"));
        if (protectRoot && CollectionManagementRestricted(item))
            throw new DmsUserInputInvalidException(UiText.Get("CollectionManagementUnavailableInScopedBrowser"));
    }
    internal bool CollectionManagementRestricted(DmsResourceItem? item) =>
        Provider is CenterDeviceDmsProviderBase && item?.ItemType == DmsResourceItem.ItemTypes.Collection &&
        !string.IsNullOrEmpty(Options.InitialFolder) && Options.InitialFolder.Trim(Provider.DirectorySeparator) !=
        (Provider.BrowseInRootFolderName ?? "").Trim(Provider.DirectorySeparator);
    internal async Task MutateAndRefreshAsync(Func<Task> operation, CancellationToken token)
    {
        Exception? failure = null;
        try { await operation(); } catch (Exception ex) { failure = ex; }
        // Refresh also after partial failure/cancellation; preserve the original exception.
        try { await RefreshCoreAsync(failure is null ? token : CancellationToken.None); }
        catch (Exception ex) { if (failure is null) failure = ex; else failure.Data["RefreshFailure"] = ex; }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    internal static Exception PartialFailure(int count, int total, Exception ex) =>
        new InvalidOperationException(UiText.Format("ActionPartlyCompleted", count, total, ex.Message), ex);
    internal Task UploadAsync(IReadOnlyList<string> paths, CancellationToken token = default) => RunAsync(async () =>
    {
        Require(FileOrFolderActions.AllowUploadFiles);
        if (SelectedNode is null || SelectedNode.Item is null && !Provider.SupportsFilesInRootFolder)
            throw new InvalidOperationException(UiText.Get("NoFolderSelected"));
        foreach (var path in paths)
        {
            LocalPaths.Validate(path, Options.LocalParentMustFolder);
            ValidateName(Path.GetFileName(path));
            if (!File.Exists(path)) throw new System.IO.FileNotFoundException(path);
        }
        await MutateAndRefreshAsync(async () =>
        {
            var count = 0;
            try { foreach (var path in paths) { await Provider.UploadFileAsync(Provider.CombinePath(CurrentPath, Path.GetFileName(path)), path, token); count++; } }
            catch (Exception ex) when (count > 0) { throw PartialFailure(count, paths.Count, ex); }
        }, token);
    });
    internal Task DownloadAsync(IReadOnlyList<(DmsResourceItem Item, string Path)> downloads,
        CancellationToken token = default) => RunAsync(async () =>
    {
        Require(FileOrFolderActions.AllowDownloadFiles);
        foreach (var download in downloads)
        {
            ValidateSource(download.Item);
            LocalPaths.Validate(download.Path, Options.LocalParentMustFolder);
        }
        var comparison = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (downloads.Select(d => Path.GetFullPath(d.Path)).Distinct(comparison).Count() != downloads.Count)
            throw new DmsUserInputInvalidException(UiText.Get("DuplicateFileNameConflict"));
        var count = 0;
        try
        {
            foreach (var download in downloads)
            { await Provider.DownloadFileAsync(download.Item, download.Path, token); count++; }
        }
        catch (Exception ex) when (count > 0) { throw PartialFailure(count, downloads.Count, ex); }
    });
}

internal static class LocalPaths
{
    internal static void Validate(string path, string? root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (string.IsNullOrEmpty(root)) return;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var actualRoot = Resolve(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var actualPath = Resolve(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!actualPath.Equals(actualRoot, comparison) && !actualPath.StartsWith(actualRoot + Path.DirectorySeparatorChar, comparison))
            throw new DmsUserInputInvalidException(UiText.Format("OutsideRequiredFolder", root));
    }
    private static string Resolve(string path)
    {
        var full = Path.GetFullPath(path);
        var prefix = Path.GetPathRoot(full)!;
        foreach (var part in full[prefix.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            prefix = Path.Combine(prefix, part);
            FileSystemInfo info = Directory.Exists(prefix) ? new DirectoryInfo(prefix) : new FileInfo(prefix);
            if (info.LinkTarget is not null) prefix = info.ResolveLinkTarget(true)?.FullName
                ?? throw new IOException("Cannot resolve local symbolic link: " + prefix);
        }
        return prefix;
    }
    internal static string SafeChild(string directory, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name || name is "." or ".." ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains('/') || name.Contains('\\') ||
            name.EndsWith('.') || name.EndsWith(' ') ||
            System.Text.RegularExpressions.Regex.IsMatch(name, "^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\\.|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            throw new DmsUserInputInvalidException(UiText.Get("InvalidDestinationName"));
        var path = Path.Combine(directory, name);
        Validate(path, directory);
        return path;
    }
}
