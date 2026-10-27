using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;

namespace CompuMaster.Dms.BrowserUI.WinUI;

internal sealed record DialogChoice(string ID, string Text)
{
    public override string ToString() => Text;
}

internal sealed class WindowHost
{
    private readonly Window window;
    private readonly TaskCompletionSource<object?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private object? result;
    private nint parent;
    private nint largeIcon, smallIcon;
    internal WindowHost(Window window, int width = 720, int height = 640)
    {
        this.window = window;
        window.AppWindow.Resize(new SizeInt32(width, height));
        if (Environment.ProcessPath is { } executable && ExtractIconEx(executable, 0, out largeIcon, out smallIcon, 1) > 0)
        {
            var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            if (largeIcon != 0) SendMessage(handle, 0x80, 1, largeIcon);
            if (smallIcon != 0) SendMessage(handle, 0x80, 0, smallIcon);
        }
        window.Closed += (_, _) =>
        {
            if (largeIcon != 0) { DestroyIcon(largeIcon); largeIcon = 0; }
            if (smallIcon != 0) { DestroyIcon(smallIcon); smallIcon = 0; }
            if (parent != 0) { EnableWindow(parent, true); SetForegroundWindow(parent); }
            completion.TrySetResult(result);
        };
    }
    internal Window? Owner { get; private set; }
    internal Task<object?> ShowAsync(Window? owner = null)
    {
        Owner = owner;
        if (owner is not null)
        {
            parent = WinRT.Interop.WindowNative.GetWindowHandle(owner);
            var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            SetWindowLongPtr(handle, -8, parent);
            EnableWindow(parent, false);
            var bounds = owner.AppWindow.Position;
            var size = owner.AppWindow.Size;
            var mine = window.AppWindow.Size;
            if (DisplayArea.GetFromWindowId(owner.AppWindow.Id, DisplayAreaFallback.Primary) is { } display)
            {
                var area = display.WorkArea;
                mine = new SizeInt32(Math.Min(mine.Width, Math.Max(1, area.Width - 16)), Math.Min(mine.Height, Math.Max(1, area.Height - 16)));
                window.AppWindow.Resize(mine);
                window.AppWindow.Move(new PointInt32(
                    Math.Clamp(bounds.X + (size.Width - mine.Width) / 2, area.X + 8, area.X + area.Width - mine.Width - 8),
                    Math.Clamp(bounds.Y + (size.Height - mine.Height) / 2, area.Y + 8, area.Y + area.Height - mine.Height - 8)));
            }
            else window.AppWindow.Move(new PointInt32(bounds.X, bounds.Y));
        }
        try { window.Activate(); }
        catch { if (parent != 0) EnableWindow(parent, true); throw; }
        return completion.Task;
    }
    internal void Close(object? value = null) { result = value; window.Close(); }
    [DllImport("user32.dll")] private static extern bool EnableWindow(nint hwnd, bool enabled);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern uint ExtractIconEx(string file, int index, out nint large, out nint small, uint count);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint window, uint message, nint word, nint value);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
}

