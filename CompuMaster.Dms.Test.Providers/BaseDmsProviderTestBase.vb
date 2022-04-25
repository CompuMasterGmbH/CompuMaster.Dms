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
    Public Overridable ReadOnly Property UploadTestFilesAndCleanupAgainFilePath As KeyValuePair(Of String, String)() = New KeyValuePair(Of String, String)() {}
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

    Private Shared Sub CreateRemoteTestFolderIfNotExisting(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, remotePath As String)
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

    Private Shared Sub RemoveRemoteTestFolder(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, remotePath As String)
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

    Protected Overridable Sub RemoveRemoteItemIfItExists(dmsProvider As BaseDmsProvider, remotePath As String, expectedRemoteItemType As DmsResourceItem.ItemTypes, preCheckItemExistance As TriState)
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
            Else
                dmsProvider.DeleteRemoteItem(remotePath, expectedRemoteItemType)
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
            CreateRemoteTestFolderIfNotExisting(DmsProvider, Me.RemoteTestFolderName)
            Assert.IsTrue(DmsProvider.CollectionExists(Me.RemoteTestFolderName))
            RemoveRemoteTestFolder(DmsProvider, Me.RemoteTestFolderName)
            Assert.IsFalse(DmsProvider.CollectionExists(Me.RemoteTestFolderName))
        Else
            System.Console.WriteLine("Remote test folder name: " & Me.RemoteTestFolderName)
            CreateRemoteTestFolderIfNotExisting(DmsProvider, Me.RemoteTestFolderName)
            Assert.IsTrue(DmsProvider.FolderExists(Me.RemoteTestFolderName))
            RemoveRemoteTestFolder(DmsProvider, Me.RemoteTestFolderName)
            Assert.IsFalse(DmsProvider.FolderExists(Me.RemoteTestFolderName))
        End If

        'Test RemoteTestFolderName with leading "/"
        Dim RemoteTestFolderNameWithLeadingDirectorySeparator As String = DmsProvider.CombinePath(DmsProvider.DirectorySeparator, Me.RemoteTestFolderName)
        If DmsProvider.SupportsCollections Then
            System.Console.WriteLine("Remote test collection name: " & RemoteTestFolderNameWithLeadingDirectorySeparator)
            CreateRemoteTestFolderIfNotExisting(DmsProvider, RemoteTestFolderNameWithLeadingDirectorySeparator)
            Assert.IsTrue(DmsProvider.CollectionExists(RemoteTestFolderNameWithLeadingDirectorySeparator))
            RemoveRemoteTestFolder(DmsProvider, RemoteTestFolderNameWithLeadingDirectorySeparator)
            Assert.IsFalse(DmsProvider.CollectionExists(RemoteTestFolderNameWithLeadingDirectorySeparator))
        Else
            System.Console.WriteLine("Remote test folder name: " & RemoteTestFolderNameWithLeadingDirectorySeparator)
            CreateRemoteTestFolderIfNotExisting(DmsProvider, RemoteTestFolderNameWithLeadingDirectorySeparator)
            Assert.IsTrue(DmsProvider.FolderExists(RemoteTestFolderNameWithLeadingDirectorySeparator))
            RemoveRemoteTestFolder(DmsProvider, RemoteTestFolderNameWithLeadingDirectorySeparator)
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

    <Test> Public Sub CreateRemoteFolderAndCleanup()
        Dim DmsProvider As BaseDmsProvider = Me.LoggedInDmsProvider
        Dim RemotePath As String = "Bierdeckel.UnitTest.Temp"
        Me.RemoveRemoteItemIfItExists(DmsProvider, RemotePath, Nothing, TriState.UseDefault)
        DmsProvider.CreateFolder(RemotePath)
        Assert.IsTrue(DmsProvider.RemoteItemExists(RemotePath))
        If DmsProvider.SupportsCollections Then
            Me.RemoveRemoteItemIfItExists(DmsProvider, RemotePath, DmsResourceItem.ItemTypes.Collection, TriState.True)
        Else
            Me.RemoveRemoteItemIfItExists(DmsProvider, RemotePath, DmsResourceItem.ItemTypes.Folder, TriState.True)
        End If
    End Sub

    <Test> Public Sub CreateRemoteCollectionAndCleanup()
        Dim DmsProvider As BaseDmsProvider = Me.LoggedInDmsProvider
        Dim RemotePath As String = "Bierdeckel.UnitTest.Temp"
        If DmsProvider.SupportsCollections Then
            'CenterDevice / Scopevisio Teamwork
            Me.RemoveRemoteItemIfItExists(DmsProvider, RemotePath, Nothing, TriState.UseDefault)
            Try
                DmsProvider.CreateCollection(RemotePath)
                Assert.IsTrue(DmsProvider.RemoteItemExists(RemotePath))
                Me.RemoveRemoteItemIfItExists(DmsProvider, RemotePath, DmsResourceItem.ItemTypes.Collection, TriState.True)
            Catch ex As NotSupportedException
            End Try
        Else
            'WebDAV
            Assert.Catch(Of NotSupportedException)(Sub()
                                                       DmsProvider.CreateCollection(RemotePath)
                                                   End Sub)
        End If
    End Sub

    <Test> Public Sub UploadFilesAndCleanup()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider

        If Me.UploadTestFilesAndCleanupAgainFilePath.Length > 0 OrElse Me.UploadTestFilesAndCleanupAgainBinary.Length > 0 Then

            CreateRemoteTestFolderIfNotExisting(DmsProvider, Me.RemoteTestFolderName)

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

            RemoveRemoteTestFolder(DmsProvider, Me.RemoteTestFolderName)

        End If

    End Sub

    Private Sub UploadFilesAndCleanup(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, binaryData As Byte(), remoteFileNameInTestFolder As String)
        If Me.RemoteTestFolderName = Nothing Then Throw New NotSupportedException(Me.RemoteTestFolderName)

        Dim Item As DmsResourceItem
        Dim RemoteFilePath As String = dmsProvider.CombinePath(Me.RemoteTestFolderName, remoteFileNameInTestFolder)

        Item = dmsProvider.ListRemoteItem(RemoteFilePath)
        If Item IsNot Nothing Then
            'JIT-cleanup
            dmsProvider.DeleteRemoteItem(Item, DmsResourceItem.ItemTypes.File)
            Item = dmsProvider.ListRemoteItem(RemoteFilePath)
        End If
        Assert.IsNull(Item)
        Assert.IsFalse(dmsProvider.RemoteItemExists(RemoteFilePath))

        'Upload 1st time
        dmsProvider.UploadFile(RemoteFilePath, binaryData)
        Item = dmsProvider.ListRemoteItem(RemoteFilePath)
        Assert.IsNotNull(Item)
        Assert.IsTrue(dmsProvider.RemoteItemExists(RemoteFilePath))

        'Upload 2nd time -> create new version instead of new file
        dmsProvider.UploadFile(RemoteFilePath, binaryData)
        Item = dmsProvider.ListRemoteItem(RemoteFilePath)
        Assert.IsNotNull(Item)
        Assert.IsTrue(dmsProvider.RemoteItemExists(RemoteFilePath))

        'Cleanup
        dmsProvider.DeleteRemoteItem(RemoteFilePath)

        Item = dmsProvider.ListRemoteItem(RemoteFilePath)
        Assert.IsNull(Item)
        Assert.IsFalse(dmsProvider.RemoteItemExists(RemoteFilePath))
    End Sub

    Private Sub UploadFilesAndCleanup(dmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider, localFilePathAbsolute As String, remoteFileNameInTestFolder As String)
        If Me.RemoteTestFolderName = Nothing Then Throw New NotSupportedException(Me.RemoteTestFolderName)

        Dim Item As DmsResourceItem
        Dim RemoteFilePath As String = dmsProvider.CombinePath(Me.RemoteTestFolderName, remoteFileNameInTestFolder)

        Item = dmsProvider.ListRemoteItem(RemoteFilePath)
        If Item IsNot Nothing Then
            'JIT-cleanup
            dmsProvider.DeleteRemoteItem(Item, DmsResourceItem.ItemTypes.File)
            Item = dmsProvider.ListRemoteItem(RemoteFilePath)
        End If
        Assert.IsNull(Item)
        Assert.IsFalse(dmsProvider.RemoteItemExists(RemoteFilePath))

        'Upload 1st time
        dmsProvider.UploadFile(RemoteFilePath, localFilePathAbsolute)
        Item = dmsProvider.ListRemoteItem(RemoteFilePath)
        Assert.IsNotNull(Item)
        Assert.IsTrue(dmsProvider.RemoteItemExists(RemoteFilePath))

        'Upload 2nd time -> create new version instead of new file
        dmsProvider.UploadFile(RemoteFilePath, localFilePathAbsolute)
        Item = dmsProvider.ListRemoteItem(RemoteFilePath)
        Assert.IsNotNull(Item)
        Assert.IsTrue(dmsProvider.RemoteItemExists(RemoteFilePath))

        'Cleanup
        dmsProvider.DeleteRemoteItem(RemoteFilePath)
        Item = dmsProvider.ListRemoteItem(RemoteFilePath)
        Assert.IsNull(Item)
        Assert.IsFalse(dmsProvider.RemoteItemExists(RemoteFilePath))
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

    ''' <summary>
    ''' Test for whole procedure of creating directory + creating link share + update link share + remove share and directory
    ''' </summary>
    <Test> Public Sub CreateUploadLinkForCollectionAndCleanup()
        Const RemoteTestFolderName As String = "ZZZ_UnitTest_CM.Dms.CenterDevice_TempUploadLinkDir"

        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        If DmsProvider.SupportsSharingSetup = False Then Assert.Ignore("Sharing setup not supported by provider")

        Dim CenterDeviceProvider As CompuMaster.Dms.Providers.CenterDeviceDmsProviderBase = CType(DmsProvider, CompuMaster.Dms.Providers.CenterDeviceDmsProviderBase)

        Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderName, DirectoryTypes.Collection)
        Dim RemoteDirItem As CompuMaster.Dms.Data.DmsResourceItem = DmsProvider.ListRemoteItem(RemoteTestFolderName)
        Assert.NotNull(RemoteDirItem.FullName)
        Assert.IsNotEmpty(RemoteDirItem.FullName)
        Assert.AreEqual(Dms.Data.DmsResourceItem.ItemTypes.Collection, RemoteDirItem.ItemType)
        Assert.AreEqual(RemoteTestFolderName, RemoteDirItem.FullName)

        Dim ShareLink As New Data.DmsLink(RemoteDirItem, DmsProvider) With
            {
            .AllowUpload = True,
            .MaxUploads = 4000,
            .MaxBytes = Integer.MaxValue,
            .Password = Guid.NewGuid.ToString("n"),
            .Name = "UnitTest_UploadLinkShare_" & RemoteTestFolderName
            }

        ShareLink = CenterDeviceProvider.CreateLink(RemoteDirItem, ShareLink)

        Assert.NotNull(ShareLink.ID)
        Assert.IsNotEmpty(ShareLink.ID)
        Assert.AreEqual(ShareLink.ID, CenterDeviceProvider.IOClient.GetUploadLink(ShareLink.ID).Id)

        Me.RemoveRemoteTestFolder(RemoteTestFolderName, DirectoryTypes.Collection, True)

        Assert.Catch(Of CenterDevice.Rest.Exceptions.NotFoundException)(Sub()
                                                                            CenterDeviceProvider.IOClient.GetUploadLink(ShareLink.ID)
                                                                        End Sub)

        Dim AllUploadLinks = CenterDeviceProvider.IOClient.ApiClient.UploadLinks.GetAllUploadLinks(CenterDeviceProvider.IOClient.CurrentAuthenticationContextUserID)
        Dim FoundUploadLink As CenterDevice.Rest.Clients.Link.UploadLink = AllUploadLinks.UploadLinksList.Find(Function(item As CenterDevice.Rest.Clients.Link.UploadLink) As Boolean
                                                                                                                   Return item.Id = ShareLink.ID
                                                                                                               End Function)
        Assert.IsNull(FoundUploadLink)

    End Sub

    <Test> Public Sub CreateDownloadLinkForCollectionAndCleanup()
        Const RemoteTestFolderName As String = "ZZZ_UnitTest_CM.Dms.CenterDevice_TempDownloadLinkDir"

        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        If DmsProvider.SupportsSharingSetup = False Then Assert.Ignore("Sharing setup not supported by provider")

        Dim CenterDeviceProvider As CompuMaster.Dms.Providers.CenterDeviceDmsProviderBase = CType(DmsProvider, CompuMaster.Dms.Providers.CenterDeviceDmsProviderBase)

        'Cleanup existing data from previous runs before start
        Me.RemoveRemoteTestFolder(RemoteTestFolderName, DirectoryTypes.Collection, False)

        Me.CreateRemoteTestFolderIfNotExisting(RemoteTestFolderName, DirectoryTypes.Collection)
        Dim RemoteDirItem As CompuMaster.Dms.Data.DmsResourceItem = DmsProvider.ListRemoteItem(RemoteTestFolderName)
        Assert.NotNull(RemoteDirItem.FullName)
        Assert.IsNotEmpty(RemoteDirItem.FullName)
        Assert.AreEqual(Dms.Data.DmsResourceItem.ItemTypes.Collection, RemoteDirItem.ItemType)
        Assert.AreEqual(RemoteTestFolderName, RemoteDirItem.FullName)

        Dim ShareLink As New Data.DmsLink(RemoteDirItem, DmsProvider) With
            {
            .AllowView = True,
            .AllowDownload = True,
            .MaxDownloads = 4000,
            .MaxBytes = Integer.MaxValue,
            .Password = Guid.NewGuid.ToString("n")
            }

        ShareLink = CenterDeviceProvider.CreateLink(RemoteDirItem, ShareLink)

        Assert.NotNull(ShareLink.ID)
        Assert.IsNotEmpty(ShareLink.ID)
        Assert.AreEqual(ShareLink.ID, CenterDeviceProvider.IOClient.GetLink(ShareLink.ID).Id)

        Me.RemoveRemoteTestFolder(RemoteTestFolderName, DirectoryTypes.Collection, True)

        Assert.Catch(Of CenterDevice.Rest.Exceptions.NotFoundException)(Sub()
                                                                            CenterDeviceProvider.IOClient.GetLink(ShareLink.ID)
                                                                        End Sub)

    End Sub

End Class