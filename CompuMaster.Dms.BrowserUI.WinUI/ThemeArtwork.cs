using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace CompuMaster.Dms.BrowserUI.WinUI;

internal static class ThemeArtwork
{
    internal sealed record Pixels(byte[] Data, int Width, int Height);
    private static readonly Dictionary<string, Task<Pixels>> Decoded = new();
    private static readonly Dictionary<string, WriteableBitmap> Images = new();
    internal static Task<Pixels> ReadDarkAsync(string resource)
    {
        if (!Decoded.TryGetValue(resource, out var result)) Decoded[resource] = result = Task.Run(() => DecodeAsync(resource));
        return result;
    }
    internal static WriteableBitmap Image(string resource, Pixels pixels)
    {
        if (Images.TryGetValue(resource, out var cached)) return cached;
        // Create UI objects synchronously only while the requesting element remains loaded.
        var bitmap = new WriteableBitmap(pixels.Width, pixels.Height);
        using (var output = bitmap.PixelBuffer.AsStream()) output.Write(pixels.Data);
        bitmap.Invalidate(); Images[resource] = bitmap; return bitmap;
    }
    private static async Task<Pixels> DecodeAsync(string resource)
    {
        using var source = typeof(ThemeArtwork).Assembly.GetManifestResourceStream("Assets." + resource + ".png")!;
        using var stream = new InMemoryRandomAccessStream();
        using var input = new MemoryStream(); await source.CopyToAsync(input);
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        { writer.WriteBytes(input.ToArray()); await writer.StoreAsync(); }
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var data = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
        var pixels = data.DetachPixelData();
        for (var x = 0; x < pixels.Length; x += 4)
        {
            if (pixels[x + 3] > 0 && Math.Max(pixels[x], Math.Max(pixels[x + 1], pixels[x + 2])) < 96)
                pixels[x] = pixels[x + 1] = pixels[x + 2] = 230;
            for (var channel = 0; channel < 3; channel++) pixels[x + channel] = (byte)(pixels[x + channel] * pixels[x + 3] / 255);
        }
        return new Pixels(pixels, (int)decoder.PixelWidth, (int)decoder.PixelHeight);
    }
}
