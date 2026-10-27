namespace CompuMaster.Dms.BrowserUI.Avalonia;

// Freedesktop icon-theme lookup, including inherited themes, user overrides,
// scale-aware sizes, scalable SVG directories and the mandatory hicolor fallback.
internal sealed class FreedesktopIconTheme
{
    private readonly string[] roots;
    private readonly string theme;
    private readonly string[] unthemed;
    internal FreedesktopIconTheme(IEnumerable<string> roots, string theme, IEnumerable<string>? unthemed = null)
    { this.roots = roots.ToArray(); this.theme = theme; this.unthemed = unthemed?.ToArray() ?? Array.Empty<string>(); }
    internal static Dictionary<string, Dictionary<string, string>> Parse(string path)
    {
        var groups = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        Dictionary<string, string>? group = null;
        if (!File.Exists(path)) return groups;
        foreach (var original in File.ReadLines(path))
        {
            var line = original.Trim();
            if (line.Length == 0 || line[0] is '#' or ';') continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            { group = new(StringComparer.Ordinal); groups[line[1..^1]] = group; continue; }
            var equal = line.IndexOf('=');
            if (group is not null && equal > 0) group[line[..equal].Trim()] = line[(equal + 1)..].Trim().Trim('"', '\'');
        }
        return groups;
    }
    internal static string? Setting(string path, string group, string key) =>
        Parse(path).GetValueOrDefault(group)?.GetValueOrDefault(key);
    private IEnumerable<string> Themes(string name, HashSet<string> seen)
    {
        if (seen.Count >= 24 || !SafeName(name) || !seen.Add(name)) yield break;
        yield return name;
        var index = roots.Select(root => Path.Combine(root, name, "index.theme")).FirstOrDefault(File.Exists);
        if (index is null) yield break;
        var inherited = Setting(index, "Icon Theme", "Inherits") ?? "";
        foreach (var parent in inherited.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            foreach (var child in Themes(parent, seen)) yield return child;
    }
    internal string? Find(IEnumerable<string> names, int pixels)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var themes = Themes(theme, seen).Concat(Themes("Adwaita", seen)).Concat(Themes("hicolor", seen))
            .Where(name => name != "hicolor").Append("hicolor").Distinct(StringComparer.Ordinal).ToArray();
        foreach (var name in names.Distinct(StringComparer.Ordinal).Where(SafeName))
        {
            foreach (var current in themes)
            {
                var index = roots.Select(root => Path.Combine(root, current, "index.theme")).FirstOrDefault(File.Exists);
                if (index is null) continue;
                var groups = Parse(index);
                if (!groups.TryGetValue("Icon Theme", out var settings)) continue;
                var directories = ((settings.GetValueOrDefault("Directories") ?? "") + "," + settings.GetValueOrDefault("ScaledDirectories"))
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal)
                    .Where(SafeDirectory).Select(directory => (Name: directory, Distance: Distance(groups.GetValueOrDefault(directory), pixels)))
                    .OrderBy(directory => directory.Distance).ToArray();
                foreach (var directory in directories)
                    foreach (var root in roots)
                        foreach (var extension in new[] { ".png", ".svg" })
                        {
                            var path = Path.Combine(root, current, directory.Name, name + extension);
                            if (File.Exists(path)) return path;
                        }
            }
            foreach (var directory in unthemed)
                foreach (var extension in new[] { ".png", ".svg" })
                {
                    var path = Path.Combine(directory, name + extension);
                    if (File.Exists(path)) return path;
                }
        }
        return null;
    }
    private static bool SafeName(string name) => !string.IsNullOrWhiteSpace(name) && name is not ("." or "..") &&
        !name.Any(character => char.IsControl(character) || character is '/' or '\\' or ':');
    private static bool SafeDirectory(string path) => !Path.IsPathRooted(path) && path.Split('/', '\\').All(SafeName);
    private static int Distance(Dictionary<string, string>? settings, int pixels)
    {
        if (settings is null) return int.MaxValue;
        int Number(string key, int fallback) => int.TryParse(settings.GetValueOrDefault(key), out var value) && value >= 0 && value <= 8192 ? value : fallback;
        var size = Number("Size", 32); var scale = Math.Max(1, Number("Scale", 1));
        var type = settings.GetValueOrDefault("Type") ?? "Threshold";
        var min = type == "Scalable" ? Number("MinSize", size) : type == "Fixed" ? size : Math.Max(0, size - Number("Threshold", 2));
        var max = type == "Scalable" ? Number("MaxSize", size) : type == "Fixed" ? size : size + Number("Threshold", 2);
        return pixels < min * scale ? min * scale - pixels : pixels > max * scale ? pixels - max * scale : 0;
    }
}
