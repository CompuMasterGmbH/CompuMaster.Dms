using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using System.Runtime.Versioning;

namespace CompuMaster.Dms.BrowserUI.Avalonia;

[SupportedOSPlatform("windows")]
internal static class WindowsFileIcons
{
    private sealed record Request(string Extension, int Size, TaskCompletionSource<FileIconData?> Completion);
    private static readonly Lazy<BlockingCollection<Request>> Requests = new(() =>
    {
        var requests = new BlockingCollection<Request>(128);
        var worker = new Thread(() =>
        {
            var initialized = CoInitializeEx(IntPtr.Zero, 2) >= 0;
            try
            {
                foreach (var request in requests.GetConsumingEnumerable())
                {
                    try { request.Completion.TrySetResult(ReadCore(request.Extension, request.Size)); }
                    catch (Exception ex) { request.Completion.TrySetException(ex); }
                }
            }
            finally { if (initialized) CoUninitialize(); }
        }) { IsBackground = true, Name = "DMS file association icons" };
        worker.SetApartmentState(ApartmentState.STA); worker.Start();
        return requests;
    });
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        internal IntPtr Icon;
        internal int Index;
        internal uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] internal string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] internal string TypeName;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        internal uint HeaderSize;
        internal int Width, Height;
        internal ushort Planes, Bits;
        internal uint Compression, ImageSize;
        internal int XResolution, YResolution;
        internal uint Colors, ImportantColors;
    }
    internal static FileIconData? Read(string extension, int size)
    {
        var completion = new TaskCompletionSource<FileIconData?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!Requests.Value.TryAdd(new(extension, size, completion))) return null;
        try { return completion.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult(); }
        catch (TimeoutException) { return null; }
    }
    private static FileIconData? ReadCore(string extension, int size)
    {
        // SHGFI_USEFILEATTRIBUTES explicitly prevents opening the named file.
        // The synthetic filename cannot refer to a user's local or remote file.
        if (SHGetFileInfo("dms-file" + extension, 0x80, out var info, (uint)Marshal.SizeOf<ShellFileInfo>(), 0x100 | 0x10) == IntPtr.Zero || info.Icon == IntPtr.Zero)
            return null;
        IntPtr dc = IntPtr.Zero, dib = IntPtr.Zero, previous = IntPtr.Zero;
        try
        {
            dc = CreateCompatibleDC(IntPtr.Zero);
            if (dc == IntPtr.Zero) return null;
            var bitmapInfo = new BitmapInfo { HeaderSize = (uint)Marshal.SizeOf<BitmapInfo>(), Width = size, Height = -size, Planes = 1, Bits = 32 };
            dib = CreateDIBSection(dc, ref bitmapInfo, 0, out var pixels, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero || pixels == IntPtr.Zero) return null;
            var bytes = new byte[checked(size * size * 4)];
            Marshal.Copy(bytes, 0, pixels, bytes.Length);
            previous = SelectObject(dc, dib);
            if (previous == IntPtr.Zero || previous == new IntPtr(-1)) return null;
            if (!DrawIconEx(dc, 0, 0, info.Icon, size, size, 0, IntPtr.Zero, 3)) return null;
            GdiFlush();
            Marshal.Copy(pixels, bytes, 0, bytes.Length);
            // Legacy monochrome/mask icons do not carry an alpha channel. Read
            // their mask by drawing against white and recover an opaque glyph.
            if (!Enumerable.Range(0, size * size).Any(i => bytes[i * 4 + 3] != 0))
            {
                var white = Enumerable.Repeat((byte)255, bytes.Length).ToArray();
                Marshal.Copy(white, 0, pixels, white.Length);
                if (!DrawIconEx(dc, 0, 0, info.Icon, size, size, 0, IntPtr.Zero, 3)) return null;
                GdiFlush();
                Marshal.Copy(pixels, white, 0, white.Length);
                for (var i = 0; i < bytes.Length; i += 4)
                {
                    var transparent = white[i] == 255 && white[i + 1] == 255 && white[i + 2] == 255 && bytes[i] == 0 && bytes[i + 1] == 0 && bytes[i + 2] == 0;
                    bytes[i + 3] = transparent ? (byte)0 : (byte)255;
                }
            }
            return new FileIconData(bytes, size, size);
        }
        finally
        {
            if (previous != IntPtr.Zero && previous != new IntPtr(-1) && dc != IntPtr.Zero) SelectObject(dc, previous);
            if (dib != IntPtr.Zero) DeleteObject(dib);
            if (dc != IntPtr.Zero) DeleteDC(dc);
            DestroyIcon(info.Icon);
        }
    }
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint mode);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attributes, out ShellFileInfo info, uint size, uint flags);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DrawIconEx(IntPtr dc, int x, int y, IntPtr icon, int width, int height, uint step, IntPtr brush, uint flags);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr pixels, IntPtr section, uint offset);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GdiFlush();
}
