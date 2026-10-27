using System.Runtime.InteropServices;

namespace CompuMaster.Dms.BrowserUI.Avalonia;

internal static class LinuxFileIcons
{
    private const string Gio = "libgio-2.0.so.0", Glib = "libglib-2.0.so.0", GObject = "libgobject-2.0.so.0";
    internal static FileIconData? Read(string extension, int size)
    {
        var names = new List<string>();
        var contentType = g_content_type_guess("dms-file" + extension, IntPtr.Zero, 0, out _);
        if (contentType == IntPtr.Zero) return null;
        IntPtr icon = IntPtr.Zero, generic = IntPtr.Zero;
        try
        {
            icon = g_content_type_get_icon(contentType);
            if (icon != IntPtr.Zero && g_type_check_instance_is_a(icon, g_themed_icon_get_type()) != 0)
            {
                var array = g_themed_icon_get_names(icon);
                for (var i = 0; array != IntPtr.Zero && i < 32; i++)
                {
                    var value = Marshal.ReadIntPtr(array, i * IntPtr.Size);
                    if (value == IntPtr.Zero) break;
                    if (Marshal.PtrToStringUTF8(value) is { } name) names.Add(name);
                }
            }
            generic = g_content_type_get_generic_icon_name(contentType);
            if (generic != IntPtr.Zero && Marshal.PtrToStringUTF8(generic) is { } fallback) names.Add(fallback);
            names.Add("text-x-generic");
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Path.Combine(home, ".config");
            var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(home, ".local", "share");
            var dataDirs = (Environment.GetEnvironmentVariable("XDG_DATA_DIRS") ?? "/usr/local/share:/usr/share").Split(':', StringSplitOptions.RemoveEmptyEntries);
            var roots = new[] { Path.Combine(home, ".icons"), Path.Combine(dataHome, "icons") }
                .Concat(dataDirs.Select(path => Path.Combine(path, "icons"))).ToArray();
            var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "";
            var theme = desktop.Contains("KDE", StringComparison.OrdinalIgnoreCase)
                ? FreedesktopIconTheme.Setting(Path.Combine(config, "kdeglobals"), "Icons", "Theme") : null;
            theme ??= FreedesktopIconTheme.Setting(Path.Combine(config, "gtk-3.0", "settings.ini"), "Settings", "gtk-icon-theme-name");
            theme ??= ReadGnomeTheme();
            var file = new FreedesktopIconTheme(roots, theme ?? "Adwaita", dataDirs.Select(path => Path.Combine(path, "pixmaps")))
                .Find(names, size);
            if (file is null) return null;
            if (Path.GetExtension(file) == ".svg") return LinuxSvgIcon.Read(file, size);
            return new FileInfo(file).Length <= 8 * 1024 * 1024 ? new FileIconData(File.ReadAllBytes(file), TargetSize: size) : null;
        }
        finally
        {
            if (generic != IntPtr.Zero) g_free(generic);
            if (icon != IntPtr.Zero) g_object_unref(icon);
            g_free(contentType);
        }
    }
    private static string? ReadGnomeTheme()
    {
        var source = g_settings_schema_source_get_default();
        if (source == IntPtr.Zero) return null;
        var schema = g_settings_schema_source_lookup(source, "org.gnome.desktop.interface", 1);
        if (schema == IntPtr.Zero) return null;
        IntPtr settings = IntPtr.Zero, value = IntPtr.Zero;
        try
        {
            if (g_settings_schema_has_key(schema, "icon-theme") == 0) return null;
            settings = g_settings_new_full(schema, IntPtr.Zero, IntPtr.Zero);
            if (settings == IntPtr.Zero) return null;
            value = g_settings_get_string(settings, "icon-theme");
            return value == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(value);
        }
        finally
        {
            if (value != IntPtr.Zero) g_free(value);
            if (settings != IntPtr.Zero) g_object_unref(settings);
            g_settings_schema_unref(schema);
        }
    }
    [DllImport(Gio)] private static extern IntPtr g_content_type_guess([MarshalAs(UnmanagedType.LPUTF8Str)] string filename, IntPtr data, nuint size, out int uncertain);
    [DllImport(Gio)] private static extern IntPtr g_content_type_get_icon(IntPtr type);
    [DllImport(Gio)] private static extern IntPtr g_content_type_get_generic_icon_name(IntPtr type);
    [DllImport(Gio)] private static extern IntPtr g_themed_icon_get_names(IntPtr icon);
    [DllImport(Gio)] private static extern nuint g_themed_icon_get_type();
    [DllImport(GObject)] private static extern int g_type_check_instance_is_a(IntPtr instance, nuint type);
    [DllImport(Glib)] private static extern void g_free(IntPtr value);
    [DllImport(GObject)] internal static extern void g_object_unref(IntPtr value);
    [DllImport(Glib)] internal static extern void g_error_free(IntPtr value);
    [DllImport(Gio)] private static extern IntPtr g_settings_schema_source_get_default();
    [DllImport(Gio)] private static extern IntPtr g_settings_schema_source_lookup(IntPtr source, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int recursive);
    [DllImport(Gio)] private static extern int g_settings_schema_has_key(IntPtr schema, [MarshalAs(UnmanagedType.LPUTF8Str)] string key);
    [DllImport(Gio)] private static extern IntPtr g_settings_new_full(IntPtr schema, IntPtr backend, IntPtr path);
    [DllImport(Gio)] private static extern IntPtr g_settings_get_string(IntPtr settings, [MarshalAs(UnmanagedType.LPUTF8Str)] string key);
    [DllImport(Gio)] private static extern void g_settings_schema_unref(IntPtr schema);
}

