using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Platform;
using global::Avalonia.Markup.Xaml.Styling;
using global::Avalonia.Styling;

namespace CompuMaster.Dms.BrowserUI.Avalonia;

/// <summary>Provides a ready-to-use Avalonia DMS browser and resource picker.</summary>
/// <remarks>Hosts must install an Avalonia theme. The browser adds the matching Fluent DataGrid styles to its own scope.</remarks>
public sealed class DmsBrowser : Window
{
    /// <summary>Creates a browser for an authorized provider, preserving its selected instance by default.</summary>
    /// <param name="provider">The provider used by the browser.</param>
    /// <param name="options">The optional workflow settings.</param>
    public DmsBrowser(BaseDmsProvider provider, DmsBrowserOptions? options = null)
        : this(new DmsBrowserControl(provider, options)) { }

    /// <summary>Creates a browser that authorizes the supplied profile asynchronously when opened.</summary>
    /// <param name="profile">The login profile used to create the provider.</param>
    /// <param name="options">The optional workflow settings.</param>
    public DmsBrowser(IDmsLoginProfile profile, DmsBrowserOptions? options = null)
        : this(new DmsBrowserControl(profile, options)) { }

    private DmsBrowser(DmsBrowserControl browser)
    {
        Browser = browser;
        FlowDirection = UiText.LayoutDirection;
        Title = browser.Options.FormTitle ?? UiText.Get("BrowseTitle");
        Width = 1100; Height = 720; MinWidth = 640; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://CompuMaster.Dms.BrowserUI.Avalonia/Assets/favicon.ico")));
        Content = Browser;
        Browser.SelectionConfirmed += (_, _) => Close(Browser.Selection);
        Browser.Cancelled += (_, _) => Close();
        Closing += (_, e) =>
        {
            if (Browser.IsBusy) { e.Cancel = true; return; }
            try { Browser.CleanupTemporaryFiles(); }
            catch (IOException ex) { Browser.ReportCleanupFailure(ex); }
        };
        KeyDown += (_, e) => { if (!Browser.IsBusy && e.Key == Key.Escape) { Close(); e.Handled = true; } };
    }
    /// <summary>Gets the embeddable browser control hosted by this dialog.</summary>
    public DmsBrowserControl Browser { get; }
    /// <summary>Gets the selected resources.</summary>
    public DmsBrowserSelection Selection => Browser.Selection;
    /// <summary>Reports original failures consumed by this browser's UI handlers.</summary>
    /// <remarks>Subscribers must protect sensitive exception data. No diagnostics are automatically persisted.</remarks>
    public event EventHandler<DmsBrowserFailureEventArgs> OperationFailed
    {
        add => Browser.OperationFailed += value;
        remove => Browser.OperationFailed -= value;
    }
}
