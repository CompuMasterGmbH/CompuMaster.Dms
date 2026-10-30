using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using Microsoft.UI.Xaml;

namespace CompuMaster.Dms.BrowserUI.WinUI;

/// <summary>Provides a native WinUI 3 DMS browser window and resource picker.</summary>
public sealed class DmsBrowser : Window
{
    private readonly WindowHost host;
    /// <summary>Creates a browser for an authorized provider.</summary>
    /// <param name="provider">The provider to browse.</param>
    /// <param name="options">The optional browser settings.</param>
    public DmsBrowser(BaseDmsProvider provider, DmsBrowserOptions? options = null) : this(new DmsBrowserControl(provider, options)) { }
    /// <summary>Creates a browser that authorizes a login profile when opened.</summary>
    /// <param name="profile">The provider login profile.</param>
    /// <param name="options">The optional browser settings.</param>
    public DmsBrowser(IDmsLoginProfile profile, DmsBrowserOptions? options = null) : this(new DmsBrowserControl(profile, options)) { }
    private DmsBrowser(DmsBrowserControl browser)
    {
        Browser = browser; host = new WindowHost(this, 1100, 760); Browser.AttachToWindow(this);
        Title = browser.Options.FormTitle ?? UiText.Get("BrowseTitle"); Content = BrowserAppearance.WindowContent(Browser);
        BrowserAppearance.SetTheme((FrameworkElement)Content, browser.Options.Theme);
        BrowserAppearance.SetPalettes((FrameworkElement)Content, browser.Options.LightPalette, browser.Options.DarkPalette);
        Browser.SelectionConfirmed += (_, _) => host.Close(Browser.Selection);
        Browser.Cancelled += (_, _) => { if (!Browser.IsBusy) host.Close(); };
        AppWindow.Closing += (_, e) =>
        {
            if (Browser.IsBusy) { e.Cancel = true; return; }
            try { Browser.CleanupTemporaryFiles(); } catch (IOException ex) { Browser.ReportCleanupFailure(ex); }
        };
    }
    /// <summary>Gets the embedded browser control.</summary>
    public DmsBrowserControl Browser { get; }
    /// <summary>Gets the selected remote resources.</summary>
    public DmsBrowserSelection Selection => Browser.Selection;
    /// <summary>Shows the browser as an owned modal window.</summary>
    /// <param name="owner">The owning WinUI window.</param>
    /// <returns>The confirmed selection, or null on cancellation.</returns>
    public async Task<DmsBrowserSelection?> ShowDialogAsync(Window owner) => await host.ShowAsync(owner) as DmsBrowserSelection;
    /// <summary>Shows the browser as a standalone window.</summary>
    /// <returns>The confirmed selection, or null on cancellation.</returns>
    public async Task<DmsBrowserSelection?> ShowAsync() => await host.ShowAsync() as DmsBrowserSelection;
    /// <summary>Reports original failures consumed by browser UI handlers.</summary>
    /// <remarks>Subscribers must protect potentially sensitive exception data.</remarks>
    public event EventHandler<DmsBrowserFailureEventArgs> OperationFailed
    { add => Browser.OperationFailed += value; remove => Browser.OperationFailed -= value; }
}
