Option Explicit On
Option Strict On

Imports NUnit.Framework
Imports CompuMaster.Dms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

<NonParallelizable> Public MustInherit Class BaseDmsProviderTestBase

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

        Item = DmsProvider.ListRemoteItem(DmsProvider.BrowseInRootFolderName)
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
    Public Overridable ReadOnly Property RemoteTestFolderName As String = "ZZZ_UnitTests_Dms"
    Public Overridable ReadOnly Property RemoteFilesMustExist As String() = New String() {}
    Public Overridable ReadOnly Property RemoteFoldersMustExist As String() = New String() {}
    Public Overridable ReadOnly Property RemoteCollectionsMustExist As String() = New String() {}
    Public Overridable ReadOnly Property RemoteItemsMustNotExist As String() = New String() {"/not-existing-test-item"}
    Public Overridable ReadOnly Property RemoteFoldersWithFiles As String() = New String() {}
    Public Overridable ReadOnly Property RemoteFoldersWithSubFolders As String() = New String() {}
    Public Overridable ReadOnly Property DownloadTestFilesText As KeyValuePair(Of String, String)() = New KeyValuePair(Of String, String)() {}
    Public Overridable ReadOnly Property DownloadTestFilesBinary As KeyValuePair(Of String, Byte())() = New KeyValuePair(Of String, Byte())() {}
    Public Overridable ReadOnly Property UploadTestFilesAndCleanupAgainText As KeyValuePair(Of String, String)() = New KeyValuePair(Of String, String)() {}
    Public Overridable ReadOnly Property UploadTestFilesAndCleanupAgainBinary As KeyValuePair(Of String, Byte())() = New KeyValuePair(Of String, Byte())() {
            New KeyValuePair(Of String, Byte())("upload-test-file.tmp", New Byte() {0, 255, 68, 46, 64, 87, 92})
        }
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
        Assert.AreEqual("/folder1", DmsProvider.ParentDirectoryPath("/folder1/"))
        Assert.AreEqual("", DmsProvider.ParentDirectoryPath("/file1"))
        Assert.AreEqual("", DmsProvider.ParentDirectoryPath("file1"))
        Assert.AreEqual(Nothing, DmsProvider.ParentDirectoryPath("/"))
        Assert.AreEqual(Nothing, DmsProvider.ParentDirectoryPath(""))
    End Sub

    <Test> Public Sub ItemName()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.UninitializedDmsProvider
        Assert.AreEqual("folder4", DmsProvider.ItemName("folder1/folder2/folder3/folder4"))
        Assert.AreEqual("folder4", DmsProvider.ItemName("/folder1/folder2/folder3/folder4"))
        Assert.AreEqual("file1", DmsProvider.ItemName("/file1"))
        Assert.AreEqual("file1", DmsProvider.ItemName("file1"))
        Assert.Catch(Of ArgumentException)(Sub()
                                               DmsProvider.ItemName("/")
                                           End Sub)
        Assert.Catch(Of ArgumentException)(Sub()
                                               DmsProvider.ItemName("")
                                           End Sub)
    End Sub

    Protected OriginalSslValidateCallback As System.Net.Security.RemoteCertificateValidationCallback = System.Net.ServicePointManager.ServerCertificateValidationCallback

    Protected Function AcceptAllCertifications(ByVal sender As Object, ByVal certification As System.Security.Cryptography.X509Certificates.X509Certificate, ByVal chain As System.Security.Cryptography.X509Certificates.X509Chain, ByVal sslPolicyErrors As System.Net.Security.SslPolicyErrors) As Boolean
        Return True
    End Function

    Private Sub CreateRemoteTestFolderIfNotExisting(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, remotePath As String)
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

    Private Sub RemoveRemoteTestFolder(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, remotePath As String)
        Dim Item As DmsResourceItem

        'Lookup status (and fill caches)
        Item = dmsProvider.ListRemoteItem(remotePath)
        Assert.IsNotNull(Item)
        Assert.IsTrue(dmsProvider.RemoteItemExists(remotePath))

        'Delete the folder
        If dmsProvider.SupportsCollections Then
            dmsProvider.DeleteRemoteItem(remotePath, DmsResourceItem.ItemTypes.Collection)
        Else
            dmsProvider.DeleteRemoteItem(remotePath, DmsResourceItem.ItemTypes.Folder)
        End If

        'Lookup status again and check that old caches don't exist
        Item = dmsProvider.ListRemoteItem(remotePath)
        Assert.IsNull(Item)
        Assert.IsFalse(dmsProvider.RemoteItemExists(remotePath))

    End Sub

    <Test> Public Sub CreateCollectionOrFolderAndCleanup()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        Me.CreateRemoteTestFolderIfNotExisting(DmsProvider, Me.RemoteTestFolderName)
        Me.RemoveRemoteTestFolder(DmsProvider, Me.RemoteTestFolderName)
    End Sub

    <Test> Public Sub UploadFilesAndCleanup()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider

        If Me.UploadTestFilesAndCleanupAgainText.Length > 0 OrElse Me.UploadTestFilesAndCleanupAgainBinary.Length > 0 Then

            Me.CreateRemoteTestFolderIfNotExisting(DmsProvider, Me.RemoteTestFolderName)

            If DmsProvider.SupportsCollections Then
                Assert.IsTrue(DmsProvider.CollectionExists(Me.RemoteTestFolderName))
            Else
                Assert.IsTrue(DmsProvider.FolderExists(Me.RemoteTestFolderName))
            End If

            For Each upload In Me.UploadTestFilesAndCleanupAgainText
                Dim LocalPathAbsolute As String = TestFileForUploadTests(upload.Value)
                UploadFilesAndCleanup(DmsProvider, LocalPathAbsolute, upload.Key)
            Next

            For Each upload In Me.UploadTestFilesAndCleanupAgainBinary
                Dim LocalPathAbsolute As String = System.IO.Path.GetTempFileName()
                System.IO.File.WriteAllBytes(LocalPathAbsolute, upload.Value)
                UploadFilesAndCleanup(DmsProvider, LocalPathAbsolute, upload.Key)
            Next

            If DmsProvider.SupportsCollections Then
                Assert.IsTrue(DmsProvider.CollectionExists(Me.RemoteTestFolderName))
            Else
                Assert.IsTrue(DmsProvider.FolderExists(Me.RemoteTestFolderName))
            End If

            Me.RemoveRemoteTestFolder(DmsProvider, Me.RemoteTestFolderName)

        End If

    End Sub

    Private Sub UploadFilesAndCleanup(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, localFilePathAbsolute As String, remoteFilePath As String)
        Dim Item As DmsResourceItem

        Item = dmsProvider.ListRemoteItem(remoteFilePath)
        Assert.IsNull(Item)
        Assert.IsFalse(dmsProvider.RemoteItemExists(remoteFilePath))

        dmsProvider.UploadFile(dmsProvider.CombinePath(Me.RemoteTestFolderName, remoteFilePath), localFilePathAbsolute)

        Item = dmsProvider.ListRemoteItem(remoteFilePath)
        Assert.IsNotNull(Item)
        Assert.IsTrue(dmsProvider.RemoteItemExists(remoteFilePath))

        dmsProvider.DeleteRemoteItem(dmsProvider.CombinePath(Me.RemoteTestFolderName, remoteFilePath))

        Item = dmsProvider.ListRemoteItem(remoteFilePath)
        Assert.IsNull(Item)
        Assert.IsFalse(dmsProvider.RemoteItemExists(remoteFilePath))
    End Sub

    Protected Function TestAssembly() As System.Reflection.Assembly
        Return System.Reflection.Assembly.GetExecutingAssembly
    End Function

    Protected Function TestFileForUploadTests(testFileName As String) As String
        Dim LocalFilePath As String = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Me.TestAssembly.Location), testFileName)
        If System.IO.File.Exists(LocalFilePath) = False Then Throw New System.IO.FileNotFoundException("File not found: " & LocalFilePath)
        Return LocalFilePath
    End Function

End Class

