Option Explicit On
Option Strict On

Imports System.Threading
Imports System.Windows.Forms
Imports System.Drawing
Imports System.IO
Imports System.ComponentModel
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture>
<Apartment(ApartmentState.STA)>
Public Class DmsBrowserInstanceSelectionTest

    <Test>
    Public Sub PreselectedInstanceOpensWithoutPromptAndReloadShowsSwitchedInstance()
        Dim Provider As New InMemoryInstanceProvider()
        Using Browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(Provider)
            Browser.EnableDmsInstanceSelection = True
            Browser.PreselectedDmsInstanceID = "tenant-b"

            ClassicAssert.IsTrue(Browser.InitializeDmsInstanceSelection())
            ClassicAssert.AreEqual("tenant-b", Provider.SelectedInstanceID)
            Browser.LoadTree()
            ClassicAssert.AreEqual("Tenant B folder", Browser.TreeViewDmsFolders.Nodes(0).Nodes(0).Text)

            Provider.SelectDmsInstance("tenant-a")
            Browser.ReloadDmsInstanceView()

            ClassicAssert.AreEqual("Tenant A folder", Browser.TreeViewDmsFolders.Nodes(0).Nodes(0).Text)
            ClassicAssert.AreEqual(0, Browser.ListViewDmsFiles.Items.Count)
            ClassicAssert.IsNull(Browser.SelectedFolder)
        End Using
    End Sub

    <Test>
    Public Sub SelectorUsesDefaultOrSpecifiedInstanceWithoutPrompt()
        Dim Provider As New InMemoryInstanceProvider()
        Dim DialogCalls As Integer
        Dim Callback As RemoteInstanceSelector.SelectionDialogMethod =
            Function(owner, instances)
                DialogCalls += 1
                Return "tenant-b"
            End Function

        ClassicAssert.IsTrue((New RemoteInstanceSelector(RemoteInstanceSelector.StartupInstance.DefaultInstance, Callback)).SelectProviderInstance(Provider, Nothing))
        ClassicAssert.AreEqual("tenant-a", Provider.SelectedInstanceID)
        ClassicAssert.IsTrue((New RemoteInstanceSelector("tenant-b", Callback)).SelectProviderInstance(Provider, Nothing))
        ClassicAssert.AreEqual("tenant-b", Provider.SelectedInstanceID)
        ClassicAssert.AreEqual(0, DialogCalls)
    End Sub

    <Test>
    Public Sub SelectionDialogOpensBeforeBrowserAndCanBeCancelled()
        Dim Provider As New InMemoryInstanceProvider()
        Dim DialogCalls As Integer
        Dim Selector As New RemoteInstanceSelector(RemoteInstanceSelector.StartupInstance.SelectionDialog,
            Function(owner, instances)
                DialogCalls += 1
                ClassicAssert.AreEqual(2, instances.Count)
                Return "tenant-b"
            End Function)

        ClassicAssert.IsTrue(Selector.SelectProviderInstance(Provider, Nothing))
        ClassicAssert.AreEqual("tenant-b", Provider.SelectedInstanceID)
        ClassicAssert.AreEqual(1, DialogCalls)

        Dim CancelSelector As New RemoteInstanceSelector(RemoteInstanceSelector.StartupInstance.SelectionDialog,
            Function(owner, instances) DirectCast(Nothing, String))
        ClassicAssert.IsFalse(CancelSelector.SelectProviderInstance(Provider, Nothing))
        ClassicAssert.AreEqual("tenant-b", Provider.SelectedInstanceID)
    End Sub

    <Test>
    Public Sub SelectionDialogSkipsPromptWhenOnlyOneInstanceIsAvailable()
        Dim Provider As New InMemoryInstanceProvider() With {
            .AvailableInstanceIDs = New String() {"tenant-b"}
        }
        Dim DialogCalls As Integer
        Dim Selector As New RemoteInstanceSelector(RemoteInstanceSelector.StartupInstance.SelectionDialog,
            Function(owner, instances)
                DialogCalls += 1
                Return "tenant-a"
            End Function)

        ClassicAssert.IsTrue(Selector.SelectProviderInstance(Provider, Nothing))
        ClassicAssert.AreEqual("tenant-b", Provider.SelectedInstanceID)
        ClassicAssert.AreEqual(0, DialogCalls)
    End Sub

    <Test>
    Public Sub SelectorRejectsMissingAndUnknownInstances()
        ClassicAssert.Throws(Of ArgumentNullException)(Sub()
                                                           Dim ignored As New RemoteInstanceSelector(RemoteInstanceSelector.StartupInstance.SelectionDialog)
                                                       End Sub)
        ClassicAssert.Throws(Of ArgumentException)(Sub()
                                                       Dim ignored As New RemoteInstanceSelector(" ")
                                                   End Sub)
        ClassicAssert.Throws(Of NotSupportedException)(Sub()
                                                           Dim ignored As New RemoteInstanceSelector(RemoteInstanceSelector.StartupInstance.SpecifiedInstance)
                                                       End Sub)

        Dim Provider As New InMemoryInstanceProvider()
        Dim InvalidSelector As New RemoteInstanceSelector(RemoteInstanceSelector.StartupInstance.SelectionDialog,
            Function(owner, instances) "tenant-missing")
        ClassicAssert.Throws(Of ArgumentOutOfRangeException)(Sub() InvalidSelector.SelectProviderInstance(Provider, Nothing))
        Provider.AvailableInstanceIDs = Array.Empty(Of String)()
        ClassicAssert.Throws(Of InvalidOperationException)(Sub() InvalidSelector.SelectProviderInstance(Provider, Nothing))
    End Sub

    <Test>
    Public Sub ProviderWithoutInstanceCapabilityKeepsDefaultAndRejectsSpecifiedInstance()
        Dim Provider As New NoDmsProvider()
        Dim DialogCalls As Integer
        Dim Selector As New RemoteInstanceSelector(RemoteInstanceSelector.StartupInstance.SelectionDialog,
            Function(owner, instances)
                DialogCalls += 1
                Return "not-used"
            End Function)

        ClassicAssert.IsTrue(Selector.SelectProviderInstance(Provider, Nothing))
        ClassicAssert.AreEqual(0, DialogCalls)
        Dim SpecifiedSelector As New RemoteInstanceSelector("tenant-a")
        ClassicAssert.Throws(Of NotSupportedException)(Sub() SpecifiedSelector.SelectProviderInstance(Provider, Nothing))

        Using Browser As DmsBrowser = CreateBrowser(Provider, Selector.SelectionDialog, DmsBrowser.FileOrFolderActions.AllowSwitchDmsInstance)
            Browser.InitializeDmsInstanceSwitching()
            ClassicAssert.AreEqual(0, Browser.Controls.Find("ButtonDmsInstance", False).Length)
        End Using
    End Sub

    <Test>
    Public Sub SelectionDialogSortsNamesAndPreservesSelectedInstance()
        Dim Instances As New List(Of DmsInstanceInfo) From {
            New DmsInstanceInfo("z", "Zulu", True),
            New DmsInstanceInfo("a", "alpha", False),
            New DmsInstanceInfo("b", "Beta", False)
        }

        Using Picker As New DmsInstanceSelectionDialog(Instances, SystemIcons.Information)
            Dim Items As ListBox = Picker.Controls.OfType(Of ListBox)().Single()
            CollectionAssert.AreEqual(New String() {"a", "b", "z"}, Items.Items.Cast(Of DmsInstanceInfo)().Select(Function(item) item.ID).ToArray())
            ClassicAssert.AreEqual("z", Picker.SelectedInstance.ID)
            AssertSameIcon(SystemIcons.Information, Picker.Icon)
        End Using
    End Sub

    <Test>
    Public Sub BrowserAndSelectionDialogKeepEmbeddedIconWhenNoIconIsSupplied()
        Using DefaultBrowser As New DmsBrowser(),
              Browser As DmsBrowser = CreateBrowser(New InMemoryInstanceProvider(), Nothing, DmsBrowser.FileOrFolderActions.AllowSelectOnly),
              Picker As New DmsInstanceSelectionDialog(New List(Of DmsInstanceInfo) From {New DmsInstanceInfo("a", "Alpha", True)})
            AssertSameIcon(DefaultBrowser.Icon, Browser.Icon)
            AssertSameIcon(DefaultBrowser.Icon, Picker.Icon)
        End Using
    End Sub

    <Test>
    Public Sub DefaultBrowserIconContainsSevenResolutions()
        Using Browser As New DmsBrowser(), Stream As New MemoryStream()
            Browser.Icon.Save(Stream)
            Dim IconBytes As Byte() = Stream.ToArray()
            ClassicAssert.AreEqual(0US, BitConverter.ToUInt16(IconBytes, 0))
            ClassicAssert.AreEqual(1US, BitConverter.ToUInt16(IconBytes, 2))
            ClassicAssert.AreEqual(7US, BitConverter.ToUInt16(IconBytes, 4))
            CollectionAssert.AreEqual(New Byte() {16, 24, 32, 48, 64, 128, 0},
                                      Enumerable.Range(0, 7).Select(Function(index) IconBytes(6 + index * 16)).ToArray())
        End Using
    End Sub

    <Test>
    Public Sub ConstructorWithoutFormIconIsHiddenAndObsolete()
        Dim LegacyConstructor = GetType(DmsBrowser).GetConstructor(Type.EmptyTypes)
        ClassicAssert.IsNotNull(LegacyConstructor)
        ClassicAssert.AreEqual("Use overload instead", LegacyConstructor.GetCustomAttributes(GetType(ObsoleteAttribute), False).Cast(Of ObsoleteAttribute)().Single().Message)
        ClassicAssert.AreEqual(EditorBrowsableState.Never, LegacyConstructor.GetCustomAttributes(GetType(EditorBrowsableAttribute), False).Cast(Of EditorBrowsableAttribute)().Single().State)
    End Sub

    Private Shared Sub AssertSameIcon(expected As Icon, actual As Icon)
        Using expectedBytes As New MemoryStream(), actualBytes As New MemoryStream()
            expected.Save(expectedBytes)
            actual.Save(actualBytes)
            CollectionAssert.AreEqual(expectedBytes.ToArray(), actualBytes.ToArray())
        End Using
    End Sub

    <Test>
    Public Sub SwitchButtonRequiresFlagCallbackAndMultipleInstances()
        Dim Provider As New InMemoryInstanceProvider()
        Dim Callback As RemoteInstanceSelector.SelectionDialogMethod = Function(owner, instances) "tenant-b"

        Using WithoutFlag As DmsBrowser = CreateBrowser(Provider, Callback, DmsBrowser.FileOrFolderActions.AllowSelectOnly)
            WithoutFlag.InitializeDmsInstanceSwitching()
            ClassicAssert.AreEqual(0, WithoutFlag.Controls.Find("ButtonDmsInstance", False).Length)
        End Using
        Using WithoutCallback As DmsBrowser = CreateBrowser(Provider, Nothing, DmsBrowser.FileOrFolderActions.AllowSwitchDmsInstance)
            WithoutCallback.InitializeDmsInstanceSwitching()
            ClassicAssert.AreEqual(0, WithoutCallback.Controls.Find("ButtonDmsInstance", False).Length)
        End Using
        Provider.AvailableInstanceIDs = New String() {"tenant-a"}
        Using WithOneInstance As DmsBrowser = CreateBrowser(Provider, Callback, DmsBrowser.FileOrFolderActions.AllowSwitchDmsInstance)
            WithOneInstance.InitializeDmsInstanceSwitching()
            ClassicAssert.AreEqual(0, WithOneInstance.Controls.Find("ButtonDmsInstance", False).Length)
        End Using
    End Sub

    <Test>
    Public Sub SwitchButtonUsesCallbackAndReloadsSelectedInstance()
        Dim Provider As New InMemoryInstanceProvider()
        Dim DialogCalls As Integer
        Dim Callback As RemoteInstanceSelector.SelectionDialogMethod =
            Function(owner, instances)
                DialogCalls += 1
                ClassicAssert.AreEqual(2, instances.Count)
                Return "tenant-b"
            End Function

        Using Browser As DmsBrowser = CreateBrowser(Provider, Callback, DmsBrowser.FileOrFolderActions.AllowSwitchDmsInstance)
            Browser.InitializeDmsInstanceSwitching()
            Browser.LoadTree()
            Dim ChangeButton As Button = CType(Browser.Controls.Find("ButtonDmsInstance", False).Single(), Button)
            StringAssert.Contains("Tenant A", ChangeButton.Text)

            Browser.ChangeDmsInstance_Click(ChangeButton, EventArgs.Empty)

            ClassicAssert.AreEqual(1, DialogCalls)
            ClassicAssert.AreEqual("tenant-b", Provider.SelectedInstanceID)
            ClassicAssert.AreEqual("Tenant B folder", Browser.TreeViewDmsFolders.Nodes(0).Nodes(0).Text)
            StringAssert.Contains("Tenant B", ChangeButton.Text)

            Browser.AllowedActions = DmsBrowser.FileOrFolderActions.AllowSelectOnly
            ClassicAssert.IsFalse(ChangeButton.Visible)
        End Using
    End Sub

    <TestCase(False, False), TestCase(False, True), TestCase(True, False), TestCase(True, True)>
    Public Sub BottomActionsTabInVisualOrderInBothDirections(withInstance As Boolean, returnSelection As Boolean)
        Dim callback As RemoteInstanceSelector.SelectionDialogMethod = Function(owner, instances) "tenant-b"
        If Not withInstance Then callback = Nothing
        Using browser As New TabOrderBrowser(callback, returnSelection) With {.ShowInTaskbar = False, .Opacity = 0}
            browser.InitializeDmsInstanceSwitching()
            browser.Show()
            browser.Activate()
            Dim expected As New List(Of Control) From {browser.ButtonCreateNewFolder, browser.ButtonShowFiles}
            If withInstance Then expected.Add(browser.Controls.Find("ButtonDmsInstance", False).Single())
            If returnSelection Then
                expected.Add(browser.ButtonOkay)
                expected.Add(browser.ButtonCancel)
            Else
                expected.Add(browser.ButtonClose)
            End If
            Assert.That(expected(0).Focus(), [Is].True)
            For index As Integer = 1 To expected.Count - 1
                Assert.That(browser.SelectNextControl(expected(index - 1), True, True, True, False), [Is].True)
                Assert.That(browser.ActiveControl, [Is].SameAs(expected(index)), "Forward Tab follows the visible bottom bar from left to right.")
            Next
            For index As Integer = expected.Count - 2 To 0 Step -1
                Assert.That(browser.SelectNextControl(expected(index + 1), False, True, True, False), [Is].True)
                Assert.That(browser.ActiveControl, [Is].SameAs(expected(index)), "Shift+Tab reverses the same sequence.")
            Next
            browser.ButtonCreateNewFolder.Enabled = False
            browser.ButtonShowFiles.Visible = False
            Dim firstAvailable As Control = expected(2)
            Assert.That(browser.SelectNextControl(browser.SplitContainer, True, True, False, False), [Is].True)
            Assert.That(browser.ActiveControl, [Is].SameAs(firstAvailable), "Unavailable actions must be skipped.")
            browser.Close()
        End Using
    End Sub

    Private Class TabOrderBrowser
        Inherits DmsBrowser
        Public Sub New(callback As RemoteInstanceSelector.SelectionDialogMethod, returnSelection As Boolean)
            MyBase.New(New InMemoryInstanceProvider(), callback, "Tab order test", Nothing, Nothing, Nothing,
                       DmsBrowser.BrowseModes.FoldersAndFiles,
                       DmsBrowser.FileOrFolderActions.AllowCreateFolders Or DmsBrowser.FileOrFolderActions.AllowSwitchBrowseMode Or DmsBrowser.FileOrFolderActions.AllowSwitchDmsInstance,
                       If(returnSelection, DmsBrowser.DialogOperationModes.ReturnSelectedItems, DmsBrowser.DialogOperationModes.NoResults), Nothing, Nothing, Nothing)
        End Sub
        Protected Overrides Sub OnLoad(e As EventArgs)
            'Exercise real WinForms focus traversal without loading provider resources.
        End Sub
    End Class

    Private Shared Function CreateBrowser(provider As BaseDmsProvider, callback As RemoteInstanceSelector.SelectionDialogMethod, actions As DmsBrowser.FileOrFolderActions) As DmsBrowser
        Return New DmsBrowser(provider, callback, "Test browser", Nothing, Nothing, Nothing,
                              DmsBrowser.BrowseModes.FoldersAndFiles, actions,
                              DmsBrowser.DialogOperationModes.NoResults, Nothing, Nothing, Nothing)
    End Function

    Private Class InMemoryInstanceProvider
        Inherits NoDmsProvider
        Implements IDmsInstanceProvider

        Public Overrides Function ListAllDirectoryItemsAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As System.Threading.Tasks.Task(Of List(Of DmsResourceItem))
            Return System.Threading.Tasks.Task.FromResult(Me.ListAllDirectoryItems(remoteFolderPath))
        End Function

        Public Overrides Function ListAllFileItemsAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As System.Threading.Tasks.Task(Of List(Of DmsResourceItem))
            Return System.Threading.Tasks.Task.FromResult(Me.ListAllFileItems(remoteFolderPath))
        End Function

        Public Overrides Function ResetCachesForRemoteItemsAsync(remoteFolderPath As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As System.Threading.Tasks.Task
            Return System.Threading.Tasks.Task.CompletedTask
        End Function

        Public Property SelectedInstanceID As String = "tenant-a"
        Public Property AvailableInstanceIDs As String() = New String() {"tenant-a", "tenant-b"}

        Public ReadOnly Property CurrentDmsInstance As DmsInstanceInfo Implements IDmsInstanceProvider.CurrentDmsInstance
            Get
                Return New DmsInstanceInfo(Me.SelectedInstanceID, If(Me.SelectedInstanceID = "tenant-a", "Tenant A", "Tenant B"), True)
            End Get
        End Property

        Public Function ListAvailableDmsInstances() As IReadOnlyList(Of DmsInstanceInfo) Implements IDmsInstanceProvider.ListAvailableDmsInstances
            Return Me.AvailableInstanceIDs.Select(Function(id) New DmsInstanceInfo(id, If(id = "tenant-a", "Tenant A", "Tenant B"), Me.SelectedInstanceID = id)).ToList().AsReadOnly()
        End Function

        Public Sub SelectDmsInstance(instanceID As String) Implements IDmsInstanceProvider.SelectDmsInstance
            If Not Me.AvailableInstanceIDs.Contains(instanceID) Then Throw New ArgumentOutOfRangeException(NameOf(instanceID))
            Me.SelectedInstanceID = instanceID
        End Sub

        Public Overrides Function ListAllRemoteItems(remoteFolderPath As String, searchType As SearchItemType) As List(Of DmsResourceItem)
            If searchType = SearchItemType.Files Then Return New List(Of DmsResourceItem)()
            Dim FolderName As String = If(Me.SelectedInstanceID = "tenant-a", "Tenant A folder", "Tenant B folder")
            Return New List(Of DmsResourceItem) From {
                New DmsResourceItem With {
                    .Name = FolderName,
                    .FullName = FolderName,
                    .ItemType = DmsResourceItem.ItemTypes.Folder,
                    .ChildDirectoryCount = 0,
                    .HasChildDirectories = False
                }
            }
        End Function

        Public Overrides Sub ResetCachesForRemoteItems(remoteFolderPath As String, searchType As SearchItemType)
        End Sub
    End Class
End Class
