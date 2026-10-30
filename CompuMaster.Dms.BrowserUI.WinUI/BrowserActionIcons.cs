using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace CompuMaster.Dms.BrowserUI.WinUI;

// Uses the same artwork and action assignments as the WinForms browser.
internal static class BrowserActionIcons
{
    internal static Image? Create(string key, bool folderMenu = false, bool menu = false)
    {
        var resource = key switch
        {
            "ActionUpload" or "ActionUploadFolder" => "iconfinder_Upload-ui-ux-mobile-web_4960752",
            "ActionDownload" or "ActionDownloadFolder" => "iconfinder_Download-ui-ux-mobile-web_4960707",
            "ActionOpen" => "iconfinder_View-ui-ux-mobile-web_4960757",
            "ActionCopy" => "iconfinder_Duplicate-ui-ux-mobile-web_4960708",
            "ActionRename" => "iconfinder_Rename-ui-ux-mobile-web_4960746",
            "ActionMove" => "iconfinder_Arrow-ui-ux-mobile-web_4960722",
            "ActionDelete" => "iconfinder_Trash-ui-ux-mobile-web_4960750",
            "ActionFileSharings" or "ActionFolderSharings" => "iconfinder_Group-ui-ux-mobile-web_4960717",
            "FileProperties" or "FolderProperties" => "iconfinder_setting-ui-ux-mobile-web_4960747",
            "FileVersionHistory" => "iconfinder_Clock-ui-ux-mobile-web_4960701",
            "ActionClose" => "iconfinder_Export-ui-ux-mobile-web_4960710",
            "ActionRefreshFiles" => folderMenu ? "iconfinder_View-ui-ux-mobile-web_4960757" : "Refresh",
            "ActionCreateFolder" => "iconfinder_Folder-ui-ux-mobile-web_new",
            _ => null
        };
        if (resource is null) return null;
        var image = new Image
        {
            Source = BrowserIcons.Load(resource), Width = 16, Height = 16,
            VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false, Tag = resource
        };
        AttachTheme(image, resource, source => image.Source = source);
        return image;
    }
    internal static void ApplyMenu(MenuFlyoutItem item, Image image)
    {
        var icon = new ImageIcon { Source = image.Source }; item.Icon = icon;
        AttachTheme(item, (string)image.Tag, source => icon.Source = source);
    }
    internal static void AttachTheme(FrameworkElement element, string resource, Action<Microsoft.UI.Xaml.Media.ImageSource> apply, Func<string, Task<ThemeArtwork.Pixels>>? readDark = null)
    {
        var loaded = element.IsLoaded;
        var generation = 0;
        async void Update()
        {
            if (!loaded || !element.IsLoaded) return;
            var request = ++generation;
            var dark = element.ActualTheme == ElementTheme.Dark;
            ThemeArtwork.Pixels? pixels;
            try { pixels = dark ? await (readDark ?? ThemeArtwork.ReadDarkAsync)(resource) : null; }
            catch (System.Runtime.InteropServices.COMException) when (!loaded) { return; }
            // Unloading invalidates outstanding requests without touching a closed native element.
            if (!loaded || generation != request || !element.IsLoaded) return;
            apply(pixels is null ? BrowserIcons.Load(resource) : ThemeArtwork.Image(resource, pixels));
        }
        element.ActualThemeChanged += (_, _) => Update();
        element.Loaded += (_, _) => { loaded = true; Update(); };
        element.Unloaded += (_, _) => { loaded = false; generation++; };
    }

    internal static void Apply(Button button, string key)
    {
        var caption = Dialogs.Caption(key);
        AutomationProperties.SetName(button, caption);
        if (Create(key) is not { } icon) return;
        button.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 6,
            Children = { icon, new TextBlock { Text = caption, VerticalAlignment = VerticalAlignment.Center } }
        };
        button.RegisterPropertyChangedCallback(Control.IsEnabledProperty, (_, _) => icon.Opacity = button.IsEnabled ? 1 : 0.4);
    }
}
