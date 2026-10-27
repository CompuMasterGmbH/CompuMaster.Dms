using CompuMaster.Dms.BrowserUI.Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless.NUnit;
using global::Avalonia.Media;
using NUnit.Framework;

namespace CompuMaster.Dms.Test.BrowserUI.Avalonia;

[TestFixture]
public sealed class FileIconTests
{
    [TestCase("Report.PDF", ".pdf")]
    [TestCase("Data.csv", ".csv")]
    [TestCase("archive.tar.gz", ".gz")]
    [TestCase("no-extension", "")]
    public void Association_keys_use_only_normalized_extensions(string name, string expected) =>
        Assert.That(PlatformFileIcons.Extension(name), Is.EqualTo(expected));

    [AvaloniaTest]
    public async Task Windows_shell_icons_are_decoded_cached_and_keep_the_sharing_badge()
    {
        if (!OperatingSystem.IsWindows()) Assert.Ignore("The Shell32 backend requires Windows; the cross-platform probe checks the host backend separately.");
        var native = PlatformFileIcons.Read(".pdf", 32);
        Assert.That(native, Is.Not.Null);
        using var decoded = native!.Decode();
        Assert.That(decoded.PixelSize.Width, Is.EqualTo(32));
        var first = await PlatformFileIcons.GetAsync(".pdf", 32);
        var second = await PlatformFileIcons.GetAsync(".pdf", 32);
        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.SameAs(first));
        Assert.That(first!.PixelSize.Width, Is.EqualTo(32));
        Assert.That(PlatformFileIcons.Decorate(first, false), Is.SameAs(first));
        var decorated = (DrawingImage)PlatformFileIcons.Decorate(first, true);
        Assert.That(((DrawingGroup)decorated.Drawing!).Children.Count, Is.EqualTo(3));
    }

    [AvaloniaTest]
    public void Generic_icons_remain_available_without_a_native_backend()
    {
        var icon = new Image();
        PlatformFileIcons.Attach(icon, "no-extension", false);
        Assert.That(icon.Source, Is.Not.Null, "The UI must have a synchronous fallback while the native icon loads.");
        Assert.That(BrowserIcons.File(true), Is.Not.Null);
    }

    [Test]
    public void Freedesktop_lookup_honors_user_overrides_scale_inheritance_and_hicolor()
    {
        var directory = Path.Combine(Path.GetTempPath(), "unittests_dms_icon_theme_" + Guid.NewGuid().ToString("N"));
        Assert.That(Directory.Exists(directory), Is.False);
        Directory.CreateDirectory(directory);
        try
        {
            var user = Path.Combine(directory, "user"); var system = Path.Combine(directory, "system");
            void Write(string root, string relative, string data)
            {
                var path = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, data);
            }
            var theme = "[Icon Theme]\nDirectories=16x16/mimetypes,32x32/mimetypes\nScaledDirectories=32x32@2/mimetypes\nInherits=parent\n" +
                "[16x16/mimetypes]\nSize=16\nType=Fixed\n[32x32/mimetypes]\nSize=32\nType=Fixed\n[32x32@2/mimetypes]\nSize=32\nScale=2\nType=Fixed\n";
            Write(system, "current/index.theme", theme);
            Write(system, "current/16x16/mimetypes/application-pdf.png", "fixture");
            Write(system, "current/32x32/mimetypes/application-pdf.png", "fixture");
            Write(user, "current/32x32/mimetypes/application-pdf.png", "user-override");
            Write(system, "current/32x32@2/mimetypes/application-pdf.svg", "fixture");
            Write(system, "parent/index.theme", "[Icon Theme]\nDirectories=scalable/mimetypes\n[scalable/mimetypes]\nSize=48\nType=Scalable\nMinSize=16\nMaxSize=256\n");
            Write(system, "parent/scalable/mimetypes/text-csv.svg", "fixture");
            Write(system, "hicolor/index.theme", "[Icon Theme]\nDirectories=32x32/mimetypes\n[32x32/mimetypes]\nSize=32\nType=Fixed\n");
            Write(system, "hicolor/32x32/mimetypes/text-x-generic.png", "fixture");
            var lookup = new FreedesktopIconTheme(new[] { user, system }, "current");
            Assert.That(Path.GetFullPath(lookup.Find(new[] { "application-pdf" }, 32)!), Is.EqualTo(Path.GetFullPath(Path.Combine(user, "current/32x32/mimetypes/application-pdf.png"))));
            Assert.That(Path.GetFullPath(lookup.Find(new[] { "application-pdf" }, 64)!), Is.EqualTo(Path.GetFullPath(Path.Combine(system, "current/32x32@2/mimetypes/application-pdf.svg"))));
            Assert.That(Path.GetFullPath(lookup.Find(new[] { "text-csv" }, 64)!), Is.EqualTo(Path.GetFullPath(Path.Combine(system, "parent/scalable/mimetypes/text-csv.svg"))));
            Assert.That(Path.GetFullPath(lookup.Find(new[] { "not-installed", "text-x-generic" }, 32)!), Is.EqualTo(Path.GetFullPath(Path.Combine(system, "hicolor/32x32/mimetypes/text-x-generic.png"))));
            Assert.That(lookup.Find(new[] { "../../outside" }, 32), Is.Null);
            Assert.That(new FreedesktopIconTheme(new[] { system }, "../outside").Find(new[] { "missing" }, 32), Is.Null);
        }
        finally
        {
            var resolved = Path.GetFullPath(directory);
            Assert.That(resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase), Is.True);
            Directory.Delete(resolved, true);
        }
    }
}
