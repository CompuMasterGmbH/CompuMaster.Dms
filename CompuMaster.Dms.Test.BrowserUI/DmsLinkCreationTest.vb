Imports System.Reflection
Imports System.Threading
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture>
<Apartment(ApartmentState.STA)>
Public Class DmsLinkCreationTest

    <Test>
    Public Sub CreateLinkWithProviderModelMutationShowsExactlyOneRowAndPreservesDetails()
        Dim provider As New ScopevisioTeamworkDmsProvider(True)
        Dim dmsItem As DmsResourceItem = CreateDmsItem()
        Dim expiryDate As New DateTime(2030, 4, 5, 16, 30, 0, DateTimeKind.Local)
        Dim createdLink As DmsLink

        Using setupDialog As New Global.CompuMaster.Dms.BrowserUI.DmsLinkShareSetup()
            setupDialog.DmsProvider = provider
            setupDialog.DmsItem = dmsItem
            setupDialog.DialogMode = Global.CompuMaster.Dms.BrowserUI.DmsLinkShareSetup.DialogModes.CreateLink
            InvokePrivateMethod(setupDialog, "DmsLinkShare_Load")
            setupDialog.CheckBoxAllowDownload.Checked = True
            setupDialog.CheckBoxExpiryDate.Checked = True
            setupDialog.DateTimePickerExpiryDate.Value = expiryDate
            setupDialog.CheckBoxMaxDownloads.Checked = True
            setupDialog.TextBoxMaxDownloads.Text = "12"
            setupDialog.CheckBoxPassword.Checked = True
            setupDialog.TextBoxPassword.Text = "secret"

            InvokePrivateMethod(setupDialog, "ButtonSave_Click")

            ClassicAssert.AreEqual(Global.System.Windows.Forms.DialogResult.OK, setupDialog.DialogResult)
            createdLink = setupDialog.DmsUpdatedLinkDetails
        End Using

        ClassicAssert.AreEqual(1, provider.CreateLinkCallCount)
        ClassicAssert.AreEqual(1, dmsItem.ExtendedInfosLinks.Count)
        ClassicAssert.AreSame(createdLink, dmsItem.ExtendedInfosLinks(0))
        ClassicAssert.AreEqual("link-1", createdLink.ID)
        ClassicAssert.IsTrue(createdLink.AllowView)
        ClassicAssert.IsTrue(createdLink.AllowDownload)
        ClassicAssert.AreEqual("https://example.test/view/link-1", createdLink.WebUrl)
        ClassicAssert.AreEqual("https://example.test/download/link-1", createdLink.DownloadUrl)
        ClassicAssert.AreEqual(expiryDate, createdLink.ExpiryDateLocalTime)
        ClassicAssert.AreEqual(1, RenderedExternalSharingRowCount(dmsItem, provider))
    End Sub

    <Test>
    Public Sub RepeatedCreationPreservesDistinctLinksAndReopeningDoesNotAccumulateRows()
        Dim provider As New ScopevisioTeamworkDmsProvider(True)
        Dim dmsItem As DmsResourceItem = CreateDmsItem()

        Dim firstLink As DmsLink = Global.CompuMaster.Dms.BrowserUI.DmsLinkShareSetup.CreateLinkAndSynchronizeDmsItem(
            provider,
            dmsItem,
            New DmsLink(dmsItem, provider) With {.AllowView = True})
        Dim secondLink As DmsLink = Global.CompuMaster.Dms.BrowserUI.DmsLinkShareSetup.CreateLinkAndSynchronizeDmsItem(
            provider,
            dmsItem,
            New DmsLink(dmsItem, provider) With {.AllowUpload = True, .Name = "Upload link"})

        ClassicAssert.AreEqual(2, provider.CreateLinkCallCount)
        ClassicAssert.AreEqual(2, dmsItem.ExtendedInfosLinks.Count)
        CollectionAssert.AreEquivalent(New String() {firstLink.ID, secondLink.ID}, dmsItem.ExtendedInfosLinks.Select(Function(link) link.ID).ToArray())
        ClassicAssert.AreEqual(2, RenderedExternalSharingRowCount(dmsItem, provider))

        Dim reloadedItem As DmsResourceItem = CreateDmsItem()
        reloadedItem.ExtendedInfosLinks.Add(firstLink)
        reloadedItem.ExtendedInfosLinks.Add(secondLink)

        ClassicAssert.AreEqual(2, RenderedExternalSharingRowCount(reloadedItem, provider))
        ClassicAssert.AreEqual(2, reloadedItem.ExtendedInfosLinks.Count)
    End Sub

    <Test>
    Public Sub CreateLinkAddsReturnedLinkWhenProviderDoesNotMutateTheModel()
        Dim provider As New ScopevisioTeamworkDmsProvider(False)
        Dim dmsItem As DmsResourceItem = CreateDmsItem()

        Dim createdLink As DmsLink = Global.CompuMaster.Dms.BrowserUI.DmsLinkShareSetup.CreateLinkAndSynchronizeDmsItem(
            provider,
            dmsItem,
            New DmsLink(dmsItem, provider) With {.AllowView = True})

        ClassicAssert.AreEqual(1, provider.CreateLinkCallCount)
        ClassicAssert.AreEqual(1, dmsItem.ExtendedInfosLinks.Count)
        ClassicAssert.AreSame(createdLink, dmsItem.ExtendedInfosLinks(0))
    End Sub

    <Test>
    Public Sub CreationFailureAndCancellationDoNotAddLinks()
        Dim provider As New ScopevisioTeamworkDmsProvider(True) With {.ThrowOnCreate = True}
        Dim dmsItem As DmsResourceItem = CreateDmsItem()
        Dim existingLink As New DmsLink(dmsItem, "existing-link", provider, Nothing)
        dmsItem.ExtendedInfosLinks.Add(existingLink)

        ClassicAssert.Throws(Of InvalidOperationException)(
            Sub()
                Global.CompuMaster.Dms.BrowserUI.DmsLinkShareSetup.CreateLinkAndSynchronizeDmsItem(
                    provider,
                    dmsItem,
                    New DmsLink(dmsItem, provider) With {.AllowView = True})
            End Sub)

        ClassicAssert.AreEqual(1, provider.CreateLinkCallCount)
        ClassicAssert.AreEqual(1, dmsItem.ExtendedInfosLinks.Count)
        ClassicAssert.AreSame(existingLink, dmsItem.ExtendedInfosLinks(0))

        provider.ThrowOnCreate = False
        Using setupDialog As New Global.CompuMaster.Dms.BrowserUI.DmsLinkShareSetup()
            setupDialog.DmsProvider = provider
            setupDialog.DmsItem = dmsItem
            setupDialog.DialogMode = Global.CompuMaster.Dms.BrowserUI.DmsLinkShareSetup.DialogModes.CreateLink
            InvokePrivateMethod(setupDialog, "ButtonCancel_Click")

            ClassicAssert.AreEqual(Global.System.Windows.Forms.DialogResult.Cancel, setupDialog.DialogResult)
            ClassicAssert.IsNull(setupDialog.DmsUpdatedLinkDetails)
        End Using

        ClassicAssert.AreEqual(1, provider.CreateLinkCallCount)
        ClassicAssert.AreEqual(1, dmsItem.ExtendedInfosLinks.Count)
    End Sub

    Private Shared Function CreateDmsItem() As DmsResourceItem
        Return New DmsResourceItem() With {
            .ItemType = DmsResourceItem.ItemTypes.File,
            .Name = "document.pdf",
            .FullName = "documents/document.pdf",
            .ExtendedInfosOwner = New DmsUser() With {.ID = "owner", .Name = "Owner"},
            .ExtendedInfosLinks = New List(Of DmsLink),
            .ExtendedInfosGroupSharings = New List(Of DmsShareForGroup),
            .ExtendedInfosUserSharings = New List(Of DmsShareForUser)
        }
    End Function

    Private Shared Function RenderedExternalSharingRowCount(dmsItem As DmsResourceItem, provider As BaseDmsProvider) As Integer
        Using sharingDialog As New Global.CompuMaster.Dms.BrowserUI.DmsItemSharings()
            sharingDialog.DmsItem = dmsItem
            sharingDialog.DmsProvider = provider
            InvokePrivateMethod(sharingDialog, "DmsItemSharings_Load")
            Return sharingDialog.ListViewExternalSharings.Items.Count
        End Using
    End Function

    Private Shared Sub InvokePrivateMethod(instance As Object, methodName As String)
        Dim method As MethodInfo = instance.GetType().GetMethod(methodName, BindingFlags.Instance Or BindingFlags.NonPublic)
        ClassicAssert.IsNotNull(method, methodName)
        method.Invoke(instance, New Object() {Nothing, EventArgs.Empty})
    End Sub

    Private Class ScopevisioTeamworkDmsProvider
        Inherits NoDmsProvider

        Private ReadOnly MutateResource As Boolean

        Public Sub New(mutateResource As Boolean)
            Me.MutateResource = mutateResource
        End Sub

        Public Property CreateLinkCallCount As Integer
        Public Property ThrowOnCreate As Boolean

        Public Overrides Function CreateLink(dmsResource As DmsResourceItem, shareInfo As DmsLink) As DmsLink
            Me.CreateLinkCallCount += 1
            If Me.ThrowOnCreate Then Throw New InvalidOperationException("Simulated backend failure.")

            Dim id As String = "link-" & Me.CreateLinkCallCount
            Dim createdLink As New DmsLink(dmsResource, id, Me, Nothing) With {
                .Name = shareInfo.Name,
                .AllowView = shareInfo.AllowView,
                .AllowDownload = shareInfo.AllowDownload,
                .AllowUpload = shareInfo.AllowUpload,
                .ExpiryDateLocalTime = shareInfo.ExpiryDateLocalTime,
                .MaxDownloads = shareInfo.MaxDownloads,
                .Password = shareInfo.Password,
                .WebUrl = "https://example.test/view/" & id,
                .DownloadUrl = "https://example.test/download/" & id
            }
            If Me.MutateResource Then dmsResource.ExtendedInfosLinks.Add(createdLink)
            Return createdLink
        End Function
    End Class

End Class
