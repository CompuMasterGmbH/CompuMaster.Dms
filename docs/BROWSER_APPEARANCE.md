# Browser appearance

WinForms, Avalonia and WinUI support `BrowserTheme.System`, `Light` and `Dark`.
Appearance is controlled by the host API; browsers do not display a theme selector. Each browser has its own scope; changing it does not change the application-wide theme.

For Avalonia and WinUI, set initial options and change `browser.Browser.Appearance` afterwards:

```csharp
var options = new DmsBrowserOptions
{
    Theme = BrowserTheme.System,
    DarkPalette = new BrowserPalette(
        background: "#172334", surface: "#24364A", foreground: "#EDF2FA",
        accent: "#5399D5", accentForeground: "#081524", border: "#647A90")
};
var browser = new DmsBrowser(provider, options);
browser.Browser.Appearance = BrowserTheme.Dark;
```

`DmsBrowserControl` also exposes `Appearance`, `LightPalette` and `DarkPalette` for embedded browsers. Change these properties on the UI thread; changing the options object after construction is not a live configuration mechanism. Clear either palette with `null` to restore the host's resources for that mode. Each `BrowserPalette` is immutable and accepts six opaque `#RRGGBB` colors. Supply both palettes when both appearances require corporate colors. Hosts are responsible for contrast, including accent text, focus and disabled states.

WinForms exposes the same appearance and palette properties directly on `DmsBrowser`:

```vb
browser.Appearance = BrowserTheme.Dark
browser.DarkPalette = New BrowserPalette(
    "#172334", "#24364A", "#EDF2FA", "#5399D5", "#081524", "#647A90")
browser.DarkPalette = Nothing 'Restore the standard dark palette.
```

`BrowserAppearance.SetTheme(root, mode)`, `SetPalettes(root, light, dark)` and `CreateSelector(root)` are available for host-owned surfaces such as login forms. Avalonia roots are `StyledElement`, WinUI roots are `FrameworkElement`, and WinForms roots are `Control`. Use the owning window/content root when it should also control owned windows. Ready-to-use browser windows propagate appearance and palette updates to their workflow dialogs. Their standard input, properties, sharing, instance, versions, conflict and transfer surfaces use the same scope. Platform file pickers and Windows system message boxes retain operating-system appearance.

System mode inherits the host theme for embedded controls. Standalone Avalonia/WinUI windows use the application's system preference; WinForms reads Windows' application color preference and responds to preference notifications. WinForms owned dialogs inherit their owner while retaining explicit overrides. Native high contrast takes precedence over custom palettes on Windows. Avalonia hosts retain their theme/accessibility setup; the library does not replace the application's Fluent theme. The native widget frameworks retain their own geometry, effects and state rendering, so a common palette does not promise pixel-identical output.

The bundled action artwork keeps its accent colors and alpha; dark mode lightens dark strokes at runtime without rewriting source images. Duplicate-name warnings retain their semantic foreground/background. Credentials and palette preferences are separate: demo credentials are never rewritten when changing appearance, and theme choices are not persisted in credential caches.

Offline verification uses test providers. Native Windows checks and headless Avalonia checks do not establish native Linux/macOS visual acceptance or remote DMS integration acceptance.
