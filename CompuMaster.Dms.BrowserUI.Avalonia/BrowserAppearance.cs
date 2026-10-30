using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Styling;

namespace CompuMaster.Dms.BrowserUI.Avalonia;

/// <summary>Applies appearance within a control or window without changing the application theme.</summary>
public static partial class BrowserAppearance
{
    /// <summary>Applies the selected appearance to an element and its descendants.</summary>
    /// <param name="root">The root of the appearance scope.</param>
    /// <param name="theme">The appearance, or System to inherit the host preference.</param>
    public static void SetTheme(StyledElement root, BrowserTheme theme)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!Enum.IsDefined(theme)) throw new ArgumentOutOfRangeException(nameof(theme));
        root.SetValue(ThemeVariantScope.RequestedThemeVariantProperty, theme switch
        { BrowserTheme.Dark => ThemeVariant.Dark, BrowserTheme.Light => ThemeVariant.Light, _ => ThemeVariant.Default });
    }
    /// <summary>Gets the appearance explicitly requested on an element.</summary>
    /// <param name="root">The appearance scope.</param>
    /// <returns>The requested appearance, with System representing inheritance.</returns>
    public static BrowserTheme GetTheme(StyledElement root) => root.GetValue(ThemeVariantScope.RequestedThemeVariantProperty) switch
    { var value when value == ThemeVariant.Dark => BrowserTheme.Dark, var value when value == ThemeVariant.Light => BrowserTheme.Light, _ => BrowserTheme.System };

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, object> InheritedWindows = new();
    internal static void Inherit(Window dialog, Window owner, StyledElement? source = null)
    {
        // Owned top levels do not inherit the owner's logical theme scope automatically.
        if (InheritedWindows.TryGetValue(dialog, out _)) return;
        InheritedWindows.Add(dialog, new object());
        source ??= owner;
        void Update()
        {
            if (dialog is DmsBrowser browser && browser.Browser.Appearance != BrowserTheme.System)
                SetTheme(dialog, browser.Browser.Appearance);
            else dialog.RequestedThemeVariant = source.ActualThemeVariant;
        }
        CopyPalettes(source, dialog, cleanup => dialog.Closed += (_, _) => cleanup(), () => dialog is DmsBrowser own ? (own.Browser.LightPalette, own.Browser.DarkPalette) : (null, null));
        Update();
        void Changed(object? sender, AvaloniaPropertyChangedEventArgs e)
        { if (e.Property == ThemeVariantScope.ActualThemeVariantProperty) Update(); }
        source.PropertyChanged += Changed;
        dialog.Closed += (_, _) => source.PropertyChanged -= Changed;
    }
    internal static Task ShowDialog(Window dialog, Window owner, StyledElement? source = null)
    { Inherit(dialog, owner, source); return dialog.ShowDialog(owner); }
    internal static Task<T> ShowDialog<T>(Window dialog, Window owner, StyledElement? source = null)
    { Inherit(dialog, owner, source); return dialog.ShowDialog<T>(owner); }
    /// <summary>Creates a localized appearance selector for an element.</summary>
    /// <param name="root">The appearance scope controlled by the selector.</param>
    /// <returns>A selector that can be placed in the host layout.</returns>
    public static ComboBox CreateSelector(StyledElement root) => Selector(root);
    internal static ComboBox Selector(StyledElement root, Action<BrowserTheme>? changed = null)
    {
        var selector = new ComboBox { Name = "BrowserThemeSelector", MinWidth = 150, Margin = new Thickness(3),
            ItemsSource = new[] { UiText.Get("ThemeSystem"), UiText.Get("ThemeLight"), UiText.Get("ThemeDark") }, SelectedIndex = (int)GetTheme(root) };
        global::Avalonia.Automation.AutomationProperties.SetName(selector, UiText.Get("Appearance"));
        selector.SelectionChanged += (_, _) => { if (selector.SelectedIndex >= 0) { var theme = (BrowserTheme)selector.SelectedIndex; SetTheme(root, theme); changed?.Invoke(theme); } };
        return selector;
    }
}
