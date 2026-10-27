using System.Runtime.InteropServices;

namespace CompuMaster.Dms.BrowserUI.Avalonia;

internal static class MacFileIcons
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    private static readonly Lazy<bool> Loaded = new(() => NativeLibrary.TryLoad("/System/Library/Frameworks/AppKit.framework/AppKit", out _));
    private static readonly Lazy<bool> ModernTypes = new(() => NativeLibrary.TryLoad("/System/Library/Frameworks/UniformTypeIdentifiers.framework/UniformTypeIdentifiers", out _));
    internal static FileIconData? Read(string extension, int size)
    {
        // Headless test workers do not own the Cocoa main thread. They retain
        // the fallback; native desktop apps call this from their UI/main thread.
        if (pthread_main_np() == 0 || !Loaded.Value) return null;
        var pool = Send(objc_getClass("NSAutoreleasePool"), Selector("new"));
        try
        {
            var workspace = Send(objc_getClass("NSWorkspace"), Selector("sharedWorkspace"));
            var text = SendString(objc_getClass("NSString"), Selector("stringWithUTF8String:"), extension.TrimStart('.'));
            IntPtr icon = IntPtr.Zero;
            if (OperatingSystem.IsMacOSVersionAtLeast(11) && ModernTypes.Value)
            {
                var type = SendArg(objc_getClass("UTType"), Selector("typeWithFilenameExtension:"), text);
                if (type != IntPtr.Zero) icon = SendArg(workspace, Selector("iconForContentType:"), type);
            }
            if (icon == IntPtr.Zero) icon = SendArg(workspace, Selector("iconForFileType:"), text);
            if (icon == IntPtr.Zero) return null;
            var tiff = Send(icon, Selector("TIFFRepresentation"));
            var representation = SendArg(objc_getClass("NSBitmapImageRep"), Selector("imageRepWithData:"), tiff);
            if (representation == IntPtr.Zero) return null;
            var png = SendFormat(representation, Selector("representationUsingType:properties:"), 4, IntPtr.Zero);
            if (png == IntPtr.Zero) return null;
            var length = Length(png, Selector("length"));
            if (length == 0 || length > 8 * 1024 * 1024) return null;
            var bytes = new byte[(int)length];
            Marshal.Copy(Send(png, Selector("bytes")), bytes, 0, bytes.Length);
            return new FileIconData(bytes, TargetSize: size);
        }
        finally { if (pool != IntPtr.Zero) Send(pool, Selector("drain")); }
    }
    private static IntPtr Selector(string name) => sel_registerName(name);
    [DllImport("/usr/lib/libSystem.B.dylib")] private static extern int pthread_main_np();
    [DllImport(ObjC)] private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport(ObjC)] private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr receiver, IntPtr selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr SendArg(IntPtr receiver, IntPtr selector, IntPtr value);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr SendString(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr SendFormat(IntPtr receiver, IntPtr selector, nuint format, IntPtr properties);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nuint Length(IntPtr receiver, IntPtr selector);
}