internal class DialogWindow : Window
{
    internal event EventHandler? Opened;
    internal new FrameworkElement Content
    {
        get => (FrameworkElement)base.Content;
        set
        {
            var content = value is ContentControl ? value : new ContentControl { Content = value, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
            content.KeyDown += (_, e) => { if (e.Key == VirtualKey.Escape) { e.Handled = true; OnEscape(); } };
            base.Content = content;
        }
    }
    internal double Width { get => AppWindow.Size.Width; set => AppWindow.Resize(new SizeInt32((int)value, AppWindow.Size.Height)); }
    internal double Height { get => AppWindow.Size.Height; set => AppWindow.Resize(new SizeInt32(AppWindow.Size.Width, (int)value)); }
    internal WindowHost Host { get; }
    internal Window? Owner => Host.Owner;
    internal DialogWindow()
    {
        Host = new WindowHost(this);
        var opened = false;
        Activated += (_, _) => { if (!opened) { opened = true; Opened?.Invoke(this, EventArgs.Empty); } };
    }
    internal async Task ShowDialog(Window owner) => await Host.ShowAsync(owner);
    internal async Task<T?> ShowDialog<T>(Window owner)
    {
        var result = await Host.ShowAsync(owner);
        return result is T value ? value : default;
    }
    internal void Close(object? result) => Host.Close(result);
    internal virtual void OnEscape() => Close();
}

internal sealed class WrapPanel : Panel
{
    protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
    {
        double x = 0, y = 0, row = 0, width = 0;
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            var size = child.DesiredSize;
            if (x > 0 && x + size.Width > availableSize.Width) { y += row; x = row = 0; }
            x += size.Width; row = Math.Max(row, size.Height); width = Math.Max(width, x);
        }
        return new(width, y + row);
    }
    protected override Windows.Foundation.Size ArrangeOverride(Windows.Foundation.Size finalSize)
    {
        double x = 0, y = 0, row = 0;
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            if (x > 0 && x + size.Width > finalSize.Width) { y += row; x = row = 0; }
            child.Arrange(new Windows.Foundation.Rect(x, y, size.Width, size.Height));
            x += size.Width; row = Math.Max(row, size.Height);
        }
        return finalSize;
    }
}

internal static class Dialogs
{
    internal static string Caption(string key) => UiText.Get(key).Replace("&", "");
    internal static Button Button(string key, Action action)
    {
        var button = new Button { Content = Caption(key), Name = key, MinWidth = 82, Margin = new Thickness(3) };
        button.Click += (_, _) => action();
        return button;
    }
    internal static WrapPanel Buttons(params UIElement[] controls)
    {
        var panel = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var control in controls) panel.Children.Add(control);
        return panel;
    }
    internal static GroupBox Group(string key, UIElement content)
    {
        return new GroupBox(key, content);
    }
    internal static Grid Layout(FrameworkElement content, FrameworkElement buttons)
    {
        var panel = new Grid { Margin = new Thickness(16), RowSpacing = 10, FlowDirection = UiText.LayoutDirection };
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.Children.Add(content); Grid.SetRow(buttons, 1); panel.Children.Add(buttons);
        return panel;
    }
    internal static DialogWindow Window(string title, UIElement content, int width = 560, int height = 320)
    {
        var window = new DialogWindow { Title = title, Content = new ContentControl { Content = content,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch } };
        window.AppWindow.Resize(new SizeInt32(width, height));
        return window;
    }
    internal static async Task MessageAsync(Window owner, string message, string? title = null)
    {
        DialogWindow? dialog = null;
        var text = new TextBox { Text = message, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        dialog = Window(title ?? owner.Title, Layout(text, Buttons(Button("ActionOkay", () => dialog!.Close()))), 650, 420);
        await dialog.ShowDialog(owner);
    }
    internal static async Task<bool> ConfirmAsync(Window owner, string message)
    {
        DialogWindow? dialog = null;
        var cancel = Button("ActionCancel", () => dialog!.Close(false));
        dialog = Window(owner.Title, Layout(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            Buttons(Button("ActionOkay", () => dialog!.Close(true)), cancel)));
        dialog.Content.Loaded += (_, _) => cancel.Focus(FocusState.Programmatic);
        return await dialog.ShowDialog<bool>(owner);
    }
    internal static async Task<string?> InputAsync(Window owner, string prompt, string value = "")
    {
        DialogWindow? dialog = null;
        var input = new TextBox { Text = value, Name = "InputValue" };
        var body = new StackPanel { Spacing = 10, Children = { new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap }, input } };
        void Save() => dialog!.Close(input.Text);
        dialog = Window(owner.Title, Layout(body, Buttons(Button("ActionOkay", Save), Button("ActionCancel", () => dialog!.Close()))));
        input.KeyDown += (_, e) => { if (e.Key == VirtualKey.Enter) { e.Handled = true; Save(); } };
        input.Loaded += (_, _) => { input.Focus(FocusState.Programmatic); input.SelectAll(); };
        return await dialog.ShowDialog<string>(owner);
    }
    internal static async Task<DialogChoice?> ChooseAsync(Window owner, string title, IReadOnlyList<DialogChoice> choices, string? selectedID = null, string? prompt = null)
    {
        DialogWindow? dialog = null;
        var list = new ListView { Name = "Choices", ItemsSource = choices, SelectionMode = ListViewSelectionMode.Single,
            SelectedItem = choices.FirstOrDefault(choice => choice.ID == selectedID) ?? choices.FirstOrDefault() };
        void Save() { if (list.SelectedItem is DialogChoice selected) dialog!.Close(selected); }
        var body = new Grid { RowSpacing = 8 };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(new TextBlock { Text = prompt ?? "", TextWrapping = TextWrapping.Wrap });
        Grid.SetRow(list, 1); body.Children.Add(list);
        dialog = Window(title, Layout(body, Buttons(Button("ActionOkay", Save), Button("ActionCancel", () => dialog!.Close()))), 560, 460);
        list.DoubleTapped += (_, _) => Save();
        list.KeyDown += (_, e) => { if (e.Key == VirtualKey.Enter) { e.Handled = true; Save(); } };
        return await dialog.ShowDialog<DialogChoice>(owner);
    }
}

