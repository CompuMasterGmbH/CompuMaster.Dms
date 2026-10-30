#if DMS_WINUI
namespace CompuMaster.Dms.BrowserUI.WinUI;
#else
namespace CompuMaster.Dms.BrowserUI.Avalonia;
#endif

/// <summary>Defines an immutable set of opaque appearance colors for a light or dark theme.</summary>
/// <remarks>Colors use #RRGGBB notation. Hosts are responsible for readable contrast. Platform high contrast takes precedence.</remarks>
public sealed class BrowserPalette
{
    /// <summary>Creates a palette using opaque hexadecimal colors.</summary>
    /// <param name="background">The window background.</param>
    /// <param name="surface">The background of lists, fields and buttons.</param>
    /// <param name="foreground">The normal text color.</param>
    /// <param name="accent">The accent and selection background.</param>
    /// <param name="accentForeground">The text color on accent and selected backgrounds.</param>
    /// <param name="border">The control border color.</param>
    /// <exception cref="ArgumentException">A color does not use #RRGGBB notation.</exception>
    public BrowserPalette(string background, string surface, string foreground, string accent, string accentForeground, string border)
    {
        Background = Validate(background); Surface = Validate(surface); Foreground = Validate(foreground);
        Accent = Validate(accent); AccentForeground = Validate(accentForeground); Border = Validate(border);
    }
    private static string Validate(string color) => color is not null && System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$")
        ? color : throw new ArgumentException("Colors must use #RRGGBB notation.", nameof(color));
    /// <summary>Gets the window background.</summary>
    public string Background { get; }
    /// <summary>Gets the background of lists, fields and buttons.</summary>
    public string Surface { get; }
    /// <summary>Gets the normal text color.</summary>
    public string Foreground { get; }
    /// <summary>Gets the accent and selection background.</summary>
    public string Accent { get; }
    /// <summary>Gets the text color on accent backgrounds.</summary>
    public string AccentForeground { get; }
    /// <summary>Gets the control border color.</summary>
    public string Border { get; }
}
