using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Data;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Input.Platform;

namespace CompuMaster.Dms.BrowserUI.Avalonia;

/// <summary>Provides standalone Avalonia sharing dialogs.</summary>
public static class DmsSharingDialogs
{
    /// <summary>Shows internal sharing and external link settings for a resource.</summary>
    /// <param name="owner">The owning Avalonia window.</param>
    /// <param name="provider">The provider owning the resource.</param>
    /// <param name="item">The resource to share.</param>
    /// <returns>A task that completes when the dialog closes.</returns>
    /// <exception cref="NotSupportedException">The provider does not support sharing.</exception>
    public static Task ShowAsync(Window owner, BaseDmsProvider provider, DmsResourceItem item)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(item);
        return new DmsItemSharings(provider, item).ShowDialog(owner);
    }
}

internal sealed record SharingRow(DmsShareBase? Share, string Description, string Type = "", string Name = "", string Permissions = "", string Limitations = "")
{
    public override string ToString() => Description;
}

internal sealed class DmsItemSharings : OperationWindow
{
    private readonly BaseDmsProvider provider;
    private readonly DmsResourceItem item;
    private readonly DataGrid internalShares = Grid("InternalSharings", ("ColumnType", "Type"), ("ColumnName", "Name"), ("ColumnAuthorizations", "Permissions"));
    private readonly DataGrid links = Grid("ExternalSharings", ("ColumnName", "Name"), ("ColumnAuthorizations", "Permissions"), ("ColumnLimitations", "Limitations"));
    private static DataGrid Grid(string name, params (string Key, string Binding)[] columns)
    {
        var grid = new DataGrid { Name = name, MinHeight = 150, MaxHeight = 230, IsReadOnly = true, AutoGenerateColumns = false,
            SelectionMode = DataGridSelectionMode.Single, CanUserSortColumns = false, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal };
        foreach (var column in columns) grid.Columns.Add(new DataGridTextColumn
        { Header = UiText.Get(column.Key), Binding = new Binding(column.Binding), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        return grid;
    }
    internal DmsItemSharings(BaseDmsProvider provider, DmsResourceItem item)
    {
        _ = new SharingPolicy(provider);
        if (item.ItemType == DmsResourceItem.ItemTypes.Root) throw new NotSupportedException(UiText.Get("RootSharingUnsupported"));
        this.provider = provider; this.item = item;
        Title = UiText.Get("SharingsTitle") + " – " + item.FullName;
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(new TextBlock { Text = UiText.Format("CurrentOwner", item.ExtendedInfosOwner.DisplayName) });
        content.Children.Add(new TextBlock { Text = UiText.Get("InternalSharings"), FontWeight = FontWeight.Bold });
        content.Children.Add(internalShares);
        content.Children.Add(Toolbar(
            ("ActionAddUser", () => EditStandardAsync(false, null)), ("ActionAddGroup", () => EditStandardAsync(true, null)),
            ("ActionEdit", EditInternalAsync), ("ActionDelete", () => DeleteAsync(internalShares))));
        content.Children.Add(new TextBlock { Text = UiText.Get("ExternalSharings"), FontWeight = FontWeight.Bold });
        content.Children.Add(links);
        content.Children.Add(Toolbar(("ActionAdd", () => EditLinkAsync(null)), ("ActionEdit", EditSelectedLinkAsync),
            ("ActionDelete", () => DeleteAsync(links)), ("ActionCopyWebLink", CopyLinkAsync)));
        Content = Dialogs.Layout(new ScrollViewer { Content = content },
            Dialogs.Buttons(Dialogs.Button("ActionClose", Close)));
        Opened += async (_, _) => await RunAsync(async () =>
        {
            foreach (var link in item.ExtendedInfosLinks?.ToArray() ?? Array.Empty<DmsLink>()) await link.RefreshAsync();
            // ToString can resolve provider metadata; prepare rows off the UI thread.
            var rows = await Task.Run(BuildRows);
            internalShares.ItemsSource = rows.Internal; links.ItemsSource = rows.Links;
        });
        internalShares.DoubleTapped += async (_, _) => await RunAsync(EditInternalAsync);
        links.DoubleTapped += async (_, _) => await RunAsync(EditSelectedLinkAsync);
    }
    private WrapPanel Toolbar(params (string Key, Func<Task> Action)[] entries)
    {
        var panel = new WrapPanel();
        foreach (var entry in entries) panel.Children.Add(Dialogs.Button(entry.Key, async () => await RunAsync(entry.Action)));
        return panel;
    }
    private (SharingRow[] Internal, SharingRow[] Links) BuildRows()
    {
        var users = (item.ExtendedInfosUserSharings ?? new()).Select(s => new SharingRow(s, UiText.Get("EntityUser") + ": " + s.User.DisplayName + " — " + Permissions(s), UiText.Get("EntityUser"), s.User.DisplayName, Permissions(s)));
        var groups = (item.ExtendedInfosGroupSharings ?? new()).Select(s => new SharingRow(s, UiText.Get("EntityGroup") + ": " + s.Group.DisplayName + " — " + Permissions(s), UiText.Get("EntityGroup"), s.Group.DisplayName, Permissions(s)));
        var rows = users.Concat(groups).ToList();
        if (item.ExtendedInfosHasHiddenUserSharings) rows.Add(new(null, UiText.Get("EntityUser") + ": " + UiText.Get("HiddenValue"), UiText.Get("EntityUser"), UiText.Get("HiddenValue"), UiText.Get("UnknownValue")));
        if (item.ExtendedInfosHasHiddenGroupSharings) rows.Add(new(null, UiText.Get("EntityGroup") + ": " + UiText.Get("HiddenValue"), UiText.Get("EntityGroup"), UiText.Get("HiddenValue"), UiText.Get("UnknownValue")));
        var external = (item.ExtendedInfosLinks ?? new()).Select(link =>
        {
            var limits = new List<string>();
            if (!string.IsNullOrEmpty(link.Password)) limits.Add(UiText.Get("LimitationPassword"));
            if (link.ExpiryDateLocalTime.HasValue) limits.Add(link.ExpiryDateLocalTime.Value.ToString("g"));
            if (link.MaxDownloads.HasValue) limits.Add(UiText.Format("LimitationDownloads", link.MaxDownloads));
            if (link.MaxUploads.HasValue) limits.Add(UiText.Format("LimitationUploads", link.MaxUploads));
            if (link.MaxBytes.HasValue) limits.Add(UiText.ByteSize(link.MaxBytes.Value));
            var name = string.IsNullOrEmpty(link.Name) ? link.ID : link.Name;
            return new SharingRow(link, name + " — " + Permissions(link) + " — " + string.Join(", ", limits), Name: name,
                Permissions: Permissions(link), Limitations: string.Join(", ", limits));
        }).ToArray();
        return (rows.ToArray(), external);
    }
    internal static string Permissions(DmsShareBase share) => string.Join(", ", share.AllowedActions().Select(a => UiText.Get("Permission" + a)));
    private async Task RefreshRowsAsync()
    {
        var rows = await Task.Run(BuildRows);
        internalShares.ItemsSource = rows.Internal; links.ItemsSource = rows.Links;
    }
    private Task EditInternalAsync()
    {
        var share = (internalShares.SelectedItem as SharingRow)?.Share ?? throw new DmsUserInputMissingException(UiText.Get("UserSharingRequired"));
        return EditStandardAsync(share is DmsShareForGroup, share);
    }
    private async Task EditStandardAsync(bool group, DmsShareBase? existing)
    {
        var dialog = new DmsStandardShareSetup(provider, item, group, existing);
        await dialog.ShowDialog(this); await RefreshRowsAsync();
    }
    private Task EditSelectedLinkAsync() => EditLinkAsync((links.SelectedItem as SharingRow)?.Share as DmsLink
        ?? throw new DmsUserInputMissingException(UiText.Get("LinkSharingRequired")));
    private async Task EditLinkAsync(DmsLink? link)
    {
        if (link is not null) await link.RefreshAsync();
        await new DmsLinkShareSetup(provider, item, link).ShowDialog(this);
        await RefreshRowsAsync();
    }
    private async Task DeleteAsync(DataGrid list)
    {
        var share = (list.SelectedItem as SharingRow)?.Share ?? throw new DmsUserInputMissingException(UiText.Get("UserSharingRequired"));
        if (!await Dialogs.ConfirmAsync(this, Dialogs.Caption("ActionDelete") + "?")) return;
        await SharingPolicy.DeleteAsync(provider, item, share);
        await RefreshRowsAsync();
    }
    private async Task CopyLinkAsync()
    {
        var link = (links.SelectedItem as SharingRow)?.Share as DmsLink ?? throw new DmsUserInputMissingException(UiText.Get("LinkSharingRequired"));
        if (Clipboard is null) throw new NotSupportedException("The host does not provide a clipboard.");
        await Clipboard.SetTextAsync(link.WebUrl);
    }
}

internal sealed class PermissionEditor
{
    private readonly Dictionary<string, CheckBox> permissions = new();
    internal WrapPanel Control { get; } = new();
    internal PermissionEditor(SharingPolicy policy, DmsShareBase share, DmsResourceItem item, bool link, bool creating)
    {
        foreach (var name in new[] { "View", "Download", "Edit", "Upload", "Delete", "Share" })
        {
            var check = new CheckBox { Name = "Permission" + name, Content = UiText.Get("Permission" + name), Margin = new Thickness(6),
                IsChecked = Get(share, name), IsEnabled = policy.EditablePermission(name, link, creating, item, share as DmsLink) };
            permissions.Add(name, check); Control.Children.Add(check);
        }
        permissions["Download"].IsCheckedChanged += (_, _) =>
        { if (Value("Download")) permissions["View"].IsChecked = true; };
        permissions["View"].IsCheckedChanged += (_, _) =>
        {
            if (Value("View")) return;
            permissions["Download"].IsChecked = false;
            if (!link) foreach (var name in new[] { "Edit", "Delete", "Share" }) permissions[name].IsChecked = false;
        };
    }
    internal bool Value(string name) => permissions[name].IsChecked == true;
    private static bool Get(DmsShareBase share, string name) => name switch
    { "View" => share.AllowView, "Download" => share.AllowDownload, "Edit" => share.AllowEdit, "Upload" => share.AllowUpload, "Delete" => share.AllowDelete, _ => share.AllowShare };
    internal void Apply(DmsShareBase share)
    {
        share.AllowView = Value("View"); share.AllowDownload = Value("Download"); share.AllowEdit = Value("Edit");
        share.AllowUpload = Value("Upload"); share.AllowDelete = Value("Delete"); share.AllowShare = Value("Share");
    }
}

internal sealed class DmsStandardShareSetup : OperationWindow
{
    private readonly ComboBox principals = new() { HorizontalAlignment = HorizontalAlignment.Stretch, Name = "SharingPrincipal" };
    private readonly PermissionEditor permissions;
    internal DmsStandardShareSetup(BaseDmsProvider provider, DmsResourceItem item, bool group, DmsShareBase? existing)
    {
        Title = UiText.Format("ShareSetupTitle", item.FullName);
        var policy = new SharingPolicy(provider);
        DmsShareBase draft = existing switch
        {
            DmsShareForUser user => (DmsShareForUser)user.Clone(),
            DmsShareForGroup groups => (DmsShareForGroup)groups.Clone(),
            _ => group ? new DmsShareForGroup(item, default, true, true, true, true, true, true) : new DmsShareForUser(item, default, true, true, true, true, true, true)
        };
        permissions = new PermissionEditor(policy, draft, item, false, existing is null);
        var body = new StackPanel { Spacing = 12, Children =
            { new TextBlock { Text = UiText.Get(group ? "LabelGroupName" : "LabelUserName") }, principals, permissions.Control } };
        var save = Dialogs.Button("ActionSave", async () =>
        {
            var saved = await RunAsync(async () =>
            {
                var principal = principals.SelectedItem as DialogChoice ?? throw new DmsUserInputMissingException(UiText.Get("AuthorizationObjectRequired"));
                if (draft is DmsShareForGroup g) g.Group = new DmsGroup { ID = principal.ID, Name = principal.Text };
                if (draft is DmsShareForUser u) u.User = new DmsUser { ID = principal.ID, DisplayName = principal.Text };
                permissions.Apply(draft);
                await SharingPolicy.SaveAsync(provider, item, draft, existing is null);
            });
            if (saved) Close(true);
        });
        Content = Dialogs.Layout(body, Dialogs.Buttons(save, Dialogs.Button("ActionCancel", Close)));
        Opened += async (_, _) => await RunAsync(async () =>
        {
            if (existing is DmsShareForGroup g)
                principals.ItemsSource = new[] { new DialogChoice(g.Group.ID, g.Group.DisplayName) };
            else if (existing is DmsShareForUser u)
                principals.ItemsSource = new[] { new DialogChoice(u.User.ID, u.User.DisplayName) };
            else if (group)
            {
                var hidden = (item.ExtendedInfosGroupSharings ?? new()).Select(s => s.Group.ID).ToHashSet();
                var groups = await provider.GetAllGroupsAsync();
                principals.ItemsSource = await Task.Run(() => groups.Where(g => !hidden.Contains(g.ID)).Select(g => new DialogChoice(g.ID, g.DisplayName)).ToArray());
            }
            else
            {
                var hidden = (item.ExtendedInfosUserSharings ?? new()).Select(s => s.User.ID).ToHashSet();
                hidden.Add(item.ExtendedInfosOwner.ID);
                hidden.Add(await provider.GetCurrentContextUserIDAsync());
                var users = await provider.GetAllUsersAsync();
                principals.ItemsSource = await Task.Run(() => users.Where(u => !hidden.Contains(u.ID)).Select(u => new DialogChoice(u.ID, u.DisplayName)).ToArray());
            }
            principals.SelectedIndex = existing is null ? -1 : 0;
            principals.IsEnabled = existing is null;
        });
    }
}

internal sealed class DmsLinkShareSetup : OperationWindow
{
    internal DmsLinkShareSetup(BaseDmsProvider provider, DmsResourceItem item, DmsLink? existing)
    {
        Title = UiText.Get("LinkSetupTitle");
        var policy = new SharingPolicy(provider);
        var draft = existing is null ? new DmsLink(item, provider) { AllowView = true, AllowDownload = policy.WebDav } : (DmsLink)existing.Clone();
        var body = new StackPanel { Spacing = 4 };
        var id = Field(body, "LabelId", existing?.ID, true);
        id.IsEnabled = existing is not null;
        var name = Field(body, "LabelName", draft.Name);
        var web = Field(body, "LabelWebLink", draft.WebUrl, true);
        web.IsEnabled = existing is not null;
        var download = Field(body, "LabelDownloadLink", draft.DownloadUrl, true);
        var permissions = new PermissionEditor(policy, draft, item, true, existing is null);
        body.Children.Add(permissions.Control);
        var expiryEnabled = new CheckBox { Name = "EnableExpiry", Content = UiText.Get("LabelExpiryDate"), IsChecked = draft.ExpiryDateLocalTime.HasValue };
        var initialExpiry = draft.ExpiryDateLocalTime ?? DateTime.Now;
        var expiryDate = new CalendarDatePicker { Name = "ExpiryDate", SelectedDate = initialExpiry, HorizontalAlignment = HorizontalAlignment.Stretch };
        var expiryTime = new TimePicker { Name = "ExpiryTime", SelectedTime = initialExpiry.TimeOfDay };
        body.Children.Add(expiryEnabled); body.Children.Add(expiryDate); body.Children.Add(expiryTime);
        var passwordEnabled = new CheckBox { Name = "EnablePassword", Content = UiText.Get("LabelPassword"), IsChecked = !string.IsNullOrEmpty(draft.Password) };
        body.Children.Add(passwordEnabled);
        var password = new TextBox { Name = "LinkPassword", Text = draft.Password, PasswordChar = '●' }; body.Children.Add(password);
        var limits = new Dictionary<string, (CheckBox Enabled, TextBox Value)>();
        foreach (var (key, value) in new[] { ("LabelMaxDownloads", draft.MaxDownloads), ("LabelMaxUploads", draft.MaxUploads), ("LabelMaxBytes", draft.MaxBytes) })
        {
            var enabled = new CheckBox { Name = "Enable" + key, Content = UiText.Get(key), IsChecked = value.HasValue, IsEnabled = policy.Teamwork };
            var field = new TextBox { Name = key, Text = value?.ToString(), IsEnabled = policy.Teamwork };
            body.Children.Add(enabled); body.Children.Add(field); limits.Add(key, (enabled, field));
        }
        body.Children.Add(new CheckBox { Name = "EnableLabelMaxViews", Content = UiText.Get("LabelMaxViews"), IsEnabled = false });
        body.Children.Add(new TextBox { Name = "LabelMaxViews", IsEnabled = false });
        Field(body, "LabelNumberDownloads", draft.DownloadsCount?.ToString(), true).IsEnabled = existing is not null;
        Field(body, "LabelNumberUploads", existing is null && policy.Teamwork ? "5000" : draft.UploadsCount?.ToString(), true).IsEnabled = existing is not null;
        Field(body, "LabelNumberBytes", draft.UploadedBytes?.ToString(), true).IsEnabled = existing is not null;
        Field(body, "LabelNumberViews", draft.ViewsCount?.ToString(), true);
        void UpdateFields()
        {
            name.IsEnabled = policy.WebDav || permissions.Value("Upload");
            download.IsEnabled = policy.Teamwork && existing is not null && permissions.Value("Download");
            foreach (var entry in limits)
            {
                var applicable = policy.Teamwork && permissions.Value(entry.Key == "LabelMaxDownloads" ? "Download" : "Upload");
                entry.Value.Enabled.IsEnabled = applicable;
                entry.Value.Value.IsEnabled = applicable;
            }
        }
        passwordEnabled.IsCheckedChanged += (_, _) => password.IsEnabled = passwordEnabled.IsChecked == true;
        expiryEnabled.IsCheckedChanged += (_, _) => expiryDate.IsEnabled = expiryTime.IsEnabled = expiryEnabled.IsChecked == true;
        password.TextChanged += (_, _) => { if (password.IsEnabled && !string.IsNullOrEmpty(password.Text)) passwordEnabled.IsChecked = true; };
        expiryDate.SelectedDateChanged += (_, _) => { if (expiryDate.IsEnabled) expiryEnabled.IsChecked = true; };
        expiryTime.SelectedTimeChanged += (_, _) => { if (expiryTime.IsEnabled) expiryEnabled.IsChecked = true; };
        foreach (var entry in limits.Values)
        {
            entry.Enabled.IsCheckedChanged += (_, _) => entry.Value.IsEnabled = entry.Enabled.IsEnabled && entry.Enabled.IsChecked == true;
            entry.Value.TextChanged += (_, _) => { if (entry.Value.IsEnabled && !string.IsNullOrEmpty(entry.Value.Text)) entry.Enabled.IsChecked = true; };
        }
        foreach (var check in permissions.Control.Children.OfType<CheckBox>())
            check.IsCheckedChanged += (_, _) => UpdateFields();
        UpdateFields();
        long? Limit(string key) => limits[key].Enabled.IsChecked == true ?
            long.Parse(limits[key].Value.Text ?? "", System.Globalization.CultureInfo.CurrentCulture) : null;
        var save = Dialogs.Button("ActionSave", async () =>
        {
            var saved = await RunAsync(async () =>
            {
                draft.Name = name.Text;
                permissions.Apply(draft);
                if (expiryEnabled.IsChecked == true && expiryDate.SelectedDate is null)
                    throw new DmsUserInputMissingException(UiText.Get("LabelExpiryDate"));
                draft.ExpiryDateLocalTime = expiryEnabled.IsChecked == true ?
                    expiryDate.SelectedDate!.Value.Date + (expiryTime.SelectedTime ?? TimeSpan.Zero) : null;
                draft.Password = passwordEnabled.IsChecked == true ? password.Text : null;
                draft.MaxDownloads = Limit("LabelMaxDownloads"); draft.MaxUploads = Limit("LabelMaxUploads"); draft.MaxBytes = Limit("LabelMaxBytes");
                await SharingPolicy.SaveAsync(provider, item, draft, existing is null);
            });
            if (saved) Close(true);
        });
        Content = Dialogs.Layout(new ScrollViewer { Content = body }, Dialogs.Buttons(save, Dialogs.Button("ActionCancel", Close)));
    }
}
