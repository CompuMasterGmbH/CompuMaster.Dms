using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;

namespace CompuMaster.Dms.BrowserUI.Avalonia;

internal sealed class LocalUploadPlan
{
    internal List<(string Source, string Relative)> Files { get; } = new();
    internal List<string> Directories { get; } = new();
    internal static LocalUploadPlan Build(IReadOnlyList<string> selection, string? requiredParent, CancellationToken token)
    {
        var plan = new LocalUploadPlan();
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var roots = selection.Select(Path.GetFullPath).Distinct(comparer).ToArray();
        var destinations = new HashSet<string>(comparer);
        void Add(string source, string relative)
        {
            token.ThrowIfCancellationRequested(); LocalPaths.Validate(source, requiredParent);
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
                throw new IOException(UiText.Format("UploadLinkedPathUnsupported", source));
            if (!destinations.Add(relative)) throw new IOException(UiText.Format("UploadSelectionCollision", relative));
            if (Directory.Exists(source))
            {
                plan.Directories.Add(relative);
                foreach (var child in Directory.EnumerateFileSystemEntries(source).OrderBy(p => p, comparer))
                    Add(child, relative + "/" + Path.GetFileName(child));
            }
            else plan.Files.Add((source, relative));
        }
        foreach (var root in roots)
        {
            if (roots.Any(other => !comparer.Equals(root, other) && Directory.Exists(other) &&
                root.StartsWith(other.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))) continue;
            var name = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).TrimEnd(':');
            if (string.IsNullOrEmpty(name)) throw new IOException(UiText.Get("InvalidDestinationName"));
            Add(root, name);
        }
        return plan;
    }
    internal static string Destination(BaseDmsProvider provider, string? remoteParent, string relative)
    {
        var result = remoteParent;
        foreach (var segment in relative.Split('/'))
        {
            if (string.IsNullOrWhiteSpace(segment) || segment is "." or ".." || segment.Contains('\\'))
                throw new ArgumentException(UiText.Get("InvalidDestinationName"));
            result = provider.CombinePath(result, segment);
        }
        return result!;
    }
}
internal sealed class RemoteDownloadPlan
{
    internal List<string> Directories { get; } = new();
    internal List<(DmsResourceItem Item, string Destination)> Files { get; } = new();
    internal static async Task<RemoteDownloadPlan> BuildAsync(BaseDmsProvider provider, DmsResourceItem folder, string destination, CancellationToken token)
    {
        var result = new RemoteDownloadPlan();
        var paths = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var ancestors = new HashSet<string>(StringComparer.Ordinal);
        async Task Add(DmsResourceItem directory, string target, int depth)
        {
            token.ThrowIfCancellationRequested();
            if (depth > 256 || !ancestors.Add(directory.ExtendedInfosFolderID ?? directory.FullName))
                throw new IOException(UiText.Get("DownloadHierarchyInvalid"));
            try
            {
                AtomicDownload.Validate(target, true);
                if (!paths.Add(target)) throw new IOException(UiText.Format("DownloadDestinationCollision", target));
                result.Directories.Add(target);
                var files = await provider.ListFileEntriesAsync(directory.FullName, token);
                foreach (var file in files)
                {
                    if (file.ItemType != DmsResourceItem.ItemTypes.File) throw new IOException(UiText.Get("DownloadHierarchyInvalid"));
                    var path = LocalPaths.SafeChild(target, file.Name); AtomicDownload.Validate(path, false);
                    if (!paths.Add(path)) throw new IOException(UiText.Format("DownloadDestinationCollision", path));
                    result.Files.Add((file, path));
                }
                foreach (var child in await provider.ListDirectoryEntriesAsync(directory.FullName, token))
                {
                    if (child.ItemType is not (DmsResourceItem.ItemTypes.Folder or DmsResourceItem.ItemTypes.Collection))
                        throw new IOException(UiText.Get("DownloadHierarchyInvalid"));
                    await Add(child, LocalPaths.SafeChild(target, child.Name), depth + 1);
                }
            }
            finally { ancestors.Remove(directory.ExtendedInfosFolderID ?? directory.FullName); }
        }
        await Add(folder, LocalPaths.SafeChild(destination, folder.ItemType == DmsResourceItem.ItemTypes.Root ? "DMS" : folder.Name), 0);
        return result;
    }
}
internal static class AtomicDownload
{
    internal static void Validate(string path, bool directory)
    {
        var current = Path.GetFullPath(path);
        var leaf = true;
        while (current is not null)
        {
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget is not null) throw new IOException(UiText.Format("DownloadLinkedPathUnsupported", current));
            if (info.Exists && ((info.Attributes & FileAttributes.Directory) != 0) != (leaf ? directory : true))
                throw new IOException(UiText.Format("DownloadDestinationCollision", current));
            leaf = false; current = Path.GetDirectoryName(current);
        }
    }
    internal static async Task SaveAsync(string path, bool replace, Func<string, Task> download)
    {
        Validate(path, false);
        var temporary = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, ".cm-dms-download-" + Guid.NewGuid().ToString("N") + ".tmp");
        Exception? failure = null;
        try
        {
            using (new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            await download(temporary);
            Validate(path, false);
            if (File.Exists(path))
            {
                if (!replace) throw new IOException(UiText.Format("DownloadDestinationChanged", path));
                File.Move(temporary, path, true);
            }
            else File.Move(temporary, path);
        }
        catch (Exception ex) { failure = ex; throw; }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception cleanup) { if (failure is null) throw; failure.Data["DownloadCleanupFailure"] = cleanup; }
        }
    }
}
internal enum ConflictAction { Cancel, Replace, Skip }
internal sealed record ConflictDecision(ConflictAction Action, bool ApplyToAll);
internal sealed class TransferConflicts
{
    private ConflictAction? all;
    private readonly Dictionary<string, (string? ID, ConflictAction Action)> decisions = new();
    internal async Task<ConflictAction> ResolveAsync(string path, string? identity, Func<string, Task<ConflictDecision>> prompt)
    {
        if (decisions.TryGetValue(path, out var previous))
        {
            if (identity != previous.ID) throw new InvalidOperationException(UiText.Format("UploadDestinationChanged", path));
            return previous.Action;
        }
        var decision = all.HasValue ? new ConflictDecision(all.Value, true) : await prompt(path);
        if (decision.ApplyToAll && decision.Action != ConflictAction.Cancel) all = decision.Action;
        if (decision.Action != ConflictAction.Cancel) decisions.Add(path, (identity, decision.Action));
        return decision.Action;
    }
    internal void ValidateAbsent(string path)
    {
        if (decisions.ContainsKey(path)) throw new InvalidOperationException(UiText.Format("UploadDestinationChanged", path));
    }
}
