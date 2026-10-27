using CompuMaster.Dms.BrowserUI.Avalonia;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using CompuMaster.Dms.TestDemo.Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless.NUnit;
using global::Avalonia.VisualTree;
using NUnit.Framework;

namespace CompuMaster.Dms.Test.BrowserUI.Avalonia;

[TestFixture]
public sealed class SharingTests
{
    [AvaloniaTest]
    public void Link_permission_toggles_preserve_WinForms_validation_and_download_dependency()
    {
        var provider = new DemoDmsProvider { ProviderKind = BaseDmsProvider.DmsProviders.Scopevisio };
        var item = provider.Items.First(i => i.Name == "Welcome.txt");
        var link = new DmsLink(item, provider) { AllowView = true };
        var policy = new SharingPolicy(provider);
        var editor = new PermissionEditor(policy, link, item, true, true);
        CheckBox Permission(string name) => editor.Control.Children.OfType<CheckBox>().Single(c => c.Name == "Permission" + name);
        Permission("Upload").IsChecked = true;
        Assert.That(editor.Value("View"), Is.True, "WinForms validates conflicting view/upload choices on Save.");
        editor.Apply(link);
        Assert.Throws<DmsUserInputInvalidException>((Action)(() => policy.Validate(link)));
        Permission("View").IsChecked = false;
        Permission("Download").IsChecked = true;
        Assert.That(editor.Value("View"), Is.True);
        Assert.That(editor.Value("Upload"), Is.True);
        Permission("View").IsChecked = false;
        Assert.That(editor.Value("Download"), Is.False);
    }

    [AvaloniaTest]
    public void Standard_share_hiding_view_clears_dependent_permissions_but_retains_upload()
    {
        var provider = new DemoDmsProvider();
        var item = provider.Items.First(i => i.Name == "Welcome.txt");
        var share = new DmsShareForUser(item, new DmsUser { ID = "fixture" }, true, true, true, true, true, true);
        var editor = new PermissionEditor(new SharingPolicy(provider), share, item, false, false);
        editor.Control.Children.OfType<CheckBox>().Single(c => c.Name == "PermissionView").IsChecked = false;
        Assert.That(new[] { "Download", "Edit", "Delete", "Share" }.Select(editor.Value), Is.All.False);
        Assert.That(editor.Value("Upload"), Is.True);
    }