internal sealed class GroupBox : ContentControl
{
    internal GroupBox(string key, UIElement content)
    {
        Name = key + "Group";
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = UiText.Get(key), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Gray), Padding = new Thickness(10), Child = content });
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Content = panel;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(this, UiText.Get(key));
    }
    protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);
    private sealed class GroupPeer(GroupBox owner) : Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer(owner)
    {
        protected override Microsoft.UI.Xaml.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() => Microsoft.UI.Xaml.Automation.Peers.AutomationControlType.Group;
    }
}

internal sealed class CheckBox : Microsoft.UI.Xaml.Controls.CheckBox
{
    internal event EventHandler? IsCheckedChanged;
    internal CheckBox()
    {
        Checked += (_, _) => IsCheckedChanged?.Invoke(this, EventArgs.Empty);
        Unchecked += (_, _) => IsCheckedChanged?.Invoke(this, EventArgs.Empty);
        Indeterminate += (_, _) => IsCheckedChanged?.Invoke(this, EventArgs.Empty);
    }
}

internal abstract class OperationWindow : DialogWindow
{
    private bool running;
    internal bool IsRunning => running;
    internal override void OnEscape() { if (!running) Close(); }
    internal OperationWindow()
    {
        AppWindow.Closing += (_, e) => { if (running) e.Cancel = true; };
    }
    internal async Task<bool> RunAsync(Func<Task> action)
    {
        if (running) return false;
        running = true;
        var content = Content as Control;
        if (content is not null) content.IsEnabled = false;
        try { await action(); return true; }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex)
        {
            var browser = Owner;
            while (browser is DialogWindow nested) browser = nested.Owner;
            var failure = (browser as DmsBrowser)?.Browser.ReportFailure(ex, DmsBrowserFailurePhase.Action, GetType().Name);
            if (failure?.SuppressDialog != true) await Dialogs.MessageAsync(this, BrowserErrorDetails.Format(ex, DmsBrowserFailurePhase.Action, GetType().Name));
            return false;
        }
        finally { running = false; if (content is not null) content.IsEnabled = true; }
    }
    internal static TextBox Field(StackPanel panel, string key, string? value = null, bool readOnly = false)
    {
        panel.Children.Add(new TextBlock { Text = Dialogs.Caption(key), Margin = new Thickness(0, 6, 0, 2) });
        var field = new TextBox { Name = key, Text = value ?? "", IsReadOnly = readOnly };
        panel.Children.Add(field); return field;
    }
}
