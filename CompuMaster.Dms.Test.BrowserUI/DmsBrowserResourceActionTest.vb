Option Explicit On
Option Strict On

Imports System.Threading
Imports System.Reflection
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture>
<Apartment(ApartmentState.STA)>
Public Class DmsBrowserResourceActionTest

    <TestCase(True)>
    <TestCase(False)>
    Public Sub DestinationPickerInheritsOnlyCreateFolderPermission(allowCreateFolders As Boolean)
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(New RecordingProvider())
            Dim actions As Global.CompuMaster.Dms.BrowserUI.DmsBrowser.FileOrFolderActions = Global.CompuMaster.Dms.BrowserUI.DmsBrowser.FileOrFolderActions.AllowCopyRenameMoveFiles
            If allowCreateFolders Then actions = actions Or Global.CompuMaster.Dms.BrowserUI.DmsBrowser.FileOrFolderActions.AllowCreateFolders
            browser.AllowedActions = actions

            Using picker As Global.CompuMaster.Dms.BrowserUI.DmsBrowser = browser.CreateDestinationPicker()
                Dim expectedPickerActions As Global.CompuMaster.Dms.BrowserUI.DmsBrowser.FileOrFolderActions =
                    If(allowCreateFolders, Global.CompuMaster.Dms.BrowserUI.DmsBrowser.FileOrFolderActions.AllowCreateFolders, Global.CompuMaster.Dms.BrowserUI.DmsBrowser.FileOrFolderActions.AllowSelectOnly)
                ClassicAssert.AreEqual(expectedPickerActions, picker.AllowedActions)
                ClassicAssert.AreEqual(allowCreateFolders, picker.ButtonCreateNewFolder.Enabled)
                ClassicAssert.IsFalse(picker.ToolStripButtonCopyFile.Visible)
                ClassicAssert.IsFalse(picker.ToolStripFolderContextButtonMoveFolder.Visible)
            End Using
        End Using
    End Sub

    <Test>
    Public Sub CopyUsesTheSelectedFileIdentityEvenWhenItsNameCollides()
        Dim provider As New RecordingProvider()
        Dim selected As DmsResourceItem = CreateItem(DmsResourceItem.ItemTypes.File, "source/report.txt")
        selected.ExtendedInfosFileID = "selected-file-id"
        selected.ExtendedInfosCollisionDetected = True

        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(provider)
            ClassicAssert.IsTrue(browser.ExecuteResourceAction(selected, "target/report-copy.txt", Global.CompuMaster.Dms.BrowserUI.DmsBrowser.ResourceAction.Copy))
        End Using

        ClassicAssert.AreSame(selected, provider.LastCopiedItem)
        ClassicAssert.AreEqual("selected-file-id", provider.LastCopiedItem.ExtendedInfosFileID)
        ClassicAssert.AreEqual("target/report-copy.txt", provider.LastDestinationPath)
    End Sub

    <Test>
    Public Sub MoveUsesTheSelectedFileIdentityEvenWhenItsNameCollides()
        Dim provider As New RecordingProvider()
        Dim selected As DmsResourceItem = CreateItem(DmsResourceItem.ItemTypes.File, "source/report.txt")
        selected.ExtendedInfosFileID = "selected-file-id"
        selected.ExtendedInfosCollisionDetected = True

        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(provider)
            ClassicAssert.IsTrue(browser.ExecuteResourceAction(selected, "target/report.txt", Global.CompuMaster.Dms.BrowserUI.DmsBrowser.ResourceAction.Move))
        End Using

        ClassicAssert.AreSame(selected, provider.LastMovedItem)
        ClassicAssert.AreEqual("selected-file-id", provider.LastMovedItem.ExtendedInfosFileID)
        ClassicAssert.AreEqual("target/report.txt", provider.LastDestinationPath)
    End Sub

    <Test>
    Public Sub RenameUsesTheSelectedDirectoryIdentity()
        Dim provider As New RecordingProvider()
        Dim selected As DmsResourceItem = CreateItem(DmsResourceItem.ItemTypes.Folder, "source/folder")
        selected.ExtendedInfosFolderID = "selected-folder-id"

        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(provider)
            ClassicAssert.IsTrue(browser.ExecuteResourceAction(selected, "source/renamed", Global.CompuMaster.Dms.BrowserUI.DmsBrowser.ResourceAction.Rename))
        End Using

        ClassicAssert.AreSame(selected, provider.LastMovedItem)
        ClassicAssert.AreEqual("selected-folder-id", provider.LastMovedItem.ExtendedInfosFolderID)
        ClassicAssert.AreEqual("source/renamed", provider.LastDestinationPath)
    End Sub

    <Test>
    Public Sub PropertiesKeepReferenceIdsWhenOptionalNameLookupFails()
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(New FailingReferenceLookupProvider())
            ClassicAssert.AreEqual("collection-id", LookupReferenceName(browser, "LookupCollectionNameForUI", "collection-id"))
            ClassicAssert.AreEqual("folder-id", LookupReferenceName(browser, "LookupFolderNameForUI", "folder-id"))
        End Using
    End Sub

    Private Shared Function LookupReferenceName(browser As Global.CompuMaster.Dms.BrowserUI.DmsBrowser, methodName As String, id As String) As String
        Dim method As MethodInfo = GetType(Global.CompuMaster.Dms.BrowserUI.DmsBrowser).GetMethod(methodName, BindingFlags.Instance Or BindingFlags.NonPublic)
        Return CStr(method.Invoke(browser, New Object() {id, Nothing}))
    End Function

    Private Shared Function CreateItem(itemType As DmsResourceItem.ItemTypes, path As String) As DmsResourceItem
        Return New DmsResourceItem With {
            .ItemType = itemType,
            .Name = path.Substring(path.LastIndexOf("/"c) + 1),
            .FullName = path
        }
    End Function

    Private Class RecordingProvider
        Inherits NoDmsProvider

        Public Property LastCopiedItem As DmsResourceItem
        Public Property LastMovedItem As DmsResourceItem
        Public Property LastDestinationPath As String

        Public Overrides Function ListRemoteItem(remotePath As String) As DmsResourceItem
            Return Nothing
        End Function

        Public Overrides Function RemoteItemExistsUniquelyAs(remotePath As String) As DmsResourceItem.FoundItemResult
            Return DmsResourceItem.FoundItemResult.Folder
        End Function

        Public Overrides Sub ResetCachesForRemoteItems(remoteFolderPath As String, searchType As SearchItemType)
        End Sub

        Protected Overrides Sub CopyItem(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?)
            Me.LastCopiedItem = remoteSource
            Me.LastDestinationPath = remoteDestinationPath
        End Sub

        Protected Overrides Sub MoveItem(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?)
            Me.LastMovedItem = remoteSource
            Me.LastDestinationPath = remoteDestinationPath
        End Sub
    End Class

    Private Class FailingReferenceLookupProvider
        Inherits NoDmsProvider

        Public Overrides ReadOnly Property DmsProviderID As DmsProviders
            Get
                Return DmsProviders.Scopevisio
            End Get
        End Property

        Public Overrides Function FindCollectionById(id As String) As DmsResourceItem
            Throw New NullReferenceException("Missing parent metadata")
        End Function

        Public Overrides Function FindFolderById(id As String) As DmsResourceItem
            Throw New NullReferenceException("Missing parent metadata")
        End Function
    End Class

End Class
