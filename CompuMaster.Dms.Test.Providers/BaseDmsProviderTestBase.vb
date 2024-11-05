Option Explicit On
Option Strict On

Imports NUnit.Framework
Imports CompuMaster.Dms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

<Parallelizable(ParallelScope.Fixtures)>
Public MustInherit Class BaseDmsProviderTestBase

    Public MustOverride ReadOnly Property TestedProvider As CompuMaster.Dms.Providers.BaseDmsProvider.DmsProviders

    Protected Overridable ReadOnly Property IgnoreSslErrors As Boolean = False

    <OneTimeSetUp> Public Sub InitSslIgnore()
        If IgnoreSslErrors Then
            OriginalSslValidateCallback = System.Net.ServicePointManager.ServerCertificateValidationCallback
            System.Net.ServicePointManager.ServerCertificateValidationCallback = AddressOf AcceptAllCertifications
            System.Console.WriteLine("GLOBAL IGNORE SSL ERRORS=ENABLED")
        End If
    End Sub

    <OneTimeTearDown> Public Sub RemoveSslIgnore()
        If IgnoreSslErrors Then
            System.Net.ServicePointManager.ServerCertificateValidationCallback = OriginalSslValidateCallback
            System.Console.WriteLine("GLOBAL IGNORE SSL ERRORS=DISABLED")
        End If
    End Sub

    Protected MustOverride Function UninitializedDmsProvider() As CompuMaster.Dms.Providers.BaseDmsProvider
    Protected MustOverride Function LoggedInDmsProvider() As CompuMaster.Dms.Providers.BaseDmsProvider

    <Test> Public Sub ProviderMatchBasic()
        Assert.AreEqual(Me.TestedProvider, Me.UninitializedDmsProvider.DmsProviderID)
        Assert.AreEqual(Me.TestedProvider, CompuMaster.Dms.Providers.DmsFactory.CreateDmsProviderInstance(Me.TestedProvider).DmsProviderID)
        Assert.AreEqual(Me.TestedProvider, CompuMaster.Dms.Providers.DmsFactory.CreateDmsProviderInstance(Me.TestedProvider).CreateNewCredentialsInstance.DmsProvider)
    End Sub

    <Test> Public Sub LoginAtRestApiWebservice()
        Me.LoggedInDmsProvider()
        Assert.Pass()
    End Sub

    <Test> Public Overridable Sub ListAllFolderNames()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        Dim Items As List(Of String)

        Items = DmsProvider.ListAllFolderNames(DmsProvider.BrowseInRootFolderName)
        For Each MyItem As String In Items
            System.Console.WriteLine(MyItem)
        Next
        Assert.NotZero(Items.Count)
        System.Console.WriteLine("---")

        For Each RemoteFolder As String In Me.RemoteFoldersWithSubFolders
            Items = DmsProvider.ListAllFolderNames(RemoteFolder)
            For Each MyItem As String In Items
                System.Console.WriteLine(MyItem)
            Next
            Assert.NotZero(Items.Count)
            System.Console.WriteLine("---")
        Next
    End Sub

    <Test> Public Overridable Sub ListAllCollectionNames()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        Dim Items As List(Of String) = DmsProvider.ListAllCollectionNames(DmsProvider.BrowseInRootFolderName)
        For Each MyItem As String In Items
            System.Console.WriteLine(MyItem)
        Next
        If DmsProvider.SupportsCollections = False AndAlso Me.RemoteCollectionsMustExist.Length <> 0 Then
            Throw New NotSupportedException("Provider doesn't support collections, but " & NameOf(RemoteCollectionsMustExist) & " contains elements")
        End If
        If Me.RemoteCollectionsMustExist.Length = 0 Then
            Assert.Zero(Items.Count)
        Else
            Assert.NotZero(Items.Count)
        End If
    End Sub

    <Test> Public Overridable Sub ListAllFileNames()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        Dim Items As List(Of String)

        Items = DmsProvider.ListAllFileNames(DmsProvider.BrowseInRootFolderName)
        For Each MyItem As String In Items
            System.Console.WriteLine(MyItem)
        Next
        If DmsProvider.SupportsFilesInRootFolder Then
            Assert.NotZero(Items.Count)
        Else
            Assert.Zero(Items.Count)
        End If
        System.Console.WriteLine("---")

        For Each RemoteFolder As String In Me.RemoteFoldersWithFiles
            Items = DmsProvider.ListAllFileNames(RemoteFolder)
            For Each MyItem As String In Items
                System.Console.WriteLine(MyItem)
            Next
            Assert.NotZero(Items.Count)
            System.Console.WriteLine("---")
        Next
    End Sub

    <Test> Public Overridable Sub ListAllFolderItems()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        Dim Items As List(Of DmsResourceItem)

        If DmsProvider.SupportsCollections Then
            Items = DmsProvider.ListAllCollectionItems(DmsProvider.BrowseInRootFolderName)
            For Each MyItem As DmsResourceItem In Items
                System.Console.WriteLine(MyItem)
                Assert.AreEqual("", MyItem.Collection)
                Assert.AreEqual("", MyItem.Folder)
                Assert.AreEqual(DmsResourceItem.ItemTypes.Collection, MyItem.ItemType)
            Next
            Assert.NotZero(Items.Count)
            System.Console.WriteLine("---")
        End If

        Items = DmsProvider.ListAllFolderItems(DmsProvider.BrowseInRootFolderName)
        For Each MyItem As DmsResourceItem In Items
            System.Console.WriteLine(MyItem)
            Assert.AreEqual("", MyItem.Collection)
            Assert.AreEqual("", MyItem.Folder)
            Assert.AreEqual(DmsResourceItem.ItemTypes.Folder, MyItem.ItemType)
        Next
        If DmsProvider.SupportsCollections Then
            'expect no folders
            Assert.Zero(Items.Count)
            System.Console.WriteLine("---")
        Else
            'expect existing folders count > 1
            Assert.NotZero(Items.Count)
            System.Console.WriteLine("---")
        End If

        For Each RemoteFolder As String In Me.RemoteFoldersWithSubFolders
            Items = DmsProvider.ListAllFolderItems(RemoteFolder)
            For Each MyItem As DmsResourceItem In Items
                System.Console.WriteLine(MyItem)
                Assert.AreEqual("", MyItem.Collection)
                Assert.AreEqual(Me.FolderNameWithoutTrailingDirectorySeparator(DmsProvider, RemoteFolder), MyItem.Folder)
                Assert.AreEqual(DmsResourceItem.ItemTypes.Folder, MyItem.ItemType)
            Next
            Assert.NotZero(Items.Count)
            System.Console.WriteLine("---")
        Next
    End Sub

    <Test> Public Sub RemoteItemExists()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        For Each MustNotExistItem As String In Me.RemoteItemsMustNotExist
            Assert.IsFalse(DmsProvider.RemoteItemExists(MustNotExistItem))
        Next
        For Each MustNotExistItem As String In Me.RemoteFilesMustExist
            Assert.IsTrue(DmsProvider.RemoteItemExists(MustNotExistItem))
        Next
        For Each MustNotExistItem As String In Me.RemoteCollectionsMustExist
            Assert.IsTrue(DmsProvider.RemoteItemExists(MustNotExistItem))
        Next
        For Each MustNotExistItem As String In Me.RemoteFoldersMustExist
            Assert.IsTrue(DmsProvider.RemoteItemExists(MustNotExistItem))
        Next
    End Sub

    <Test> Public Overridable Sub ListAllCollectionItems()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        Dim Items As List(Of DmsResourceItem) = DmsProvider.ListAllCollectionItems(DmsProvider.BrowseInRootFolderName)
        For Each MyItem As DmsResourceItem In Items
            System.Console.WriteLine(MyItem)
        Next
        If DmsProvider.SupportsCollections = False AndAlso Me.RemoteCollectionsMustExist.Length <> 0 Then
            Throw New NotSupportedException("Provider doesn't support collections, but " & NameOf(RemoteCollectionsMustExist) & " contains elements")
        End If
        If Me.RemoteCollectionsMustExist.Length = 0 Then
            Assert.Zero(Items.Count)
        Else
            Assert.NotZero(Items.Count)
        End If
    End Sub

    <Test> Public Overridable Sub ListAllFileItems()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        Dim Items As List(Of DmsResourceItem)

        Items = DmsProvider.ListAllFileItems(DmsProvider.BrowseInRootFolderName)
        For Each MyItem As DmsResourceItem In Items
            System.Console.WriteLine(MyItem)
            Assert.AreEqual("", MyItem.Collection)
            Assert.AreEqual("", MyItem.Folder)
            Assert.AreEqual(DmsResourceItem.ItemTypes.File, MyItem.ItemType)
        Next
        If DmsProvider.SupportsFilesInRootFolder Then
            Assert.NotZero(Items.Count)
        Else
            Assert.Zero(Items.Count)
        End If
        System.Console.WriteLine("---")

        For Each RemoteFolder As String In Me.RemoteFoldersWithFiles
            Items = DmsProvider.ListAllFileItems(RemoteFolder)
            For Each MyItem As DmsResourceItem In Items
                System.Console.WriteLine(MyItem)
                Assert.AreEqual("", MyItem.Collection)
                Assert.AreEqual(Me.FolderNameWithoutTrailingDirectorySeparator(DmsProvider, RemoteFolder), MyItem.Folder)
                Assert.AreEqual(DmsResourceItem.ItemTypes.File, MyItem.ItemType)
            Next
            System.Console.WriteLine("---")
            Assert.NotZero(Items.Count)
        Next
    End Sub

    Protected Function FolderNameWithoutTrailingDirectorySeparator(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, folderName As String) As String
        If folderName.EndsWith(dmsProvider.DirectorySeparator) Then
            Return folderName.Substring(0, folderName.Length - 1)
        Else
            Return folderName
        End If
    End Function

    <Test> Public Overridable Sub ListAllRemoteItems()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        Dim Items As List(Of DmsResourceItem)

        Items = DmsProvider.ListAllRemoteItems(DmsProvider.BrowseInRootFolderName, BaseDmsProvider.SearchItemType.AllItems)
        For Each MyItem As DmsResourceItem In Items
            System.Console.WriteLine(MyItem.ToString & " [ETag/ProvderSpecificHash: " & MyItem.ProviderSpecificHashOrETag & "] [Last Modification: " & MyItem.LastModificationOnLocalTime.GetValueOrDefault.ToString("yyyy-MM-dd HH:mm:ss") & "]")
            Assert.AreEqual("", MyItem.Folder)
        Next
        Assert.NotZero(Items.Count)
        System.Console.WriteLine("---")

        For Each RemoteFolder As String In Me.RemoteFoldersMustExist
            Items = DmsProvider.ListAllRemoteItems(RemoteFolder, BaseDmsProvider.SearchItemType.AllItems)
            For Each MyItem As DmsResourceItem In Items
                System.Console.WriteLine(MyItem.ToString & " [ETag/ProvderSpecificHash: " & MyItem.ProviderSpecificHashOrETag & "] [Last Modification: " & MyItem.LastModificationOnLocalTime.GetValueOrDefault.ToString("yyyy-MM-dd HH:mm:ss") & "]")
                Assert.AreEqual(Me.FolderNameWithoutTrailingDirectorySeparator(DmsProvider, RemoteFolder), IIf(MyItem.Folder <> Nothing, MyItem.Folder, MyItem.Collection))
                Assert.IsNotEmpty(MyItem.Name)
            Next
            System.Console.WriteLine("---")
        Next
    End Sub

    <Test> Public Overridable Sub ListRemoteItem()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        Dim Item As DmsResourceItem

        Assert.Catch(Of System.ArgumentNullException)(Sub()
                                                          DmsProvider.ListRemoteItem(Nothing)
                                                      End Sub)
        Item = DmsProvider.ListRemoteItem(DmsProvider.BrowseInRootFolderName)
        System.Console.WriteLine(Item)
        Assert.NotNull(Item)
        Assert.AreEqual(DmsResourceItem.ItemTypes.Root, Item.ItemType)
        Assert.AreEqual("", Item.Folder)
        Assert.AreEqual("", Item.Name)

        Item = DmsProvider.ListRemoteItem(DmsProvider.DirectorySeparator)
        System.Console.WriteLine(Item)
        Assert.NotNull(Item)
        Assert.AreEqual(DmsResourceItem.ItemTypes.Root, Item.ItemType)
        Assert.AreEqual("", Item.Folder)
        Assert.AreEqual("", Item.Name)

        Item = DmsProvider.ListRemoteItem("")
        System.Console.WriteLine(Item)
        Assert.NotNull(Item)
        Assert.AreEqual(DmsResourceItem.ItemTypes.Root, Item.ItemType)
        Assert.AreEqual("", Item.Folder)
        Assert.AreEqual("", Item.Name)

        For Each RemoteFolder As String In Me.RemoteFoldersMustExist
            Item = DmsProvider.ListRemoteItem(RemoteFolder)
            System.Console.WriteLine(Item)
            Assert.NotNull(Item)
            Assert.AreEqual(True, Item.ItemType = DmsResourceItem.ItemTypes.Folder OrElse Item.ItemType = DmsResourceItem.ItemTypes.Collection)
            Assert.AreEqual(Me.RemoteTestItemFolderName(DmsProvider, Me.FolderNameWithoutTrailingDirectorySeparator(DmsProvider, RemoteFolder)), IIf(Item.Folder <> Nothing, Item.Folder, Item.Collection))
            Assert.AreEqual(Me.RemoteTestItemFileName(DmsProvider, Me.FolderNameWithoutTrailingDirectorySeparator(DmsProvider, RemoteFolder)), Item.Name)
            System.Console.WriteLine("---")
        Next

        For Each RemoteFile As String In Me.RemoteFilesMustExist
            Item = DmsProvider.ListRemoteItem(RemoteFile)
            System.Console.WriteLine(Item)
            Assert.NotNull(Item)
            Assert.AreEqual(DmsResourceItem.ItemTypes.File, Item.ItemType)
            Assert.AreEqual(Me.RemoteTestItemFolderName(DmsProvider, Me.FolderNameWithoutTrailingDirectorySeparator(DmsProvider, RemoteFile)), IIf(Item.Folder <> Nothing, Item.Folder, Item.Collection))
            Assert.AreEqual(Me.RemoteTestItemFileName(DmsProvider, Me.FolderNameWithoutTrailingDirectorySeparator(DmsProvider, RemoteFile)), Item.Name)
            System.Console.WriteLine("---")
        Next

        Item = DmsProvider.ListRemoteItem("not-found")
        System.Console.WriteLine(Item)
        Assert.Null(Item)
        For Each RemoteItem As String In Me.RemoteItemsMustNotExist
            Item = DmsProvider.ListRemoteItem(RemoteItem)
            System.Console.WriteLine(Item)
            Assert.Null(Item)
        Next

    End Sub

