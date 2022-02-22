﻿Option Explicit On
Option Strict On

Imports NUnit.Framework
Imports CompuMaster.Dms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Public MustInherit Class BaseDmsProviderTestBase

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
        Assert.NotZero(Items.Count)
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

        Items = DmsProvider.ListAllFolderItems(DmsProvider.BrowseInRootFolderName)
        For Each MyItem As DmsResourceItem In Items
            System.Console.WriteLine(MyItem)
            Assert.AreEqual("", MyItem.Collection)
            Assert.AreEqual("", MyItem.Folder)
            Assert.AreEqual(DmsResourceItem.ItemTypes.Folder, MyItem.ItemType)
        Next
        Assert.NotZero(Items.Count)
        System.Console.WriteLine("---")

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

    <Test> Public Overridable Sub ListAllCollectionItems()
        Dim DmsProvider As CompuMaster.Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        Dim Items As List(Of DmsResourceItem) = DmsProvider.ListAllCollectionItems(DmsProvider.BrowseInRootFolderName)
        For Each MyItem As DmsResourceItem In Items
            System.Console.WriteLine(MyItem)
        Next
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
        Assert.NotZero(Items.Count)
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
                Assert.AreEqual(Me.FolderNameWithoutTrailingDirectorySeparator(DmsProvider, RemoteFolder), MyItem.Folder)
                Assert.IsNotEmpty(MyItem.Name)
            Next
            Assert.NotZero(Items.Count, "RemoteFoldersMustExist for " & RemoteFolder & ": Items.Count must be not 0")
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
            Assert.AreEqual(Me.RemoteTestItemFolderName(DmsProvider, Me.FolderNameWithoutTrailingDirectorySeparator(DmsProvider, RemoteFolder)), Item.Folder)
            Assert.AreEqual(Me.RemoteTestItemFileName(DmsProvider, Me.FolderNameWithoutTrailingDirectorySeparator(DmsProvider, RemoteFolder)), Item.Name)
            System.Console.WriteLine("---")
        Next

        For Each RemoteFile As String In Me.RemoteFilesMustExist
            Item = DmsProvider.ListRemoteItem(RemoteFile)
            System.Console.WriteLine(Item)
            Assert.NotNull(Item)
            Assert.AreEqual(DmsResourceItem.ItemTypes.File, Item.ItemType)
            Assert.AreEqual(Me.RemoteTestItemFolderName(DmsProvider, Me.FolderNameWithoutTrailingDirectorySeparator(DmsProvider, RemoteFile)), Item.Folder)
            Assert.AreEqual(Me.RemoteTestItemFileName(DmsProvider, Me.FolderNameWithoutTrailingDirectorySeparator(DmsProvider, RemoteFile)), Item.Name)
            System.Console.WriteLine("---")
        Next

        For Each RemoteItem As String In Me.RemoteItemsMustNotExist
            Item = DmsProvider.ListRemoteItem("not-found")
            System.Console.WriteLine(Item)
            Assert.Null(Item)
        Next

    End Sub

#Disable Warning CA1819 ' Properties should not return arrays
#Disable Warning CA1825 ' Avoid zero-length array allocations.
    Public Overridable ReadOnly Property RemoteFilesMustExist As String() = New String() {}
    Public Overridable ReadOnly Property RemoteFoldersMustExist As String() = New String() {}
    Public Overridable ReadOnly Property RemoteCollectionsMustExist As String() = New String() {}
    Public Overridable ReadOnly Property RemoteItemsMustNotExist As String() = New String() {}
    Public Overridable ReadOnly Property RemoteFoldersWithFiles As String() = New String() {}
    Public Overridable ReadOnly Property RemoteFoldersWithSubFolders As String() = New String() {}
    Public Overridable ReadOnly Property DownloadTestFilesText As KeyValuePair(Of String, String)() = New KeyValuePair(Of String, String)() {}
    Public Overridable ReadOnly Property DownloadTestFilesBinary As KeyValuePair(Of String, Byte())() = New KeyValuePair(Of String, Byte())() {}
    Public Overridable ReadOnly Property UploadTestFilesAndCleanupAgainText As KeyValuePair(Of String, String)() = New KeyValuePair(Of String, String)() {}
    Public Overridable ReadOnly Property UploadTestFilesAndCleanupAgainBinary As KeyValuePair(Of String, Byte())() = New KeyValuePair(Of String, Byte())() {}
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

    Protected OriginalSslValidateCallback As System.Net.Security.RemoteCertificateValidationCallback = System.Net.ServicePointManager.ServerCertificateValidationCallback

    Protected Function AcceptAllCertifications(ByVal sender As Object, ByVal certification As System.Security.Cryptography.X509Certificates.X509Certificate, ByVal chain As System.Security.Cryptography.X509Certificates.X509Chain, ByVal sslPolicyErrors As System.Net.Security.SslPolicyErrors) As Boolean
        Return True
    End Function

End Class

