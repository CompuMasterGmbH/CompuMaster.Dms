#if DMS_DOCUMENTATION_SCREENSHOTS
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using CompuMaster.Dms.BrowserUI.WinUI;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using CompuMaster.Dms.TestDemo.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NUnit.Framework;
using DataGrid = CommunityToolkit.WinUI.UI.Controls.DataGrid;

namespace CompuMaster.Dms.Test.BrowserUI.WinUI;

[TestFixture]
public sealed class DocumentationSharingScreenshots
{
    [TestCase("teamwork"), TestCase("nextcloud"), Explicit("Exports the README sharing examples; select this method explicitly.")]
    public Task Export_sharing_setup_screenshots(string target) => UiThread.Run(async () =>
    {
        var directory = Environment.GetEnvironmentVariable("DMS_DOCUMENTATION_SCREENSHOTS")
            ?? throw new InvalidOperationException("Set DMS_DOCUMENTATION_SCREENSHOTS.");
        Directory.CreateDirectory(directory);
        var previous = CultureInfo.CurrentCulture; var previousUi = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        var (provider, item) = CreateExample(target == "teamwork");
        var dialog = new DmsItemSharings(provider, item);
        var root = (FrameworkElement)dialog.Content;
        BrowserAppearance.SetTheme(root, BrowserTheme.Light);
        var pending = dialog.ShowDialog(UiThread.Owner);
        try
        {
            await RefreshSelectionUiTests.Until(() => root.IsLoaded && !dialog.IsRunning &&
                NativeUiTests.Descendants(root).OfType<DataGrid>().Count(grid => grid.ItemsSource is not null) == 2);
            const int width = 1100, height = 720;
            var scale = root.XamlRoot.RasterizationScale;
            var chromeWidth = dialog.AppWindow.Size.Width - root.ActualWidth * scale;
            var chromeHeight = dialog.AppWindow.Size.Height - root.ActualHeight * scale;
            dialog.AppWindow.Resize(new Windows.Graphics.SizeInt32(
                (int)Math.Ceiling(width * scale + chromeWidth), (int)Math.Ceiling(height * scale + chromeHeight)));
            await Task.Delay(250); root.UpdateLayout();
            var grids = NativeUiTests.Descendants(root).OfType<DataGrid>().ToArray();
            Assert.That(grids.Single(grid => grid.Name == "InternalSharings").ItemsSource.Cast<SharingRow>().Count(), Is.EqualTo(2));
            var links = grids.Single(grid => grid.Name == "ExternalSharings");
            Assert.That(links.ItemsSource.Cast<SharingRow>().Count(), Is.EqualTo(2));
            Assert.That(links.TransformToVisual(root).TransformBounds(new Windows.Foundation.Rect(0, 0, links.ActualWidth, links.ActualHeight)).Bottom,
                Is.LessThan(height - 45), "Both sharing tables must be visible without scrolling.");
            Assert.That(dialog.HasChanges, Is.False);
            Assert.That(provider.Calls, Is.Empty, "Captures must not access servers or change resources.");
            var bitmap = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap(); await bitmap.RenderAsync(root, width, height);
            var pixels = (await bitmap.GetPixelsAsync()).ToArray(); Assert.That(pixels[3], Is.EqualTo(255));
            using var output = File.Create(Path.Combine(directory, "winui-sharing-" + target + ".png"));
            using var stream = output.AsRandomAccessStream();
            var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels);
            await encoder.FlushAsync();
        }
        finally
        {
            dialog.Close(); await pending;
            CultureInfo.CurrentCulture = previous; CultureInfo.CurrentUICulture = previousUi;
        }
    });

    private static (DemoDmsProvider Provider, DmsResourceItem Item) CreateExample(bool teamwork)
    {
        // Mirror the provider's sharing contract without creating a network client or loading a login profile.
        var provider = new DemoDmsProvider(false)
        {
            ProviderKind = teamwork ? BaseDmsProvider.DmsProviders.Scopevisio : BaseDmsProvider.DmsProviders.WebDAV,
            CollectionsSupported = teamwork
        };
        var item = provider.AddDirectory("/unittests_documentation/Project documents",
            teamwork ? DmsResourceItem.ItemTypes.Collection : DmsResourceItem.ItemTypes.Folder);
        item.ExtendedInfosOwner = new DmsUser { ID = "unittests_documentation_team", DisplayName = "Documentation team" };
        item.ExtendedInfosUserSharings.Add(new DmsShareForUser(item,
            new DmsUser { ID = "unittests_alex", DisplayName = "Alex Taylor" }, true, true, true, true, teamwork, teamwork));
        item.ExtendedInfosGroupSharings.Add(new DmsShareForGroup(item,
            new DmsGroup { ID = "unittests_project_team", Name = "Project team" }, true, true, teamwork, teamwork, teamwork, teamwork));
        var review = new DmsLink(item, provider)
        {
            ID = "unittests_review_link", Name = teamwork ? null : "Project review",
            AllowView = true, AllowDownload = true, ExpiryDateLocalTime = new DateTime(2026, 11, 30, 18, 0, 0),
            MaxDownloads = teamwork ? 25 : null, WebUrl = "https://example.invalid/unittests_review"
        };
        var upload = new DmsLink(item, provider)
        {
            ID = "unittests_upload_link", Name = "Collect project files", AllowUpload = true,
            MaxUploads = teamwork ? 100 : null, MaxBytes = teamwork ? 104857600 : null,
            WebUrl = "https://example.invalid/unittests_upload"
        };
        var policy = new SharingPolicy(provider);
        foreach (var share in new DmsShareBase[] { review, upload }.Concat(item.ExtendedInfosUserSharings).Concat(item.ExtendedInfosGroupSharings))
            policy.Validate(share);
        item.ExtendedInfosLinks.Add(review); item.ExtendedInfosLinks.Add(upload);
        item.ExtendedInfosHasUserSharings = item.ExtendedInfosHasGroupSharings = true;
        item.ExtendedInfosIsShared = true; item.ExtendedInfosIsReceivedShare = false; item.ExtendedInfosHasOutgoingSharings = true;
        return (provider, item);
    }
}
#endif