#Disable Warning CA1819 ' Properties should not return arrays
#Disable Warning CA1825 ' Avoid zero-length array allocations.
    Public Overridable ReadOnly Property RemoteTestFolderName As String = "ZZZ_UnitTests_Dms_TestFolder"
    Public Overridable ReadOnly Property RemoteTestCollectionName As String = "ZZZ_UnitTests_Dms_TestCollection"
    Public Overridable ReadOnly Property RemoteFilesMustExist As String() = New String() {}
    Public Overridable ReadOnly Property RemoteFoldersMustExist As String() = New String() {}
    Public Overridable ReadOnly Property RemoteCollectionsMustExist As String() = New String() {}
    Public Overridable ReadOnly Property RemoteItemsMustNotExist As String() = New String() {"/not-existing-test-item"}
    Public Overridable ReadOnly Property RemoteFoldersWithFiles As String() = New String() {}
    Public Overridable ReadOnly Property RemoteFoldersWithSubFolders As String() = New String() {}
    Public Overridable ReadOnly Property DownloadTestFilesText As KeyValuePair(Of String, String)() = New KeyValuePair(Of String, String)() {}
    Public Overridable ReadOnly Property DownloadTestFilesBinary As KeyValuePair(Of String, Byte())() = New KeyValuePair(Of String, Byte())() {}
    Public Overridable ReadOnly Property UploadTestFilesAndCleanupAgainFilePath As KeyValuePair(Of String, String)() = New KeyValuePair(Of String, String)() {}
    Public Overridable ReadOnly Property UploadTestFilesAndCleanupAgainBinary As KeyValuePair(Of String, Byte())() = New KeyValuePair(Of String, Byte())() {
            New KeyValuePair(Of String, Byte())("upload-test-file.tmp", New Byte() {0, 255, 68, 46, 64, 87, 92})
        }

    Public Overridable ReadOnly Property CopyFileTestFileSource As New KeyValuePair(Of String, Byte())("copy-source-test-file.tmp", New Byte() {0, 255, 68, 46, 64, 87, 92})
    Public Overridable ReadOnly Property CopyFileTestFileTargets As String() = New String() {"copy-target-test-file.tmp", "copy-sub-target/copy-target-test-file.tmp"}
    Public Overridable ReadOnly Property CopyDirTestFileSource As New KeyValuePair(Of String, Byte())("sub/sub2/sub3/copy-source-test-file.tmp", New Byte() {0, 255, 68, 46, 64, 87, 92})
    Public Overridable ReadOnly Property CopyDirTestDirSource As String() = New String() {
        "sub",
        "sub",
        "sub/sub2",
        "sub/sub2/"
    }
    Public Overridable ReadOnly Property CopyDirTestDirTarget As String() = New String() {
        "sub-copy1",
        "sub-copy2/",
        "sub-copy3",
        "sub-copy4"
    }
    Public Overridable ReadOnly Property CopyDirTestExpectedTargetFile As String() = New String() {
        "sub-copy1/sub2/sub3/copy-source-test-file.tmp",
        "{NOT-SUPPORTED:System.ArgumentException}",
        "sub-copy3/sub3/copy-source-test-file.tmp",
        "sub-copy4/sub3/copy-source-test-file.tmp"
    }
    Public Overridable ReadOnly Property MoveFileTestFileSource As New KeyValuePair(Of String, Byte())("move-source-test-file.tmp", New Byte() {0, 255, 68, 46, 64, 87, 92})
    Public Overridable ReadOnly Property MoveFileTestFileTargets As String() = New String() {"move-target-test-file.tmp", "move-sub-target/move-target-test-file.tmp"}
    Public Overridable ReadOnly Property MoveDirTestFileSource As New KeyValuePair(Of String, Byte())("sub/sub2/sub3/move-source-test-file.tmp", New Byte() {0, 255, 68, 46, 64, 87, 92})
    Public Overridable ReadOnly Property MoveDirTestDirSource As String() = New String() {
        "sub",
        "sub",
        "sub/sub2",
        "sub/sub2/"
    }
    Public Overridable ReadOnly Property MoveDirTestDirTarget As String() = New String() {
        "sub-move1",
        "sub-move2/",
        "sub-move3",
        "sub-move4"
    }
    Public Overridable ReadOnly Property MoveDirTestExpectedTargetFile As String() = New String() {
        "sub-move1/sub2/sub3/move-source-test-file.tmp",
        "{NOT-SUPPORTED:System.ArgumentException}",
        "sub-move3/sub3/move-source-test-file.tmp",
        "sub-move4/sub3/move-source-test-file.tmp"
    }

    <Test> Public Sub CorrectTestConfigOverrides()
        'Copy property
        Assert.AreEqual(CopyDirTestDirSource.Length, CopyDirTestDirTarget.Length)
        Assert.AreEqual(CopyDirTestDirSource.Length, CopyDirTestExpectedTargetFile.Length)
        For Each SourceDir As String In Me.CopyDirTestDirSource
            Assert.True(CopyDirTestFileSource.Key.StartsWith(SourceDir))
        Next
        'Move property
        Assert.AreEqual(MoveDirTestDirSource.Length, MoveDirTestDirTarget.Length)
        Assert.AreEqual(MoveDirTestDirSource.Length, MoveDirTestExpectedTargetFile.Length)
        For Each SourceDir As String In Me.MoveDirTestDirSource
            Assert.True(MoveDirTestFileSource.Key.StartsWith(SourceDir))
        Next
    End Sub

