using System.Runtime.InteropServices;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform;

namespace CompuMaster.Dms.BrowserUI.Avalonia;

internal static class ThemeArtwork
{
    private static readonly Dictionary<string, Bitmap> Dark = new();
    internal static Bitmap ForDark(string resource)
    {
        if (Dark.TryGetValue(resource, out var cached)) return cached;
        var original = (Bitmap)BrowserIcons.Load(resource);
        var result = new WriteableBitmap(original.PixelSize, original.Dpi, PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using var buffer = result.Lock();
        original.CopyPixels(buffer);
        var row = new byte[buffer.RowBytes];
        for (var y = 0; y < buffer.Size.Height; y++)
        {
            Marshal.Copy(buffer.Address + y * buffer.RowBytes, row, 0, row.Length);
            for (var x = 0; x < buffer.Size.Width * 4; x += 4)
                if (row[x + 3] > 0 && Math.Max(row[x], Math.Max(row[x + 1], row[x + 2])) < 96)
                    row[x] = row[x + 1] = row[x + 2] = 230;
            Marshal.Copy(row, 0, buffer.Address + y * buffer.RowBytes, row.Length);
        }
        Dark[resource] = result;
        return result;
    }
}
