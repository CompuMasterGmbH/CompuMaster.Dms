using System.Runtime.InteropServices;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform;
using global::Avalonia.Threading;

namespace CompuMaster.Dms.BrowserUI.Avalonia;

internal sealed record FileIconData(byte[] Data, int Width = 0, int Height = 0, int TargetSize = 64)
{
    internal Bitmap Decode()
    {
        if (Width == 0)
        {
            using var stream = new MemoryStream(Data, writable: false);
            return Bitmap.DecodeToWidth(stream, TargetSize, BitmapInterpolationMode.HighQuality);
        }
        var bitmap = new WriteableBitmap(new PixelSize(Width, Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var frame = bitmap.Lock();
        for (var row = 0; row < Height; row++) Marshal.Copy(Data, row * Width * 4, frame.Address + row * frame.RowBytes, Width * 4);
        return bitmap;
    }
}

// Only association/type metadata is queried. No DMS resource is downloaded and no
// temporary placeholder file is created. Optional native libraries fail closed to
// the existing application artwork, independently of all DMS operations.
internal static class PlatformFileIcons
{
    private const int Capacity = 128;
    private static readonly object Gate = new();
    private static readonly Dictionary<(string Extension, int Size), Task<Bitmap?>> Cache = new();
    private static readonly Queue<(string Extension, int Size)> Order = new();
    private static readonly SemaphoreSlim Readers = new(4);
    internal static string Extension(string name)
    {
        try
        {
            var value = Path.GetExtension(name).ToLowerInvariant();
            return value.Length <= 64 && !value.Any(char.IsControl) ? value : "";
        }
        catch (ArgumentException) { return ""; }
    }
    internal static FileIconData? Read(string extension, int size)
    {
        try
        {
            if (OperatingSystem.IsWindows()) return WindowsFileIcons.Read(extension, size);
            if (OperatingSystem.IsMacOS()) return MacFileIcons.Read(extension, size);
            if (OperatingSystem.IsLinux()) return LinuxFileIcons.Read(extension, size);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or
            IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or InvalidOperationException)
        { /* Native icons are optional; file browsing must remain available. */ }
        return null;
    }
    private static async Task<Bitmap?> LoadAsync(string extension, int size)
    {
        await Readers.WaitAsync();
        try
        {
            // AppKit belongs on the native main thread; Windows Shell and Linux
            // MIME/theme lookups run off the UI thread, as required by Shell32.
            var data = OperatingSystem.IsMacOS()
                ? await Dispatcher.UIThread.InvokeAsync(() => Read(extension, size))
                : await Task.Run(() => Read(extension, size));
            return data?.Decode();
        }
        catch (Exception) { return null; }
        finally { Readers.Release(); }
    }
    internal static Task<Bitmap?> GetAsync(string extension, int size)
    {
        lock (Gate)
        {
            var key = (extension, size);
            if (Cache.TryGetValue(key, out var existing)) return existing;
            while (Cache.Count >= Capacity && Order.TryDequeue(out var oldest)) Cache.Remove(oldest);
            var loading = LoadAsync(extension, size);
            Cache.Add(key, loading); Order.Enqueue(key);
            return loading;
        }
    }
    internal static void Attach(Image target, string name, bool shared)
    {
        target.Source = BrowserIcons.File(shared);
        var weak = new WeakReference<Image>(target);
        var extension = Extension(name);
        async Task UpdateAsync(int size)
        {
            try
            {
                var bitmap = await GetAsync(extension, size);
                if (bitmap is null) return;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (weak.TryGetTarget(out var image)) image.Source = Decorate(bitmap, shared);
                });
            }
            catch (OperationCanceledException) { }
            catch (InvalidOperationException) { /* The host may already be shutting down. */ }
        }
        void Request(Image image)
        {
            var scaling = TopLevel.GetTopLevel(image)?.RenderScaling ?? 1;
            _ = UpdateAsync(scaling > 2 ? 96 : scaling > 1 ? 48 : 32);
        }
        if (TopLevel.GetTopLevel(target) is not null) Request(target);
        else
        {
            EventHandler<VisualTreeAttachmentEventArgs>? attached = null;
            attached = (sender, _) =>
            {
                var image = (Image)sender!; image.AttachedToVisualTree -= attached; Request(image);
            };
            target.AttachedToVisualTree += attached;
        }
    }
    internal static IImage Decorate(IImage native, bool shared)
    {
        if (!shared) return native;
        // Same existing sharing artwork/backplate as the WinForms compositor.
        var drawing = new DrawingGroup();
        drawing.Children.Add(new ImageDrawing { ImageSource = native, Rect = new Rect(0, 0, 24, 24) });
        drawing.Children.Add(new GeometryDrawing
        {
            Brush = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)),
            Pen = new Pen(new SolidColorBrush(Color.FromArgb(180, 160, 160, 160)), 1),
            Geometry = new EllipseGeometry(new Rect(7, 7, 16, 16))
        });
        drawing.Children.Add(new ImageDrawing { ImageSource = BrowserIcons.Load("sharing"), Rect = new Rect(9, 9, 12, 12) });
        return new DrawingImage(drawing);
    }
}
