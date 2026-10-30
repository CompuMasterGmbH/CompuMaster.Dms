#if DMS_DOCUMENTATION_SCREENSHOTS
using System.Globalization;
using System.Reflection;
using CompuMaster.Dms.Test.BrowserUI;
using CompuMaster.Dms.Test.Screenshots;
using global::Avalonia;
using global::Avalonia.Headless.NUnit;
using global::Avalonia.Media.Imaging;
using NUnit.Framework;
using Av = CompuMaster.Dms.BrowserUI.Avalonia;
using Wf = CompuMaster.Dms.BrowserUI;
using Forms = System.Windows.Forms;

namespace CompuMaster.Dms.Test.BrowserUI.Parity;

public sealed partial class BrowserParityTests
{
    [AvaloniaTest, Explicit("Exports the README screenshot gallery; select this method explicitly.")]
    [TestCase(false), TestCase(true)]
    public async Task Export_documentation_screenshots(bool dark)
    {
        var directory = Environment.GetEnvironmentVariable("DMS_DOCUMENTATION_SCREENSHOTS") ?? throw new InvalidOperationException("Set DMS_DOCUMENTATION_SCREENSHOTS to the output directory.");
        Directory.CreateDirectory(directory);
        var previousCulture = CultureInfo.CurrentCulture; var previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        var suffix = dark ? "dark" : "light";
        try
        {
            Native(() =>
            {
                CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                Forms.Application.EnableVisualStyles();
                using var dispatcher = new UiTestDispatcher();
                using var browser = new ReferenceBrowser(DocumentationScreenshotData.CreateProvider(), 2047, false, true);
                browser.Text = DocumentationScreenshotData.Title;
                browser.FormBorderStyle = Forms.FormBorderStyle.None;
                browser.ClientSize = new System.Drawing.Size(1100, 720);
                browser.Appearance = dark ? Wf.BrowserTheme.Dark : Wf.BrowserTheme.Light;
                browser.Opacity = 1; browser.Show(); dispatcher.WaitForEvents();
                var list = Member<Forms.ListView>(browser, "ListViewDmsFiles");
                // Preserve the production default: latest modification first, with its header indicator.
                Assert.That(list.Items.Cast<Forms.ListViewItem>().Select(item => item.Text), Is.EqualTo(DocumentationScreenshotData.Names));
                list.Columns[0].Width = 300; list.Columns[1].Width = 100; list.Columns[2].Width = Math.Max(250, list.ClientSize.Width - 400);
                list.Items[0].Selected = true; dispatcher.WaitForEvents();
                SaveForms(browser, "winforms-browser-" + suffix);
                Exception? failure = null;
                using var timer = new Forms.Timer { Interval = 100 };
                timer.Tick += (_, _) =>
                {
                    if (browser.OwnedForms.SingleOrDefault() is not { } dialog) return;
                    timer.Stop();
                    try
                    {
                        dialog.FormBorderStyle = Forms.FormBorderStyle.None;
                        dialog.ClientSize = new System.Drawing.Size(650, 380); dialog.PerformLayout(); dialog.Refresh();
                        var input = dialog.Controls.Find("PropertiesDetails", true).Cast<Forms.TextBox>().Single();
                        Assert.That(input.Text.Replace("\r\n", "\n"), Is.EqualTo(DocumentationScreenshotData.Details));
                        input.SelectionStart = 0; input.SelectionLength = 0;
                        SaveForms(dialog, "winforms-details-" + suffix);
                    }
                    catch (Exception ex) { failure = ex; }
                    finally { dialog.Close(); }
                };
                timer.Start();
                typeof(Wf.DmsBrowser).GetMethod("ShowPropertiesDialog", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(browser,
                    new object[] { DocumentationScreenshotData.Details.Replace("\n", "\r\n"), DocumentationScreenshotData.DetailsTitle });
                if (failure is not null) throw failure;
                return true;
            });
            var window = new Av.DmsBrowser(DocumentationScreenshotData.CreateProvider(), new()
            {
                FormTitle = DocumentationScreenshotData.Title, SelectedFolder = "Documents", AllowedActions = (Av.FileOrFolderActions)2047,
                DialogOperationMode = Av.DialogOperationModes.NoResults, Theme = dark ? Av.BrowserTheme.Dark : Av.BrowserTheme.Light
            }) { Width = 1100, Height = 720 };
            try
            {
                window.Show(); await window.Browser.InitializeAsync(); window.UpdateLayout();
                Assert.That(window.Browser.FileGrid.ItemsSource.Cast<Av.FileRow>().Select(row => row.Name), Is.EqualTo(DocumentationScreenshotData.Names));
                window.Browser.FileGrid.SelectedIndex = 0; await Task.Delay(150); window.UpdateLayout();
                SaveAvalonia(window, "avalonia-browser-" + suffix);
                var pending = Av.Dialogs.MessageAsync(window, DocumentationScreenshotData.Details, DocumentationScreenshotData.DetailsTitle);
                try
                {
                    var dialog = window.OwnedWindows.Single(); dialog.Width = 650; dialog.Height = 380; dialog.UpdateLayout();
                    SaveAvalonia(dialog, "avalonia-details-" + suffix); dialog.Close(); await pending;
                }
                finally { foreach (var child in window.OwnedWindows.ToArray()) child.Close(); }
            }
            finally { window.Close(); }
        }
        finally { CultureInfo.CurrentCulture = previousCulture; CultureInfo.CurrentUICulture = previousUiCulture; }

        void SaveForms(Forms.Form form, string name)
        {
            using var bitmap = new System.Drawing.Bitmap(form.ClientSize.Width, form.ClientSize.Height);
            form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(System.Drawing.Point.Empty, form.ClientSize));
            bitmap.Save(Path.Combine(directory, name + ".png"));
        }
        void SaveAvalonia(global::Avalonia.Controls.Window window, string name)
        {
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
            bitmap.Render(window); bitmap.Save(Path.Combine(directory, name + ".png"), PngBitmapEncoderOptions.Default);
        }
    }
}

#endif
