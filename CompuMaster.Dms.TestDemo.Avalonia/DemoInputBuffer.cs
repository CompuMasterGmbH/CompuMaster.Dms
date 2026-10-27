using System.Security.Cryptography;
using System.Text;

#if DMS_WINUI
namespace CompuMaster.Dms.TestDemo.WinUI;
#else
namespace CompuMaster.Dms.TestDemo.Avalonia;
#endif

// Uses the exact WinForms demo cache keys and environment-variable precedence.
internal sealed class DemoInputBuffer
{
    private readonly string directory;
    private readonly string title;
    private readonly string prefix;
    private readonly Func<string, string?> environment;
    internal DemoInputBuffer(DemoTarget target, string? directory = null, Func<string, string?>? environment = null)
    {
        this.directory = directory ?? Path.GetTempPath();
        this.environment = environment ?? Environment.GetEnvironmentVariable;
        (title, prefix) = target switch
        {
            DemoTarget.WebDav => ("WebDav.Test", "WEBDAV"),
            DemoTarget.Nextcloud => ("NextcloudWebDav.Test", "CMNEXTCLOUD"),
            DemoTarget.OwnCloudClassic => ("OwnCloudWebDav.Test", "CMOWNCLOUD"),
            DemoTarget.ScopevisioTeamwork => ("Scopevisio.Teamwork.Test", "SCOPEVISIOTEAMWORK"),
            _ => throw new ArgumentOutOfRangeException(nameof(target))
        };
    }
    internal string FilePath(string field) => Path.Combine(directory,
        "~Buffer." + title + "." + Convert.ToHexString(MD5.HashData(Encoding.ASCII.GetBytes(field))) + ".tmp");
    internal string? Read(string field)
    {
        var value = environment(EnvironmentVariableName(field));
        if (!string.IsNullOrWhiteSpace(value)) return value;
        return Exists(field) ? File.ReadAllText(FilePath(field)) : null;
    }
    internal string EnvironmentVariableName(string field) => "TEST_" + prefix + "_" + field.Replace(" ", "").Replace(".", "").ToUpperInvariant();
    internal bool Exists(string field) => File.Exists(FilePath(field));
    internal void Persist(string field, string value) => File.WriteAllText(FilePath(field), value);
    internal void Remove(string field) => File.Delete(FilePath(field));
    internal IEnumerable<string> Fields => prefix == "SCOPEVISIOTEAMWORK"
        ? new[] { "username", "password", "customer no.", "start path", "organisation id", "expected teamwork tenant id" }
        : new[] { "username", "password", "server url", "start path" };
    internal bool HasPersistedCredentials => Fields.Where(field => field != "start path").Any(Exists);
}
