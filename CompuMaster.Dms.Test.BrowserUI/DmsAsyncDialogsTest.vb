Option Strict On

Imports System.Reflection
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports UiComponents = CompuMaster.Dms.BrowserUI

<TestFixture, Apartment(ApartmentState.STA)>
Public Class DmsAsyncDialogsTest
    Private Dispatcher As UiTestDispatcher

    <SetUp>
    Public Sub InstallDispatcher()
        Dispatcher = New UiTestDispatcher()
    End Sub

    <TearDown>
    Public Sub RestoreDispatcher()
        Dispatcher.Dispose()
    End Sub

    <TestCase(0), TestCase(1), TestCase(2)>
    Public Sub BusyDialogRejectsDuplicateOperationsAndClosingAndRestoresState(outcome As Integer)
        Using dialog As New ClosingProbeForm()
            Dim guard As New UiComponents.UiAsyncOperation(dialog)
            Dim pending As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            Dim operation = guard.RunAsync(Function() pending.Task)
            Assert.That(dialog.Enabled, [Is].True)
            Assert.That(dialog.UseWaitCursor, [Is].True)
            Assert.That(dialog.AttemptClose(), [Is].False)
            Dim duplicateCalls As Integer
            Dispatcher.Finish(guard.RunAsync(Function()
                                                duplicateCalls += 1
                                                Return Task.CompletedTask
                                            End Function))
            Assert.That(duplicateCalls, [Is].Zero)
            Select Case outcome
                Case 0
                    pending.SetResult(True)
                Case 1
                    pending.SetException(New InvalidOperationException("Rejected by server."))
                Case 2
                    pending.SetCanceled()
            End Select
            Dispatcher.PumpUntil(Function() operation.IsCompleted)
            If outcome = 0 Then
                operation.GetAwaiter().GetResult()
            ElseIf outcome = 1 Then
                Assert.Throws(Of InvalidOperationException)(Sub() operation.GetAwaiter().GetResult())
            Else
                Assert.Throws(Of TaskCanceledException)(Sub() operation.GetAwaiter().GetResult())
            End If
            Assert.That(dialog.Enabled, [Is].True)
            Assert.That(dialog.UseWaitCursor, [Is].False)
            Assert.That(dialog.AttemptClose(), [Is].True)
            Dispatcher.Finish(guard.RunAsync(Function() Task.CompletedTask))
        End Using
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub PrincipalListsWaitForTheAsyncProviderAndPreserveIdentifiersAndDisplayNames(groups As Boolean)
        Dim provider As New ScopevisioTeamworkDmsProvider()
        Using dialog As New UiComponents.DmsStandardShareSetup()
            dialog.DmsProvider = provider
            dialog.DmsItem = Item()
            dialog.HideIDs = New List(Of String) From {"hidden"}
            dialog.DialogObjectMode = If(groups, UiComponents.DmsStandardShareSetup.DialogObjectModes.GroupSharing, UiComponents.DmsStandardShareSetup.DialogObjectModes.UserSharing)
            InvokeEvent(dialog, "DmsStandardShare_Load")
            Assert.That(dialog.Enabled, [Is].True)
            Assert.That(dialog.GroupBoxGeneral.Enabled, [Is].False)
            Assert.That(dialog.ComboBoxUsersOrGroups.Items.Count, [Is].Zero)
            If groups Then
                provider.Groups.SetResult(New List(Of DmsGroup) From {New DmsGroup With {.ID = "hidden", .Name = "Hidden"}, New DmsGroup With {.ID = "visible", .Name = "Visible group"}})
            Else
                provider.Users.SetResult(New List(Of DmsUser) From {New DmsUser With {.ID = "hidden", .DisplayName = "Hidden"}, New DmsUser With {.ID = "visible", .DisplayName = "Visible user"}})
            End If
            Dispatcher.WaitForEvents()
            Assert.That(dialog.Enabled, [Is].True)
            Assert.That(dialog.ComboBoxUsersOrGroups.Items.Count, [Is].EqualTo(1))
            Dim entry = DirectCast(dialog.ComboBoxUsersOrGroups.Items(0), KeyValuePair(Of String, String))
            Assert.That(entry.Key, [Is].EqualTo("visible"))
            Assert.That(entry.Value, [Is].EqualTo(If(groups, "Visible group", "Visible user")))
        End Using
    End Sub

    <Test>
    Public Sub LinkSaveWaitsAndPublishesOneCreatedLinkOnlyAfterSuccess()
        Dim provider As New ScopevisioTeamworkDmsProvider()
        Dim resource = Item()
        Using dialog As New UiComponents.DmsLinkShareSetup With {.DmsProvider = provider, .DmsItem = resource}
            InvokeEvent(dialog, "DmsLinkShare_Load")
            InvokeEvent(dialog, "ButtonSave_Click")
            InvokeEvent(dialog, "ButtonSave_Click")
            Assert.That(provider.CreateCalls, [Is].EqualTo(1))
            Assert.That(dialog.Enabled, [Is].True)
            Assert.That(dialog.ButtonSave.Enabled, [Is].False)
            Assert.That(dialog.DialogResult, [Is].EqualTo(DialogResult.None))
            Assert.That(resource.ExtendedInfosLinks, [Is].Empty)
            Dim link As New DmsLink(resource, "created", provider, Nothing) With {.AllowView = True}
            provider.Created.SetResult(link)
            Dispatcher.WaitForEvents()
            Assert.That(dialog.DialogResult, [Is].EqualTo(DialogResult.OK))
            Assert.That(resource.ExtendedInfosLinks.Single(), [Is].SameAs(link))
        End Using
    End Sub

    <Test>
    Public Sub LinkDeletionWaitsBeforeUpdatingMetadataAndNotifyingTheBrowser()
        Dim provider As New ScopevisioTeamworkDmsProvider()
        Dim resource = Item()
        Dim link As New DmsLink(resource, "existing", provider, Nothing) With {.AllowView = True}
        resource.ExtendedInfosLinks.Add(link)
        Using dialog As New UiComponents.DmsItemSharings With {.DmsProvider = provider, .DmsItem = resource}
            InvokeEvent(dialog, "DmsItemSharings_Load")
            Dispatcher.WaitForEvents()
            dialog.ListViewExternalSharings.CreateControl()
            dialog.ListViewExternalSharings.Items(0).Selected = True
            Dim notifications As Integer
            AddHandler dialog.SharingsChanged, Sub(sender, e) notifications += 1
            InvokeEvent(dialog, "ToolStripButtonExternalSharingsDelete_Click")
            InvokeEvent(dialog, "ToolStripButtonExternalSharingsDelete_Click")
            Assert.That(provider.DeleteCalls, [Is].EqualTo(1))
            Assert.That(resource.ExtendedInfosLinks.Count, [Is].EqualTo(1))
            Assert.That(notifications, [Is].Zero)
            provider.Deleted.SetResult(True)
            Dispatcher.WaitForEvents()
            Assert.That(resource.ExtendedInfosLinks, [Is].Empty)
            Assert.That(notifications, [Is].EqualTo(1))
        End Using
    End Sub

    <Test>
    Public Sub PropertyDetailsAwaitReferencesAndKeepOtherDetailsWhenALookupFails()
        Dim provider As New ScopevisioTeamworkDmsProvider()
        Dim resource = Item()
        resource.ExtendedInfosReferencedFromCollectionIDs = New List(Of String) From {"collection"}
        resource.ExtendedInfosReferencedFromFolderIDs = New List(Of String) From {"unavailable"}
        Using browser As New UiComponents.DmsBrowser(provider)
            Dim operation = browser.PropertiesDetailsAsync(resource)
            Assert.That(operation.IsCompleted, [Is].False)
            provider.Collection.SetResult(New DmsResourceItem With {.Name = "Referenced collection"})
            Dispatcher.Finish(operation)
            Assert.That(operation.Result, Does.Contain("Referenced collection (collection)"))
            Assert.That(operation.Result, Does.Contain("unavailable"))
            Assert.That(operation.Result, Does.Contain(resource.FullName))
            Assert.That(provider.CollectionCalls, [Is].EqualTo(1))
            Assert.That(provider.FolderCalls, [Is].EqualTo(1))
        End Using
    End Sub

    <Test>
    Public Sub ProfileConstructorDefersAuthorizationAndDoesNotCacheFailedAttempts()
        Dim profile As New CompuMaster.Dms.DmsLoginProfile With {.DmsProvider = CType(255, BaseDmsProvider.DmsProviders)}
        Using browser As New UiComponents.DmsBrowser(profile, "Test", Nothing, "/", Nothing, UiComponents.DmsBrowser.BrowseModes.Folders, UiComponents.DmsBrowser.FileOrFolderActions.AllowSharings, UiComponents.DmsBrowser.DialogOperationModes.NoResults, Nothing, Nothing, Nothing)
            Dim failed = browser.EnsureProviderAsync()
            Assert.Throws(Of NotImplementedException)(Sub() Dispatcher.Finish(failed))
            profile.DmsProvider = BaseDmsProvider.DmsProviders.None
            Assert.Throws(Of NotSupportedException)(Sub() Dispatcher.Finish(browser.EnsureProviderAsync()))
            Assert.That(GetType(UiComponents.DmsBrowser).GetField("DmsProviderInstance", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(browser), [Is].Null)
        End Using
    End Sub

    <Test>
    Public Sub FailedAsyncLinkCreationDoesNotAddAnUnconfirmedLink()
        Dim provider As New ScopevisioTeamworkDmsProvider()
        Dim resource = Item()
        Dim operation = UiComponents.DmsLinkShareSetup.CreateLinkAndSynchronizeDmsItemAsync(provider, resource, New DmsLink(resource, provider))
        provider.Created.SetException(New InvalidOperationException("Ambiguous server failure."))
        Assert.Throws(Of InvalidOperationException)(Sub() Dispatcher.Finish(operation))
        Assert.That(resource.ExtendedInfosLinks, [Is].Empty)
        Assert.That(provider.CreateCalls, [Is].EqualTo(1), "An ambiguous create failure must not be replayed.")
    End Sub

    <Test>
    Public Sub HiddenPrincipalRowsRemainVisibleWithoutBeingTreatedAsSelectableIds()
        Dim resource = Item()
        resource.ExtendedInfosHasHiddenUserSharings = True
        resource.ExtendedInfosHasHiddenGroupSharings = True
        Using dialog As New UiComponents.DmsItemSharings With {.DmsProvider = New ScopevisioTeamworkDmsProvider(), .DmsItem = resource}
            InvokeEvent(dialog, "DmsItemSharings_Load")
            Dispatcher.WaitForEvents()
            Assert.That(dialog.ListViewInternalSharings.Items.Count, [Is].EqualTo(2))
            Assert.That(dialog.AuthorizedUserIDs, [Is].Empty)
            Assert.That(dialog.AuthorizedGroupIDs, [Is].Empty)
        End Using
    End Sub

    <Test>
    Public Sub SharingRefreshWaitsForOtherBrowserWorkAndUpdatesOnlyTheAffectedItem()
        Dim provider As New ScopevisioTeamworkDmsProvider()
        Dim resource = Item()
        resource.ExtendedInfosFileID = "changed"
        Dim other = Item()
        other.ExtendedInfosFileID = "other"
        Using browser As New UiComponents.DmsBrowser(provider), dialog As New UiComponents.DmsItemSharings With {.DmsItem = resource}
            Dim changedRow As New ListViewItem(resource.Name, 6) With {.Tag = resource}
            Dim otherRow As New ListViewItem(other.Name, 6) With {.Tag = other}
            browser.ListViewDmsFiles.Items.AddRange({changedRow, otherRow})
            Dim pending As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            Dim busy = browser.RunTransferAsync(Function() pending.Task)
            GetType(UiComponents.DmsBrowser).GetMethod("DmsShareForm_SharingsChanged", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(browser, {dialog, EventArgs.Empty})
            Assert.That(provider.ListCalls, [Is].Zero)
            pending.SetResult(True)
            Dispatcher.Finish(busy)
            Dispatcher.PumpUntil(Function() provider.ListCalls = 1)
            Assert.That(browser.Enabled, [Is].True)
            Assert.That(browser.SplitContainer.Enabled, [Is].False)
            Assert.That(resource.ExtendedInfosIsShared, [Is].False)
            provider.Listed.SetResult(New List(Of DmsResourceItem) From {New DmsResourceItem With {.ItemType = resource.ItemType, .ExtendedInfosFileID = "changed", .ExtendedInfosIsShared = True}})
            Dispatcher.WaitForEvents()
            Assert.That(browser.Enabled, [Is].True)
            Assert.That(resource.ExtendedInfosIsShared, [Is].True)
            Assert.That(changedRow.ImageIndex, [Is].Not.EqualTo(6))
            Assert.That(otherRow.ImageIndex, [Is].EqualTo(6))
        End Using
    End Sub

    <Test>
    Public Sub BrowserDoesNotCloseAndCleanTemporaryFilesWhileWorkIsActive()
        Using browser As New UiComponents.DmsBrowser(New ScopevisioTeamworkDmsProvider())
            Dim pending As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            Dim busy = browser.RunTransferAsync(Function() pending.Task)
            Dim args As New FormClosingEventArgs(CloseReason.UserClosing, False)
            GetType(UiComponents.DmsBrowser).GetMethod("DmsBrowser_FormClosing", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(browser, {browser, args})
            Assert.That(args.Cancel, [Is].True)
            pending.SetResult(True)
            Dispatcher.Finish(busy)
            Assert.That(browser.Enabled, [Is].True)
        End Using
    End Sub

    Private Shared Function Item() As DmsResourceItem
        Return New DmsResourceItem With {.Name = "file.txt", .FullName = "folder/file.txt", .ItemType = DmsResourceItem.ItemTypes.File,
            .ExtendedInfosOwner = New DmsUser With {.ID = "owner", .DisplayName = "Owner"}, .ExtendedInfosLinks = New List(Of DmsLink),
            .ExtendedInfosGroupSharings = New List(Of DmsShareForGroup), .ExtendedInfosUserSharings = New List(Of DmsShareForUser)}
    End Function

    Private Shared Sub InvokeEvent(dialog As Form, method As String)
        dialog.GetType().GetMethod(method, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(dialog, {Nothing, EventArgs.Empty})
    End Sub

    Private Class ClosingProbeForm
        Inherits Form
        Friend Function AttemptClose() As Boolean
            Dim args As New FormClosingEventArgs(CloseReason.UserClosing, False)
            MyBase.OnFormClosing(args)
            Return Not args.Cancel
        End Function
    End Class

    Private Class ScopevisioTeamworkDmsProvider
        Inherits NoDmsProvider
        Friend ReadOnly Users As New TaskCompletionSource(Of List(Of DmsUser))(TaskCreationOptions.RunContinuationsAsynchronously)
        Friend ReadOnly Groups As New TaskCompletionSource(Of List(Of DmsGroup))(TaskCreationOptions.RunContinuationsAsynchronously)
        Friend ReadOnly Created As New TaskCompletionSource(Of DmsLink)(TaskCreationOptions.RunContinuationsAsynchronously)
        Friend ReadOnly Deleted As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Friend ReadOnly Collection As New TaskCompletionSource(Of DmsResourceItem)(TaskCreationOptions.RunContinuationsAsynchronously)
        Friend ReadOnly Listed As New TaskCompletionSource(Of List(Of DmsResourceItem))(TaskCreationOptions.RunContinuationsAsynchronously)
        Friend CreateCalls As Integer
        Friend DeleteCalls As Integer
        Friend CollectionCalls As Integer
        Friend FolderCalls As Integer
        Friend ListCalls As Integer

        Public Overrides Function ResetCachesForRemoteItemsAsync(path As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Task.CompletedTask
        End Function

        Public Overrides Function ListAllRemoteItemsAsync(path As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            ListCalls += 1
            Return Listed.Task
        End Function

        Public Overrides ReadOnly Property DmsProviderID As DmsProviders
            Get
                Return DmsProviders.Scopevisio
            End Get
        End Property

        Public Overrides Function GetAllUsersAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsUser))
            Return Users.Task
        End Function

        Public Overrides Function GetAllGroupsAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsGroup))
            Return Groups.Task
        End Function

        Public Overrides Function CreateLinkAsync(resource As DmsResourceItem, link As DmsLink, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsLink)
            CreateCalls += 1
            Return Created.Task
        End Function

        Public Overrides Function DeleteLinkAsync(link As DmsLink, Optional cancellationToken As CancellationToken = Nothing) As Task
            DeleteCalls += 1
            Return Deleted.Task
        End Function

        Public Overrides Function RefreshLinkAsync(link As DmsLink, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Task.CompletedTask
        End Function

        Public Overrides Function FindCollectionByIdAsync(id As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            CollectionCalls += 1
            Return Collection.Task
        End Function

        Public Overrides Function FindFolderByIdAsync(id As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            FolderCalls += 1
            Return Task.FromException(Of DmsResourceItem)(New InvalidOperationException("Optional reference unavailable."))
        End Function
    End Class
End Class
