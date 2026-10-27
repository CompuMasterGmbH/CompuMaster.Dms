#if DMS_WINUI
using CompuMaster.Dms.BrowserUI.WinUI;
#else
using CompuMaster.Dms.BrowserUI.Avalonia;
#endif
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;

#if DMS_WINUI
namespace CompuMaster.Dms.TestDemo.WinUI;
#else
namespace CompuMaster.Dms.TestDemo.Avalonia;
#endif

internal enum DemoTarget { Offline, WebDav, Nextcloud, OwnCloudClassic, ScopevisioTeamwork }

internal sealed record DemoDefinition(DemoTarget Target)
{
#if DMS_WINUI
    private const string FrameworkName = "WinUI";
#else
    private const string FrameworkName = "Avalonia";
#endif
    internal string Name => Target switch
    {
        DemoTarget.WebDav => "WebDAV", DemoTarget.Nextcloud => "Nextcloud", DemoTarget.OwnCloudClassic => "ownCloud Classic",
        DemoTarget.ScopevisioTeamwork => "Scopevisio Teamwork", _ => FrameworkName
    };
    internal string BrowserTitle => Target == DemoTarget.Offline ? "DMS Browser DEMO for " + FrameworkName : "DMS Browser DEMO for " + Name + " (" + FrameworkName + ")";
    internal string LoginTitle => Name + " — " + DemoText.Extra(FrameworkName + " DMS sign-in", FrameworkName + "-DMS-Anmeldung");
    internal bool RequiresCustomer => Target == DemoTarget.ScopevisioTeamwork;
    internal bool RequiresServer => Target is not (DemoTarget.Offline or DemoTarget.ScopevisioTeamwork);
    internal BaseDmsProvider.DmsProviders Provider => RequiresCustomer ? BaseDmsProvider.DmsProviders.Scopevisio : BaseDmsProvider.DmsProviders.WebDAV;
    internal static DemoTarget FromArguments(IReadOnlyList<string> args)
    {
        var targets = args.Select(arg => arg switch
        {
            "--webdav" => (DemoTarget?)DemoTarget.WebDav, "--nextcloud" => DemoTarget.Nextcloud,
            "--owncloud" => DemoTarget.OwnCloudClassic, "--scopevisio" => DemoTarget.ScopevisioTeamwork, _ => null
        }).Where(t => t.HasValue).Distinct().ToArray();
        if (targets.Length > 1) throw new ArgumentException("Select only one DMS demo target.");
        return targets.SingleOrDefault() ?? DemoTarget.Offline;
    }
    internal IDmsLoginProfile CreateProfile(string server, string username, string password, string customer,
        string organisation = "", string expectedTenant = "")
    {
        if (Target == DemoTarget.Offline) throw new InvalidOperationException("The offline demo does not authorize a remote provider.");
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) throw new ArgumentException(DemoText.Get("OpenDmsFailed"));
        if (RequiresCustomer && string.IsNullOrWhiteSpace(customer)) throw new ArgumentException(DemoText.Get("CustomerNumber"));
        if (RequiresCustomer) return DemoScopevisioSelection.CreateProfile(username, password, customer, organisation, expectedTenant);
        return new DmsLoginProfile
        {
            DmsProvider = Provider, Username = username, Password = password,
            CustomerInstance = RequiresCustomer ? customer.Trim() : null,
            BaseUrl = RequiresServer ? ResolveServerUrl(server, username) : null
        };
    }
    internal string ResolveServerUrl(string server, string username)
    {
        if (!Uri.TryCreate(server, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException(DemoText.Extra("Enter an absolute HTTP or HTTPS server URL without embedded credentials.",
                "Bitte eine absolute HTTP- oder HTTPS-Serveradresse ohne eingebettete Zugangsdaten eingeben."));
        if (Target != DemoTarget.Nextcloud) return server;
        var builder = new UriBuilder(uri) { Query = "", Fragment = "" };
        var path = builder.Path.TrimEnd('/');
        if (path.Contains("/remote.php/dav/files/", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/remote.php/webdav", StringComparison.OrdinalIgnoreCase)) return server;
        builder.Path = path.EndsWith("/remote.php/dav", StringComparison.OrdinalIgnoreCase)
            ? path + "/files/" + Uri.EscapeDataString(username) + "/"
            : path + "/remote.php/dav/files/" + Uri.EscapeDataString(username) + "/";
        return builder.Uri.AbsoluteUri;
    }
    internal DmsBrowserOptions BrowserOptions(string? root = null, bool offline = false) => new()
    {
        FormTitle = BrowserTitle + (offline || Target == DemoTarget.Offline ? " — offline" : ""),
        InitialFolder = string.IsNullOrEmpty(root) || root == "/" ? null : root.StartsWith('/') ? root[1..] : root,
        SelectedFolder = offline ? "Documents" : null,
        AllowedActions = (FileOrFolderActions)2047,
        DialogOperationMode = DialogOperationModes.NoResults,
        RemoteInstance = new RemoteInstanceSelector(!offline && RequiresCustomer ?
            RemoteInstanceSelector.StartupInstance.SelectionDialog : RemoteInstanceSelector.StartupInstance.DefaultInstance,
            RemoteInstanceSelector.ShowDefaultSelectionDialogAsync)
    };
}
