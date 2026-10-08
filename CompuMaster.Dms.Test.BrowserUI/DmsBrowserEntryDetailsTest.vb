Option Explicit On
Option Strict On

Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA)>
Public Class DmsBrowserEntryDetailsTest
    <TestCase(DmsResourceItem.ItemTypes.Collection)>
    <TestCase(DmsResourceItem.ItemTypes.Folder)>
    <TestCase(DmsResourceItem.ItemTypes.File)>
    Public Sub DetailsUseTheSelectedIdentifierAndRetainItsBrowsingContext(kind As DmsResourceItem.ItemTypes)
        Dim entry As New DmsResourceItem With {
            .ItemType = kind, .Name = "duplicate", .FullName = "collection/parent/duplicate", .Collection = "collection", .Folder = "parent",
            .ExtendedInfosCollectionID = "selected-collection", .ExtendedInfosFolderID = "selected-folder", .ExtendedInfosFileID = "selected-file",
            .ExtendedInfosAssignedCollectionID = "parent-collection", .ExtendedInfosAssignedFolderID = "parent-folder", .ExtendedInfosCollisionDetected = True
        }
        Dim provider As New DetailsProvider(kind)
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(provider), dispatcher As New UiTestDispatcher
            Dim operation = browser.LoadResourceDetailsAsync(entry)
            dispatcher.Finish(operation)
            Dim details = operation.GetAwaiter().GetResult()
            Assert.That(provider.SelectedId, [Is].EqualTo(If(kind = DmsResourceItem.ItemTypes.Collection, "selected-collection", If(kind = DmsResourceItem.ItemTypes.Folder, "selected-folder", "selected-file"))))
            Assert.That(details.FullName, [Is].EqualTo(entry.FullName))
            Assert.That(details.Collection, [Is].EqualTo(entry.Collection))
            Assert.That(details.Folder, [Is].EqualTo(entry.Folder))
            Assert.That(details.ExtendedInfosAssignedCollectionID, [Is].EqualTo(entry.ExtendedInfosAssignedCollectionID))
            Assert.That(details.ExtendedInfosAssignedFolderID, [Is].EqualTo(entry.ExtendedInfosAssignedFolderID))
            Assert.That(details.ExtendedInfosCollisionDetected, [Is].True)
            Assert.That(details.ExtendedInfosOwner.DisplayName, [Is].EqualTo("Actual resource owner"))
            Assert.That(details.ExtendedInfosHasHiddenUserSharings, [Is].True)
            Assert.That(details.ExtendedInfosLinks(0).ID, [Is].EqualTo("selected-link"))
            Assert.That(provider.PathCalls, [Is].Zero, "Duplicate names must not replace the selected identifier with an ambiguous path lookup.")
        End Using
    End Sub

    <Test>
    Public Sub ProvidersWithoutIdentifiersRetrieveDetailsByPath()
        Dim provider As New DetailsProvider(DmsResourceItem.ItemTypes.Folder)
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(provider), dispatcher As New UiTestDispatcher
            Dim entry As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .FullName = "parent/folder"}
            Dim operation = browser.LoadResourceDetailsAsync(entry)
            dispatcher.Finish(operation)
            Assert.That(provider.PathCalls, [Is].EqualTo(1))
            Assert.That(provider.SelectedPath, [Is].EqualTo(entry.FullName))
            Assert.That(operation.Result.ExtendedInfosOwner.DisplayName, [Is].EqualTo("Actual resource owner"))
        End Using
    End Sub

    <Test>
    Public Sub BusyStateRetainsDisabledActionsAndThePreviousCursor()
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(New NoDmsProvider()), dispatcher As New UiTestDispatcher
            browser.ButtonShowFiles.Enabled = False
            browser.UseWaitCursor = True
            Dim pending As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            Dim operation = browser.RunTransferAsync(Function() pending.Task)
            Assert.That(browser.Enabled, [Is].True)
            Assert.That(browser.SplitContainer.Enabled, [Is].False)
            pending.SetResult(True)
            dispatcher.Finish(operation)
            Assert.That(browser.Enabled, [Is].True)
            Assert.That(browser.SplitContainer.Enabled, [Is].True)
            Assert.That(browser.ButtonShowFiles.Enabled, [Is].False)
            Assert.That(browser.UseWaitCursor, [Is].True)
        End Using
    End Sub

    Private Class DetailsProvider
        Inherits NoDmsProvider
        Private ReadOnly Kind As DmsResourceItem.ItemTypes
        Public SelectedId As String
        Public SelectedPath As String
        Public PathCalls As Integer
        Public Sub New(kind As DmsResourceItem.ItemTypes)
            Me.Kind = kind
        End Sub
        Private Function Details() As Task(Of DmsResourceItem)
            Dim item As New DmsResourceItem With {.ItemType = Kind, .Name = "duplicate", .FullName = "duplicate", .ExtendedInfosOwner = New DmsUser With {.DisplayName = "Actual resource owner"}, .ExtendedInfosHasHiddenUserSharings = True}
            item.ExtendedInfosLinks = New List(Of DmsLink) From {New DmsLink(item, "selected-link", Me, Nothing)}
            Return Task.FromResult(item)
        End Function
        Public Overrides Function FindCollectionByIdAsync(id As String, Optional token As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            SelectedId = id
            Return Details()
        End Function
        Public Overrides Function FindFolderByIdAsync(id As String, Optional token As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            SelectedId = id
            Return Details()
        End Function
        Public Overrides Function FindFileByIdAsync(id As String, Optional token As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            SelectedId = id
            Return Details()
        End Function
        Public Overrides Function ListRemoteItemAsync(path As String, Optional token As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            PathCalls += 1
            SelectedPath = path
            Return Details()
        End Function
    End Class
End Class
