Imports System.Reflection
Imports System.Threading
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture>
<Apartment(ApartmentState.STA)>
Public Class DmsLinkCreationTest

    <TestCase(DmsResourceItem.ItemTypes.File, False)>
    <TestCase(DmsResourceItem.ItemTypes.Folder, True)>
    Public Sub WebDavLinkControlsDoNotOfferUnsupportedLimits(itemType As DmsResourceItem.ItemTypes, uploadSupported As Boolean)
        Dim provider As New WebDavDmsProvider
        Dim item As DmsResourceItem = CreateDmsItem()
        item.ItemType = itemType
        Using dialog As New Global.CompuMaster.Dms.BrowserUI.DmsLinkShareSetup()
            dialog.DmsProvider = provider
            dialog.DmsItem = item
            dialog.DialogMode = Global.CompuMaster.Dms.BrowserUI.DmsLinkShareSetup.DialogModes.CreateLink
            InvokePrivateMethod(dialog, "DmsLinkShare_Load")
            Assert.That(dialog.CheckBoxMaxBytes.Enabled, [Is].False)
            Assert.That(dialog.TextBoxMaxBytes.Enabled, [Is].False)
            Assert.That(dialog.CheckBoxMaxDownloads.Enabled, [Is].False)
            Assert.That(dialog.CheckBoxMaxUploads.Enabled, [Is].False)
            Assert.That(dialog.CheckBoxAllowView.Checked, [Is].True)
            Assert.That(dialog.CheckBoxAllowDownload.Checked, [Is].True)
            Assert.That(dialog.CheckBoxAllowUpload.Enabled, [Is].EqualTo(uploadSupported))
        End Using
    End Sub

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
        ClassicAssert.AreEqual(12, createdLink.MaxDownloads)
        ClassicAssert.AreEqual("secret", createdLink.Password)
        ClassicAssert.AreEqual(1, RenderedExternalSharingRowCount(dmsItem, provider))
    End Sub

    <TestCase(DmsResourceItem.ItemTypes.File, False)>
    <TestCase(DmsResourceItem.ItemTypes.Folder, False)>
    <TestCase(DmsResourceItem.ItemTypes.Collection, True)>
    Public Sub ScopevisioUploadLinksAreOfferedOnlyForCollections(itemType As DmsResourceItem.ItemTypes, uploadSupported As Boolean)
        Dim provider As New ScopevisioTeamworkDmsProvider(True)
        Dim dmsItem As DmsResourceItem = CreateDmsItem()
        dmsItem.ItemType = itemType

        Using setupDialog As New Global.CompuMaster.Dms.BrowserUI.DmsLinkShareSetup()
            setupDialog.DmsProvider = provider
            setupDialog.DmsItem = dmsItem
            setupDialog.DialogMode = Global.CompuMaster.Dms.BrowserUI.DmsLinkShareSetup.DialogModes.CreateLink
            InvokePrivateMethod(setupDialog, "DmsLinkShare_Load")

            ClassicAssert.AreEqual(uploadSupported, setupDialog.CheckBoxAllowUpload.Enabled)
            ClassicAssert.IsFalse(setupDialog.CheckBoxAllowUpload.Checked)
            ClassicAssert.IsTrue(setupDialog.CheckBoxAllowView.Enabled)
            ClassicAssert.IsFalse(setupDialog.CheckBoxMaxBytes.Enabled)
            If uploadSupported Then
                setupDialog.CheckBoxAllowView.Checked = False
                setupDialog.CheckBoxAllowUpload.Checked = True
                ClassicAssert.IsTrue(setupDialog.CheckBoxMaxBytes.Enabled)
            End If
        End Using
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

    <Test>
    Public Sub EditingOptionalValuesAutomaticallySelectsTheirCheckboxes()
        Dim provider As New ScopevisioTeamworkDmsProvider(True)
        Using setupDialog As New Global.CompuMaster.Dms.BrowserUI.DmsLinkShareSetup()
            setupDialog.DmsProvider = provider
            setupDialog.DmsItem = CreateDmsItem()
            setupDialog.DialogMode = Global.CompuMaster.Dms.BrowserUI.DmsLinkShareSetup.DialogModes.CreateLink
            InvokePrivateMethod(setupDialog, "DmsLinkShare_Load")

            ClassicAssert.IsFalse(setupDialog.CheckBoxExpiryDate.Checked)
            Dim originalDate As DateTime = setupDialog.DateTimePickerExpiryDate.Value
            setupDialog.DateTimePickerExpiryDate.Value = originalDate
            ClassicAssert.IsFalse(setupDialog.CheckBoxExpiryDate.Checked)
            setupDialog.DateTimePickerExpiryDate.Value = originalDate.AddDays(1)
            ClassicAssert.IsTrue(setupDialog.CheckBoxExpiryDate.Checked)

            setupDialog.TextBoxPassword.Text = "secret"
            ClassicAssert.IsTrue(setupDialog.CheckBoxPassword.Checked)
            setupDialog.CheckBoxAllowDownload.Checked = True
            setupDialog.TextBoxMaxDownloads.Text = "12"
            ClassicAssert.IsTrue(setupDialog.CheckBoxMaxDownloads.Checked)
            ClassicAssert.IsFalse(setupDialog.TextBoxMaxBytes.Enabled)

            setupDialog.CheckBoxAllowDownload.Checked = False
            setupDialog.CheckBoxAllowView.Checked = False
            setupDialog.CheckBoxAllowUpload.Checked = True
            setupDialog.TextBoxMaxUploads.Text = "4"
            ClassicAssert.IsTrue(setupDialog.CheckBoxMaxUploads.Checked)
            setupDialog.TextBoxMaxBytes.Text = "123"
            ClassicAssert.IsTrue(setupDialog.CheckBoxMaxBytes.Checked)

            'Scopevisio does not support a maximum view count; verify the handler if a provider enables it.
            setupDialog.TextBoxMaxViews.Enabled = True
            setupDialog.TextBoxMaxViews.Text = "8"
            ClassicAssert.IsTrue(setupDialog.CheckBoxMaxViews.Checked)

            InvokePrivateMethod(setupDialog, "SaveControlDataIntoDmsLink", False)
            ClassicAssert.AreEqual(4, setupDialog.DmsUpdatedLinkDetails.MaxUploads)
            ClassicAssert.AreEqual(123, setupDialog.DmsUpdatedLinkDetails.MaxBytes)
            ClassicAssert.IsNull(setupDialog.DmsUpdatedLinkDetails.MaxDownloads)
        End Using
    End Sub

    <Test>
    Public Sub ClearingPasswordCheckboxOmitsEnteredPassword()
        Dim provider As New ScopevisioTeamworkDmsProvider(True)
        Using setupDialog As New Global.CompuMaster.Dms.BrowserUI.DmsLinkShareSetup()
            setupDialog.DmsProvider = provider
            setupDialog.DmsItem = CreateDmsItem()
            setupDialog.DialogMode = Global.CompuMaster.Dms.BrowserUI.DmsLinkShareSetup.DialogModes.CreateLink
            InvokePrivateMethod(setupDialog, "DmsLinkShare_Load")

            setupDialog.TextBoxPassword.Text = "secret"
            setupDialog.CheckBoxPassword.Checked = False
            InvokePrivateMethod(setupDialog, "SaveControlDataIntoDmsLink", False)

            ClassicAssert.IsNull(setupDialog.DmsUpdatedLinkDetails.Password)
        End Using
    End Sub

    <Test>
    Public Sub DeletingLinkNotifiesBrowserToRefreshItsSharingIcon()
        Dim provider As New ScopevisioTeamworkDmsProvider(True)
        Dim dmsItem As DmsResourceItem = CreateDmsItem()
        dmsItem.ExtendedInfosLinks.Add(New DmsLink(dmsItem, "existing-link", provider, Nothing) With {.AllowView = True})

        Using sharingDialog As New Global.CompuMaster.Dms.BrowserUI.DmsItemSharings()
            sharingDialog.DmsItem = dmsItem
            sharingDialog.DmsProvider = provider
            InvokePrivateMethod(sharingDialog, "DmsItemSharings_Load")
            sharingDialog.ListViewExternalSharings.CreateControl()
            sharingDialog.ListViewExternalSharings.Items(0).Selected = True
            Dim notifications As Integer
            AddHandler sharingDialog.SharingsChanged, Sub(sender, e) notifications += 1

            InvokePrivateMethod(sharingDialog, "ToolStripButtonExternalSharingsDelete_Click")

            ClassicAssert.AreEqual(1, provider.DeleteLinkCallCount)
            ClassicAssert.AreEqual(1, notifications)
            ClassicAssert.AreEqual(0, dmsItem.ExtendedInfosLinks.Count)
        End Using
    End Sub

    <Test>
    Public Sub SharingChangeImmediatelyUpdatesOnlyTheAffectedFileIcon()
        Dim provider As New ScopevisioTeamworkDmsProvider(True)
        Dim changedFile As DmsResourceItem = CreateDmsItem()
        changedFile.Name = "document"
        changedFile.ExtendedInfosFileID = "changed-file"
        Dim otherFile As DmsResourceItem = CreateDmsItem()
        otherFile.Name = "other"
        otherFile.ExtendedInfosFileID = "other-file"

        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser()
            Dim changedRow As New Global.System.Windows.Forms.ListViewItem(changedFile.Name, 6) With {.Tag = changedFile}
            Dim otherRow As New Global.System.Windows.Forms.ListViewItem(otherFile.Name, 6) With {.Tag = otherFile}
            browser.ListViewDmsFiles.Items.Add(changedRow)
            browser.ListViewDmsFiles.Items.Add(otherRow)

            provider.RefreshedFile = New DmsResourceItem() With {.ItemType = DmsResourceItem.ItemTypes.File, .ExtendedInfosFileID = changedFile.ExtendedInfosFileID, .ExtendedInfosIsShared = True}
            browser.RefreshSharingVisuals(changedFile, provider)

            Dim sharedImageIndex As Integer = changedRow.ImageIndex
            ClassicAssert.AreNotEqual(6, sharedImageIndex)
            ClassicAssert.AreEqual(6, otherRow.ImageIndex)
            ClassicAssert.IsTrue(changedFile.ExtendedInfosIsShared)

            provider.RefreshedFile.ExtendedInfosIsShared = False
            browser.RefreshSharingVisuals(changedFile, provider)

            ClassicAssert.AreNotEqual(sharedImageIndex, changedRow.ImageIndex)
            ClassicAssert.AreEqual(6, otherRow.ImageIndex)
            ClassicAssert.IsFalse(changedFile.ExtendedInfosIsShared)
            ClassicAssert.AreEqual(2, browser.ListViewDmsFiles.Items.Count)
        End Using
    End Sub

    <Test>
    Public Sub SharingChangeImmediatelyUpdatesFolderTreeIcon()
        Dim provider As New ScopevisioTeamworkDmsProvider(True)
        Dim folder As New DmsResourceItem() With {
            .ItemType = DmsResourceItem.ItemTypes.Folder,
            .Name = "folder",
            .FullName = "folder",
            .ExtendedInfosFolderID = "folder-id"
        }

        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser()
            Dim parentNode As Global.System.Windows.Forms.TreeNode = browser.TreeViewDmsFolders.Nodes.Add("root")
            Dim folderNode As Global.System.Windows.Forms.TreeNode = parentNode.Nodes.Add("folder")
            Dim nodeTagType As Type = GetType(Global.CompuMaster.Dms.BrowserUI.DmsBrowser).GetNestedType("NodeTagData", BindingFlags.NonPublic)
            ClassicAssert.IsNotNull(nodeTagType)
            folderNode.Tag = Activator.CreateInstance(nodeTagType, BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic, Nothing, New Object() {folder}, Nothing)

            provider.RefreshedFolder = New DmsResourceItem() With {.ItemType = DmsResourceItem.ItemTypes.Folder, .ExtendedInfosFolderID = folder.ExtendedInfosFolderID, .ExtendedInfosIsShared = True}
            browser.RefreshSharingVisuals(folder, provider)
            ClassicAssert.AreEqual(5, folderNode.ImageIndex)
            ClassicAssert.AreEqual(5, folderNode.SelectedImageIndex)

            provider.RefreshedFolder.ExtendedInfosIsShared = False
            browser.RefreshSharingVisuals(folder, provider)
            ClassicAssert.AreEqual(2, folderNode.ImageIndex)
            ClassicAssert.AreEqual(2, folderNode.SelectedImageIndex)
        End Using
    End Sub

    Private Shared Function CreateDmsItem() As DmsResourceItem
        Return New DmsResourceItem() With {
            .ItemType = DmsResourceItem.ItemTypes.File,
            .Name = "document.pdf",
            .FullName = "documents/document.pdf",
            .ExtendedInfosOwner = New DmsUser() With {.ID = "owner", .DisplayName = "Owner"},
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

    Private Shared Sub InvokePrivateMethod(instance As Object, methodName As String, Optional hasEventArgs As Boolean = True)
        Dim method As MethodInfo = instance.GetType().GetMethod(methodName, BindingFlags.Instance Or BindingFlags.NonPublic)
        ClassicAssert.IsNotNull(method, methodName)
        method.Invoke(instance, If(hasEventArgs, New Object() {Nothing, EventArgs.Empty}, Nothing))
    End Sub

    Private Class ScopevisioTeamworkDmsProvider
        Inherits NoDmsProvider

        Private ReadOnly MutateResource As Boolean

        Public Sub New(mutateResource As Boolean)
            Me.MutateResource = mutateResource
        End Sub

        Public Property CreateLinkCallCount As Integer
        Public Property DeleteLinkCallCount As Integer
        Public Property ThrowOnCreate As Boolean
        Public Property RefreshedFile As DmsResourceItem
        Public Property RefreshedFolder As DmsResourceItem

        Public Overrides Sub ResetCachesForRemoteItems(remoteFolderPath As String, searchType As SearchItemType)
        End Sub

        Public Overrides Function ListAllRemoteItems(remoteFolderPath As String, searchType As SearchItemType) As List(Of DmsResourceItem)
            Select Case searchType
                Case SearchItemType.Files
                    Return If(Me.RefreshedFile Is Nothing, New List(Of DmsResourceItem), New List(Of DmsResourceItem) From {Me.RefreshedFile})
                Case SearchItemType.Folders
                    Return If(Me.RefreshedFolder Is Nothing, New List(Of DmsResourceItem), New List(Of DmsResourceItem) From {Me.RefreshedFolder})
                Case Else
                    Return New List(Of DmsResourceItem)
            End Select
        End Function

        Public Overrides Sub DeleteLink(shareInfo As DmsLink)
            Me.DeleteLinkCallCount += 1
        End Sub

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
