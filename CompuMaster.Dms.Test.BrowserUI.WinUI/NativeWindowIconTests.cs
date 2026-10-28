using System.Runtime.InteropServices;
using CompuMaster.Dms.BrowserUI.WinUI;
using CompuMaster.Dms.TestDemo.WinUI;
using Microsoft.UI.Xaml;
using NUnit.Framework;

namespace CompuMaster.Dms.Test.BrowserUI.WinUI;

[TestFixture]
public sealed class NativeWindowIconTests
{
    [TestCase("login"), TestCase("browser"), TestCase("dialog")]
    public Task Native_windows_keep_the_executable_artwork_for_titlebar_and_taskbar(string kind) => UiThread.Run(async () =>
    {
        using var directory = new OwnedDirectory();
        Window window = kind switch
        {
            "login" => new DemoLoginWindow(new DemoDefinition(DemoTarget.Nextcloud), buffer: new DemoInputBuffer(DemoTarget.Nextcloud, directory.Path, _ => null)),
            "browser" => new DmsBrowser(new DemoDmsProvider()),
            _ => new DmsItemSharings(new DemoDmsProvider(), ReceivedSharingTests.ReceivedItem())
        };
        Task? pending = null;
        nint expectedLarge = 0, expectedSmall = 0;
        try
        {
            window.Title = "DMS WinUI icon review – " + kind;
            if (window is DmsItemSharings sharing) pending = sharing.ShowDialog(UiThread.Owner);
            else if (window is DmsBrowser browser) { _ = browser.ShowAsync(); await browser.Browser.InitializeAsync(); }
            else window.Activate();
            await Task.Delay(100);
            var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            Assert.That(ExtractIconEx(Environment.ProcessPath!, 0, out expectedLarge, out expectedSmall, 1), Is.GreaterThan(0));
            var large = SendMessage(handle, 0x7F, 1, 0); // WM_GETICON, ICON_BIG: taskbar/Alt+Tab.
            var small = SendMessage(handle, 0x7F, 0, 0); // WM_GETICON, ICON_SMALL: titlebar/system menu.
            Assert.That(large, Is.Not.EqualTo(nint.Zero)); Assert.That(small, Is.Not.EqualTo(nint.Zero));
            Assert.That(Pixels(large), Is.EqualTo(Pixels(expectedLarge)));
            Assert.That(Pixels(small), Is.EqualTo(Pixels(expectedSmall)));
            Assert.That(window.AppWindow.TitleBar.IconShowOptions, Is.EqualTo(Microsoft.UI.Windowing.IconShowOptions.ShowIconAndSystemMenu));
            if (int.TryParse(Environment.GetEnvironmentVariable("DMS_WINUI_ICON_REVIEW_SECONDS"), out var seconds))
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(seconds, 0, 45)));
        }
        finally
        {
            window.Close(); if (pending is not null) await pending;
            if (expectedLarge != 0) DestroyIcon(expectedLarge); if (expectedSmall != 0) DestroyIcon(expectedSmall);
        }
    });
    private static int[] Pixels(nint handle)
    {
        using var icon = System.Drawing.Icon.FromHandle(handle);
        using var bitmap = icon.ToBitmap();
        return Enumerable.Range(0, bitmap.Width * bitmap.Height).Select(index => bitmap.GetPixel(index % bitmap.Width, index / bitmap.Width).ToArgb()).ToArray();
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern uint ExtractIconEx(string file, int index, out nint large, out nint small, uint count);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint window, uint message, nint word, nint value);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
}
