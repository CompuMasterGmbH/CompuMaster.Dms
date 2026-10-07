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
            Dim Reloaded As Dms.Data.DmsLink = Provider.ListRemoteItem(Path).ExtendedInfosLinks.Single()
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
