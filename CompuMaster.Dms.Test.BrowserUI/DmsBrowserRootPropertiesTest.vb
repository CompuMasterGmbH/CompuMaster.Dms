Option Explicit On
Option Strict On

Imports System.Globalization
Imports System.Threading
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class DmsBrowserRootPropertiesTest
    Private Const TenantA As String = "11111111-2222-3333-4444-555555555555"
    Private Const TenantB As String = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"

    <TestCase("en"), TestCase("de"), TestCase("fr"), TestCase("es"), TestCase("ar"), TestCase("he"), TestCase("hi"), TestCase("ja"), TestCase("zh-Hans"), TestCase("zh-Hant")>
    Public Sub ActualRootShowsCurrentInstanceNameAndExactIdWithoutDiscovery(culture As String)
        Dim original = CultureInfo.CurrentUICulture
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture)
            Dim provider As New InstanceRootProvider()
            Using browser = CreateBrowser(provider)
                Dim details = browser.RootPropertiesDetails(browser.TreeViewDmsFolders.Nodes(0))
                Assert.That(details, Does.StartWith(UiStrings.GetText("RootValue")))
                Assert.That(details, Does.Contain(UiStrings.GetText("PropertiesInstanceName") & "unittests_Tenant A"))
                Assert.That(details, Does.Contain(UiStrings.GetText("PropertiesInstanceId") & TenantA))
                Assert.That(provider.InstanceReads, [Is].EqualTo(1))
                Assert.That(provider.CatalogReads, [Is].Zero)
            End Using
        Finally
            CultureInfo.CurrentUICulture = original
        End Try
    End Sub

    <TestCase(Nothing), TestCase(""), TestCase("/")>
    Public Sub EquivalentActualRootPathsExposeTenantMetadata(initialPath As String)
        Using browser = CreateBrowser(New InstanceRootProvider(), initialPath)
            Assert.That(browser.RootPropertiesDetails(browser.TreeViewDmsFolders.Nodes(0)), Does.Contain(TenantA))
        End Using
    End Sub

    <TestCase(DmsResourceItem.ItemTypes.Folder), TestCase(DmsResourceItem.ItemTypes.Collection), TestCase(DmsResourceItem.ItemTypes.Root)>
    Public Sub ScopedTopNodeDoesNotExposeTenantRootDetails(itemType As DmsResourceItem.ItemTypes)
        Dim provider As New InstanceRootProvider With {.ScopedItemType = itemType}
        Using browser = CreateBrowser(provider, "unittests_initial_root")
            Assert.That(browser.RootPropertiesDetails(browser.TreeViewDmsFolders.Nodes(0)), [Is].Null)
            Assert.That(provider.InstanceReads, [Is].Zero)
            Assert.That(provider.CatalogReads, [Is].Zero)
        End Using
    End Sub

    <Test>
    Public Sub MetadataReflectsSuccessfulInstanceChangesWithoutTreeLabelInference()
        Dim provider As New InstanceRootProvider()
        Using browser = CreateBrowser(provider)
            Dim root = browser.TreeViewDmsFolders.Nodes(0)
            root.Text = "Untrusted stale tenant label"
            Assert.That(browser.RootPropertiesDetails(root), Does.Contain(TenantA))
            provider.SelectDmsInstance(TenantB)
            Dim details = browser.RootPropertiesDetails(root)
            Assert.That(details, Does.Contain("unittests_Tenant B"))
            Assert.That(details, Does.Contain(TenantB))
            Assert.That(details, Does.Not.Contain(TenantA))
            Assert.That(details, Does.Not.Contain(root.Text))
            Assert.That(provider.CatalogReads, [Is].Zero)
        End Using
    End Sub

    <Test>
    Public Sub MissingInstanceMetadataPreservesTheExistingRootDescription()
        Dim provider As New InstanceRootProvider With {.Instance = Nothing}
        Using browser = CreateBrowser(provider)
            Assert.That(browser.RootPropertiesDetails(browser.TreeViewDmsFolders.Nodes(0)), [Is].EqualTo(UiStrings.GetText("RootValue")))
            Assert.That(provider.CatalogReads, [Is].Zero)
        End Using
    End Sub

    <Test>
    Public Sub ProviderWithoutInstanceCapabilityPreservesTheExistingRootDescription()
        Using browser = CreateBrowser(New RootProvider())
            Assert.That(browser.RootPropertiesDetails(browser.TreeViewDmsFolders.Nodes(0)), [Is].EqualTo(UiStrings.GetText("RootValue")))
        End Using
    End Sub

    <Test>
    Public Sub MissingChildForeignAndDetachedNodesDoNotReadTenantMetadata()
        Dim provider As New InstanceRootProvider()
        Using browser = CreateBrowser(provider), foreignTree As New TreeView()
            Dim root = browser.TreeViewDmsFolders.Nodes(0)
            Assert.That(browser.RootPropertiesDetails(Nothing), [Is].Null)
            Assert.That(browser.RootPropertiesDetails(root.Nodes(0)), [Is].Null)
            Assert.That(browser.RootPropertiesDetails(foreignTree.Nodes.Add("unittests_foreign")), [Is].Null)
            root.Remove()
            Assert.That(browser.RootPropertiesDetails(root), [Is].Null)
            Assert.That(provider.InstanceReads, [Is].Zero)
            Assert.That(provider.CatalogReads, [Is].Zero)
        End Using
    End Sub

    <Test>
    Public Sub MissingRootMetadataTagDoesNotMasqueradeAsTheServerRoot()
        Dim provider As New InstanceRootProvider()
        Using browser = CreateBrowser(provider)
            Dim root = browser.TreeViewDmsFolders.Nodes(0)
            root.Tag = Nothing
            Assert.That(browser.RootPropertiesDetails(root), [Is].Null)
            Assert.That(provider.InstanceReads, [Is].Zero)
        End Using
    End Sub

    <Test>
    Public Sub FailedCurrentInstanceReadIsReportedInsteadOfReturningCachedIdentity()
        Dim provider As New InstanceRootProvider()
        Using browser = CreateBrowser(provider)
            Dim root = browser.TreeViewDmsFolders.Nodes(0)
            Assert.That(browser.RootPropertiesDetails(root), Does.Contain(TenantA))
            provider.FailInstanceRead = True
            Assert.Throws(Of InvalidOperationException)(Sub() browser.RootPropertiesDetails(root))
        End Using
    End Sub

    Private Shared Function CreateBrowser(provider As RootProvider, Optional initialPath As String = Nothing) As DmsBrowser
        Dim browser As New DmsBrowser(provider) With {.InitialFolder = initialPath}
        Try
            browser.LoadTree()
            Return browser
        Catch
            browser.Dispose()
            Throw
        End Try
    End Function

    Private Class RootProvider
        Inherits NoDmsProvider
        Public Property ScopedItemType As DmsResourceItem.ItemTypes = DmsResourceItem.ItemTypes.Folder
        Public Overrides Function ListRemoteItem(remotePath As String) As DmsResourceItem
            Return New DmsResourceItem With {.Name = remotePath, .FullName = remotePath, .ItemType = ScopedItemType, .ChildDirectoryCount = 1}
        End Function
        Public Overrides Function ListAllRemoteItems(remoteFolderPath As String, searchType As SearchItemType) As List(Of DmsResourceItem)
            Return New List(Of DmsResourceItem) From {
                New DmsResourceItem With {.Name = "unittests_child", .FullName = CombinePath(remoteFolderPath, "unittests_child"), .ItemType = DmsResourceItem.ItemTypes.Folder, .ChildDirectoryCount = 0}}
        End Function
    End Class

    Private Class InstanceRootProvider
        Inherits RootProvider
        Implements IDmsInstanceProvider
        Public Property Instance As DmsInstanceInfo = New DmsInstanceInfo(TenantA, "unittests_Tenant A", True)
        Public Property InstanceReads As Integer
        Public Property CatalogReads As Integer
        Public Property FailInstanceRead As Boolean
        Public ReadOnly Property CurrentDmsInstance As DmsInstanceInfo Implements IDmsInstanceProvider.CurrentDmsInstance
            Get
                InstanceReads += 1
                If FailInstanceRead Then Throw New InvalidOperationException("Fixture identity unavailable.")
                Return Instance
            End Get
        End Property
        Public Function ListAvailableDmsInstances() As IReadOnlyList(Of DmsInstanceInfo) Implements IDmsInstanceProvider.ListAvailableDmsInstances
            CatalogReads += 1
            Throw New InvalidOperationException("Properties must not discover other instances.")
        End Function
        Public Sub SelectDmsInstance(instanceID As String) Implements IDmsInstanceProvider.SelectDmsInstance
            Instance = New DmsInstanceInfo(instanceID, "unittests_Tenant B", True)
        End Sub
    End Class
End Class
