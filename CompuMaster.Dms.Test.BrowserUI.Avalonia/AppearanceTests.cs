using CompuMaster.Dms.BrowserUI.Avalonia;
using CompuMaster.Dms.TestDemo.Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless.NUnit;
using global::Avalonia.Styling;
using global::Avalonia.VisualTree;
using NUnit.Framework;

namespace CompuMaster.Dms.Test.BrowserUI.Avalonia;

[TestFixture]
public sealed class AppearanceTests
{
    [AvaloniaTest]
    public async Task Custom_palettes_follow_mode_and_restore_host_resources()
    {
        var palette = new BrowserPalette("#172334", "#24364A", "#EDF2FA", "#5399D5", "#081524", "#647A90");
        var window = new DmsBrowser(new DemoDmsProvider(), new() { Theme = BrowserTheme.Dark, DarkPalette = palette });
        var dialog = new DmsItemSharings(new DemoDmsProvider(), ReceivedSharingTests.ReceivedItem());
        Task? pending = null;
        try
        {
            window.Show(); await window.Browser.InitializeAsync(); window.UpdateLayout();
            Assert.That(((global::Avalonia.Media.SolidColorBrush)window.Background!).Color, Is.EqualTo(global::Avalonia.Media.Color.Parse(palette.Background)));
            pending = BrowserAppearance.ShowDialog(dialog, window);
            while (dialog.IsRunning) await Task.Delay(5);
            Assert.That(((global::Avalonia.Media.SolidColorBrush)dialog.Background!).Color, Is.EqualTo(global::Avalonia.Media.Color.Parse(palette.Background)));
            window.Browser.DarkPalette = null;
            Assert.That(((global::Avalonia.Media.SolidColorBrush)window.Background!).Color, Is.Not.EqualTo(global::Avalonia.Media.Color.Parse(palette.Background)));
            Assert.That(((global::Avalonia.Media.SolidColorBrush)dialog.Background!).Color, Is.Not.EqualTo(global::Avalonia.Media.Color.Parse(palette.Background)));
            Assert.Throws<ArgumentException>((Action)(() => new BrowserPalette("invalid", "#000000", "#ffffff", "#000000", "#ffffff", "#000000")));
        }
        finally { dialog.Close(); if (pending is not null) await pending; window.Close(); }
    }
    [AvaloniaTest]
    public async Task Browser_switches_theme_without_changing_host_or_application()
    {
        var host = new Window { RequestedThemeVariant = ThemeVariant.Light };
        var control = new DmsBrowserControl(new DemoDmsProvider(), new() { Theme = BrowserTheme.Dark });
        host.Content = control;
        try
        {
            host.Show(); await control.InitializeAsync(); host.UpdateLayout();
            Assert.That(control.ActualThemeVariant, Is.EqualTo(ThemeVariant.Dark));
            Assert.That(control.FileGrid.ActualThemeVariant, Is.EqualTo(ThemeVariant.Dark));
            Assert.That(host.ActualThemeVariant, Is.EqualTo(ThemeVariant.Light));
            control.Appearance = BrowserTheme.Light;
            Assert.That(control.FileGrid.ActualThemeVariant, Is.EqualTo(ThemeVariant.Light));
            control.Appearance = BrowserTheme.System;
            host.RequestedThemeVariant = ThemeVariant.Dark;
            Assert.That(control.FileGrid.ActualThemeVariant, Is.EqualTo(ThemeVariant.Dark));
            Assert.That(control.Options.Theme, Is.EqualTo(BrowserTheme.System));
        }
        finally { host.Close(); }
    }
    [AvaloniaTest]
    public async Task Host_api_switches_browser_window_and_owned_workflow_dialogs()
    {
        var browser = new DmsBrowser(new DemoDmsProvider());
        var dialog = new DmsItemSharings(new DemoDmsProvider(), ReceivedSharingTests.ReceivedItem());
        Task? pending = null;
        try
        {
            browser.Show(); await browser.Browser.InitializeAsync();
            Assert.That(browser.GetVisualDescendants().OfType<ComboBox>().Any(c => c.Name == "BrowserThemeSelector"), Is.False);
            browser.Browser.Appearance = BrowserTheme.Dark;
            Assert.That(browser.Browser.Appearance, Is.EqualTo(BrowserTheme.Dark));
            Assert.That(browser.ActualThemeVariant, Is.EqualTo(ThemeVariant.Dark));
            pending = BrowserAppearance.ShowDialog(dialog, browser);
            while (dialog.IsRunning) await Task.Delay(5);
            Assert.That(dialog.ActualThemeVariant, Is.EqualTo(ThemeVariant.Dark));
            Assert.That(dialog.GetVisualDescendants().OfType<DataGrid>().All(g => g.ActualThemeVariant == ThemeVariant.Dark), Is.True);
            browser.Browser.Appearance = BrowserTheme.Light;
            Assert.That(dialog.ActualThemeVariant, Is.EqualTo(ThemeVariant.Light));
        }
        finally { dialog.Close(); if (pending is not null) await pending; browser.Close(); }
    }
    [AvaloniaTest]
    public void Explicit_picker_theme_overrides_owner_and_invalid_mode_does_not_mutate_state()
    {
        var owner = new Window { RequestedThemeVariant = ThemeVariant.Dark };
        var picker = new DmsBrowser(new DemoDmsProvider(), new() { Theme = BrowserTheme.Light });
        try
        {
            BrowserAppearance.Inherit(picker, owner);
            Assert.That(picker.RequestedThemeVariant, Is.EqualTo(ThemeVariant.Light));
            Assert.Throws<ArgumentOutOfRangeException>((Action)(() => picker.Browser.Appearance = (BrowserTheme)99));
            Assert.That(picker.Browser.Appearance, Is.EqualTo(BrowserTheme.Light));
            Assert.That(new DmsBrowserOptions().Theme, Is.EqualTo(BrowserTheme.System));
        }
        finally { picker.Close(); owner.Close(); }
    }
}
