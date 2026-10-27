using System.Collections.Concurrent;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace CompuMaster.Dms.BrowserUI.WinUI;

internal sealed record FileIconData(byte[] Data, int Width = 0, int Height = 0, int TargetSize = 64);
internal static class BrowserIcons
{
    private static readonly Dictionary<string, ImageSource> Artwork = new();
    private static readonly ConcurrentDictionary<string, Task<FileIconData?>> Associations = new(StringComparer.OrdinalIgnoreCase);
    internal static ImageSource Load(string name)
    {
        if (Artwork.TryGetValue(name, out var image)) return image;
        using var source = typeof(BrowserIcons).Assembly.GetManifestResourceStream("Assets." + name + ".png")
            ?? throw new InvalidDataException("Missing browser artwork: " + name);
        using var input = new MemoryStream(); source.CopyTo(input);
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(input.ToArray()); writer.StoreAsync().AsTask().GetAwaiter().GetResult(); }
        stream.Seek(0); var bitmap = new BitmapImage(); bitmap.SetSource(stream); Artwork[name] = bitmap; return bitmap;
    }
    internal static FrameworkElement DirectoryRow(BrowserNode node)
    {
        var displayName = node.Parent is null && string.IsNullOrEmpty(node.Name) ? "/" : node.Name;
        var name = node.Parent is null ? "iconfinder_Home-ui-ux-mobile-web_4960719" :
            node.Item?.ItemType == Data.DmsResourceItem.ItemTypes.Collection ? "iconfinder_bookmark-ui-ux-mobile-web_4960727" : "iconfinder_Folder-ui-ux-mobile-web_4960713";
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children =
        { new Image { Source = Load(name + (node.Shared ? " - Shared" : "")), Width = 24, Height = 24 }, new TextBlock { Text = displayName, VerticalAlignment = VerticalAlignment.Center } } };
        row.Loaded += (_, _) =>
        {
            DependencyObject? parent = row;
            while (parent is not null && parent is not TreeViewItem) parent = VisualTreeHelper.GetParent(parent);
            if (parent is TreeViewItem item) Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, displayName);
        };
        return row;
    }
    internal static FrameworkElement FileRow(FileRow row)
    {
        var image = new Image { Width = 24, Height = 24, Source = Load("iconfinder_Document-ui-ux-mobile-web-office-microsoftofficeico_4960706") };
        var icon = new Grid { Width = 24, Height = 24 }; icon.Children.Add(image);
        if (row.Shared) icon.Children.Add(new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Colors.White), BorderBrush = new SolidColorBrush(Colors.Gray), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
            Child = new Image { Source = Load("sharing"), Width = 12, Height = 12 } });
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { icon, new TextBlock { Text = row.Name, VerticalAlignment = VerticalAlignment.Center } } };
        if (row.Duplicate) { panel.Background = new SolidColorBrush(Colors.Firebrick); panel.Children.OfType<TextBlock>().Single().Foreground = new SolidColorBrush(Colors.White); ToolTipService.SetToolTip(panel, UiText.Get("DuplicateFileNameConflict")); }
        else if (row.Shared) ToolTipService.SetToolTip(panel, Dialogs.Caption("ActionSharings"));
        image.Loaded += async (_, _) =>
        {
            try
            {
                var extension = Path.GetExtension(row.Name).ToLowerInvariant();
                if (extension.Length > 32) extension = "";
                if (Associations.Count >= 128 && !Associations.ContainsKey(extension)) return;
                var data = await Associations.GetOrAdd(extension, value => Task.Run(() => WindowsFileIcons.Read(value, 48)));
                if (data is null) return;
                var bitmap = new WriteableBitmap(data.Width, data.Height);
                using (var stream = bitmap.PixelBuffer.AsStream()) stream.Write(data.Data);
                bitmap.Invalidate(); image.Source = bitmap;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Runtime.InteropServices.COMException) { }
        };
        return panel;
    }
}
