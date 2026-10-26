using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Input;

namespace CompuMaster.Dms.BrowserUI.Avalonia;

internal sealed record DialogChoice(string ID, string Text)
{
    public override string ToString() => Text;
}

internal static class Dialogs
{
    internal static string Caption(string key) => UiText.Get(key).Replace("&", "");
    internal static Button Button(string key, Action action)
    {
        var button = new Button { Content = Caption(key), MinWidth = 82, Margin = new Thickness(3) };
        button.Click += (_, _) => action();
        return button;
    }
    internal static StackPanel Buttons(params Control[] controls)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var c in controls) panel.Children.Add(c);
        return panel;
    }
    internal static Window Window(string title, Control content, double width = 560, double height = 320) =>
        new() { Title = title, Content = content, Width = width, Height = height, MinWidth = 360, MinHeight = 220, FlowDirection = UiText.LayoutDirection,
            WindowStartupLocation = WindowStartupLocation.CenterOwner };
    internal static DockPanel Layout(Control content, Control buttons)
    {
        var panel = new DockPanel { Margin = new Thickness(16), LastChildFill = true };
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons); panel.Children.Add(content);
        return panel;
    }
    internal static async Task MessageAsync(Window owner, string message, string? title = null)
    {
        Window? dialog = null;
        var text = new TextBox { Text = message, IsReadOnly = true, AcceptsReturn = true, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        dialog = Window(title ?? owner.Title ?? "", Layout(text, Buttons(Button("ActionOkay", () => dialog!.Close()))), 650, 380);
        await dialog.ShowDialog(owner);
    }
    internal static async Task<bool> ConfirmAsync(Window owner, string message)
    {
        Window? dialog = null;
        var yes = Button("ActionOkay", () => dialog!.Close(true));
        var no = Button("ActionCancel", () => dialog!.Close(false)); no.IsDefault = true; no.IsCancel = true;
        dialog = Window(owner.Title ?? "", Layout(new TextBlock { Text = message, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap },
            Buttons(yes, no)));
        return await dialog.ShowDialog<bool>(owner);
    }
    internal static async Task<string?> InputAsync(Window owner, string prompt, string value = "")
    {
        Window? dialog = null;
        var input = new TextBox { Text = value, Margin = new Thickness(0, 12), Name = "InputValue" };
        var body = new StackPanel { Children = { new TextBlock { Text = prompt, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap }, input } };
        var ok = Button("ActionOkay", () => dialog!.Close(input.Text ?? "")); ok.IsDefault = true;
        var cancel = Button("ActionCancel", () => dialog!.Close()); cancel.IsCancel = true;
        dialog = Window(owner.Title ?? "", Layout(body, Buttons(ok, cancel)));
        dialog.Opened += (_, _) => { input.Focus(); input.SelectAll(); };
        return await dialog.ShowDialog<string?>(owner);
    }
    internal static async Task<DialogChoice?> ChooseAsync(Window owner, string title, IReadOnlyList<DialogChoice> choices)
    {
        Window? dialog = null;
        var list = new ListBox { ItemsSource = choices, SelectedIndex = choices.Count > 0 ? 0 : -1 };
        var ok = Button("ActionOkay", () => { if (list.SelectedItem is DialogChoice selected) dialog!.Close(selected); }); ok.IsDefault = true;
        var cancel = Button("ActionCancel", () => dialog!.Close()); cancel.IsCancel = true;
        dialog = Window(title, Layout(list, Buttons(ok, cancel)), 560, 420);
        list.DoubleTapped += (_, _) => { if (list.SelectedItem is DialogChoice selected) dialog.Close(selected); };
        return await dialog.ShowDialog<DialogChoice?>(owner);
    }
}

internal abstract class OperationWindow : Window
{
    private bool running;
    internal bool IsRunning => running;
    internal OperationWindow()
    {
        FlowDirection = UiText.LayoutDirection;
        MinWidth = 440; MinHeight = 300;
        Width = 720; Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Closing += (_, e) => { if (running) e.Cancel = true; };
        KeyDown += (_, e) => { if (!running && e.Key == Key.Escape) { Close(); e.Handled = true; } };
    }
    internal async Task<bool> RunAsync(Func<Task> action)
    {
        if (running) return false;
        running = true;
        if (Content is Control control) control.IsEnabled = false;
        try { await action(); return true; }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex)
        {
            var browser = Owner;
            while (browser is not null && browser is not DmsBrowser) browser = browser.Owner;
            var failure = (browser as DmsBrowser)?.Browser.ReportFailure(ex, DmsBrowserFailurePhase.Action, GetType().Name);
            if (failure?.SuppressDialog != true) await Dialogs.MessageAsync(this, UiText.Get("BrowserOperationFailed"));
            return false;
        }
        finally { running = false; if (Content is Control c) c.IsEnabled = true; }
    }
    internal static TextBox Field(StackPanel panel, string key, string? value = null, bool readOnly = false)
    {
        panel.Children.Add(new TextBlock { Text = Dialogs.Caption(key), Margin = new Thickness(0, 6, 0, 2) });
        var field = new TextBox { Text = value, IsReadOnly = readOnly };
        panel.Children.Add(field);
        return field;
    }
}