    [Test]
    public async Task Link_creation_reconciles_provider_updates_and_deletion_updates_shared_flags()
    {
        var provider = new UpdatingLinkProvider();
        var item = provider.Items.First(i => i.Name == "Welcome.txt");
        var requested = new DmsLink(item, provider) { AllowView = true, AllowDownload = true };
        await SharingPolicy.SaveAsync(provider, item, requested, true);
        Assert.That(item.ExtendedInfosLinks.Count, Is.EqualTo(1));
        Assert.That(item.ExtendedInfosLinks.Single().ID, Is.EqualTo("created"));
        Assert.That(item.ExtendedInfosLinks.Single().ParentDmsResourceItem, Is.SameAs(item));
        Assert.That(item.ExtendedInfosIsShared, Is.True);
        await SharingPolicy.DeleteAsync(provider, item, item.ExtendedInfosLinks.Single());
        Assert.That(item.ExtendedInfosLinks, Is.Empty);
        Assert.That(item.ExtendedInfosIsShared, Is.False);
    }
    private sealed class UpdatingLinkProvider : DemoDmsProvider
    {
        public override DmsLink CreateLink(DmsResourceItem resource, DmsLink share)
        {
            var result = new DmsLink(resource, this) { ID = "created", AllowView = true, AllowDownload = true };
            resource.ExtendedInfosLinks.Add(result); resource.ExtendedInfosLinks.Add(result);
            return result;
        }
    }
    [Test]
    public async Task User_and_group_sharing_create_update_delete_are_independent()
    {
        var provider = new DemoDmsProvider();
        var item = provider.Items.First(i => i.Name == "Welcome.txt");
        var user = new DmsShareForUser(item, new DmsUser { ID = "bob", DisplayName = "Bob" }, true, true, false, false, false, false);
        var group = new DmsShareForGroup(item, new DmsGroup { ID = "team", Name = "Team" }, true, true, false, false, false, false);
        await SharingPolicy.SaveAsync(provider, item, user, true);
        await SharingPolicy.SaveAsync(provider, item, group, true);
        var updated = (DmsShareForUser)user.Clone(); updated.AllowEdit = true;
        await SharingPolicy.SaveAsync(provider, item, updated, false);
        Assert.That(item.ExtendedInfosUserSharings.Single().AllowEdit, Is.True);
        await SharingPolicy.DeleteAsync(provider, item, updated);
        Assert.That(item.ExtendedInfosHasUserSharings, Is.False);
        Assert.That(item.ExtendedInfosHasGroupSharings, Is.True);
        Assert.That(item.ExtendedInfosIsShared, Is.True);
        await SharingPolicy.DeleteAsync(provider, item, group);
        Assert.That(item.ExtendedInfosIsShared, Is.False);
        item.ExtendedInfosHasHiddenUserSharings = true;
        await SharingPolicy.SaveAsync(provider, item, user, true);
        await SharingPolicy.DeleteAsync(provider, item, user);
        Assert.That(item.ExtendedInfosIsShared, Is.True, "Hidden shares must retain the sharing indicator.");
    }
    [Test]
    public void Provider_permission_and_link_limit_rules_match_WinForms()
    {
        var webdav = new DemoDmsProvider();
        var webdavPolicy = new SharingPolicy(webdav);
        var file = webdav.Items.First(i => i.ItemType == DmsResourceItem.ItemTypes.File);
        Assert.That(webdavPolicy.EditablePermission("View", true, true, file, null), Is.False);
        Assert.That(webdavPolicy.EditablePermission("Upload", true, true, file, null), Is.False);
        Assert.Throws<DmsUserInputInvalidException>((Action)(() => webdavPolicy.Validate(new DmsLink(file, webdav) { AllowView = true })));
        var teamwork = new DemoDmsProvider { ProviderKind = BaseDmsProvider.DmsProviders.Scopevisio };
        var policy = new SharingPolicy(teamwork);
        Assert.That(policy.EditablePermission("Edit", false, true, file, null), Is.False);
        var upload = new DmsLink(file, teamwork) { AllowUpload = true };
        Assert.Throws<DmsUserInputMissingException>((Action)(() => policy.Validate(upload)));
        upload.Name = "Upload"; upload.MaxBytes = -1;
        Assert.Throws<DmsUserInputInvalidException>((Action)(() => policy.Validate(upload)));
        upload.MaxBytes = 1024;
        policy.Validate(upload);
        var noSharing = new DemoDmsProvider { SharingSupported = false };
        Assert.Throws<NotSupportedException>((Action)(() => new SharingPolicy(noSharing)));
    }
    [Test]
    public async Task Properties_include_versions_links_locks_and_unknown_child_metadata()
    {
        var provider = new DemoDmsProvider();
        var item = provider.Items.First(i => i.Name == "Shared.txt");
        item.ExtendedInfosVersion = "42"; item.ExtendedInfosLocks = new() { "lock-example" };
        item.ExtendedInfosArchivedDateLocalTime = new DateTime(2025, 1, 2);
        var properties = await ItemProperties.FormatAsync(provider, item);
        Assert.That(properties, Does.Contain("42").And.Contain("lock-example").And.Contain("demo-link").And.Contain("https://example.invalid/demo-link"));
        Assert.That(properties, Does.Contain("File ID: ").And.Contain("HasChildDirectories: False"));
        var folder = provider.Items.First(i => i.Name == "Empty");
        folder.HasChildDirectories = null; folder.ChildDirectoryCount = null;
        var unknown = await ItemProperties.FormatAsync(provider, folder);
        Assert.That(unknown, Does.Contain("HasChildDirectories: " + Environment.NewLine));
    }
    [AvaloniaTest]
    public async Task Standard_sharing_dialog_excludes_current_owner_and_existing_principals()
    {
        var provider = new DemoDmsProvider();
        var item = provider.Items.First(i => i.Name == "Shared.txt");
        var owner = new Window(); owner.Show();
        var dialog = new DmsStandardShareSetup(provider, item, false, null);
        try
        {
            dialog.Show(owner); await WaitAsync(dialog);
            var principals = dialog.GetVisualDescendants().OfType<ComboBox>().Single();
            Assert.That(principals.ItemsSource!.Cast<DialogChoice>().Select(c => c.ID), Is.EqualTo(new[] { "bob" }));
            principals.SelectedIndex = 0;
            BrowserUiTests.Click(BrowserUiTests.FindButton(dialog, "ActionSave")); await WaitAsync(dialog);
            Assert.That(dialog.IsVisible, Is.False);
            Assert.That(item.ExtendedInfosUserSharings.Any(s => s.User.ID == "bob"), Is.True);
        }
        finally { dialog.Close(); owner.Close(); }
    }
    [AvaloniaTest]
    public async Task External_link_dialog_saves_free_framework_controls_and_capability_gates()
    {
        var provider = new DemoDmsProvider();
        var item = provider.Items.First(i => i.Name == "Welcome.txt");
        var owner = new Window(); owner.Show();
        var dialog = new DmsLinkShareSetup(provider, item, null);
        try
        {
            dialog.Show(owner);
            var view = dialog.GetVisualDescendants().OfType<CheckBox>().Single(c => c.Content?.ToString() == UiText.Get("PermissionView"));
            var upload = dialog.GetVisualDescendants().OfType<CheckBox>().Single(c => c.Content?.ToString() == UiText.Get("PermissionUpload"));
            Assert.That(view.IsEnabled, Is.False); Assert.That(upload.IsEnabled, Is.False);
            BrowserUiTests.Click(BrowserUiTests.FindButton(dialog, "ActionSave")); await WaitAsync(dialog);
            Assert.That(dialog.IsVisible, Is.False);
            Assert.That(item.ExtendedInfosLinks.Count, Is.EqualTo(1));
        }
        finally { dialog.Close(); owner.Close(); }
    }
    [AvaloniaTest]
    public async Task Sharing_dialog_shows_hidden_shares_and_link_permissions_without_mutating_them()
    {
        var provider = new DemoDmsProvider();
        var item = provider.Items.First(i => i.Name == "Shared.txt"); item.ExtendedInfosHasHiddenGroupSharings = true;
        var owner = new Window(); owner.Show();
        var dialog = new DmsItemSharings(provider, item);
        try
        {
            dialog.Show(owner); await WaitAsync(dialog);
            var lists = dialog.GetVisualDescendants().OfType<DataGrid>().ToArray();
            Assert.That(lists.Select(grid => grid.Columns.Count), Is.EqualTo(new[] { 3, 3 }));
            var rows = lists.SelectMany(l => l.ItemsSource!.Cast<SharingRow>()).ToArray();
            Assert.That(rows.Any(r => r.Share is null && r.Description.Contains(UiText.Get("HiddenValue"))), Is.True);
            Assert.That(rows.Any(r => r.Share is DmsLink), Is.True);
        }
        finally { dialog.Close(); owner.Close(); }
    }
    private static async Task WaitAsync(OperationWindow window)
    {
        var end = DateTime.UtcNow.AddSeconds(5);
        while (window.IsRunning)
        {
            if (DateTime.UtcNow > end) Assert.Fail("Sharing operation did not complete.");
            await Task.Delay(5);
        }
    }
}