internal static class LinuxSvgIcon
{
    [StructLayout(LayoutKind.Sequential)] private struct Viewport { internal double X, Y, Width, Height; }
    internal static FileIconData? Read(string path, int size)
    {
        if (!BitConverter.IsLittleEndian) return null;
        IntPtr error = IntPtr.Zero, handle = IntPtr.Zero, surface = IntPtr.Zero, context = IntPtr.Zero;
        try
        {
            handle = rsvg_handle_new_from_file(path, out error);
            if (handle == IntPtr.Zero) return null;
            if (error != IntPtr.Zero) { LinuxFileIcons.g_error_free(error); error = IntPtr.Zero; }
            surface = cairo_image_surface_create(0, size, size);
            if (surface == IntPtr.Zero || cairo_surface_status(surface) != 0) return null;
            context = cairo_create(surface);
            if (context == IntPtr.Zero) return null;
            var viewport = new Viewport { Width = size, Height = size };
            if (rsvg_handle_render_document(handle, context, ref viewport, out error) == 0) return null;
            cairo_surface_flush(surface);
            var pixels = cairo_image_surface_get_data(surface);
            var stride = cairo_image_surface_get_stride(surface);
            if (pixels == IntPtr.Zero || stride < size * 4) return null;
            var bytes = new byte[size * size * 4];
            for (var row = 0; row < size; row++) Marshal.Copy(pixels + row * stride, bytes, row * size * 4, size * 4);
            return new FileIconData(bytes, size, size);
        }
        finally
        {
            if (context != IntPtr.Zero) cairo_destroy(context);
            if (surface != IntPtr.Zero) cairo_surface_destroy(surface);
            if (handle != IntPtr.Zero) LinuxFileIcons.g_object_unref(handle);
            if (error != IntPtr.Zero) LinuxFileIcons.g_error_free(error);
        }
    }
    [DllImport("librsvg-2.so.2")] private static extern IntPtr rsvg_handle_new_from_file([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr error);
    [DllImport("librsvg-2.so.2")] private static extern int rsvg_handle_render_document(IntPtr handle, IntPtr context, ref Viewport viewport, out IntPtr error);
    [DllImport("libcairo.so.2")] private static extern IntPtr cairo_image_surface_create(int format, int width, int height);
    [DllImport("libcairo.so.2")] private static extern IntPtr cairo_create(IntPtr surface);
    [DllImport("libcairo.so.2")] private static extern int cairo_surface_status(IntPtr surface);
    [DllImport("libcairo.so.2")] private static extern void cairo_surface_flush(IntPtr surface);
    [DllImport("libcairo.so.2")] private static extern IntPtr cairo_image_surface_get_data(IntPtr surface);
    [DllImport("libcairo.so.2")] private static extern int cairo_image_surface_get_stride(IntPtr surface);
    [DllImport("libcairo.so.2")] private static extern void cairo_destroy(IntPtr context);
    [DllImport("libcairo.so.2")] private static extern void cairo_surface_destroy(IntPtr surface);
}
