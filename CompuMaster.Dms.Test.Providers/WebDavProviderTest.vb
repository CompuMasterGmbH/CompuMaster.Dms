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

<TestFixture, Category("RemoteDms"), Category("OwnCloudWebDav")>
Public NotInheritable Class WebDavProviderTest
    Inherits WebDavProviderTestBase

    Protected Overrides Function CreateSettings() As SettingsBase
        Return New WebDavSettings
    End Function
End Class
