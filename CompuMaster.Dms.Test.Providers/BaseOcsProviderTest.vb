Option Explicit On
Option Strict On

Imports NUnit.Framework

Public MustInherit Class BaseOcsProviderTest
    Inherits BaseLoginProfileProviderTest

    ''FOR FUTURE IMPLEMENTATION OF OcsProviderTest:
    'Protected Overrides Function CreateLoginProfile() As DmsLoginProfile
    '    Dim Settings As New OcsSettings
    '    Dim username As String = Settings.InputLine("username")
    '    Dim serverurl As String = Settings.InputLine("server url")
    '    Dim password As String = Settings.InputLine("password")

    '    Return New DmsLoginProfile() With {
    '                        .DmsProvider = CompuMaster.Dms.Providers.BaseDmsProvider.DmsProviders.OCS,
    '                        .BaseUrl = serverurl,
    '                        .Username = username,
    '                        .Password = password
    '                        }
    'End Function

    Protected Overrides ReadOnly Property IgnoreSslErrors As Boolean
        Get
            Return False
        End Get
    End Property

    Private ReadOnly Property TestDirName As String = "ZZZ_UnitTests_CM." & Me.TestedProvider.ToString & ".Dms"
    Private ReadOnly Property TestDirNameSub1 As String = "ZZZ_UnitTests_CM." & Me.TestedProvider.ToString & ".Dms/Folder"
    Private ReadOnly Property TestDirNameSub2 As String = "ZZZ_UnitTests_CM." & Me.TestedProvider.ToString & ".Dms/Folder/Sub"

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