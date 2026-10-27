Option Explicit On
Option Strict On
Imports System.Globalization
Imports System.Threading
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class ReceivedSharingTest
    <Test>
    Public Sub RemovingOutgoingGrantsPreservesTheReceivedDirectionAndAggregate()
        Dim item As New DmsResourceItem With {.ExtendedInfosIsReceivedShare = True, .ExtendedInfosHasOutgoingSharings = True}
        Global.CompuMaster.Dms.BrowserUI.SharingSummary.SynchronizeOutgoing(item)
        Assert.That(item.ExtendedInfosHasOutgoingSharings, [Is].False)
        Assert.That(item.ExtendedInfosIsReceivedShare, [Is].True)
        Assert.That(item.ExtendedInfosIsShared, [Is].True)
    End Sub
    <TestCase("en"), TestCase("de")>
    Public Sub ReceivedGrantIsReadOnlyAndDistinctFromEditableOutgoingLists(language As String)
        Dim previous = CultureInfo.CurrentUICulture
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language)
        Try
            Dim item As New DmsResourceItem With {.Name = "Received.txt", .FullName = "/Received.txt", .ItemType = DmsResourceItem.ItemTypes.File,
                .ExtendedInfosOwner = New DmsUser With {.ID = "original-owner"}, .ExtendedInfosIsReceivedShare = True, .ExtendedInfosHasOutgoingSharings = False,
                .ExtendedInfosLinks = New List(Of DmsLink), .ExtendedInfosUserSharings = New List(Of DmsShareForUser), .ExtendedInfosGroupSharings = New List(Of DmsShareForGroup)}
            item.ExtendedInfosReceivedSharings.Add(New DmsReceivedShare With {.SharedBy = New DmsUser With {.ID = "sharing-user"},
                .ResourceOwner = item.ExtendedInfosOwner, .RecipientKind = DmsReceivedShareKind.User, .RecipientID = "test-recipient", .PermissionsAvailable = True, .AllowView = True})
            Using dispatcher As New UiTestDispatcher(), dialog As New Global.CompuMaster.Dms.BrowserUI.DmsItemSharings With {.DmsProvider = New NoDmsProvider(), .DmsItem = item, .ShowInTaskbar = False, .Opacity = 0}
                dialog.Show() : dispatcher.WaitForEvents()
                Dim summary = dialog.Controls.Find("ReceivedSharingSummary", True).OfType(Of Label)().Single()
                Assert.That(summary.Visible, [Is].True)
                Assert.That(summary.Text, Does.Contain("sharing-user").And.Contain("original-owner"))
                Assert.That(dialog.ListViewInternalSharings.Items.Count, [Is].Zero)
                Assert.That(dialog.ListViewExternalSharings.Items.Count, [Is].Zero)
                Assert.That(summary.Parent.Bottom, [Is].LessThan(dialog.GroupBoxInternalSharings.Top))
                dialog.Close()
            End Using
        Finally
            CultureInfo.CurrentUICulture = previous
        End Try
    End Sub
End Class
