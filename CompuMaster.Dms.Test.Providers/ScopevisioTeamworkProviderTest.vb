Option Explicit On
Option Strict On

Imports System.IO
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture, Category("RemoteDms"), Category("ScopevisioTeamwork")>
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
    ''' Verifies that an upload byte limit survives creation and update on the Scopevisio server.
    ''' </summary>
    <Test>
    Public Sub UploadLinkMaxBytesPersistsAfterCreateAndUpdate()
        Const CollectionName As String = "ZZZ_UnitTests_CM.Dms_MaxBytesLink"
        Const InitialMaxBytes As Long = 1073741824L
        Const UpdatedMaxBytes As Long = 3221225472L
        Dim Provider As CompuMaster.Dms.Providers.CenterDeviceDmsProviderBase =
            DirectCast(Me.LoggedInDmsProvider, CompuMaster.Dms.Providers.CenterDeviceDmsProviderBase)
        Dim TestFailure As Exception = Nothing
        Dim CleanupFailure As Exception = Nothing

        Try
            DeleteTestCollectionIfExisting(Provider, CollectionName)
            ClassicAssert.IsFalse(Provider.CollectionExists(CollectionName), "The test-owned collection must be absent before creation.")

            Provider.CreateCollection(CollectionName)
            Dim Collection = Provider.ListRemoteItem(CollectionName)
            ClassicAssert.IsNotNull(Collection)
            Dim Link As New Dms.Data.DmsLink(Collection, Provider) With {
                .AllowUpload = True,
                .Name = "MaxBytes regression test",
                .MaxBytes = InitialMaxBytes
            }

            Link = Provider.CreateLink(Collection, Link)
            ClassicAssert.IsNotEmpty(Link.ID)
            ClassicAssert.AreEqual(InitialMaxBytes, Provider.IOClient.GetUploadLink(Link.ID).MaxBytes)
            ClassicAssert.AreEqual(InitialMaxBytes, Link.MaxBytes)

            Link.MaxBytes = UpdatedMaxBytes
            Provider.UpdateLink(Link)
            ClassicAssert.AreEqual(UpdatedMaxBytes, Provider.IOClient.GetUploadLink(Link.ID).MaxBytes)
        Catch ex As Exception
            TestFailure = ex
        Finally
            Try
                DeleteTestCollectionIfExisting(Provider, CollectionName)
                ClassicAssert.IsFalse(Provider.CollectionExists(CollectionName), "The test-owned collection must be removed after the test.")
            Catch ex As Exception
                CleanupFailure = ex
            End Try
        End Try

        If TestFailure IsNot Nothing AndAlso CleanupFailure IsNot Nothing Then
            Throw New AggregateException("The upload-link test and cleanup both failed.", TestFailure, CleanupFailure)
        End If
        If TestFailure IsNot Nothing Then System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(TestFailure).Throw()
        If CleanupFailure IsNot Nothing Then System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(CleanupFailure).Throw()
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
        DeleteTestFiles(provider, TestDirNameSub2, fileName)
    End Sub

    ''' <summary>
    ''' Verifies that copy and move use the selected CenterDevice file ID when several files share the same path.
    ''' </summary>
    <Test>
    Public Sub CopyAndMoveDuplicateFileNamesBySelectedResourceId()
        Dim Provider As CompuMaster.Dms.Providers.CenterDeviceDmsProviderBase = DirectCast(Me.LoggedInDmsProvider, CompuMaster.Dms.Providers.CenterDeviceDmsProviderBase)
        Dim TestSuffix As String = Guid.NewGuid().ToString("N")
        Dim DuplicateName As String = "duplicate-actions-" & TestSuffix & ".test"
        Dim CopyTargetName As String = "duplicate-copy-" & TestSuffix & ".test"
        Dim MoveTargetName As String = "duplicate-move-" & TestSuffix & ".test"
        Dim OlderContent As Byte() = {11, 13, 17, 19}
        Dim NewerContent As Byte() = {23, 29, 31, 37, 41}
        Dim SourceDirectory = Provider.IOClient.RootDirectory.OpenDirectoryPath(TestDirNameSub2)
        Dim CopyTargetPath As String = Provider.CombinePath(TestDirNameSub1, CopyTargetName)
        Dim MoveTargetPath As String = Provider.CombinePath(TestDirNameSub1, MoveTargetName)

        DeleteTestFiles(Provider, TestDirNameSub2, DuplicateName)
        DeleteTestFiles(Provider, TestDirNameSub1, CopyTargetName, MoveTargetName)
        Try
            SourceDirectory.UploadAndCreateNewFile(Function() New MemoryStream(OlderContent, writable:=False), DuplicateName)
            SourceDirectory.ResetFilesCache()
            Dim OlderFile = Provider.ListAllRemoteItems(TestDirNameSub2, CompuMaster.Dms.Providers.BaseDmsProvider.SearchItemType.Files).Single(Function(Item) Item.Name = DuplicateName)

            SourceDirectory.UploadAndCreateNewFile(Function() New MemoryStream(NewerContent, writable:=False), DuplicateName)
            SourceDirectory.ResetFilesCache()
            Dim DuplicateFiles = Provider.ListAllRemoteItems(TestDirNameSub2, CompuMaster.Dms.Providers.BaseDmsProvider.SearchItemType.Files).Where(Function(Item) Item.Name = DuplicateName).ToList()
            Dim NewerFile = DuplicateFiles.Single(Function(Item) Item.ExtendedInfosFileID <> OlderFile.ExtendedInfosFileID)

            Provider.Copy(OlderFile, CopyTargetPath, False, False)
            Provider.Move(NewerFile, MoveTargetPath, False, False)

            Dim RemainingSourceFiles = Provider.ListAllRemoteItems(TestDirNameSub2, CompuMaster.Dms.Providers.BaseDmsProvider.SearchItemType.Files).Where(Function(Item) Item.Name = DuplicateName).ToList()
            ClassicAssert.AreEqual(1, RemainingSourceFiles.Count)
            ClassicAssert.AreEqual(OlderFile.ExtendedInfosFileID, RemainingSourceFiles(0).ExtendedInfosFileID)

            Dim CopyDownloadPath As String = Path.GetTempFileName()
            Dim MoveDownloadPath As String = Path.GetTempFileName()
            Try
                Provider.DownloadFile(Provider.ListRemoteItem(CopyTargetPath), CopyDownloadPath)
                Provider.DownloadFile(Provider.ListRemoteItem(MoveTargetPath), MoveDownloadPath)
                CollectionAssert.AreEqual(OlderContent, File.ReadAllBytes(CopyDownloadPath))
                CollectionAssert.AreEqual(NewerContent, File.ReadAllBytes(MoveDownloadPath))
            Finally
                File.Delete(CopyDownloadPath)
                File.Delete(MoveDownloadPath)
            End Try
        Finally
            DeleteTestFiles(Provider, TestDirNameSub2, DuplicateName)
            DeleteTestFiles(Provider, TestDirNameSub1, CopyTargetName, MoveTargetName)
        End Try
    End Sub

    Private Shared Sub DeleteTestFiles(provider As CompuMaster.Dms.Providers.BaseDmsProvider, directoryPath As String, ParamArray fileNames As String())
        Dim ExistingFiles = provider.ListAllRemoteItems(directoryPath, CompuMaster.Dms.Providers.BaseDmsProvider.SearchItemType.Files).Where(Function(Item) fileNames.Contains(Item.Name)).ToList()
        For Each ExistingFile In ExistingFiles
            provider.DeleteRemoteItem(ExistingFile)
        Next
    End Sub

    ''' <summary>
    ''' Verifies that a CenterDevice collection can be renamed in the root without being treated as a regular movable folder.
    ''' </summary>
    <Test>
    Public Sub RenameCollectionInRoot()
        Dim Provider As Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        Dim TestSuffix As String = Guid.NewGuid().ToString("N")
        Dim SourceName As String = "ZZZ_UnitTests_CM.Dms_CollectionMove_" & TestSuffix & "_Source"
        Dim DestinationName As String = "ZZZ_UnitTests_CM.Dms_CollectionMove_" & TestSuffix & "_Target"

        DeleteTestCollectionIfExisting(Provider, SourceName)
        DeleteTestCollectionIfExisting(Provider, DestinationName)
        ClassicAssert.IsFalse(Provider.RemoteItemExists(SourceName))
        ClassicAssert.IsFalse(Provider.RemoteItemExists(DestinationName))

        Try
            Provider.CreateCollection(SourceName)
            Dim Source As Dms.Data.DmsResourceItem = Provider.ListRemoteItem(SourceName)
            ClassicAssert.IsNotNull(Source)
            ClassicAssert.AreEqual(Dms.Data.DmsResourceItem.ItemTypes.Collection, Source.ItemType)

            Provider.Move(Source, DestinationName, False, False)

            ClassicAssert.IsFalse(Provider.RemoteItemExists(SourceName))
            Dim Destination As Dms.Data.DmsResourceItem = Provider.ListRemoteItem(DestinationName)
            ClassicAssert.IsNotNull(Destination)
            ClassicAssert.AreEqual(Dms.Data.DmsResourceItem.ItemTypes.Collection, Destination.ItemType)
            ClassicAssert.AreEqual(Source.ExtendedInfosCollectionID, Destination.ExtendedInfosCollectionID)
        Finally
            DeleteTestCollectionIfExisting(Provider, SourceName)
            DeleteTestCollectionIfExisting(Provider, DestinationName)
        End Try
    End Sub

    ''' <summary>
    ''' Verifies that collections aren't exposed as regular copyable directory trees.
    ''' </summary>
    <Test>
    Public Sub CopyCollectionIsNotSupported()
        Dim Provider As Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        Dim DestinationName As String = "ZZZ_UnitTests_CM.Dms_CollectionCopy_" & Guid.NewGuid().ToString("N")
        DeleteTestCollectionIfExisting(Provider, DestinationName)
        ClassicAssert.IsFalse(Provider.RemoteItemExists(DestinationName))

        Try
            Dim Source As Dms.Data.DmsResourceItem = Provider.ListRemoteItem(TestDirName)
            ClassicAssert.IsNotNull(Source)
            ClassicAssert.AreEqual(Dms.Data.DmsResourceItem.ItemTypes.Collection, Source.ItemType)
            ClassicAssert.Throws(Of NotSupportedException)(Sub() Provider.Copy(Source, DestinationName, False, False))
            ClassicAssert.IsFalse(Provider.RemoteItemExists(DestinationName))
        Finally
            DeleteTestCollectionIfExisting(Provider, DestinationName)
        End Try
    End Sub

    Private Shared Sub DeleteTestCollectionIfExisting(provider As Dms.Providers.BaseDmsProvider, collectionName As String)
        Dim ExistingItem As Dms.Data.DmsResourceItem = provider.ListRemoteItem(collectionName)
        If ExistingItem IsNot Nothing Then provider.DeleteRemoteItem(ExistingItem, Dms.Data.DmsResourceItem.ItemTypes.Collection)
    End Sub

End Class
