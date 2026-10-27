using System.Globalization;
using CompuMaster.Dms.Data;
using NUnit.Framework;
#if DMS_WINUI
using CompuMaster.Dms.BrowserUI.WinUI;
using CompuMaster.Dms.TestDemo.WinUI;
namespace CompuMaster.Dms.Test.BrowserUI.WinUI;
#else
using CompuMaster.Dms.BrowserUI.Avalonia;
using CompuMaster.Dms.TestDemo.Avalonia;
namespace CompuMaster.Dms.Test.BrowserUI.Avalonia;
#endif

[TestFixture]
public sealed class ReceivedSharingTests
{
    internal static DmsResourceItem ReceivedItem()
    {
        var item = new DmsResourceItem { Name = "Received.txt", FullName = "/Received.txt", ItemType = DmsResourceItem.ItemTypes.File,
            ExtendedInfosOwner = new DmsUser { ID = "original-owner", DisplayName = "Original owner" },
            ExtendedInfosIsReceivedShare = true, ExtendedInfosHasOutgoingSharings = false };
        item.ExtendedInfosReceivedSharings.Add(new DmsReceivedShare { ParentDmsResourceItem = item, ID = "grant-1",
            SharedBy = new DmsUser { ID = "sharing-user", DisplayName = "Sharing person" }, ResourceOwner = item.ExtendedInfosOwner,
            RecipientKind = DmsReceivedShareKind.Group, RecipientID = "reviewers", RecipientDisplayName = "Review team", IsInherited = true, SharedPath = "/Parent",
            PermissionsAvailable = true, AllowView = true, AllowDownload = true });
        return item;
    }
    [TestCase("en"), TestCase("de")]
    public void Summary_separates_received_grants_owner_sharer_inheritance_and_outgoing_state(string language)
    {
        var previous = CultureInfo.CurrentUICulture; CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        try
        {
            var item = ReceivedItem(); var text = SharingSummary.Format(item);
            Assert.That(text, Does.Contain(UiText.Get("ReceivedSharingTitle")).And.Contain("Sharing person").And.Contain("Original owner").And.Contain("Review team").And.Contain("/Parent"));
            Assert.That(text, Does.Contain(UiText.Format("OutgoingSharingStatus", UiText.Get("SharingNo"))));
            Assert.That(text, Does.Contain(UiText.Get("PermissionDownload")));
            Assert.That(item.ExtendedInfosIsShared, Is.True, "The compatibility aggregate includes a received grant.");
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
    [Test]
    public void Unknown_received_state_is_distinct_from_false_and_never_inferred_from_owner()
    {
        var item = ReceivedItem(); item.ExtendedInfosIsReceivedShare = null;
        var unknown = SharingSummary.Format(item);
        Assert.That(unknown, Does.Contain(UiText.Get("SharingUnknown")));
        Assert.That(unknown, Does.Not.Contain("Sharing person"));
        item.ExtendedInfosIsReceivedShare = false;
        Assert.That(SharingSummary.Format(item), Does.Contain(UiText.Format("ReceivedSharingStatus", UiText.Get("SharingNo"))));
    }
    [Test]
    public void Unknown_permissions_differ_from_explicit_zero_permissions()
    {
        var item = ReceivedItem(); var grant = item.ExtendedInfosReceivedSharings.Single(); grant.PermissionsAvailable = false;
        Assert.That(SharingSummary.Format(item), Does.Contain(UiText.Format("SharingGrantPermissions", UiText.Get("SharingUnknown"))));
        grant.PermissionsAvailable = true; grant.AllowView = grant.AllowDownload = false;
        Assert.That(SharingSummary.Format(item), Does.Contain(UiText.Format("SharingGrantPermissions", UiText.Get("SharingNoPermissions"))));
    }
    [Test]
    public async Task Properties_show_received_direction_and_saving_or_removing_outgoing_grants_preserves_it()
    {
        var item = ReceivedItem(); var provider = new DemoDmsProvider();
        var share = new DmsShareForUser(item, new DmsUser { ID = "outgoing-recipient" }, true, true, false, false, false, false);
        await SharingPolicy.SaveAsync(provider, item, share, true);
        Assert.That(item.ExtendedInfosHasOutgoingSharings, Is.True);
        await SharingPolicy.DeleteAsync(provider, item, share);
        Assert.That(item.ExtendedInfosHasOutgoingSharings, Is.False);
        Assert.That(item.ExtendedInfosIsShared, Is.True);
        var details = await ItemProperties.FormatAsync(provider, item);
        Assert.That(details, Does.Contain(UiText.Get("ReceivedSharingTitle")).And.Contain("Sharing person").And.Contain("Original owner"));
    }
}