#Enable Warning CA1825 ' Avoid zero-length array allocations.
#Enable Warning CA1819 ' Properties should not return arrays

    Protected Function RemoteTestItemFolderName(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, remoteItem As String) As String
        If remoteItem.Contains(dmsProvider.DirectorySeparator) Then
            Return remoteItem.Substring(0, remoteItem.LastIndexOf(dmsProvider.DirectorySeparator))
        Else
            Return ""
        End If
    End Function

    Protected Function RemoteTestItemFileName(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, remoteItem As String) As String
        If remoteItem.Contains(dmsProvider.DirectorySeparator) Then
            Return remoteItem.Substring(remoteItem.LastIndexOf(dmsProvider.DirectorySeparator) + 1)
        Else
            Return remoteItem
        End If
    End Function

    <Test> Public Sub CombinePath()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.UninitializedDmsProvider
        Assert.AreEqual("folder1/folder2/folder3/folder4", DmsProvider.CombinePath("folder1/folder2", "folder3/folder4"))
        Assert.AreEqual("folder1/folder2/folder3/folder4", DmsProvider.CombinePath("folder1", "folder2", "folder3", "folder4"))
        Assert.AreEqual("folder3/folder4", DmsProvider.CombinePath("folder1/folder2", "/folder3/folder4"))
        Assert.AreEqual("folder3/folder4", DmsProvider.CombinePath("", "/folder3/folder4"))
        Assert.AreEqual("folder3/folder4", DmsProvider.CombinePath("", "folder3/folder4"))
        Assert.AreEqual("", DmsProvider.CombinePath("folder1/folder2", "/"))
        Assert.AreEqual("", DmsProvider.CombinePath("", "/"))
        Assert.AreEqual("", DmsProvider.CombinePath("", ""))
    End Sub

    <Test> Public Sub ParentDirectoryPath()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.UninitializedDmsProvider
        Assert.AreEqual("folder1/folder2/folder3/folder4", DmsProvider.ParentDirectoryPath("folder1/folder2/folder3/folder4/folder5"))
        Assert.AreEqual("/folder1/folder2/folder3/folder4", DmsProvider.ParentDirectoryPath("/folder1/folder2/folder3/folder4/folder5"))
        Assert.AreEqual("", DmsProvider.ParentDirectoryPath("/folder1/"))
        Assert.AreEqual("", DmsProvider.ParentDirectoryPath("/file1"))
        Assert.AreEqual("", DmsProvider.ParentDirectoryPath("file1"))
        Assert.AreEqual(Nothing, DmsProvider.ParentDirectoryPath("/"))
        Assert.AreEqual(Nothing, DmsProvider.ParentDirectoryPath(""))
    End Sub

    <Test> Public Sub ItemName()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.UninitializedDmsProvider
        Assert.AreEqual("folder4", DmsProvider.ItemName("folder1/folder2/folder3/folder4"))
        Assert.AreEqual("folder4", DmsProvider.ItemName("/folder1/folder2/folder3/folder4"))
        Assert.AreEqual("folder1", DmsProvider.ItemName("/folder1/"))
        Assert.AreEqual("file1", DmsProvider.ItemName("/file1"))
        Assert.AreEqual("file1", DmsProvider.ItemName("file1"))
        Assert.AreEqual("", DmsProvider.ItemName("/"))
        Assert.AreEqual("", DmsProvider.ItemName(""))
        Assert.Catch(Of ArgumentException)(Sub()
                                               DmsProvider.ItemName(Nothing)
                                           End Sub)
    End Sub

    Protected OriginalSslValidateCallback As System.Net.Security.RemoteCertificateValidationCallback = System.Net.ServicePointManager.ServerCertificateValidationCallback

    Protected Function AcceptAllCertifications(ByVal sender As Object, ByVal certification As System.Security.Cryptography.X509Certificates.X509Certificate, ByVal chain As System.Security.Cryptography.X509Certificates.X509Chain, ByVal sslPolicyErrors As System.Net.Security.SslPolicyErrors) As Boolean
        Return True
    End Function

    Private Shared Sub CreateRemoteTestCollectionOrFolderIfNotExisting(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, remotePath As String)
        Dim Item As DmsResourceItem

        'Lookup status (and fill caches)
        Item = dmsProvider.ListRemoteItem(remotePath)

        'Create the folder
        If dmsProvider.RemoteItemExists(remotePath) = False Then
            If dmsProvider.SupportsCollections Then
                dmsProvider.CreateCollection(remotePath)
            Else
                dmsProvider.CreateFolder(remotePath)
            End If
        End If

        'Lookup status again and check that old caches don't exist
        Item = dmsProvider.ListRemoteItem(remotePath)
        Assert.IsNotNull(Item)
        Assert.IsTrue(dmsProvider.RemoteItemExists(remotePath))
    End Sub

    Private Shared Sub CreateRemoteTestFolderIfNotExisting(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, remotePath As String)
        Dim Item As DmsResourceItem

        'Lookup status (and fill caches)
        Item = dmsProvider.ListRemoteItem(remotePath)

        'Create the folder
        If dmsProvider.RemoteItemExists(remotePath) = False Then
            dmsProvider.CreateFolder(remotePath)
        End If

        'Lookup status again and check that old caches don't exist
        Item = dmsProvider.ListRemoteItem(remotePath)
        Assert.IsNotNull(Item)
        Assert.IsTrue(dmsProvider.RemoteItemExists(remotePath))
    End Sub

    Private Shared Sub RemoveRemoteTestCollectionOrFolder(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, remotePath As String)
        Dim Item As DmsResourceItem

        'Lookup status (and fill caches)
        Item = dmsProvider.ListRemoteItem(remotePath)
        Assert.IsNotNull(Item)
        Assert.IsTrue(dmsProvider.RemoteItemExists(remotePath))

        'Delete the folder
        dmsProvider.DeleteRemoteItem(remotePath, DmsResourceItem.ItemTypes.Collection, DmsResourceItem.ItemTypes.Folder)

        'Lookup status again and check that old caches don't exist
        Item = dmsProvider.ListRemoteItem(remotePath)
        Assert.IsNull(Item)
        Assert.IsFalse(dmsProvider.RemoteItemExists(remotePath))

    End Sub

    Protected Sub RemoveRemoteItemIfItExists(dmsProvider As BaseDmsProvider, remotePath As String, expectedRemoteItemType As DmsResourceItem.ItemTypes, preCheckItemExistance As TriState)
        Me.RemoveRemoteItemIfItExists(dmsProvider, remotePath, expectedRemoteItemType, Nothing, preCheckItemExistance)
    End Sub

    Protected Overridable Sub RemoveRemoteItemIfItExists(dmsProvider As BaseDmsProvider, remotePath As String, expectedRemoteItemType As DmsResourceItem.ItemTypes, alternativeExpectedRemoteItemType As DmsResourceItem.ItemTypes, preCheckItemExistance As TriState)
        Dim RemoteItemExists As Boolean
        RemoteItemExists = dmsProvider.RemoteItemExists(remotePath)
        System.Console.WriteLine("CHECK: file " & remotePath & " exists: " & RemoteItemExists)
        If preCheckItemExistance = TriState.True Then
            Assert.IsTrue(RemoteItemExists)
        ElseIf preCheckItemExistance = TriState.False Then
            Assert.IsFalse(RemoteItemExists)
        End If
        If RemoteItemExists Then
            If expectedRemoteItemType = Nothing Then
                dmsProvider.DeleteRemoteItem(remotePath)
            ElseIf alternativeExpectedRemoteItemType = Nothing Then
                dmsProvider.DeleteRemoteItem(remotePath, expectedRemoteItemType)
            Else
                dmsProvider.DeleteRemoteItem(remotePath, expectedRemoteItemType, alternativeExpectedRemoteItemType)
            End If
            System.Console.WriteLine("DELETED: " & remotePath)
            RemoteItemExists = dmsProvider.RemoteItemExists(remotePath)
            System.Console.WriteLine("CHECK: file " & remotePath & " exists: " & RemoteItemExists)
        Else
            System.Console.WriteLine("DELETION NOT REQUIRED")
        End If
        Assert.IsFalse(RemoteItemExists)
    End Sub

    <Test> Public Sub CreateCollectionOrFolderAndCleanup()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider

        'Test RemoteTestFolderName as is
        Assert.IsFalse(Me.RemoteTestFolderName.StartsWith(DmsProvider.DirectorySeparator)) 'ensure RemoteTestFolderName is without leading "/"
        If DmsProvider.SupportsCollections Then
            System.Console.WriteLine("Remote test collection name: " & Me.RemoteTestFolderName)
            CreateRemoteTestCollectionOrFolderIfNotExisting(DmsProvider, Me.RemoteTestFolderName)
            Assert.IsTrue(DmsProvider.CollectionExists(Me.RemoteTestFolderName))
            RemoveRemoteTestCollectionOrFolder(DmsProvider, Me.RemoteTestFolderName)
            Assert.IsFalse(DmsProvider.CollectionExists(Me.RemoteTestFolderName))
        Else
            System.Console.WriteLine("Remote test folder name: " & Me.RemoteTestFolderName)
            CreateRemoteTestCollectionOrFolderIfNotExisting(DmsProvider, Me.RemoteTestFolderName)
            Assert.IsTrue(DmsProvider.FolderExists(Me.RemoteTestFolderName))
            RemoveRemoteTestCollectionOrFolder(DmsProvider, Me.RemoteTestFolderName)
            Assert.IsFalse(DmsProvider.FolderExists(Me.RemoteTestFolderName))
        End If

        'Test RemoteTestFolderName with leading "/"
        Dim RemoteTestFolderNameWithLeadingDirectorySeparator As String = DmsProvider.CombinePath(DmsProvider.DirectorySeparator, Me.RemoteTestFolderName)
        If DmsProvider.SupportsCollections Then
            System.Console.WriteLine("Remote test collection name: " & RemoteTestFolderNameWithLeadingDirectorySeparator)
            CreateRemoteTestCollectionOrFolderIfNotExisting(DmsProvider, RemoteTestFolderNameWithLeadingDirectorySeparator)
            Assert.IsTrue(DmsProvider.CollectionExists(RemoteTestFolderNameWithLeadingDirectorySeparator))
            RemoveRemoteTestCollectionOrFolder(DmsProvider, RemoteTestFolderNameWithLeadingDirectorySeparator)
            Assert.IsFalse(DmsProvider.CollectionExists(RemoteTestFolderNameWithLeadingDirectorySeparator))
        Else
            System.Console.WriteLine("Remote test folder name: " & RemoteTestFolderNameWithLeadingDirectorySeparator)
            CreateRemoteTestCollectionOrFolderIfNotExisting(DmsProvider, RemoteTestFolderNameWithLeadingDirectorySeparator)
            Assert.IsTrue(DmsProvider.FolderExists(RemoteTestFolderNameWithLeadingDirectorySeparator))
            RemoveRemoteTestCollectionOrFolder(DmsProvider, RemoteTestFolderNameWithLeadingDirectorySeparator)
            Assert.IsFalse(DmsProvider.FolderExists(RemoteTestFolderNameWithLeadingDirectorySeparator))
        End If
    End Sub

    Protected Overridable Sub CreateRemoteCollection(dmsProvider As BaseDmsProvider, remoteCollectionName As String)
        Try
            dmsProvider.CreateCollection(remoteCollectionName)
            Assert.IsTrue(dmsProvider.RemoteItemExists(remoteCollectionName))
        Catch ex As NotSupportedException
            'OK: e.g. WebDAV doesn't support collections
#Disable Warning CA1031 ' Do not catch general exception types
        Catch ex As Exception
            Assert.Fail(ex.Message)
