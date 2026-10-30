using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace CompuMaster.Dms.BrowserUI.WinUI;

internal static class ThemeArtwork
{
    private static readonly Dictionary<string, Task<WriteableBitmap>> Dark = new();
    internal static Task<WriteableBitmap> ForDarkAsync(string resource)
    {
        if (!Dark.TryGetValue(resource, out var result)) Dark[resource] = result = DecodeAsync(resource);
        return result;
    }
    private static async Task<WriteableBitmap> DecodeAsync(string resource)
    {
        using var source = typeof(ThemeArtwork).Assembly.GetManifestResourceStream("Assets." + resource + ".png")!;
        using var stream = new InMemoryRandomAccessStream();
        await source.CopyToAsync(stream.AsStreamForWrite()); stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var data = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
        var pixels = data.DetachPixelData();
        for (var x = 0; x < pixels.Length; x += 4)
        {
            if (pixels[x + 3] > 0 && Math.Max(pixels[x], Math.Max(pixels[x + 1], pixels[x + 2])) < 96)
                pixels[x] = pixels[x + 1] = pixels[x + 2] = 230;
            // WriteableBitmap expects premultiplied BGRA.
            for (var channel = 0; channel < 3; channel++) pixels[x + channel] = (byte)(pixels[x + channel] * pixels[x + 3] / 255);
        }
        var bitmap = new WriteableBitmap((int)decoder.PixelWidth, (int)decoder.PixelHeight);
        using var output = bitmap.PixelBuffer.AsStream(); await output.WriteAsync(pixels);
        bitmap.Invalidate(); return bitmap;
    }
}
