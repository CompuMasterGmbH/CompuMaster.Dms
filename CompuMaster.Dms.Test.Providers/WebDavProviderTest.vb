Option Explicit On
Option Strict On

Imports NUnit.Framework

Public MustInherit Class WebDavProviderTestBase
    Inherits BaseDmsProviderTestBase

    Private UninitializedProvider As Dms.Providers.BaseDmsProvider
    Private AuthorizedProvider As Dms.Providers.BaseDmsProvider

    Protected MustOverride Function CreateSettings() As SettingsBase

    Private Function CreateLoginProfile() As DmsLoginProfile
        Dim Settings As SettingsBase = CreateSettings()
        Dim username As String = Settings.InputLine("username")
        Dim serverurl As String = Settings.NormalizeServerUrl(Settings.InputLine("server url"), username)
        Dim password As String = Settings.InputLine("password")

        Return New DmsLoginProfile() With {
                            .DmsProvider = CompuMaster.Dms.Providers.BaseDmsProvider.DmsProviders.WebDAV,
                            .BaseUrl = serverurl,
                            .Username = username,
                            .Password = password
                            }
    End Function

    Protected Overrides Function UninitializedDmsProvider() As Dms.Providers.BaseDmsProvider
        If UninitializedProvider Is Nothing Then
            UninitializedProvider = Dms.Providers.CreateDmsProviderInstance(Me.CreateLoginProfile.DmsProvider)
        End If
        Return UninitializedProvider
    End Function

    Protected Overrides Function LoggedInDmsProvider() As Dms.Providers.BaseDmsProvider
        If AuthorizedProvider Is Nothing Then
            AuthorizedProvider = Dms.Providers.CreateAuthorizedDmsProviderInstance(Me.CreateLoginProfile)
        End If
        Return AuthorizedProvider
    End Function

    Protected Overrides ReadOnly Property IgnoreSslErrors As Boolean
        Get
            Return False
        End Get
    End Property

    <Test, Category("TestLevel2")>
    Public Sub CloudOcsPublicLinkRoundTrip()
        If Not TypeOf Me Is OwnCloudWebDavProviderTest AndAlso Not TypeOf Me Is NextcloudWebDavProviderTest Then
            Assert.Ignore("Generic WebDAV does not require an OCS sharing API.")
        End If
        Dim Provider As Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        'Fail rather than silently skip all sharing tests if discovery regresses.
        Assert.That(Provider.SupportsSharingSetup, [Is].True, "The cloud test server must expose OCS sharing to verify this feature.")
        Const Path As String = "ZZZ_UnitTests_CM.Dms/OCS_Link_RoundTrip"
        If Provider.FolderExists(Path) Then Provider.DeleteRemoteItem(Path)
        Assert.That(Provider.FolderExists(Path), [Is].False, "Stale sharing test resources could not be removed.")
        Dim OriginalFailure As Exception = Nothing
        Try
            Provider.CreateFolder(Path)
            Dim Item As Dms.Data.DmsResourceItem = Provider.ListRemoteItem(Path)
            Dim Link As Dms.Data.DmsLink = Provider.CreateLink(Item, New Dms.Data.DmsLink(Item, Provider) With {
                .Name = "OCS round trip", .AllowView = True, .AllowDownload = True,
                .Password = Guid.NewGuid().ToString("N"), .ExpiryDateLocalTime = Date.Today.AddDays(2)})
            Assert.That(Link.ID, [Is].Not.Empty)
            Assert.That(Link.WebUrl, [Is].Not.Empty)
            Dim SharedItem = Provider.ListRemoteItem(Path)
            AssertOwnerEqual(Item, SharedItem)
            Dim Reloaded As Dms.Data.DmsLink = SharedItem.ExtendedInfosLinks.Single()
            Assert.That(Reloaded.ID, [Is].EqualTo(Link.ID))
            Assert.That(Reloaded.AllowView, [Is].True)
            Assert.That(Reloaded.AllowDownload, [Is].True)
            Link.Name = "OCS updated"
            Link.ExpiryDateLocalTime = Date.Today.AddDays(3)
            Provider.UpdateLink(Link)
            Reloaded = Provider.ListRemoteItem(Path).ExtendedInfosLinks.Single()
            Assert.That(Reloaded.Name, [Is].EqualTo("OCS updated"))
            Assert.That(Reloaded.ExpiryDateLocalTime.Value.Date, [Is].EqualTo(Date.Today.AddDays(3)))
            Provider.DeleteLink(Link)
            Assert.That(Provider.ListRemoteItem(Path).ExtendedInfosLinks, [Is].Empty)
        Catch ex As Exception
            OriginalFailure = ex
            Throw
        Finally
            Try
                If Provider.FolderExists(Path) Then Provider.DeleteRemoteItem(Path)
                Assert.That(Provider.FolderExists(Path), [Is].False, "Sharing test directory cleanup failed.")
            Catch cleanupFailure As Exception
                If OriginalFailure IsNot Nothing Then Throw New AggregateException("Sharing test and cleanup both failed.", OriginalFailure, cleanupFailure)
                Throw
            End Try
        End Try
    End Sub

    <Test, Category("TestLevel2")>
    Public Async Function ResourceOwnerMetadataMatchesTheServerAndAsyncListings() As Task
        Await WithOwnedMetadataFixtureAsync("Owner_Metadata", Async Function(provider, root)
            Dim synchronous = provider.ListAllRemoteItems(root, Dms.Providers.BaseDmsProvider.SearchItemType.AllItems)
            Dim asynchronous = Await provider.ListAllRemoteItemsAsync(root, Dms.Providers.BaseDmsProvider.SearchItemType.AllItems)
            For Each item In synchronous
                Dim asyncItem = asynchronous.Single(Function(candidate) candidate.Name = item.Name)
                AssertOwnerEqual(item, asyncItem)
                AssertOwnerEqual(item, Await provider.ListRemoteItemAsync(item.FullName))
                Await AssertOwnerMatchesResourceResponseAsync(CType(provider, Dms.Providers.WebDavDmsProvider), item)
            Next
        End Function)
    End Function

    <Test, Category("TestLevel2")>
    Public Async Function ChildDirectoryMetadataReportsEmptyNonemptyOrUnknownAcrossListingPaths() As Task
        Await WithOwnedMetadataFixtureAsync("Child_Metadata", Async Function(provider, root)
            Dim parent = Await provider.ListRemoteItemAsync(root)
            AssertChildMetadata(parent, 1)
            Dim children = Await provider.ListAllRemoteItemsAsync(root, Dms.Providers.BaseDmsProvider.SearchItemType.Folders)
            Assert.That(children, Has.Count.EqualTo(1))
            Dim empty = children.Single()
            AssertChildMetadata(empty, 0)
            Dim synchronous = provider.ListAllRemoteItems(root, Dms.Providers.BaseDmsProvider.SearchItemType.Folders).Single()
            Assert.That(synchronous.ChildDirectoryCount, [Is].EqualTo(empty.ChildDirectoryCount))
            Assert.That(synchronous.HasChildDirectories, [Is].EqualTo(empty.HasChildDirectories))
            Dim individual = Await provider.ListRemoteItemAsync(empty.FullName)
            Assert.That(individual.ChildDirectoryCount, [Is].EqualTo(empty.ChildDirectoryCount))
            Assert.That(individual.HasChildDirectories, [Is].EqualTo(empty.HasChildDirectories))
            Dim field = GetType(Dms.Providers.WebDavDmsProvider).GetField("WebDavClient", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic)
            Dim client = CType(field.GetValue(provider), WebDav.WebDavClient)
            Dim name = Xml.Linq.XName.Get("contained-folder-count", "http://nextcloud.org/ns")
            Dim response = Await client.Propfind(CType(provider, Dms.Providers.WebDavDmsProvider).CustomWebApiUrl & empty.FullName, New WebDav.PropfindParameters With {
                .ApplyTo = WebDav.ApplyTo.Propfind.ResourceOnly, .RequestType = WebDav.PropfindRequestType.NamedProperties,
                .CustomProperties = New Xml.Linq.XName() {Xml.Linq.XName.Get("resourcetype", "DAV:"), name}})
            Assert.That(response.IsSuccessful, [Is].True, "Named child-metadata request failed.")
            Dim resource = response.Resources.Single()
            Dim status = resource.PropertyStatuses.FirstOrDefault(Function(candidate) candidate.Name = name)
            Dim count = resource.Properties.FirstOrDefault(Function(prop) prop.Name = name)
            TestContext.WriteLine("Child-folder capability: property status=" & If(status Is Nothing, "unreported", status.StatusCode.ToString(Globalization.CultureInfo.InvariantCulture)) & ", mappedKnown=" & empty.ChildDirectoryCount.HasValue.ToString())
            CapabilityEvidence.Record(Me.GetType().Name & "-children", New With {
                .ProviderFixture = Me.GetType().Name,
                .NamedPropertyStatus = If(status Is Nothing, CType(Nothing, Integer?), status.StatusCode),
                .ParentCount = parent.ChildDirectoryCount, .ParentHasChildren = parent.HasChildDirectories,
                .EmptyCount = empty.ChildDirectoryCount, .EmptyHasChildren = empty.HasChildDirectories})
            If count IsNot Nothing AndAlso (status Is Nothing OrElse status.IsSuccessful) Then
                Dim numeric As Integer
                If Integer.TryParse(count.Value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, numeric) AndAlso numeric >= 0 Then
                    Assert.That(empty.ChildDirectoryCount, [Is].EqualTo(numeric), "A supported explicit count must reach the initial listing.")
                End If
            Else
                Assert.That(empty.ChildDirectoryCount.HasValue, [Is].False, "Unavailable metadata must remain unknown, even for this empty fixture.")
            End If
        End Function)
    End Function

    <Test, Category("TestLevel2")>
    Public Async Function FileBackedBatchUploadReportsSourceBytesAndConfirmedCompletion() As Task
        Await WithOwnedMetadataFixtureAsync("Upload_Progress", Async Function(provider, root)
            Dim local = System.IO.Path.GetTempFileName()
            Try
                Using output As New System.IO.FileStream(local, System.IO.FileMode.Truncate, System.IO.FileAccess.Write)
                    output.SetLength(32L * 1024L * 1024L)
                End Using
                For index As Integer = 1 To 2
                    Dim observer As New LiveUploadProgressCollector()
                    Dim remote = provider.CombinePath(root, "payload-" & index.ToString(Globalization.CultureInfo.InvariantCulture) & ".bin")
                    Await provider.UploadFileWithProgressAsync(remote, local, observer)
                    Assert.That(observer.Latest.Phase, [Is].EqualTo(Dms.Data.DmsTransferPhase.Completed))
                    Assert.That(observer.Latest.BytesTransferred, [Is].EqualTo(32L * 1024L * 1024L))
                    Assert.That(observer.Latest.TotalBytes, [Is].EqualTo(32L * 1024L * 1024L))
                    Assert.That((Await provider.ListRemoteItemAsync(remote)).ContentLength, [Is].EqualTo(32L * 1024L * 1024L))
                Next
            Finally
                System.IO.File.Delete(local)
            End Try
        End Function)
    End Function

    Private Class LiveUploadProgressCollector
        Implements IProgress(Of Dms.Data.DmsTransferProgress)
        Friend Latest As Dms.Data.DmsTransferProgress
        Public Sub Report(value As Dms.Data.DmsTransferProgress) Implements IProgress(Of Dms.Data.DmsTransferProgress).Report
            Latest = value
        End Sub
    End Class

    Private Shared Sub AssertChildMetadata(item As Dms.Data.DmsResourceItem, expected As Integer)
        If item.ChildDirectoryCount.HasValue Then
            Assert.That(item.ChildDirectoryCount.Value, [Is].EqualTo(expected))
            Assert.That(item.HasChildDirectories, [Is].EqualTo(expected > 0))
        Else
            Assert.That(item.HasChildDirectories.HasValue, [Is].False)
        End If
    End Sub

    Private Async Function WithOwnedMetadataFixtureAsync(name As String, action As Func(Of Dms.Providers.BaseDmsProvider, String, Task)) As Task
        Dim provider = Me.LoggedInDmsProvider()
        Dim root = provider.CombinePath(TestDirName, name)
        If Await provider.RemoteItemExistsAsync(root) Then Await provider.DeleteRemoteItemAsync(root)
        Assert.That(Await provider.RemoteItemExistsAsync(root), [Is].False, "Stale metadata fixture could not be removed.")
        Dim originalFailure As Exception = Nothing
        Try
            Await provider.CreateFolderAsync(root)
            Await provider.CreateFolderAsync(provider.CombinePath(root, "empty"))
            Await provider.UploadFileAsync(provider.CombinePath(root, "file.txt"), New Byte() {1, 2, 3})
            Await action(provider, root)
        Catch ex As Exception
            originalFailure = ex
            Throw
        Finally
            Try
                If provider.RemoteItemExists(root) Then provider.DeleteRemoteItem(root)
                Assert.That(provider.RemoteItemExists(root), [Is].False, "Metadata fixture cleanup failed.")
            Catch cleanupFailure As Exception
                If originalFailure IsNot Nothing Then Throw New AggregateException("Metadata test and cleanup both failed.", originalFailure, cleanupFailure)
                Throw
            End Try
        End Try
    End Function

    Private Shared Sub AssertOwnerEqual(first As Dms.Data.DmsResourceItem, second As Dms.Data.DmsResourceItem)
        Assert.That(String.Equals(first.ExtendedInfosOwner.ID, second.ExtendedInfosOwner.ID, StringComparison.Ordinal), [Is].True, "Owner identity differs between listing paths; values are intentionally omitted.")
        Assert.That(String.Equals(first.ExtendedInfosOwner.DisplayName, second.ExtendedInfosOwner.DisplayName, StringComparison.Ordinal), [Is].True, "Owner display metadata differs between listing paths; values are intentionally omitted.")
    End Sub

    Private Async Function AssertOwnerMatchesResourceResponseAsync(provider As Dms.Providers.WebDavDmsProvider, item As Dms.Data.DmsResourceItem) As Task
        Dim field = GetType(Dms.Providers.WebDavDmsProvider).GetField("WebDavClient", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic)
        Dim client = CType(field.GetValue(provider), WebDav.WebDavClient)
        Dim ownerId = Xml.Linq.XName.Get("owner-id", "http://owncloud.org/ns")
        Dim ownerDisplay = Xml.Linq.XName.Get("owner-display-name", "http://owncloud.org/ns")
        Dim davOwner = Xml.Linq.XName.Get("owner", "DAV:")
        Dim response = Await client.Propfind(provider.CustomWebApiUrl & item.FullName, New WebDav.PropfindParameters With {
            .ApplyTo = WebDav.ApplyTo.Propfind.ResourceOnly, .RequestType = WebDav.PropfindRequestType.NamedProperties,
            .CustomProperties = New Xml.Linq.XName() {Xml.Linq.XName.Get("resourcetype", "DAV:"), ownerId, ownerDisplay, davOwner}})
        Assert.That(response.IsSuccessful, [Is].True, "Named owner-property request failed.")
        Dim resource = response.Resources.Single()
        CapabilityEvidence.Record(Me.GetType().Name & "-owner-" & item.ItemType.ToString(), New With {
            .ProviderFixture = Me.GetType().Name, .ResourceKind = item.ItemType.ToString(),
            .OwnerIdKnown = Not String.IsNullOrEmpty(item.ExtendedInfosOwner.ID),
            .OwnerDisplayTextAvailable = Not String.IsNullOrEmpty(item.ExtendedInfosOwner.DisplayName),
            .Properties = New Xml.Linq.XName() {ownerId, ownerDisplay, davOwner}.Select(Function(name) New With {
                .Name = name.ToString(),
                .Status = resource.PropertyStatuses.Where(Function(prop) prop.Name = name).Select(Function(prop) CType(prop.StatusCode, Integer?)).FirstOrDefault()}).ToArray()})
        For Each propertyName In New Xml.Linq.XName() {ownerId, ownerDisplay, davOwner}
            Dim status = resource.PropertyStatuses.FirstOrDefault(Function(candidate) candidate.Name = propertyName)
            TestContext.WriteLine("Owner capability (" & item.ItemType.ToString() & "): " & propertyName.LocalName & " status=" & If(status Is Nothing, "unreported", status.StatusCode.ToString(Globalization.CultureInfo.InvariantCulture)))
        Next
        Dim id = resource.Properties.FirstOrDefault(Function(prop) prop.Name = ownerId)
        Dim display = resource.Properties.FirstOrDefault(Function(prop) prop.Name = ownerDisplay)
        If id IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(id.Value) Then
            Assert.That(String.Equals(item.ExtendedInfosOwner.ID, Xml.Linq.XElement.Parse("<root>" & id.Value & "</root>").Value.Trim(), StringComparison.Ordinal), [Is].True, "Mapped owner differs from the reported resource owner; values are intentionally omitted.")
        End If
        If display IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(display.Value) Then
            Assert.That(String.Equals(item.ExtendedInfosOwner.DisplayName, Xml.Linq.XElement.Parse("<root>" & display.Value & "</root>").Value.Trim(), StringComparison.Ordinal), [Is].True, "Mapped owner display differs from the resource response; values are intentionally omitted.")
        End If
        If TypeOf Me Is OwnCloudWebDavProviderTest OrElse TypeOf Me Is NextcloudWebDavProviderTest Then
            Assert.That(Not String.IsNullOrWhiteSpace(item.ExtendedInfosOwner.ID) OrElse Not String.IsNullOrWhiteSpace(item.ExtendedInfosOwner.DisplayName), [Is].True, "The configured cloud server must report owner metadata for its own test resources.")
        End If
    End Function

    Private Const TestDirName As String = "ZZZ_UnitTests_CM.Dms"
    Private Const TestDirNameSub1 As String = "ZZZ_UnitTests_CM.Dms/Folder"
    Private Const TestDirNameSub2 As String = "ZZZ_UnitTests_CM.Dms/Folder/Sub"

    <OneTimeSetUp> Public Sub CreateTestDir()
        Dim Provider As Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        If Provider.FolderExists(TestDirName) = False Then
            Provider.CreateFolder(TestDirName)
        End If
        If Provider.FolderExists(TestDirNameSub1) = False Then
            Provider.CreateFolder(TestDirNameSub1)
        End If
        If Provider.FolderExists(TestDirNameSub2) = False Then
            Provider.CreateFolder(TestDirNameSub2)
        End If
    End Sub

    <OneTimeTearDown> Public Sub CleanupTestDir()
        Dim Provider As Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        If Provider.FolderExists(TestDirName) Then
            Provider.DeleteRemoteItem(TestDirName)
        End If
    End Sub

    Public Overrides ReadOnly Property RemoteFilesMustExist As String() = New String() {}
    Public Overrides ReadOnly Property RemoteFoldersMustExist As String() = New String() {TestDirName, TestDirNameSub1, TestDirNameSub2}
    Public Overrides ReadOnly Property RemoteCollectionsMustExist As String() = New String() {}
    Public Overrides ReadOnly Property RemoteItemsMustNotExist As String() = New String() {"/gibt's nicht"}
    Public Overrides ReadOnly Property RemoteFoldersWithFiles As String() = New String() {}
    Public Overrides ReadOnly Property RemoteFoldersWithSubFolders As String() = New String() {TestDirName, TestDirNameSub1}
    Public Overrides ReadOnly Property DownloadTestFilesText As KeyValuePair(Of String, String)() = New KeyValuePair(Of String, String)() {}
    Public Overrides ReadOnly Property DownloadTestFilesBinary As KeyValuePair(Of String, Byte())() = New KeyValuePair(Of String, Byte())() {}
    Public Overrides ReadOnly Property UploadTestFilesAndCleanupAgainFilePath As KeyValuePair(Of String, String)() = New KeyValuePair(Of String, String)() {
        New KeyValuePair(Of String, String)("upload.file.test", Me.TestFileForUploadTests("TestFile.txt"))
        }
    Public Overrides ReadOnly Property UploadTestFilesAndCleanupAgainBinary As KeyValuePair(Of String, Byte())() = New KeyValuePair(Of String, Byte())() {
        New KeyValuePair(Of String, Byte())("upload.binary.test", New Byte() {40, 50, 60, 10, 13, 35, 45, 55})
        }

End Class

<TestFixture, Category("RemoteDms"), Category("WebDav")>
Public NotInheritable Class WebDavProviderTest
    Inherits WebDavProviderTestBase

    Protected Overrides Function CreateSettings() As SettingsBase
        Return New WebDavSettings
    End Function
End Class

<TestFixture, Category("RemoteDms"), Category("OwnCloud")>
Public NotInheritable Class OwnCloudWebDavProviderTest
    Inherits WebDavProviderTestBase

    Protected Overrides Function CreateSettings() As SettingsBase
        Return New OwnCloudWebDavSettings
    End Function
End Class
