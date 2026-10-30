#if DMS_DOCUMENTATION_SCREENSHOTS
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using CompuMaster.Dms.BrowserUI.WinUI;
using CompuMaster.Dms.Test.Screenshots;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NUnit.Framework;

namespace CompuMaster.Dms.Test.BrowserUI.WinUI;

[TestFixture]
public sealed class DocumentationScreenshots
{
    [TestCase(false), TestCase(true), Explicit("Exports README images; select this fixture explicitly.")]
    public Task Export_documentation_screenshots(bool dark) => UiThread.Run(async () =>
    {
        var directory = Environment.GetEnvironmentVariable("DMS_DOCUMENTATION_SCREENSHOTS") ?? throw new InvalidOperationException("Set DMS_DOCUMENTATION_SCREENSHOTS.");
        Directory.CreateDirectory(directory);
        var previous = CultureInfo.CurrentCulture; var previousUi = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        var suffix = dark ? "dark" : "light";
        var browser = new DmsBrowser(DocumentationScreenshotData.CreateProvider(), new()
        {
            FormTitle = DocumentationScreenshotData.Title, SelectedFolder = "Documents", AllowedActions = (FileOrFolderActions)2047,
            DialogOperationMode = DialogOperationModes.NoResults, Theme = dark ? BrowserTheme.Dark : BrowserTheme.Light
        });
        try
        {
            _ = browser.ShowAsync(); await browser.Browser.InitializeAsync();
            var root = (FrameworkElement)browser.Content; while (!root.IsLoaded || root.ActualWidth <= 0) await Task.Delay(5);
            ResizeClient(browser, root, 1100, 720); await Task.Delay(500); root.UpdateLayout();
            var list = (ListView)typeof(DmsBrowserControl).GetField("files", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(browser.Browser)!;
            Assert.That(list.Items.Cast<ListViewItem>().Select(item => ((FileRow)item.Tag).Name), Is.EqualTo(DocumentationScreenshotData.Names));
            list.SelectedIndex = 0; await Task.Delay(100);
            await Save(root, "winui-browser-" + suffix, 1100, 720);
            DialogWindow? dialog = null;
            dialog = Dialogs.Window(DocumentationScreenshotData.DetailsTitle,
                Dialogs.Layout(Dialogs.MessageText(DocumentationScreenshotData.Details),
                    Dialogs.Buttons(Dialogs.Button("ActionOkay", () => dialog!.Close()))), 650, 420);
            var pending = BrowserAppearance.ShowDialog(dialog, browser);
            try
            {
                var body = (FrameworkElement)dialog.Content; while (!body.IsLoaded || body.ActualWidth <= 0) await Task.Delay(5);
                ResizeClient(dialog, body, 650, 380); await Task.Delay(100); body.UpdateLayout();
                await Save(body, "winui-details-" + suffix, 650, 380);
                dialog.Close(); await pending;
            }
            finally { if (!pending.IsCompleted) dialog.Close(); }
        }
        finally { browser.Close(); CultureInfo.CurrentCulture = previous; CultureInfo.CurrentUICulture = previousUi; }

        static void ResizeClient(Window window, FrameworkElement root, int width, int height)
        {
            var scale = root.XamlRoot.RasterizationScale;
            var chromeWidth = window.AppWindow.Size.Width - root.ActualWidth * scale;
            var chromeHeight = window.AppWindow.Size.Height - root.ActualHeight * scale;
            window.AppWindow.Resize(new Windows.Graphics.SizeInt32((int)Math.Ceiling(width * scale + chromeWidth), (int)Math.Ceiling(height * scale + chromeHeight)));
        }
        async Task Save(FrameworkElement root, string name, int width, int height)
        {
            var bitmap = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap(); await bitmap.RenderAsync(root, width, height);
            var data = (await bitmap.GetPixelsAsync()).ToArray();
            Assert.That(data[3], Is.EqualTo(255));
            using var file = File.Create(Path.Combine(directory, name + ".png")); using var stream = file.AsRandomAccessStream();
            var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, data);
            await encoder.FlushAsync();
        }
    });
}

#endif
