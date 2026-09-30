Option Explicit On
Option Strict On

Imports System.IO
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

Public Class ScopevisioTeamworkProviderTest
    Inherits BaseDmsProviderTestBase

    Private Function CreateLoginProfile() As DmsLoginProfile
        Dim Settings As New ScopevisioTeamworkSettings
        Dim username As String = Settings.InputLine("username")
        Dim customerno As String = Settings.InputLine("customer no.")
        Dim password As String = Settings.InputLine("password")

        Return New DmsLoginProfile() With {
                            .DmsProvider = CompuMaster.Dms.Providers.BaseDmsProvider.DmsProviders.Scopevisio,
                            .CustomerInstance = customerno,
                            .Username = username,
                            .Password = password
                            }
    End Function

    Protected Overrides Function UninitializedDmsProvider() As Dms.Providers.BaseDmsProvider
        Static Result As Dms.Providers.BaseDmsProvider
        If Result Is Nothing Then
            Result = Dms.Providers.CreateDmsProviderInstance(Me.CreateLoginProfile.DmsProvider)
        End If
        Return Result
    End Function

    Protected Overrides Function LoggedInDmsProvider() As Dms.Providers.BaseDmsProvider
        Static Result As Dms.Providers.BaseDmsProvider
        If Result Is Nothing Then
            Result = Dms.Providers.CreateAuthorizedDmsProviderInstance(Me.CreateLoginProfile)
        End If
        Return Result
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
        If Provider.CollectionExists(TestDirName) = False Then
            Provider.CreateCollection(TestDirName)
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
        If Provider.CollectionExists(TestDirName) Then
            Provider.DeleteRemoteItem(TestDirName)
        End If
    End Sub

    Public Overrides ReadOnly Property RemoteFilesMustExist As String() = New String() {}
    Public Overrides ReadOnly Property RemoteFoldersMustExist As String() = New String() {TestDirNameSub1, TestDirNameSub2}
    Public Overrides ReadOnly Property RemoteCollectionsMustExist As String() = New String() {TestDirName, "Eingangsrechnungen"}
    Public Overrides ReadOnly Property RemoteItemsMustNotExist As String() = New String() {"/gibt's nicht"}
    Public Overrides ReadOnly Property RemoteFoldersWithFiles As String() = New String() {}
    Public Overrides ReadOnly Property RemoteFoldersWithSubFolders As String() = New String() {TestDirNameSub1}
    Public Overrides ReadOnly Property DownloadTestFilesText As KeyValuePair(Of String, String)() = New KeyValuePair(Of String, String)() {}
    Public Overrides ReadOnly Property DownloadTestFilesBinary As KeyValuePair(Of String, Byte())() = New KeyValuePair(Of String, Byte())() {}
    Public Overrides ReadOnly Property UploadTestFilesAndCleanupAgainFilePath As KeyValuePair(Of String, String)() = New KeyValuePair(Of String, String)() {
        New KeyValuePair(Of String, String)("upload.file.test", Me.TestFileForUploadTests("TestFile.txt"))
        }
    Public Overrides ReadOnly Property UploadTestFilesAndCleanupAgainBinary As KeyValuePair(Of String, Byte())() = New KeyValuePair(Of String, Byte())() {
        New KeyValuePair(Of String, Byte())("upload.binary.test", New Byte() {40, 50, 60, 10, 13, 35, 45, 55})
        }

    ''' <summary>
    ''' Verifies that Scopevisio reports child-folder metadata for both nested and empty folders.
    ''' </summary>
    <Test>
    Public Sub DirectoryListingIncludesKnownChildFolderFlags()
        Dim Provider As Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider

        ClassicAssert.IsEmpty(Provider.ListAllFolderNames(TestDirNameSub2), "The test leaf folder must have no child folders.")

        Dim CollectionChildren = Provider.ListAllDirectoryItems(TestDirName)
        Dim Folder = CollectionChildren.Single(Function(Item) Item.Name = "Folder")
        ClassicAssert.AreEqual(True, Folder.HasChildDirectories)

        Dim FolderChildren = Provider.ListAllDirectoryItems(TestDirNameSub1)
        Dim SubFolder = FolderChildren.Single(Function(Item) Item.Name = "Sub")
        ClassicAssert.AreEqual(False, SubFolder.HasChildDirectories)
    End Sub

    ''' <summary>
    ''' Verifies that downloads of duplicate Scopevisio file names return the content belonging to the selected file ID.
    ''' </summary>
    <Test>
    Public Sub DownloadDuplicateFileNamesBySelectedResourceId()
        Const FileName As String = "duplicate-download-content.test"
        Dim olderContent As Byte() = {1, 3, 5, 7, 9}
        Dim newerContent As Byte() = {2, 4, 6, 8, 10, 12, 14}
        Dim Provider As CompuMaster.Dms.Providers.CenterDeviceDmsProviderBase = DirectCast(Me.LoggedInDmsProvider, CompuMaster.Dms.Providers.CenterDeviceDmsProviderBase)
        Dim RemoteDirectory = Provider.IOClient.RootDirectory.OpenDirectoryPath(TestDirNameSub2)

        DeleteDuplicateDownloadTestFiles(Provider, FileName)
        Try
            RemoteDirectory.UploadAndCreateNewFile(Function() New MemoryStream(olderContent, writable:=False), FileName)
            RemoteDirectory.ResetFilesCache()
            Dim OlderFile = Provider.ListAllRemoteItems(TestDirNameSub2, CompuMaster.Dms.Providers.BaseDmsProvider.SearchItemType.Files).Single(Function(Item) Item.Name = FileName)

            RemoteDirectory.UploadAndCreateNewFile(Function() New MemoryStream(newerContent, writable:=False), FileName)
            RemoteDirectory.ResetFilesCache()
            Dim DuplicateFiles = Provider.ListAllRemoteItems(TestDirNameSub2, CompuMaster.Dms.Providers.BaseDmsProvider.SearchItemType.Files).Where(Function(Item) Item.Name = FileName).ToList()
            Dim NewerFile = DuplicateFiles.Single(Function(Item) Item.ExtendedInfosFileID <> OlderFile.ExtendedInfosFileID)

            ClassicAssert.AreEqual(2, DuplicateFiles.Count)
            ClassicAssert.AreEqual(OlderFile.FullName, NewerFile.FullName)
            ClassicAssert.AreNotEqual(OlderFile.ExtendedInfosFileID, NewerFile.ExtendedInfosFileID)

            Dim NewerDownloadPath As String = Path.GetTempFileName()
            Dim OlderDownloadPath As String = Path.GetTempFileName()
            Try
                Provider.DownloadFile(NewerFile, NewerDownloadPath)
                Provider.DownloadFile(OlderFile, OlderDownloadPath)

                CollectionAssert.AreEqual(newerContent, File.ReadAllBytes(NewerDownloadPath))
                CollectionAssert.AreEqual(olderContent, File.ReadAllBytes(OlderDownloadPath))
            Finally
                File.Delete(NewerDownloadPath)
                File.Delete(OlderDownloadPath)
            End Try
        Finally
            DeleteDuplicateDownloadTestFiles(Provider, FileName)
        End Try
    End Sub

    Private Sub DeleteDuplicateDownloadTestFiles(provider As CompuMaster.Dms.Providers.BaseDmsProvider, fileName As String)
        Dim ExistingFiles = provider.ListAllRemoteItems(TestDirNameSub2, CompuMaster.Dms.Providers.BaseDmsProvider.SearchItemType.Files).Where(Function(Item) Item.Name = fileName).ToList()
        For Each ExistingFile In ExistingFiles
            provider.DeleteRemoteItem(ExistingFile)
        Next
    End Sub

End Class