#Enable Warning CA1031 ' Do not catch general exception types
        End Try
    End Sub

    Protected Overridable Sub CreateRemoteFolder(dmsProvider As BaseDmsProvider, remoteFolderName As String)
        dmsProvider.CreateCollection(remoteFolderName)
        Assert.IsTrue(dmsProvider.RemoteItemExists(remoteFolderName))
    End Sub

    ''' <summary>
    ''' Test creation of provider-decided directory type of collection or folder
    ''' </summary>
    <Test> Public Sub CreateRemoteDirectoryAndCleanup()
        Dim DmsProvider As BaseDmsProvider = Me.LoggedInDmsProvider
        Dim RemotePath As String = Me.RemoteTestFolderName
        Me.RemoveRemoteItemIfItExists(DmsProvider, RemotePath, Nothing, TriState.UseDefault)
        DmsProvider.CreateDirectory(RemotePath)
        Dim FoundFirstDirLevelType = DmsProvider.RemoteItemExistsUniquelyAs(RemotePath)
        Select Case FoundFirstDirLevelType
            Case DmsResourceItem.FoundItemResult.Collection
                Assert.AreEqual(DmsResourceItem.FoundItemResult.Collection, FoundFirstDirLevelType)
            Case Else
                Assert.AreEqual(DmsResourceItem.FoundItemResult.Folder, FoundFirstDirLevelType)
        End Select

        Dim DeepFolderStructure As String = DmsProvider.CombinePath(RemotePath, "with", "a", "deep", "folder", "structure")
        Assert.Catch(Of DirectoryNotFoundException)(Sub()
                                                        DmsProvider.CreateDirectory(DeepFolderStructure, False)
                                                    End Sub)

        DmsProvider.CreateFolder(DeepFolderStructure, True)
        Assert.AreEqual(DmsResourceItem.FoundItemResult.Folder, DmsProvider.RemoteItemExistsUniquelyAs(DeepFolderStructure))
        Dim FirstDirType = DmsProvider.RemoteItemExistsUniquelyAs(RemotePath)
        Select Case FirstDirType
            Case DmsResourceItem.FoundItemResult.Collection
                Assert.AreEqual(DmsResourceItem.FoundItemResult.Collection, FirstDirType)
            Case Else
                Assert.AreEqual(DmsResourceItem.FoundItemResult.Folder, FirstDirType)
        End Select

        Me.RemoveRemoteItemIfItExists(DmsProvider, RemotePath, DmsResourceItem.ItemTypes.Folder, DmsResourceItem.ItemTypes.Collection, TriState.True)
    End Sub

    <Test> Public Sub CreateRemoteFolderAndCleanup()
        Dim DmsProvider As BaseDmsProvider = Me.LoggedInDmsProvider
        Dim RemotePath As String = Me.RemoteTestFolderName
        Me.RemoveRemoteItemIfItExists(DmsProvider, RemotePath, Nothing, TriState.UseDefault)

        'Initial step: create test directory
        DmsProvider.CreateDirectory(RemotePath) 'NOTE: CreateFolder will fail at CenterDevice because folders are not supported as 1st level of directories
        Dim FoundFirstDirLevelType = DmsProvider.RemoteItemExistsUniquelyAs(RemotePath)
        Select Case FoundFirstDirLevelType
            Case DmsResourceItem.FoundItemResult.Collection
                Assert.AreEqual(DmsResourceItem.FoundItemResult.Collection, FoundFirstDirLevelType)
            Case Else
                Assert.AreEqual(DmsResourceItem.FoundItemResult.Folder, FoundFirstDirLevelType)
        End Select

        Dim DeepFolderStructure As String = DmsProvider.CombinePath(RemotePath, "with", "a", "deep", "folder", "structure")
        Assert.Catch(Of DirectoryNotFoundException)(Sub()
                                                        DmsProvider.CreateFolder(DeepFolderStructure, False)
                                                    End Sub)

        DmsProvider.CreateFolder(DeepFolderStructure, True)
        Assert.AreEqual(DmsResourceItem.FoundItemResult.Folder, DmsProvider.RemoteItemExistsUniquelyAs(DeepFolderStructure))
        FoundFirstDirLevelType = DmsProvider.RemoteItemExistsUniquelyAs(RemotePath)
        Select Case FoundFirstDirLevelType
            Case DmsResourceItem.FoundItemResult.Collection
                Assert.AreEqual(DmsResourceItem.FoundItemResult.Collection, FoundFirstDirLevelType)
            Case Else
                Assert.AreEqual(DmsResourceItem.FoundItemResult.Folder, FoundFirstDirLevelType)
        End Select

        Me.RemoveRemoteItemIfItExists(DmsProvider, RemotePath, DmsResourceItem.ItemTypes.Folder, DmsResourceItem.ItemTypes.Collection, TriState.True)
    End Sub

    <Test> Public Sub CreateRemoteCollectionAndCleanup()
        Dim DmsProvider As BaseDmsProvider = Me.LoggedInDmsProvider
        Dim RemotePath As String = Me.RemoteTestCollectionName
        If DmsProvider.SupportsCollections Then
            'CenterDevice / Scopevisio Teamwork
            Me.RemoveRemoteItemIfItExists(DmsProvider, RemotePath, Nothing, TriState.UseDefault)

            '1st step: create collection as it must work
            DmsProvider.CreateCollection(RemotePath)
            Assert.AreEqual(DmsResourceItem.FoundItemResult.Collection, DmsProvider.RemoteItemExistsUniquelyAs(RemotePath))
            Me.RemoveRemoteItemIfItExists(DmsProvider, RemotePath, DmsResourceItem.ItemTypes.Collection, TriState.True)

            '2nd step: create a remote collection with multiple sub directories that is expected to fail because parent directory doesn't exist
            If DmsProvider.SupportsCollections Then
                Assert.Catch(Of NotSupportedException)(Sub()
                                                           DmsProvider.CreateCollection(DmsProvider.CombinePath(RemotePath, "with", "a", "deep", "folder", "structure"))
                                                       End Sub)
            Else
                Assert.Catch(Of CompuMaster.Dms.Data.DirectoryNotFoundException)(Sub()
                                                                                     DmsProvider.CreateCollection(DmsProvider.CombinePath(RemotePath, "with", "a", "deep", "folder", "structure"))
                                                                                 End Sub)
            End If

            '3nd step: create a remote collection with multiple sub directories that is expected to fail because collections are supported at very first directory level, only
            DmsProvider.CreateCollection(RemotePath)
            Assert.AreEqual(DmsResourceItem.FoundItemResult.Collection, DmsProvider.RemoteItemExistsUniquelyAs(RemotePath))

            Assert.Catch(Of NotSupportedException)(Sub()
                                                       DmsProvider.CreateCollection(DmsProvider.CombinePath(RemotePath, "sub-collection"))
                                                   End Sub)
        Else
            'WebDAV
            Assert.Catch(Of NotSupportedException)(Sub()
                                                       DmsProvider.CreateCollection(RemotePath)
                                                   End Sub)
        End If

        'Cleanup
        Me.CleanupRemoteDirectory(DmsProvider, RemotePath)
    End Sub

    <Test> Public Sub UploadFilesAndCleanup()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider

        If Me.UploadTestFilesAndCleanupAgainFilePath.Length > 0 OrElse Me.UploadTestFilesAndCleanupAgainBinary.Length > 0 Then

            CreateRemoteTestCollectionOrFolderIfNotExisting(DmsProvider, Me.RemoteTestFolderName)

            If DmsProvider.SupportsCollections Then
                Assert.IsTrue(DmsProvider.CollectionExists(Me.RemoteTestFolderName))
            Else
                Assert.IsTrue(DmsProvider.FolderExists(Me.RemoteTestFolderName))
            End If

            For Each upload In Me.UploadTestFilesAndCleanupAgainFilePath
                Dim LocalPathAbsolute As String = TestFileForUploadTests(upload.Value)
                UploadFilesAndCleanup(DmsProvider, LocalPathAbsolute, upload.Key)
            Next

            For Each upload In Me.UploadTestFilesAndCleanupAgainBinary
                UploadFilesAndCleanup(DmsProvider, upload.Value, upload.Key)
            Next

            If DmsProvider.SupportsCollections Then
                Assert.IsTrue(DmsProvider.CollectionExists(Me.RemoteTestFolderName))
            Else
                Assert.IsTrue(DmsProvider.FolderExists(Me.RemoteTestFolderName))
            End If

            RemoveRemoteTestCollectionOrFolder(DmsProvider, Me.RemoteTestFolderName)

        End If

    End Sub

    Private Sub UploadFilesAndCleanup(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, binaryData As Byte(), remoteFileNameInTestFolder As String)
        If Me.RemoteTestFolderName = Nothing Then Throw New NotSupportedException(Me.RemoteTestFolderName)

        Dim RemoteFilePath As String = dmsProvider.CombinePath(Me.RemoteTestFolderName, remoteFileNameInTestFolder)

        AssertRemoteFileNotExists(dmsProvider, RemoteFilePath, True)

        'Upload 1st time
        dmsProvider.UploadFile(RemoteFilePath, binaryData)
        AssertRemoteFileExists(dmsProvider, RemoteFilePath)

        'Upload 2nd time -> create new version instead of new file
        dmsProvider.UploadFile(RemoteFilePath, binaryData)
        AssertRemoteFileExists(dmsProvider, RemoteFilePath)

        'Cleanup
        Me.CleanupRemoteFile(dmsProvider, RemoteFilePath)
    End Sub

    Private Sub UploadFilesAndCleanup(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, localFilePathAbsolute As String, remoteFileNameInTestFolder As String)
        If Me.RemoteTestFolderName = Nothing Then Throw New NotSupportedException(Me.RemoteTestFolderName)

        Dim RemoteFilePath As String = dmsProvider.CombinePath(Me.RemoteTestFolderName, remoteFileNameInTestFolder)

        AssertRemoteFileNotExists(dmsProvider, RemoteFilePath, True)

        'Upload 1st time
        dmsProvider.UploadFile(RemoteFilePath, localFilePathAbsolute)
        AssertRemoteFileExists(dmsProvider, RemoteFilePath)

        'Upload 2nd time -> create new version instead of new file
        dmsProvider.UploadFile(RemoteFilePath, localFilePathAbsolute)
        AssertRemoteFileExists(dmsProvider, RemoteFilePath)

        'Cleanup
        Me.CleanupRemoteFile(dmsProvider, RemoteFilePath)
    End Sub

    Protected Function TestAssembly() As System.Reflection.Assembly
        Return System.Reflection.Assembly.GetExecutingAssembly
    End Function

    Protected Function TestFileForUploadTests(testFileName As String) As String
        Dim LocalFilePath As String = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Me.TestAssembly.Location), testFileName)
        If System.IO.File.Exists(LocalFilePath) = False Then Throw New System.IO.FileNotFoundException("File not found: " & LocalFilePath)
        Return LocalFilePath
    End Function

    Protected Enum DirectoryTypes As Byte
        Folder = 0
        Collection = 1
    End Enum

    Protected Sub CreateRemoteTestFolderIfNotExisting(remotePath As String, directoryType As DirectoryTypes)
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        Dim RemoteItem As Dms.Data.DmsResourceItem

        'Lookup status (and fill caches) + create directory
        RemoteItem = DmsProvider.ListRemoteItem(remotePath)
        Select Case directoryType
            Case DirectoryTypes.Collection
                If DmsProvider.CollectionExists(remotePath) = False Then
                    'Create the collection
                    DmsProvider.CreateCollection(remotePath)
                End If
            Case DirectoryTypes.Folder
                If DmsProvider.FolderExists(remotePath) = False Then
                    'Create the folder
                    DmsProvider.CreateFolder(remotePath)
                End If
            Case Else
                Throw New NotImplementedException
        End Select


        'Lookup status again and check that old caches don't exist
        Select Case directoryType
            Case DirectoryTypes.Collection
                Assert.IsTrue(DmsProvider.CollectionExists(remotePath))
                RemoteItem = DmsProvider.ListRemoteItem(remotePath)
                Assert.AreEqual(Dms.Data.DmsResourceItem.ItemTypes.Collection, RemoteItem.ItemType)
            Case DirectoryTypes.Folder
                Assert.IsTrue(DmsProvider.FolderExists(remotePath))
                RemoteItem = DmsProvider.ListRemoteItem(remotePath)
                Assert.AreEqual(Dms.Data.DmsResourceItem.ItemTypes.Folder, RemoteItem.ItemType)
            Case Else
                Throw New NotImplementedException
        End Select
    End Sub

    Protected Sub RemoveRemoteTestFolder(remotePath As String, directoryType As DirectoryTypes, remoteItemMustExist As Boolean)
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        Dim RemoteItem As Dms.Data.DmsResourceItem

        'Lookup status (and fill caches)
        If remoteItemMustExist Then
            Select Case directoryType
                Case DirectoryTypes.Collection
                    Assert.IsTrue(DmsProvider.CollectionExists(remotePath))
                    RemoteItem = DmsProvider.ListRemoteItem(remotePath)
                    Assert.AreEqual(Dms.Data.DmsResourceItem.ItemTypes.Collection, RemoteItem.ItemType)
                Case DirectoryTypes.Folder
                    Assert.IsTrue(DmsProvider.FolderExists(remotePath))
                    RemoteItem = DmsProvider.ListRemoteItem(remotePath)
                    Assert.AreEqual(Dms.Data.DmsResourceItem.ItemTypes.Folder, RemoteItem.ItemType)
                Case Else
                    Throw New NotImplementedException
            End Select
        Else
            Select Case directoryType
                Case DirectoryTypes.Collection
                    DmsProvider.CollectionExists(remotePath)
                Case DirectoryTypes.Folder
                    DmsProvider.FolderExists(remotePath)
                Case Else
                    Throw New NotImplementedException
            End Select
            DmsProvider.ListRemoteItem(remotePath)
        End If

        'Lookup status (and fill caches)
        Select Case directoryType
            Case DirectoryTypes.Collection
                If DmsProvider.CollectionExists(remotePath) Then
                    'Delete the collection
                    DmsProvider.DeleteRemoteItem(remotePath)
                End If
            Case DirectoryTypes.Folder
                If DmsProvider.FolderExists(remotePath) Then
                    'Delete the folder
                    DmsProvider.DeleteRemoteItem(remotePath)
                End If
            Case Else
                Throw New NotImplementedException
        End Select

        'Lookup status again and check that old caches don't exist
        Select Case directoryType
            Case DirectoryTypes.Collection
                Assert.IsFalse(DmsProvider.CollectionExists(remotePath))
                RemoteItem = DmsProvider.ListRemoteItem(remotePath)
                Assert.IsNull(RemoteItem)
            Case DirectoryTypes.Folder
                Assert.IsFalse(DmsProvider.FolderExists(remotePath))
                RemoteItem = DmsProvider.ListRemoteItem(remotePath)
                Assert.IsNull(RemoteItem)
            Case Else
                Throw New NotImplementedException
        End Select

    End Sub

    <Test> Public Sub GetListOfGroups()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        If DmsProvider.SupportsSharingSetup = False Then Assert.Ignore("Sharing setup not supported by provider")

        Dim FoundGroupList As List(Of DmsGroup) = DmsProvider.GetAllGroups()
        Assert.NotNull(FoundGroupList)
        Assert.Greater(FoundGroupList.Count, 0)
    End Sub

    <Test> Public Sub GetListOfUsers()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        If DmsProvider.SupportsSharingSetup = False Then Assert.Ignore("Sharing setup not supported by provider")

        Dim FoundUserList As List(Of DmsUser) = DmsProvider.GetAllUsers()
        Assert.NotNull(FoundUserList)
        Assert.Greater(FoundUserList.Count, 0)
    End Sub

    ''' <summary>
    ''' Test for whole procedure of creating directory + creating link share + update link share + remove share and directory
    ''' </summary>
    <Test> Public Sub CreateUploadLinkForCollectionAndCleanup()
        Dim RemoteTestFolderName As String = "ZZZ_UnitTest_CM.Dms." & Me.TestedProvider.ToString & "_TempUploadLinkDir"

        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        If DmsProvider.SupportsSharingSetup = False Then Assert.Ignore("Sharing setup not supported by provider")

        'Cleanup existing data from previous runs before start
        Me.RemoveRemoteTestFolder(RemoteTestFolderName, If(DmsProvider.SupportsCollections, DirectoryTypes.Collection, DirectoryTypes.Folder), False)

        'Create collection/folder for sharing test
        Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderName, If(DmsProvider.SupportsCollections, DirectoryTypes.Collection, DirectoryTypes.Folder))
        Dim RemoteDirItem As CompuMaster.Dms.Data.DmsResourceItem = DmsProvider.ListRemoteItem(RemoteTestFolderName)
        Assert.NotNull(RemoteDirItem.FullName)
        Assert.IsNotEmpty(RemoteDirItem.FullName)
        If DmsProvider.SupportsCollections Then
            Assert.AreEqual(Dms.Data.DmsResourceItem.ItemTypes.Collection, RemoteDirItem.ItemType)
        Else
            Assert.AreEqual(Dms.Data.DmsResourceItem.ItemTypes.Folder, RemoteDirItem.ItemType)
        End If
        Assert.AreEqual(RemoteTestFolderName, RemoteDirItem.FullName)

        Dim ShareLink As New Data.DmsLink(RemoteDirItem, DmsProvider) With
            {
            .AllowUpload = True,
            .MaxUploads = 4000,
            .MaxBytes = Integer.MaxValue,
            .Password = Guid.NewGuid.ToString("n"),
            .Name = "UnitTest_UploadLinkShare_" & RemoteTestFolderName
            }

        ShareLink = DmsProvider.CreateLink(RemoteDirItem, ShareLink)
        System.Console.WriteLine("Created link: " & ShareLink.ToString())

        'Test created link
        Assert.NotNull(ShareLink.ID)
        Assert.IsNotEmpty(ShareLink.ID)
        Select Case DmsProvider.DmsProviderID
            Case BaseDmsProvider.DmsProviders.CenterDevice, BaseDmsProvider.DmsProviders.Scopevisio
                Dim CenterDeviceProvider As CompuMaster.Dms.Providers.CenterDeviceDmsProviderBase = CType(DmsProvider, CompuMaster.Dms.Providers.CenterDeviceDmsProviderBase)
                Assert.AreEqual(ShareLink.ID, CenterDeviceProvider.IOClient.GetUploadLink(ShareLink.ID).Id)
                Assert.IsNull(CenterDeviceProvider._AllUploadLinks)
                Assert.AreEqual(ShareLink.ID, CenterDeviceProvider.IOClient.GetUploadLink(ShareLink.ID).Id)
            Case BaseDmsProvider.DmsProviders.OCS, BaseDmsProvider.DmsProviders.OwnCloud, BaseDmsProvider.DmsProviders.NextCloud
                Dim OcsProvider As CompuMaster.Dms.Providers.OcsDmsProvider = CType(DmsProvider, CompuMaster.Dms.Providers.OcsDmsProvider)
            Case Else
                Throw New NotImplementedException
        End Select
        Dim RefreshedRemoteDirItem As CompuMaster.Dms.Data.DmsResourceItem = DmsProvider.ListRemoteItem(RemoteTestFolderName)
        Assert.AreEqual(True, RefreshedRemoteDirItem.ExtendedInfosHasLinks)
        Assert.AreEqual(1, RefreshedRemoteDirItem.ExtendedInfosLinks.Count)
        Assert.AreEqual(SortedListOfString(ShareLink.AllowedActions), SortedListOfString(RefreshedRemoteDirItem.ExtendedInfosLinks(0).AllowedActions))

        'Remove link again
        Me.RemoveRemoteTestFolder(RemoteTestFolderName, If(DmsProvider.SupportsCollections, DirectoryTypes.Collection, DirectoryTypes.Folder), True)

        'Test removal of link
        Select Case DmsProvider.DmsProviderID
            Case BaseDmsProvider.DmsProviders.CenterDevice, BaseDmsProvider.DmsProviders.Scopevisio
                Dim CenterDeviceProvider As CompuMaster.Dms.Providers.CenterDeviceDmsProviderBase = CType(DmsProvider, CompuMaster.Dms.Providers.CenterDeviceDmsProviderBase)
                'Re-check with re-querying from server
                Assert.Catch(Of CenterDevice.Rest.Exceptions.NotFoundException)(
                    Sub()
                        CenterDeviceProvider.IOClient.GetUploadLink(ShareLink.ID)
                    End Sub)
                'Re-check with full list of upload links
                Dim AllUploadLinks = CenterDeviceProvider.IOClient.ApiClient.UploadLinks.GetAllUploadLinks(CenterDeviceProvider.IOClient.CurrentAuthenticationContextUserID)
                Dim FoundUploadLink As CenterDevice.Rest.Clients.Link.UploadLink = AllUploadLinks.UploadLinksList.Find(
                    Function(item As CenterDevice.Rest.Clients.Link.UploadLink) As Boolean
                        Return item.Id = ShareLink.ID
                    End Function)
                Assert.IsNull(FoundUploadLink)
            Case BaseDmsProvider.DmsProviders.OCS, BaseDmsProvider.DmsProviders.OwnCloud, BaseDmsProvider.DmsProviders.NextCloud
                Dim OcsProvider As CompuMaster.Dms.Providers.OcsDmsProvider = CType(DmsProvider, CompuMaster.Dms.Providers.OcsDmsProvider)
            Case Else
                Throw New NotImplementedException
        End Select

    End Sub

    <Test> Public Sub CreateDownloadLinkForCollectionAndCleanup()
        Dim RemoteTestFolderName As String = "ZZZ_UnitTest_CM.Dms." & Me.TestedProvider.ToString & "_TempDownloadLinkDir"

        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        If DmsProvider.SupportsSharingSetup = False Then Assert.Ignore("Sharing setup not supported by provider")

        'Cleanup existing data from previous runs before start
        Me.RemoveRemoteTestFolder(RemoteTestFolderName, If(DmsProvider.SupportsCollections, DirectoryTypes.Collection, DirectoryTypes.Folder), False)

        'Create collection/folder for sharing test
        Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderName, If(DmsProvider.SupportsCollections, DirectoryTypes.Collection, DirectoryTypes.Folder))
        Dim RemoteDirItem As CompuMaster.Dms.Data.DmsResourceItem = DmsProvider.ListRemoteItem(RemoteTestFolderName)
        Assert.NotNull(RemoteDirItem.FullName)
        Assert.IsNotEmpty(RemoteDirItem.FullName)
        If DmsProvider.SupportsCollections Then
            Assert.AreEqual(Dms.Data.DmsResourceItem.ItemTypes.Collection, RemoteDirItem.ItemType)
        Else
            Assert.AreEqual(Dms.Data.DmsResourceItem.ItemTypes.Folder, RemoteDirItem.ItemType)
        End If
        Assert.AreEqual(RemoteTestFolderName, RemoteDirItem.FullName)

        Dim ShareLink As New Data.DmsLink(RemoteDirItem, DmsProvider) With
            {
            .AllowView = True,
            .AllowDownload = True,
            .MaxDownloads = 4000,
            .MaxBytes = Integer.MaxValue,
            .Password = Guid.NewGuid.ToString("n")
            }

        ShareLink = DmsProvider.CreateLink(RemoteDirItem, ShareLink)
        System.Console.WriteLine("Created link: " & ShareLink.ToString())

        'Test created link
        Assert.NotNull(ShareLink.ID)
        Assert.IsNotEmpty(ShareLink.ID)
        Select Case DmsProvider.DmsProviderID
            Case BaseDmsProvider.DmsProviders.CenterDevice, BaseDmsProvider.DmsProviders.Scopevisio
                Dim CenterDeviceProvider As CompuMaster.Dms.Providers.CenterDeviceDmsProviderBase = CType(DmsProvider, CompuMaster.Dms.Providers.CenterDeviceDmsProviderBase)
                Assert.AreEqual(ShareLink.ID, CenterDeviceProvider.IOClient.GetLink(ShareLink.ID).Id)
            Case BaseDmsProvider.DmsProviders.OCS, BaseDmsProvider.DmsProviders.OwnCloud, BaseDmsProvider.DmsProviders.NextCloud
                Dim OcsProvider As CompuMaster.Dms.Providers.OcsDmsProvider = CType(DmsProvider, CompuMaster.Dms.Providers.OcsDmsProvider)
            Case Else
                Throw New NotImplementedException
        End Select
        Dim RefreshedRemoteDirItem As CompuMaster.Dms.Data.DmsResourceItem = DmsProvider.ListRemoteItem(RemoteTestFolderName)
        Assert.AreEqual(True, RefreshedRemoteDirItem.ExtendedInfosHasLinks)
        Assert.AreEqual(1, RefreshedRemoteDirItem.ExtendedInfosLinks.Count)
        Assert.AreEqual(SortedListOfString(ShareLink.AllowedActions), SortedListOfString(RefreshedRemoteDirItem.ExtendedInfosLinks(0).AllowedActions))

        'Remove link again
        Me.RemoveRemoteTestFolder(RemoteTestFolderName, If(DmsProvider.SupportsCollections, DirectoryTypes.Collection, DirectoryTypes.Folder), True)

        'Test removal of link
        Select Case DmsProvider.DmsProviderID
            Case BaseDmsProvider.DmsProviders.CenterDevice, BaseDmsProvider.DmsProviders.Scopevisio
                Dim CenterDeviceProvider As CompuMaster.Dms.Providers.CenterDeviceDmsProviderBase = CType(DmsProvider, CompuMaster.Dms.Providers.CenterDeviceDmsProviderBase)
                Assert.Catch(Of CenterDevice.Rest.Exceptions.NotFoundException)(
                    Sub()
                        CenterDeviceProvider.IOClient.GetLink(ShareLink.ID)
                    End Sub)
            Case BaseDmsProvider.DmsProviders.OCS, BaseDmsProvider.DmsProviders.OwnCloud, BaseDmsProvider.DmsProviders.NextCloud
                Dim OcsProvider As CompuMaster.Dms.Providers.OcsDmsProvider = CType(DmsProvider, CompuMaster.Dms.Providers.OcsDmsProvider)
            Case Else
                Throw New NotImplementedException
        End Select

    End Sub

    Private Function SortedListOfString(list As List(Of String)) As List(Of String)
        Dim Result As New List(Of String)(list)
        Result.Sort()
        Return Result
    End Function

    <Test> Public Sub Copy_Files()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider

        If Me.UploadTestFilesAndCleanupAgainBinary.Length > 0 Then

            'Prepare pre-requisites
            CreateRemoteTestCollectionOrFolderIfNotExisting(DmsProvider, Me.RemoteTestFolderName)
            If DmsProvider.SupportsCollections Then
                Assert.IsTrue(DmsProvider.CollectionExists(Me.RemoteTestFolderName))
            Else
                Assert.IsTrue(DmsProvider.FolderExists(Me.RemoteTestFolderName))
            End If

            'Run copy-file tests
            For Each CopyTarget As String In Me.CopyFileTestFileTargets
                UploadFileAndCreateCopy(DmsProvider, Me.CopyFileTestFileSource.Value, Me.CopyFileTestFileSource.Key, CopyTarget)
                UploadFileAndCreateCopyAsync(DmsProvider, Me.CopyFileTestFileSource.Value, Me.CopyFileTestFileSource.Key, CopyTarget)
            Next

            'Test the test
            If DmsProvider.SupportsCollections Then
                Assert.IsTrue(DmsProvider.CollectionExists(Me.RemoteTestFolderName))
            Else
                Assert.IsTrue(DmsProvider.FolderExists(Me.RemoteTestFolderName))
            End If

            'Cleanup
            RemoveRemoteTestCollectionOrFolder(DmsProvider, Me.RemoteTestFolderName)

        End If

    End Sub

    <Test> Public Sub Copy_Directories()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider

        If Me.UploadTestFilesAndCleanupAgainBinary.Length > 0 Then

            'Prepare pre-requisites
            CreateRemoteTestCollectionOrFolderIfNotExisting(DmsProvider, Me.RemoteTestFolderName)
            If DmsProvider.SupportsCollections Then
                Assert.IsTrue(DmsProvider.CollectionExists(Me.RemoteTestFolderName))
            Else
                Assert.IsTrue(DmsProvider.FolderExists(Me.RemoteTestFolderName))
            End If

            'Run copy-dir tests
            '1st step: create initial directory structure with at least 1 file
            UploadInitialTestFile(DmsProvider, Me.CopyDirTestFileSource.Key, Me.CopyDirTestFileSource.Value)

            '2nd step: copy and check
            For MyCounter As Integer = 0 To CopyDirTestDirSource.Length - 1
                UploadDirAndCreateCopy(DmsProvider, Me.CopyDirTestDirSource(MyCounter), Me.CopyDirTestDirTarget(MyCounter), Me.CopyDirTestExpectedTargetFile(MyCounter))
            Next

            'Test the test
            If DmsProvider.SupportsCollections Then
                Assert.IsTrue(DmsProvider.CollectionExists(Me.RemoteTestFolderName))
            Else
                Assert.IsTrue(DmsProvider.FolderExists(Me.RemoteTestFolderName))
            End If

            'Cleanup
            RemoveRemoteTestCollectionOrFolder(DmsProvider, Me.RemoteTestFolderName)

        End If

    End Sub

    ''' <summary>
    ''' Upload initial test file and create sub directory structure
    ''' </summary>
    ''' <param name="dmsProvider"></param>
    ''' <param name="sourceRemoteFileNameInTestFolder"></param>
    ''' <param name="binaryData"></param>
    Private Sub UploadInitialTestFile(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, sourceRemoteFileNameInTestFolder As String, binaryData As Byte())
        Dim RemoteFilePathSource As String = dmsProvider.CombinePath(Me.RemoteTestFolderName, sourceRemoteFileNameInTestFolder)
        AssertRemoteFileNotExists(dmsProvider, RemoteFilePathSource, True)
        dmsProvider.UploadFile(RemoteFilePathSource, binaryData, True)
        AssertRemoteFileExists(dmsProvider, RemoteFilePathSource)
    End Sub

    Private Sub UploadDirAndCreateCopy(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, sourceRemoteFileNameInTestFolder As String, targetRemoteFileNameInTestFolder As String, expectedFileExistingAfterCopy As String)
        If Me.RemoteTestFolderName = Nothing Then Throw New NotSupportedException(Me.RemoteTestFolderName)

        Dim RemotePathSource As String = dmsProvider.CombinePath(Me.RemoteTestFolderName, sourceRemoteFileNameInTestFolder)
        Dim RemotePathTarget As String = dmsProvider.CombinePath(Me.RemoteTestFolderName, targetRemoteFileNameInTestFolder)
        Dim RemotePathExpectedTarget As String
        If expectedFileExistingAfterCopy.StartsWith("{NOT-SUPPORTED:") Then
            RemotePathExpectedTarget = expectedFileExistingAfterCopy
        Else
            RemotePathExpectedTarget = dmsProvider.CombinePath(Me.RemoteTestFolderName, expectedFileExistingAfterCopy)
        End If

        AssertRemoteDirectoryExists(dmsProvider, RemotePathSource)
        AssertRemoteDirectoryNotExists(dmsProvider, RemotePathTarget, True)

        '1st step: copy remote dir on remote storage into a non-existing directory
        Assert.Catch(Of DirectoryNotFoundException)(Sub()
                                                        dmsProvider.Copy(RemotePathSource, dmsProvider.CombinePath(RemotePathTarget, "will", "never", "exist"), False, False)
                                                    End Sub)

        Try
            dmsProvider.Copy(RemotePathSource, RemotePathTarget, False, True)
            AssertRemoteDirectoryExists(dmsProvider, RemotePathTarget)
            AssertRemoteFileExists(dmsProvider, RemotePathExpectedTarget)
        Catch ex As NotImplementedException
            Throw New IgnoreException("Implementation required" & System.Environment.NewLine & ex.ToString, ex)
        Catch ex As Exception
            Assert.AreEqual(RemotePathExpectedTarget, "{NOT-SUPPORTED:" & ex.GetType.FullName & "}", "Catched exception type must match with expected result" & System.Environment.NewLine & ex.ToString)
        End Try

        '3rd step: copy again and fail because of trial to overwrite: not yet implemented to handle what happens if destination already exists (partially)!
        If RemotePathTarget.EndsWith(dmsProvider.DirectorySeparator) Then
            Assert.Catch(Of System.ArgumentException)(Sub()
                                                          dmsProvider.Copy(RemotePathSource, RemotePathTarget, True, False)
                                                      End Sub)
        Else
            Assert.Catch(Of NotImplementedException)(Sub()
                                                         dmsProvider.Copy(RemotePathSource, RemotePathTarget, True, False)
                                                     End Sub)
        End If

        'Cleanup
        CleanupRemoteDirectory(dmsProvider, RemotePathTarget)

    End Sub

    Private Sub UploadFileAndCreateCopy(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, binaryData As Byte(), sourceRemoteFileNameInTestFolder As String, targetRemoteFileNameInTestFolder As String)
        If Me.RemoteTestFolderName = Nothing Then Throw New NotSupportedException(Me.RemoteTestFolderName)

        Dim RemoteFilePathSource As String = dmsProvider.CombinePath(Me.RemoteTestFolderName, sourceRemoteFileNameInTestFolder)
        Dim RemoteFilePathTarget As String = dmsProvider.CombinePath(Me.RemoteTestFolderName, targetRemoteFileNameInTestFolder)

        AssertRemoteFileNotExists(dmsProvider, RemoteFilePathSource, True)
        AssertRemoteFileNotExists(dmsProvider, RemoteFilePathTarget, True)

        '1st step: upload data and create remote source file
        dmsProvider.UploadFile(RemoteFilePathSource, binaryData)
        AssertRemoteFileExists(dmsProvider, RemoteFilePathSource)

        '2nd step: copy remote file on remote storage
        dmsProvider.Copy(RemoteFilePathSource, RemoteFilePathTarget, False, True)
        AssertRemoteFileExists(dmsProvider, RemoteFilePathTarget)

        '3rd step: copy again and fail because of trial to overwrite
        Assert.Catch(Of FileAlreadyExistsException)(Sub()
                                                        dmsProvider.Copy(RemoteFilePathSource, RemoteFilePathTarget, False, False)
                                                    End Sub)

        '4th step: copy again and overwrite
        dmsProvider.Copy(RemoteFilePathSource, RemoteFilePathTarget, True, False)
        AssertRemoteFileExists(dmsProvider, RemoteFilePathTarget)

        'Cleanup
        CleanupRemoteFile(dmsProvider, RemoteFilePathSource)
        CleanupRemoteFile(dmsProvider, RemoteFilePathTarget)
    End Sub

    Private Sub UploadFileAndCreateCopyAsync(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, binaryData As Byte(), sourceRemoteFileNameInTestFolder As String, targetRemoteFileNameInTestFolder As String)
        If Me.RemoteTestFolderName = Nothing Then Throw New NotSupportedException(Me.RemoteTestFolderName)

        Dim RemoteFilePathSource As String = dmsProvider.CombinePath(Me.RemoteTestFolderName, sourceRemoteFileNameInTestFolder)
        Dim RemoteFilePathTarget As String = dmsProvider.CombinePath(Me.RemoteTestFolderName, targetRemoteFileNameInTestFolder)

        AssertRemoteFileNotExists(dmsProvider, RemoteFilePathSource, True)
        AssertRemoteFileNotExists(dmsProvider, RemoteFilePathTarget, True)

        '1st step: upload data and create remote source file
        dmsProvider.UploadFile(RemoteFilePathSource, binaryData)
        AssertRemoteFileExists(dmsProvider, RemoteFilePathSource)

        '2nd step: copy remote file on remote storage
        Dim CopyTask As Task
        CopyTask = dmsProvider.CopyAsync(RemoteFilePathSource, RemoteFilePathTarget, False, True)
        CopyTask.Wait()
        Assert.True(CopyTask.IsCompleted)
        Assert.False(CopyTask.IsFaulted)
        Assert.AreEqual(TaskStatus.RanToCompletion, CopyTask.Status)
        AssertRemoteFileExists(dmsProvider, RemoteFilePathTarget)

        '3rd step: copy again and fail because of trial to overwrite
        Assert.ThrowsAsync(Of FileAlreadyExistsException)(Async Function()
                                                              Await dmsProvider.CopyAsync(RemoteFilePathSource, RemoteFilePathTarget, False, False)
                                                          End Function)

        '4th step: copy again and overwrite
        CopyTask = dmsProvider.CopyAsync(RemoteFilePathSource, RemoteFilePathTarget, True, False)
        CopyTask.Wait()
        Assert.True(CopyTask.IsCompleted)
        Assert.False(CopyTask.IsFaulted)
        Assert.AreEqual(TaskStatus.RanToCompletion, CopyTask.Status)
        AssertRemoteFileExists(dmsProvider, RemoteFilePathTarget)

        'Cleanup
        CleanupRemoteFile(dmsProvider, RemoteFilePathSource)
        CleanupRemoteFile(dmsProvider, RemoteFilePathTarget)
    End Sub

    <Test> Public Sub Move_Files()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider

        If Me.UploadTestFilesAndCleanupAgainBinary.Length > 0 Then

            'Prepare pre-requisites
            CreateRemoteTestCollectionOrFolderIfNotExisting(DmsProvider, Me.RemoteTestFolderName)
            If DmsProvider.SupportsCollections Then
                Assert.IsTrue(DmsProvider.CollectionExists(Me.RemoteTestFolderName))
            Else
                Assert.IsTrue(DmsProvider.FolderExists(Me.RemoteTestFolderName))
            End If

            'Run move-file tests
            For Each MoveTarget As String In Me.MoveFileTestFileTargets
                UploadFileAndMove(DmsProvider, Me.MoveFileTestFileSource.Value, Me.MoveFileTestFileSource.Key, MoveTarget)
                'UploadFileAndMoveAsync(DmsProvider, Me.MoveFileTestFileSource.Value, Me.MoveFileTestFileSource.Key, MoveTarget)
            Next

            'Test the test
            If DmsProvider.SupportsCollections Then
                Assert.IsTrue(DmsProvider.CollectionExists(Me.RemoteTestFolderName))
            Else
                Assert.IsTrue(DmsProvider.FolderExists(Me.RemoteTestFolderName))
            End If

            'Cleanup
            RemoveRemoteTestCollectionOrFolder(DmsProvider, Me.RemoteTestFolderName)

        End If

    End Sub

    <Test> Public Sub Move_Directories()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider

        If Me.UploadTestFilesAndCleanupAgainBinary.Length > 0 Then

            'Prepare pre-requisites
            CreateRemoteTestCollectionOrFolderIfNotExisting(DmsProvider, Me.RemoteTestFolderName)
            If DmsProvider.SupportsCollections Then
                Assert.IsTrue(DmsProvider.CollectionExists(Me.RemoteTestFolderName))
            Else
                Assert.IsTrue(DmsProvider.FolderExists(Me.RemoteTestFolderName))
            End If

            'Run move-dir tests
            For MyCounter As Integer = 0 To MoveDirTestDirSource.Length - 1
                'move and check
                UploadDirAndMove(DmsProvider, Me.MoveDirTestDirSource(MyCounter), Me.MoveDirTestDirTarget(MyCounter), Me.MoveDirTestFileSource.Key, Me.MoveDirTestExpectedTargetFile(MyCounter))
            Next

            'Test the test
            If DmsProvider.SupportsCollections Then
                Assert.IsTrue(DmsProvider.CollectionExists(Me.RemoteTestFolderName))
            Else
                Assert.IsTrue(DmsProvider.FolderExists(Me.RemoteTestFolderName))
            End If

            'Cleanup
            RemoveRemoteTestCollectionOrFolder(DmsProvider, Me.RemoteTestFolderName)
        End If

    End Sub

    Private Sub UploadDirAndMove(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, sourceRemoteFileNameInTestFolder As String, targetRemoteFileNameInTestFolder As String, expectedFileNotExistingAnyMoreAfterMove As String, expectedFileExistingAfterMove As String)
        If Me.RemoteTestFolderName = Nothing Then Throw New NotSupportedException(Me.RemoteTestFolderName)

        Dim RemotePathSource As String = dmsProvider.CombinePath(Me.RemoteTestFolderName, sourceRemoteFileNameInTestFolder)
        Dim RemotePathTarget As String = dmsProvider.CombinePath(Me.RemoteTestFolderName, targetRemoteFileNameInTestFolder)
        Dim RemotePathExpectedTarget As String
        If expectedFileExistingAfterMove.StartsWith("{NOT-SUPPORTED:") Then
            RemotePathExpectedTarget = expectedFileExistingAfterMove
        Else
            RemotePathExpectedTarget = dmsProvider.CombinePath(Me.RemoteTestFolderName, expectedFileExistingAfterMove)
        End If
        Dim RemotePathExpectedMovedSourceNotExistingAnyMore As String = dmsProvider.CombinePath(Me.RemoteTestFolderName, expectedFileNotExistingAnyMoreAfterMove)

        '1st step: create initial directory structure with at least 1 file
        UploadInitialTestFile(dmsProvider, Me.MoveDirTestFileSource.Key, Me.MoveDirTestFileSource.Value)
        AssertRemoteDirectoryExists(dmsProvider, RemotePathSource)
        AssertRemoteDirectoryNotExists(dmsProvider, RemotePathTarget, True)

        '2nd step: move remote dir on remote storage into a non-existing directory
        Assert.Catch(Of DirectoryNotFoundException)(Sub()
                                                        dmsProvider.Move(RemotePathSource, dmsProvider.CombinePath(RemotePathTarget, "will", "never", "exist"), False, False)
                                                    End Sub)

        Try
            dmsProvider.Move(RemotePathSource, RemotePathTarget, False, True)
            AssertRemoteDirectoryNotExists(dmsProvider, RemotePathSource, True)
            AssertRemoteDirectoryExists(dmsProvider, RemotePathTarget)
            AssertRemoteFileExists(dmsProvider, RemotePathExpectedTarget)
            AssertRemoteFileNotExists(dmsProvider, RemotePathExpectedMovedSourceNotExistingAnyMore, False)
        Catch ex As DirectoryNotFoundException
            Assert.AreEqual(RemotePathExpectedTarget, "{NOT-FOUND:" & ex.RemotePath & "}", "Catched exception type must match with expected result")
        Catch ex As DirectoryActionFailedException
            Throw New IgnoreException(ex.ToString)
        Catch ex As NotImplementedException
            Throw New IgnoreException(ex.ToString)
        Catch ex As Exception
            Assert.AreEqual(RemotePathExpectedTarget, "{NOT-SUPPORTED:" & ex.GetType.FullName & "}", "Catched exception type must match with expected result" & System.Environment.NewLine & ex.ToString)
        End Try

        '3rd step: move again and fail because of trial to overwrite: not yet implemented to handle what happens if destination already exists (partially)!
        UploadInitialTestFile(dmsProvider, Me.MoveDirTestFileSource.Key, Me.MoveDirTestFileSource.Value)
        AssertRemoteDirectoryExists(dmsProvider, RemotePathSource)
        If RemotePathTarget.EndsWith(dmsProvider.DirectorySeparator) Then
            Assert.Catch(Of System.ArgumentException)(Sub()
                                                          dmsProvider.Move(RemotePathSource, RemotePathTarget, True, False)
                                                      End Sub)
        Else
            Assert.Catch(Of NotImplementedException)(Sub()
                                                         dmsProvider.Move(RemotePathSource, RemotePathTarget, True, False)
                                                     End Sub)
        End If

        'Cleanup
        CleanupRemoteDirectory(dmsProvider, RemotePathTarget)

    End Sub

    Private Sub UploadFileAndMove(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, binaryData As Byte(), sourceRemoteFileNameInTestFolder As String, targetRemoteFileNameInTestFolder As String)
        If Me.RemoteTestFolderName = Nothing Then Throw New NotSupportedException(Me.RemoteTestFolderName)

        Dim RemoteFilePathSource As String = dmsProvider.CombinePath(Me.RemoteTestFolderName, sourceRemoteFileNameInTestFolder)
        Dim RemoteFilePathTarget As String = dmsProvider.CombinePath(Me.RemoteTestFolderName, targetRemoteFileNameInTestFolder)

        AssertRemoteFileNotExists(dmsProvider, RemoteFilePathSource, True)
        AssertRemoteFileNotExists(dmsProvider, RemoteFilePathTarget, True)

        '1st step: upload data and create remote source file
        dmsProvider.UploadFile(RemoteFilePathSource, binaryData)
        AssertRemoteFileExists(dmsProvider, RemoteFilePathSource)

        '2nd step: move remote file on remote storage
        Try
            dmsProvider.Move(RemoteFilePathSource, RemoteFilePathTarget, False, True)
        Catch ex As FileActionFailedException
            Select Case dmsProvider.DmsProviderID
                Case BaseDmsProvider.DmsProviders.CenterDevice, BaseDmsProvider.DmsProviders.Scopevisio
                    Throw New IgnoreException("Move action failed - CenterDevice implementation to be completed" & System.Environment.NewLine & ex.ToString, ex)
                Case Else
                    Throw
            End Select
        Catch ex As NotImplementedException
            Throw New IgnoreException("Implementation required" & System.Environment.NewLine & ex.ToString, ex)
        End Try
        AssertRemoteFileExists(dmsProvider, RemoteFilePathTarget)
        AssertRemoteFileNotExists(dmsProvider, RemoteFilePathSource, False)

        '3rd step: move again and fail because of trial to overwrite
        If dmsProvider.RemoteItemExists(RemoteFilePathSource) = False Then dmsProvider.UploadFile(RemoteFilePathSource, binaryData)
        Assert.Catch(Of FileAlreadyExistsException)(Sub()
                                                        dmsProvider.Move(RemoteFilePathSource, RemoteFilePathTarget, False, False)
                                                    End Sub)

        '4th step: move again and overwrite
        If dmsProvider.RemoteItemExists(RemoteFilePathSource) = False Then dmsProvider.UploadFile(RemoteFilePathSource, binaryData)
        AssertRemoteFileExists(dmsProvider, RemoteFilePathSource)
        AssertRemoteFileExists(dmsProvider, RemoteFilePathTarget)
        dmsProvider.Move(RemoteFilePathSource, RemoteFilePathTarget, True, False)
        AssertRemoteFileExists(dmsProvider, RemoteFilePathTarget)
        AssertRemoteFileNotExists(dmsProvider, RemoteFilePathSource, False)

        'Cleanup
        CleanupRemoteFile(dmsProvider, RemoteFilePathSource)
        CleanupRemoteFile(dmsProvider, RemoteFilePathTarget)
    End Sub

    ''' <summary>
    ''' Assert a remote file doesn't exist, optionally cleanup an existing remote file before test
    ''' </summary>
    ''' <param name="dmsProvider"></param>
    ''' <param name="remoteFileNameInTestFolder"></param>
    ''' <param name="deleteRemoteFileBeforeTest"></param>
    Private Sub AssertRemoteFileNotExists(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, remoteFilePath As String, deleteRemoteFileBeforeTest As Boolean)
        Dim Item As DmsResourceItem
        Item = dmsProvider.ListRemoteItem(remoteFilePath)
        If deleteRemoteFileBeforeTest AndAlso Item IsNot Nothing Then
            'JIT-cleanup
            dmsProvider.DeleteRemoteItem(Item, DmsResourceItem.ItemTypes.File)
            Item = dmsProvider.ListRemoteItem(remoteFilePath)
        End If
        Assert.IsNull(Item, "Remote file must not exist: " & remoteFilePath)
        Assert.IsFalse(dmsProvider.RemoteItemExists(remoteFilePath), "Remote file must not exist: " & remoteFilePath)
    End Sub

    ''' <summary>
    ''' Delete a remote item (if existing) and verify it's cleanup
    ''' </summary>
    ''' <param name="dmsProvider"></param>
    ''' <param name="remoteFilePath"></param>
    Private Sub CleanupRemoteFile(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, remoteFilePath As String)
        'Cleanup
        Try
            dmsProvider.DeleteRemoteItem(remoteFilePath)
        Catch ex As CompuMaster.Dms.Data.DirectoryNotFoundException
            'ignore
        Catch ex As CompuMaster.Dms.Data.RessourceNotFoundException
            'ignore
        End Try
        Dim Item As DmsResourceItem
        Item = dmsProvider.ListRemoteItem(remoteFilePath)
        Assert.IsNull(Item, "Remote file must not exist: " & remoteFilePath)
        Assert.IsFalse(dmsProvider.RemoteItemExists(remoteFilePath), "Remote file must not exist: " & remoteFilePath)
    End Sub

    ''' <summary>
    ''' Delete a remote item (if existing) and verify it's cleanup
    ''' </summary>
    ''' <param name="dmsProvider"></param>
    ''' <param name="remoteItemPath"></param>
    Private Sub CleanupRemoteItem(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, remoteItemPath As String)
        'Cleanup
        Try
            dmsProvider.DeleteRemoteItem(remoteItemPath)
        Catch ex As RessourceNotFoundException
            'ignore
        End Try
        Dim Item As DmsResourceItem
        Item = dmsProvider.ListRemoteItem(remoteItemPath)
        Assert.IsNull(Item, "Remote item must not exist: " & remoteItemPath)
        Assert.IsFalse(dmsProvider.RemoteItemExists(remoteItemPath), "Remote item must not exist: " & remoteItemPath)
    End Sub

    ''' <summary>
    ''' Delete a remote directory (if existing) and verify it's cleanup
    ''' </summary>
    ''' <param name="dmsProvider"></param>
    ''' <param name="remoteDirectoryPath"></param>
    Private Sub CleanupRemoteDirectory(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, remoteDirectoryPath As String)
        'Cleanup
        Try
            dmsProvider.DeleteRemoteItem(remoteDirectoryPath)
        Catch ex As CompuMaster.Dms.Data.DirectoryNotFoundException
            'ignore
        Catch ex As RessourceNotFoundException
            'ignore
        End Try
        Dim Item As DmsResourceItem
        Item = dmsProvider.ListRemoteItem(remoteDirectoryPath)
        Assert.IsNull(Item, "Remote item must not exist: " & remoteDirectoryPath)
        Assert.IsFalse(dmsProvider.RemoteItemExists(remoteDirectoryPath), "Remote item must not exist: " & remoteDirectoryPath)
    End Sub

    ''' <summary>
    ''' Assert a remote file exists and there are no file name collissions (especially 2 files with the very same file name)
    ''' </summary>
    ''' <param name="dmsProvider"></param>
    ''' <param name="remoteFilePath"></param>
    Private Sub AssertRemoteFileExists(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, remoteFilePath As String)
        Dim Item As DmsResourceItem
        Item = dmsProvider.ListRemoteItem(remoteFilePath)
        Assert.IsNotNull(Item, "Remote file must exist: " & remoteFilePath)
        Assert.IsFalse(Item.ExtendedInfosCollisionDetected, "Remote file with file name collissions: " & remoteFilePath)
        Assert.IsTrue(dmsProvider.RemoteItemExists(remoteFilePath), "Remote file must exist: " & remoteFilePath)
    End Sub

    ''' <summary>
    ''' Assert a remote directory doesn't exist, optionally cleanup an existing remote file before test
    ''' </summary>
    ''' <param name="dmsProvider"></param>
    ''' <param name="remoteFileNameInTestFolder"></param>
    ''' <param name="deleteRemoteFileBeforeTest"></param>
    Private Sub AssertRemoteDirectoryNotExists(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, remoteDirectoryPath As String, deleteRemoteFileBeforeTest As Boolean)
        Dim Item As DmsResourceItem
        Item = dmsProvider.ListRemoteItem(remoteDirectoryPath)
        If deleteRemoteFileBeforeTest AndAlso Item IsNot Nothing Then
            'JIT-cleanup
            dmsProvider.DeleteRemoteItem(Item)
            Item = dmsProvider.ListRemoteItem(remoteDirectoryPath)
        End If
        Assert.IsNull(Item, "Remote file must not exist: " & remoteDirectoryPath)
        Assert.IsFalse(dmsProvider.RemoteItemExists(remoteDirectoryPath), "Remote directory must not exist: " & remoteDirectoryPath)
    End Sub

    ''' <summary>
    ''' Assert a remote directory exists and there are no file name collissions (especially 2 files with the very same file name)
    ''' </summary>
    ''' <param name="dmsProvider"></param>
    ''' <param name="remoteFilePath"></param>
    Private Sub AssertRemoteDirectoryExists(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, remoteDirectoryPath As String)
        Dim Item As DmsResourceItem
        Item = dmsProvider.ListRemoteItem(remoteDirectoryPath)
        Assert.IsNotNull(Item, "Remote file must exist: " & remoteDirectoryPath)
        Assert.IsFalse(Item.ExtendedInfosCollisionDetected, "Remote directory with directory name collissions: " & remoteDirectoryPath)
        Assert.IsTrue(dmsProvider.RemoteItemExists(remoteDirectoryPath), "Remote directory must exist: " & remoteDirectoryPath)
    End Sub

End Class