using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CompuMaster.Dms.BrowserUI.WinUI;

/// <summary>Applies appearance within a control or window without changing the application theme.</summary>
public static partial class BrowserAppearance
{
    /// <summary>Applies the selected appearance to an element and its descendants.</summary>
    /// <param name="root">The root of the appearance scope.</param>
    /// <param name="theme">The appearance, or System to inherit the host preference.</param>
    public static void SetTheme(FrameworkElement root, BrowserTheme theme)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!Enum.IsDefined(theme)) throw new ArgumentOutOfRangeException(nameof(theme));
        root.RequestedTheme = theme switch { BrowserTheme.Dark => ElementTheme.Dark, BrowserTheme.Light => ElementTheme.Light, _ => ElementTheme.Default };
    }
    /// <summary>Gets the appearance explicitly requested on an element.</summary>
    /// <param name="root">The appearance scope.</param>
    /// <returns>The requested appearance, with System representing inheritance.</returns>
    public static BrowserTheme GetTheme(FrameworkElement root) => root.RequestedTheme switch
    { ElementTheme.Dark => BrowserTheme.Dark, ElementTheme.Light => BrowserTheme.Light, _ => BrowserTheme.System };

    internal static ContentControl WindowContent(UIElement child) => (ContentControl)CreateRoot(child);
    private static object CreateRoot(UIElement child)
    {
        var root = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        root.Style = (Style)Microsoft.UI.Xaml.Markup.XamlReader.Load("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ContentControl'><Setter Property='Background' Value='{ThemeResource ApplicationPageBackgroundThemeBrush}'/><Setter Property='Foreground' Value='{ThemeResource ApplicationForegroundThemeBrush}'/></Style>");
        root.Template = (Microsoft.UI.Xaml.Controls.ControlTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ContentControl'><Border Background='{TemplateBinding Background}'><ContentPresenter Content='{TemplateBinding Content}' ContentTemplate='{TemplateBinding ContentTemplate}' HorizontalAlignment='Stretch' VerticalAlignment='Stretch'/></Border></ControlTemplate>");
        root.Content = child;
        return root;
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, FrameworkElement> DialogSources = new();
    internal static void UseSource(Window dialog, FrameworkElement source)
    { DialogSources.Remove(dialog); DialogSources.Add(dialog, source); }
    internal static Task ShowDialog(DialogWindow dialog, Window owner, FrameworkElement? source = null)
    { if (source is not null) UseSource(dialog, source); return dialog.ShowDialog(owner); }
    internal static Task<T?> ShowDialog<T>(DialogWindow dialog, Window owner, FrameworkElement? source = null)
    { if (source is not null) UseSource(dialog, source); return dialog.ShowDialog<T>(owner); }
    internal static Task<DmsBrowserSelection?> ShowBrowserDialog(DmsBrowser dialog, Window owner, FrameworkElement source)
    { UseSource(dialog, source); return dialog.ShowDialogAsync(owner); }
    internal static void Inherit(Window dialog, Window owner)
    {
        if (dialog.Content is not FrameworkElement content || owner.Content is not FrameworkElement parent) return;
        if (DialogSources.TryGetValue(dialog, out var source)) parent = source;
        void Update()
        {
            if (dialog is DmsBrowser browser && browser.Browser.Appearance != BrowserTheme.System)
                SetTheme(content, browser.Browser.Appearance);
            else content.RequestedTheme = parent.RequestedTheme == ElementTheme.Default ? parent.ActualTheme : parent.RequestedTheme;
        }
        CopyPalettes(parent, content, cleanup => dialog.Closed += (_, _) => cleanup(), () => dialog is DmsBrowser own ? (own.Browser.LightPalette, own.Browser.DarkPalette) : (null, null));
        Update();
        void Changed(FrameworkElement sender, object args) => Update();
        parent.ActualThemeChanged += Changed;
        var token = parent.RegisterPropertyChangedCallback(FrameworkElement.RequestedThemeProperty, (_, _) => Update());
        dialog.Closed += (_, _) => { parent.ActualThemeChanged -= Changed; parent.UnregisterPropertyChangedCallback(FrameworkElement.RequestedThemeProperty, token); };
    }
    /// <summary>Creates a localized appearance selector for an element.</summary>
    /// <param name="root">The appearance scope controlled by the selector.</param>
    /// <returns>A selector that can be placed in the host layout.</returns>
    public static ComboBox CreateSelector(FrameworkElement root) => Selector(root);
    internal static ComboBox Selector(FrameworkElement root, Action<BrowserTheme>? changed = null)
    {
        var selector = new ComboBox { Name = "BrowserThemeSelector", MinWidth = 150, Margin = new Thickness(3), SelectedIndex = -1 };
        foreach (var key in new[] { "ThemeSystem", "ThemeLight", "ThemeDark" }) selector.Items.Add(UiText.Get(key));
        selector.SelectedIndex = (int)GetTheme(root);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(selector, UiText.Get("Appearance"));
        selector.SelectionChanged += (_, _) => { if (selector.SelectedIndex >= 0) { var theme = (BrowserTheme)selector.SelectedIndex; SetTheme(root, theme); changed?.Invoke(theme); } };
        return selector;
    }
}
