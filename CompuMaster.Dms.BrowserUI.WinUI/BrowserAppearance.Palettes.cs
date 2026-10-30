using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CompuMaster.Dms.BrowserUI.WinUI;

public static partial class BrowserAppearance
{
    private static readonly ConditionalWeakTable<FrameworkElement, PaletteScope> Palettes = new();
    private sealed class PaletteScope
    {
        internal ResourceDictionary Resources = new();
        internal BrowserPalette? Light, Dark;
        internal bool Bound, Applying, LastContrast;
        internal Microsoft.UI.Dispatching.DispatcherQueueTimer? ContrastTimer;
        internal readonly Windows.UI.ViewManagement.AccessibilitySettings Accessibility = new();
        internal readonly Dictionary<DependencyObject, Dictionary<DependencyProperty, object>> Originals = new();
        internal event Action? Changed;
        internal void Notify() => Changed?.Invoke();
    }
    /// <summary>Sets optional light and dark palettes within a local appearance scope.</summary>
    /// <param name="root">The root controlled by the palettes.</param>
    /// <param name="light">The light palette, or null to restore the host's light resources.</param>
    /// <param name="dark">The dark palette, or null to restore the host's dark resources.</param>
    /// <remarks>Call on the UI thread. Existing application resources are preserved.</remarks>
    public static void SetPalettes(FrameworkElement root, BrowserPalette? light, BrowserPalette? dark)
    {
        ArgumentNullException.ThrowIfNull(root);
        var scope = Palettes.GetValue(root, _ => new());
        root.Resources.MergedDictionaries.Remove(scope.Resources);
        scope.Resources = new(); scope.Light = light; scope.Dark = dark;
        var keys = new[] { "Light", "Dark" };
        if (light is not null) scope.Resources.ThemeDictionaries[keys[0]] = Colors(light);
        if (dark is not null) scope.Resources.ThemeDictionaries[keys[1]] = Colors(dark);
        if (light is not null || dark is not null) root.Resources.MergedDictionaries.Add(scope.Resources);
        if (!scope.Bound)
        {
            scope.Bound = true;
            scope.ContrastTimer = root.DispatcherQueue.CreateTimer();
            scope.ContrastTimer.Interval = TimeSpan.FromMilliseconds(500);
            scope.LastContrast = scope.Accessibility.HighContrast;
            scope.ContrastTimer.Tick += (_, _) =>
            {
                var contrast = scope.Accessibility.HighContrast;
                if (contrast != scope.LastContrast) { scope.LastContrast = contrast; ApplyNativePalette(root, scope); }
            };
            root.Loaded += (_, _) => { if (scope.Light is not null || scope.Dark is not null) scope.ContrastTimer.Start(); ApplyNativePalette(root, scope); };
            root.Unloaded += (_, _) => scope.ContrastTimer.Stop();
            root.RegisterPropertyChangedCallback(FrameworkElement.RequestedThemeProperty, (_, _) => ApplyNativePalette(root, scope));
            root.ActualThemeChanged += (_, _) => ApplyNativePalette(root, scope);
        }
        if (root.IsLoaded && (light is not null || dark is not null)) scope.ContrastTimer!.Start();
        else scope.ContrastTimer!.Stop();
        ApplyNativePalette(root, scope);
        scope.Notify();
    }
    private static void CopyPalettes(FrameworkElement source, FrameworkElement target, Action<Action> onClosed, Func<(BrowserPalette? Light, BrowserPalette? Dark)>? overrides = null)
    {
        var scope = Palettes.GetValue(source, _ => new());
        void Update()
        {
            var own = overrides?.Invoke() ?? (null, null);
            SetPalettes(target, own.Item1 ?? scope.Light, own.Item2 ?? scope.Dark);
        }
        Update(); scope.Changed += Update; onClosed(() => scope.Changed -= Update);
    }
    private static void ApplyNativePalette(FrameworkElement root, PaletteScope scope)
    {
        if (scope.Applying || !root.IsLoaded) return;
        scope.Applying = true;
        try
        {
            var theme = root.RequestedTheme == ElementTheme.Default ? root.ActualTheme : root.RequestedTheme;
            var palette = theme == ElementTheme.Dark ? scope.Dark : scope.Light;
            if (scope.Accessibility.HighContrast) palette = null;
            void Restore()
            {
                foreach (var (element, properties) in scope.Originals)
                    foreach (var (property, value) in properties)
                        if (value == DependencyProperty.UnsetValue) element.ClearValue(property); else element.SetValue(property, value);
                scope.Originals.Clear();
            }
            Restore();
            if (palette is null) return;
            Color Parse(string value) => Color.FromArgb(255, Convert.ToByte(value.Substring(1, 2), 16), Convert.ToByte(value.Substring(3, 2), 16), Convert.ToByte(value.Substring(5, 2), 16));
            void Set(DependencyObject element, DependencyProperty property, string value)
            {
                if (!scope.Originals.TryGetValue(element, out var properties)) scope.Originals[element] = properties = new();
                properties[property] = element.ReadLocalValue(property);
                element.SetValue(property, new SolidColorBrush(Parse(value)));
            }
            void Visit(DependencyObject element)
            {
                if (!ReferenceEquals(element, root) && element is FrameworkElement nested && Palettes.TryGetValue(nested, out _)) return;
                if (element is Control control)
                {
                    Set(control, Control.ForegroundProperty, palette.Foreground);
                    if (control is not ListViewItem && control is not TreeViewItem)
                        Set(control, Control.BackgroundProperty, ReferenceEquals(control, root) ? palette.Background : palette.Surface);
                    if (control is TextBox or ComboBox or Button) Set(control, Control.BorderBrushProperty, palette.Border);
                }
                else if (element is TextBlock text && text.ReadLocalValue(TextBlock.ForegroundProperty) == DependencyProperty.UnsetValue)
                    Set(text, TextBlock.ForegroundProperty, palette.Foreground);
                // Visit application content, leaving native template implementation details to their styles.
                if (element is Panel panel) foreach (var child in panel.Children) Visit(child);
                else if (element is Border border && border.Child is { } child) Visit(child);
                else if (element is ContentControl container && container.Content is DependencyObject content) Visit(content);
                else if (element is ItemsControl items) foreach (var item in items.Items.OfType<DependencyObject>()) Visit(item);
            }
            Visit(root);
        }
        finally { scope.Applying = false; }
    }
    private static ResourceDictionary Colors(BrowserPalette palette)
    {
        var resources = new ResourceDictionary();
        void Brush(string key, string value) => resources[key] = new SolidColorBrush(Color.FromArgb(255, Convert.ToByte(value.Substring(1, 2), 16), Convert.ToByte(value.Substring(3, 2), 16), Convert.ToByte(value.Substring(5, 2), 16)));
        foreach (var key in new[] { "ApplicationPageBackgroundThemeBrush", "SystemControlPageBackgroundChromeLowBrush", "SystemControlBackgroundChromeLowBrush" }) Brush(key, palette.Background);
        foreach (var key in new[] { "ControlFillColorDefaultBrush", "ControlFillColorSecondaryBrush", "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused", "ComboBoxBackground", "ButtonBackground", "ButtonBackgroundPointerOver", "MenuFlyoutPresenterBackground", "SystemControlBackgroundChromeMediumLowBrush", "SystemControlBackgroundAltHighBrush", "SystemControlBackgroundListLowBrush", "DataGridBackground", "DataGridRowBackground", "DataGridColumnHeaderBackgroundBrush" }) Brush(key, palette.Surface);
        foreach (var key in new[] { "ApplicationForegroundThemeBrush", "TextFillColorPrimaryBrush", "SystemControlForegroundBaseHighBrush", "TextControlForeground", "TextControlForegroundFocused", "TextControlForegroundPointerOver", "ButtonForeground", "ButtonForegroundPointerOver", "ComboBoxForeground", "MenuFlyoutItemForeground", "DataGridRowForeground", "DataGridColumnHeaderForegroundBrush" }) Brush(key, palette.Foreground);
        foreach (var key in new[] { "AccentFillColorDefaultBrush", "SystemControlHighlightAccentBrush", "SystemControlHighlightListAccentLowBrush", "SystemControlHighlightListAccentMediumBrush", "SystemControlHighlightListAccentHighBrush", "ListViewItemBackgroundSelected", "ListViewItemBackgroundSelectedPointerOver", "TextControlSelectionHighlightColor", "DataGridRowSelectedBackground" }) Brush(key, palette.Accent);
        foreach (var key in new[] { "TextOnAccentFillColorPrimaryBrush", "SystemControlHighlightAltBaseHighBrush", "ListViewItemForegroundSelected", "DataGridRowSelectedForeground" }) Brush(key, palette.AccentForeground);
        foreach (var key in new[] { "ControlStrokeColorDefaultBrush", "SystemControlForegroundBaseMediumLowBrush", "TextControlBorderBrush", "ComboBoxBorderBrush", "ButtonBorderBrush", "DataGridGridLinesBrush" }) Brush(key, palette.Border);
        resources["SystemAccentColor"] = Color.FromArgb(255, Convert.ToByte(palette.Accent.Substring(1, 2), 16), Convert.ToByte(palette.Accent.Substring(3, 2), 16), Convert.ToByte(palette.Accent.Substring(5, 2), 16));
        return resources;
    }
}
