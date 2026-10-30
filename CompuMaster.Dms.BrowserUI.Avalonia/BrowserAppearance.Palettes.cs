using System.Runtime.CompilerServices;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Styling;

namespace CompuMaster.Dms.BrowserUI.Avalonia;

public static partial class BrowserAppearance
{
    private static readonly ConditionalWeakTable<StyledElement, PaletteScope> Palettes = new();
    private sealed class PaletteScope
    {
        internal ResourceDictionary Resources = new();
        internal BrowserPalette? Light, Dark;
        internal event Action? Changed;
        internal void Notify() => Changed?.Invoke();
    }
    /// <summary>Sets optional light and dark palettes within a local appearance scope.</summary>
    /// <param name="root">The root controlled by the palettes.</param>
    /// <param name="light">The light palette, or null to restore the host's light resources.</param>
    /// <param name="dark">The dark palette, or null to restore the host's dark resources.</param>
    /// <remarks>Call on the UI thread. Existing application resources are preserved.</remarks>
    public static void SetPalettes(StyledElement root, BrowserPalette? light, BrowserPalette? dark)
    {
        ArgumentNullException.ThrowIfNull(root);
        var scope = Palettes.GetValue(root, _ => new());
        root.Resources.MergedDictionaries.Remove(scope.Resources);
        scope.Resources = new(); scope.Light = light; scope.Dark = dark;
        var keys = new[] { ThemeVariant.Light, ThemeVariant.Dark };
        if (light is not null) scope.Resources.ThemeDictionaries[keys[0]] = Colors(light);
        if (dark is not null) scope.Resources.ThemeDictionaries[keys[1]] = Colors(dark);
        if (light is not null || dark is not null) root.Resources.MergedDictionaries.Add(scope.Resources);
        scope.Notify();
    }
    private static void CopyPalettes(StyledElement source, StyledElement target, Action<Action> onClosed, Func<(BrowserPalette? Light, BrowserPalette? Dark)>? overrides = null)
    {
        var scope = Palettes.GetValue(source, _ => new());
        void Update()
        {
            var own = overrides?.Invoke() ?? (null, null);
            SetPalettes(target, own.Item1 ?? scope.Light, own.Item2 ?? scope.Dark);
        }
        Update(); scope.Changed += Update; onClosed(() => scope.Changed -= Update);
    }
    private static ResourceDictionary Colors(BrowserPalette palette)
    {
        var resources = new ResourceDictionary();
        void Brush(string key, string value) => resources[key] = new SolidColorBrush(Color.Parse(value));
        foreach (var key in new[] { "SystemRegionBrush", "ApplicationPageBackgroundThemeBrush", "SystemControlPageBackgroundChromeLowBrush", "SystemControlBackgroundChromeLowBrush" }) Brush(key, palette.Background);
        foreach (var key in new[] { "ControlFillColorDefaultBrush", "ControlFillColorSecondaryBrush", "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused", "ComboBoxBackground", "ButtonBackground", "ButtonBackgroundPointerOver", "MenuFlyoutPresenterBackground", "SystemControlBackgroundChromeMediumLowBrush", "SystemControlBackgroundAltHighBrush", "SystemControlBackgroundListLowBrush", "DataGridBackground", "DataGridRowBackground", "DataGridColumnHeaderBackgroundBrush" }) Brush(key, palette.Surface);
        foreach (var key in new[] { "ApplicationForegroundThemeBrush", "TextFillColorPrimaryBrush", "SystemControlForegroundBaseHighBrush", "TextControlForeground", "TextControlForegroundFocused", "TextControlForegroundPointerOver", "ButtonForeground", "ButtonForegroundPointerOver", "ComboBoxForeground", "MenuFlyoutItemForeground", "DataGridRowForeground", "DataGridColumnHeaderForegroundBrush" }) Brush(key, palette.Foreground);
        foreach (var key in new[] { "AccentFillColorDefaultBrush", "SystemControlHighlightAccentBrush", "SystemControlHighlightListAccentLowBrush", "SystemControlHighlightListAccentMediumBrush", "SystemControlHighlightListAccentHighBrush", "ListViewItemBackgroundSelected", "ListViewItemBackgroundSelectedPointerOver", "TextControlSelectionHighlightColor", "DataGridRowSelectedBackground" }) Brush(key, palette.Accent);
        foreach (var key in new[] { "TextOnAccentFillColorPrimaryBrush", "SystemControlHighlightAltBaseHighBrush", "ListViewItemForegroundSelected", "DataGridRowSelectedForeground" }) Brush(key, palette.AccentForeground);
        foreach (var key in new[] { "ControlStrokeColorDefaultBrush", "SystemControlForegroundBaseMediumLowBrush", "TextControlBorderBrush", "ComboBoxBorderBrush", "ButtonBorderBrush", "DataGridGridLinesBrush" }) Brush(key, palette.Border);
        resources["SystemAccentColor"] = Color.Parse(palette.Accent);
        return resources;
    }
}
