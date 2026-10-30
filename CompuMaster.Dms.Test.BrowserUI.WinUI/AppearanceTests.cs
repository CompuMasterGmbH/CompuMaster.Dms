using CompuMaster.Dms.BrowserUI.WinUI;
using CompuMaster.Dms.TestDemo.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NUnit.Framework;
using DataGrid = CommunityToolkit.WinUI.UI.Controls.DataGrid;

namespace CompuMaster.Dms.Test.BrowserUI.WinUI;

[TestFixture]
public sealed class AppearanceTests
{
    private static async Task CaptureAsync(FrameworkElement content, string name)
    {
        var directory = Environment.GetEnvironmentVariable("DMS_THEME_SCREENSHOTS");

        var image = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap(); await image.RenderAsync(content);
        var pixels = await image.GetPixelsAsync();
        var data = System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.ToArray(pixels);
        Assert.That(data[3], Is.EqualTo(255), "The window scope must render an opaque theme background.");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        using var file = File.Create(Path.Combine(directory, name + ".png"));
        using var stream = System.IO.WindowsRuntimeStreamExtensions.AsRandomAccessStream(file);
        var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
            (uint)image.PixelWidth, (uint)image.PixelHeight, 96, 96, data);
        await encoder.FlushAsync();
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    [TestCase("Refresh"), TestCase("iconfinder_Trash-ui-ux-mobile-web_4960750")]
    public Task Dark_artwork_decodes_complete_pixels_and_creates_a_native_bitmap(string resource) => UiThread.Run(async () =>
    {
        var pixels = await ThemeArtwork.ReadDarkAsync(resource);
        Assert.That(pixels.Width, Is.GreaterThan(0)); Assert.That(pixels.Height, Is.GreaterThan(0));
        Assert.That(pixels.Data.Length, Is.EqualTo(pixels.Width * pixels.Height * 4));
        Assert.That(Enumerable.Range(0, pixels.Width * pixels.Height).Any(index => pixels.Data[index * 4 + 3] == 255 && pixels.Data[index * 4] == 230 && pixels.Data[index * 4 + 1] == 230 && pixels.Data[index * 4 + 2] == 230), Is.True,
            "Dark strokes must be lightened while retaining an opaque image.");
        Assert.That(ThemeArtwork.Image(resource, pixels).PixelWidth, Is.EqualTo(pixels.Width));
    });
    [TestCase(false), TestCase(true)]
    public Task Unloading_during_artwork_loading_discards_the_late_UI_update(bool decodeFails) => UiThread.Run(async () =>
    {
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var decoded = new TaskCompletionSource<ThemeArtwork.Pixels>(TaskCreationOptions.RunContinuationsAsynchronously);
        var image = new Image { Width = 1, Height = 1, RequestedTheme = ElementTheme.Dark };
        var assignments = 0;
        BrowserActionIcons.AttachTheme(image, "Refresh", _ => assignments++, _ => { requested.TrySetResult(); return decoded.Task; });
        var host = (Grid)UiThread.Owner.Content;
        try
        {
            host.Children.Add(image); await requested.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var beforeUnload = assignments;
            host.Children.Remove(image);
            while (image.IsLoaded) await Task.Delay(5);
            await Task.Delay(10);
            if (decodeFails) decoded.SetException(new System.Runtime.InteropServices.COMException("Expected decode shutdown failure.", unchecked((int)0x80004005)));
            else decoded.SetResult(new ThemeArtwork.Pixels(new byte[4], 1, 1));
            await Task.Delay(30);
            Assert.That(assignments, Is.EqualTo(beforeUnload), "Decoded artwork must not update an unloaded native element.");
        }
        finally { host.Children.Remove(image); decoded.TrySetResult(new ThemeArtwork.Pixels(new byte[4], 1, 1)); }
    });
    [Test]
    public Task Custom_palette_is_scoped_inherited_and_resettable() => UiThread.Run(async () =>
    {
        var palette = new BrowserPalette("#172334", "#24364A", "#EDF2FA", "#5399D5", "#081524", "#647A90");
        var browser = new DmsBrowser(new DemoDmsProvider(), new() { Theme = BrowserTheme.Dark, DarkPalette = palette });
        var dialog = new DmsItemSharings(new DemoDmsProvider(), ReceivedSharingTests.ReceivedItem());
        Task? pending = null;
        try
        {
            _ = browser.ShowAsync(); await browser.Browser.InitializeAsync();
            var root = (ContentControl)browser.Content;
            while (!root.IsLoaded) await Task.Delay(5);
            Assert.That(((Microsoft.UI.Xaml.Media.SolidColorBrush)root.Background).Color, Is.EqualTo(Windows.UI.Color.FromArgb(255, 23, 35, 52)));
            pending = dialog.ShowDialog(browser); await Task.Delay(50);
            Assert.That(((Microsoft.UI.Xaml.Media.SolidColorBrush)((ContentControl)dialog.Content).Background).Color, Is.EqualTo(Windows.UI.Color.FromArgb(255, 23, 35, 52)));
            await CaptureAsync(root, "winui-custom");
            browser.Browser.DarkPalette = null; await Task.Delay(30);
            Assert.That(((Microsoft.UI.Xaml.Media.SolidColorBrush)root.Background).Color, Is.Not.EqualTo(Windows.UI.Color.FromArgb(255, 23, 35, 52)));
        }
        finally { dialog.Close(); if (pending is not null) await pending; browser.Close(); }
    });
    [Test]
    public Task Browser_switches_modes_without_changing_the_host() => UiThread.Run(async () =>
    {
        var host = (FrameworkElement)UiThread.Owner.Content;
        var original = host.RequestedTheme;
        var browser = new DmsBrowserControl(new DemoDmsProvider(), new() { Theme = BrowserTheme.Dark });
        try
        {
            ((Grid)host).Children.Add(browser); browser.AttachToWindow(UiThread.Owner); await browser.InitializeAsync(); await Task.Delay(30);
            Assert.That(browser.ActualTheme, Is.EqualTo(ElementTheme.Dark));
            Assert.That(host.RequestedTheme, Is.EqualTo(original));
            var dialog = new DmsItemSharings(new DemoDmsProvider(), ReceivedSharingTests.ReceivedItem());
            var pending = BrowserAppearance.ShowDialog(dialog, UiThread.Owner, browser);
            try { await Task.Delay(30); Assert.That(dialog.Content.ActualTheme, Is.EqualTo(ElementTheme.Dark)); }
            finally { dialog.Close(); await pending; }
            browser.Appearance = BrowserTheme.Light;
            Assert.That(browser.ActualTheme, Is.EqualTo(ElementTheme.Light));
            browser.Appearance = BrowserTheme.System;
            Assert.That(browser.RequestedTheme, Is.EqualTo(ElementTheme.Default));
            Assert.Throws<ArgumentOutOfRangeException>((Action)(() => browser.Appearance = (BrowserTheme)99));
            Assert.That(browser.Appearance, Is.EqualTo(BrowserTheme.System));
        }
        finally { ((Grid)host).Children.Remove(browser); }
    });
    [Test]
    public Task Owned_workflow_dialogs_follow_browser_appearance() => UiThread.Run(async () =>
    {
        var browser = new DmsBrowser(new DemoDmsProvider(), new() { Theme = BrowserTheme.Dark });
        var dialog = new DmsItemSharings(new DemoDmsProvider(), ReceivedSharingTests.ReceivedItem());
        Task? pending = null;
        try
        {
            _ = browser.ShowAsync(); await browser.Browser.InitializeAsync();
            pending = dialog.ShowDialog(browser); await Task.Delay(100);
            Assert.That(dialog.Content.ActualTheme, Is.EqualTo(ElementTheme.Dark));
            Assert.That(Descendants(dialog.Content).OfType<DataGrid>().All(g => g.ActualTheme == ElementTheme.Dark), Is.True);
            await CaptureAsync((FrameworkElement)browser.Content, "winui-dark");
            await CaptureAsync(dialog.Content, "winui-sharing-dark");
            browser.Browser.Appearance = BrowserTheme.Light;
            Assert.That(((FrameworkElement)browser.Content).ActualTheme, Is.EqualTo(ElementTheme.Light));
            await Task.Delay(30);
            Assert.That(dialog.Content.ActualTheme, Is.EqualTo(ElementTheme.Light));
        }
        finally { dialog.Close(); if (pending is not null) await pending; browser.Close(); }
    });
    [Test]
    public Task Selector_switches_appearance_and_default_options_inherit() => UiThread.Run(() =>
    {
        var root = new Grid(); var selector = BrowserAppearance.CreateSelector(root);
        Assert.That(new DmsBrowserOptions().Theme, Is.EqualTo(BrowserTheme.System));
        selector.SelectedIndex = (int)BrowserTheme.Dark;
        Assert.That(root.RequestedTheme, Is.EqualTo(ElementTheme.Dark));
        selector.SelectedIndex = (int)BrowserTheme.System;
        Assert.That(root.RequestedTheme, Is.EqualTo(ElementTheme.Default));
        return Task.CompletedTask;
    });
}
