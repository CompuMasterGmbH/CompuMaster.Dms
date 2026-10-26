using CompuMaster.Dms.BrowserUI.Avalonia;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using CompuMaster.Dms.TestDemo.Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless.NUnit;
using global::Avalonia.Interactivity;
using global::Avalonia.VisualTree;
using NUnit.Framework;

namespace CompuMaster.Dms.Test.BrowserUI.Avalonia;

[TestFixture]
public sealed class BrowserUiTests
{
    internal static async Task IdleAsync(DmsBrowserControl browser)
    {
        var end = DateTime.UtcNow.AddSeconds(5);
        while (browser.IsBusy)
        {
            if (DateTime.UtcNow > end) Assert.Fail("Browser did not become idle.");
            await Task.Delay(5);
        }
    }
    internal static Button FindButton(Control control, string key) => control.GetVisualDescendants().OfType<Button>()
        .Single(b => b.Content?.ToString() == Dialogs.Caption(key));
    internal static void Click(Button button)
    {
        Assert.That(button.IsEnabled, Is.True, "The button should be enabled.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
    [AvaloniaTest]
    public async Task Browser_loads_images_localization_theme_columns_and_returns_selected_files()
    {
        var window = new DmsBrowser(new DemoDmsProvider(), new DmsBrowserOptions { SelectedFolder = "Documents" });
        try
        {
            window.Show(); await window.Browser.InitializeAsync(); await IdleAsync(window.Browser);
            Assert.That(window.Title, Is.EqualTo(UiText.Get("BrowseTitle")));
            Assert.That(window.Icon, Is.Not.Null);
            Assert.That(window.Browser.FileGrid.Columns.Count, Is.EqualTo(3));
            Assert.That(window.Browser.FileGrid.ItemsSource.Cast<FileRow>().Count(), Is.EqualTo(2));
            Assert.That(window.Browser.FileGrid.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text is "Welcome.txt" or "Shared.txt")
                .All(t => t.Foreground is not null), Is.True, "File names require the inherited theme foreground.");
            Assert.That(window.Browser.FolderTree.SelectedItem, Is.SameAs(window.Browser.Session.SelectedNode));
            window.Browser.FileGrid.SelectedItem = window.Browser.Session.Files[0];
            var confirmed = false; window.Browser.SelectionConfirmed += (_, _) => confirmed = true;
            Click(FindButton(window.Browser, "ActionOkay"));
            await IdleAsync(window.Browser);
            Assert.That(confirmed, Is.True);
            Assert.That(window.Selection.Files.Count, Is.EqualTo(1));
            Assert.That(window.Selection.SelectedFolder, Is.EqualTo("Documents"));
            Assert.That(window.IsVisible, Is.False, "Confirmation should close the browser window.");
        }
        finally { window.Close(); }
    }
    [AvaloniaTest]
    public async Task Cancel_and_close_buttons_close_the_window_after_the_operation_guard_is_released()
    {
        foreach (var mode in new[] { DialogOperationModes.ReturnSelectedItems, DialogOperationModes.NoResults })
        {
            var window = new DmsBrowser(new DemoDmsProvider(), new DmsBrowserOptions { DialogOperationMode = mode });
            try
            {
                window.Show(); await window.Browser.InitializeAsync(); await IdleAsync(window.Browser);
                Click(FindButton(window.Browser, mode == DialogOperationModes.NoResults ? "ActionClose" : "ActionCancel"));
                await IdleAsync(window.Browser);
                Assert.That(window.IsVisible, Is.False);
            }
            finally { window.Close(); }
        }
    }
    [AvaloniaTest]
    public async Task Embedded_control_can_switch_modes_and_preserves_selection()
    {
        var browser = new DmsBrowserControl(new DemoDmsProvider(), new DmsBrowserOptions
        { SelectedFolder = "Documents", AllowedActions = FileOrFolderActions.AllowSwitchBrowseMode });
        var host = new Window { Content = browser };
        try
        {
            host.Show(); await browser.InitializeAsync(); await IdleAsync(browser);
            await browser.SetBrowseModeAsync(BrowseModes.Folders);
            Assert.That(browser.FileGrid.IsVisible, Is.False);
            Assert.That(browser.SelectedFolder, Is.EqualTo("Documents"));
            await browser.SetBrowseModeAsync(BrowseModes.FoldersAndFiles);
            Assert.That(browser.FileGrid.IsVisible, Is.True);
            Assert.That(browser.Session.Files.Count, Is.EqualTo(2));
            Assert.That(FindButton(browser, "ActionUpload").IsVisible, Is.False);
            Assert.That(FindButton(browser, "ActionFileSharings").IsVisible, Is.False);
        }
        finally { host.Close(); browser.CleanupTemporaryFiles(); }
    }
    [AvaloniaTest]
    public async Task Closing_is_prevented_during_operations_and_restored_after_completion()
    {
        var provider = new DemoDmsProvider();
        var window = new DmsBrowser(provider);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            window.Show(); await window.Browser.InitializeAsync(); await IdleAsync(window.Browser);
            provider.BeforeListing = (_, _) => gate.Task;
            var operation = window.Browser.RefreshAsync();
            window.Close();
            Assert.That(window.IsVisible, Is.True);
            Assert.That(window.Browser.FolderTree.IsEnabled, Is.False);
            gate.SetResult(); await operation;
            window.Close();
            Assert.That(window.IsVisible, Is.False);
        }
        finally { gate.TrySetResult(); await IdleAsync(window.Browser); window.Close(); }
    }
    [AvaloniaTest]
    public async Task Preview_uses_selected_resource_and_removes_owned_temporary_files()
    {
        var window = new DmsBrowser(new DemoDmsProvider(), new DmsBrowserOptions
        { SelectedFolder = "Documents", AllowedActions = FileOrFolderActions.AllowDownloadFiles });
        string? preview = null;
        try
        {
            window.Show(); await window.Browser.InitializeAsync(); await IdleAsync(window.Browser);
            window.Browser.OpenDownloadedFileItem = path => { preview = path; return Task.CompletedTask; };
            window.Browser.FileGrid.SelectedItem = window.Browser.Session.Files.Single(f => f.Name == "Welcome.txt");
            Click(FindButton(window.Browser, "ActionOpen")); await IdleAsync(window.Browser);
            Assert.That(preview, Is.Not.Null);
            Assert.That(File.Exists(preview), Is.True);
            Assert.That(File.GetAttributes(preview!) & FileAttributes.ReadOnly, Is.Not.Zero);
            window.Browser.CleanupTemporaryFiles(); window.Browser.CleanupTemporaryFiles();
            Assert.That(File.Exists(preview), Is.False);
        }
        finally { window.Close(); }
    }
    [AvaloniaTest]
    public async Task Startup_default_preserves_explicit_provider_and_switching_resets_selection()
    {
        var provider = new DemoDmsProvider { InstanceID = "second" };
        var window = new DmsBrowser(provider, new DmsBrowserOptions
        {
            SelectedFolder = "Documents", AllowedActions = FileOrFolderActions.AllowSwitchDmsInstance,
            RemoteInstance = new RemoteInstanceSelector(RemoteInstanceSelector.StartupInstance.DefaultInstance,
                (_, _) => Task.FromResult<string?>("demo"))
        });
        try
        {
            window.Show(); await window.Browser.InitializeAsync(); await IdleAsync(window.Browser);
            Assert.That(provider.InstanceID, Is.EqualTo("second"));
            var button = window.Browser.GetVisualDescendants().OfType<Button>().Single(b => b.Content?.ToString()?.Contains("Second instance") == true);
            Click(button); await IdleAsync(window.Browser);
            Assert.That(provider.InstanceID, Is.EqualTo("demo"));
            Assert.That(window.Browser.SelectedFolder, Is.Null);
            Assert.That(window.Browser.Selection.Files, Is.Empty);
        }
        finally { window.Close(); }
    }
    [AvaloniaTest]
    public async Task Instance_selection_handles_cancel_invalid_selection_and_explicit_ids()
    {
        var provider = new DemoDmsProvider();
        var owner = new Window();
        var cancelled = new RemoteInstanceSelector(RemoteInstanceSelector.StartupInstance.SelectionDialog, (_, _) => Task.FromResult<string?>(null));
        Assert.That(await cancelled.SelectAsync(provider, owner), Is.False);
        Assert.That(provider.InstanceID, Is.EqualTo("demo"));
        var invalid = new RemoteInstanceSelector(RemoteInstanceSelector.StartupInstance.SelectionDialog, (_, _) => Task.FromResult<string?>("missing"));
        await ThrowsAsync<ArgumentOutOfRangeException>(async () => await invalid.SelectAsync(provider, owner));
        Assert.That(await new RemoteInstanceSelector("second").SelectAsync(provider, owner), Is.True);
        Assert.That(provider.InstanceID, Is.EqualTo("second"));
        await ThrowsAsync<NotSupportedException>(async () => await new RemoteInstanceSelector("second").SelectAsync(new NoDmsProvider(), owner));
    }
    [AvaloniaTest]
    public async Task Startup_selection_cancellation_closes_browser_without_leaving_busy_guard()
    {
        var window = new DmsBrowser(new DemoDmsProvider(), new DmsBrowserOptions
        {
            RemoteInstance = new RemoteInstanceSelector(RemoteInstanceSelector.StartupInstance.SelectionDialog, (_, _) => Task.FromResult<string?>(null))
        });
        try
        {
            window.Show();
            await IdleAsync(window.Browser);
            Assert.That(window.IsVisible, Is.False);
            Assert.That(window.Browser.IsBusy, Is.False);
        }
        finally { window.Close(); }
    }
    private static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); Assert.Fail("Expected " + typeof(T).Name); }
        catch (T) { }
    }
}
