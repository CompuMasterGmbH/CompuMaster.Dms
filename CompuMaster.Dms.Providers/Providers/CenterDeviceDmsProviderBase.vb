﻿Option Explicit On
Option Strict On

Imports System.IO
Imports System.Runtime.CompilerServices
Imports CenterDevice.Rest.Clients.Common
Imports CenterDevice.Rest.Clients.Groups
Imports CenterDevice.Rest.Clients.Link
Imports CenterDevice.Rest.Clients.User
Imports CenterDevice.Rest.Exceptions
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports CompuMaster.Scopevisio.OpenApi.Model

Namespace Providers

    ''' <summary>
    ''' Common Center Device REST API (incl. Scopevisio Teamwork API) implementations
    ''' </summary>
    ''' <inheritdoc path="https://public.centerdevice.de/02bf3cfd-06c6-4d43-9cd4-3c18aab0020a"/>
    Public MustInherit Class CenterDeviceDmsProviderBase
        Inherits Providers.BaseDmsProvider

        Public Overrides ReadOnly Property WebApiUrlCustomization As UrlCustomizationType
            Get
                Return UrlCustomizationType.WebApiUrlNotCustomizable
            End Get
        End Property

        Protected Overrides Function CustomizedWebApiUrl(loginCredentials As BaseDmsLoginCredentials) As String
            Return Me.WebApiDefaultUrl
        End Function

        'Protected Property ApiToken As TokenResponse
        'Protected Property CenterDeviceClient As CenterDevice.Rest.Clients.CenterDeviceClient
        Protected Friend Property IOClient As CenterDevice.IO.IOClientBase

        Public Overrides ReadOnly Property BrowseInRootFolderName() As String = "/"

        Public Overrides Function FindCollectionById(id As String) As DmsResourceItem
            Return Me.CreateDmsResourceItem(New CenterDevice.IO.DirectoryInfo(Me.IOClient, Nothing, Me.IOClient.ApiClient.Collection.GetCollection(Me.IOClient.CurrentAuthenticationContextUserID, id)))
        End Function

        Public Overrides Function FindFolderById(id As String) As DmsResourceItem
            Return Me.CreateDmsResourceItem(New CenterDevice.IO.DirectoryInfo(Me.IOClient, Nothing, Me.IOClient.ApiClient.Folder.GetFolder(Me.IOClient.CurrentAuthenticationContextUserID, id, Nothing)))
        End Function

        Public Overrides Function FindFileById(id As String) As DmsResourceItem
            Return Me.CreateDmsResourceItem(New CenterDevice.IO.FileInfo(Me.IOClient, Nothing, Me.IOClient.ApiClient.Document.GetDocumentMetadata(Me.IOClient.CurrentAuthenticationContextUserID, id)))
        End Function

        Public Overrides Function ListRemoteItem(remotePath As String) As DmsResourceItem
            Dim ParentRemoteDirName As String = Me.ParentDirectoryPath(remotePath)
            Dim ParentRemoteDir As CenterDevice.IO.DirectoryInfo
            If ParentRemoteDirName = Nothing Then
                ParentRemoteDir = Me.IOClient.RootDirectory
            Else
                Try
                    ParentRemoteDir = Me.IOClient.RootDirectory.OpenDirectoryPath(ParentRemoteDirName)
                Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                    Return Nothing
                End Try
            End If
            Dim RemoteFileName As String = Me.ItemName(remotePath)
            Dim FoundFileItem As CenterDevice.IO.FileInfo = ParentRemoteDir.TryGetFile(RemoteFileName)
            Dim FoundDirItem As CenterDevice.IO.DirectoryInfo = Nothing
            If FoundFileItem Is Nothing Then
                Try
                    FoundDirItem = Me.IOClient.RootDirectory.OpenDirectoryPath(remotePath)
                Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                    FoundDirItem = Nothing
                End Try
            End If
            If FoundDirItem IsNot Nothing Then
                Return Me.CreateDmsResourceItem(FoundDirItem)
            ElseIf FoundFileItem IsNot Nothing Then
                Return Me.CreateDmsResourceItem(FoundFileItem)
            Else
                Return Nothing
            End If
        End Function

        Public Overrides Sub ResetCachesForRemoteItems(remoteFolderPath As String, searchType As SearchItemType)
            Dim RemoteDir As CenterDevice.IO.DirectoryInfo = Me.IOClient.RootDirectory.OpenDirectoryPath(remoteFolderPath)
            Select Case searchType
                Case SearchItemType.AllItems
                    RemoteDir.ResetDirectoriesCache()
                    RemoteDir.ResetFilesCache()
                Case SearchItemType.Folders, SearchItemType.Collections
                    RemoteDir.ResetDirectoriesCache()
                Case SearchItemType.Files
                    RemoteDir.ResetFilesCache()
                Case Else
                    Throw New ArgumentOutOfRangeException(NameOf(searchType))
            End Select
        End Sub

        ''' <summary>
        ''' Reset cache of parent directory of remoteItem
        ''' </summary>
        ''' <param name="remoteItem"></param>
        Protected Sub ResetParentDirectoryCache(remoteItem As DmsResourceItem)
            Dim IsFileItem As Boolean
            Select Case remoteItem.ItemType
                Case DmsResourceItem.ItemTypes.Root, DmsResourceItem.ItemTypes.Collection, DmsResourceItem.ItemTypes.Folder
                Case DmsResourceItem.ItemTypes.File
                    IsFileItem = True
                Case Else
                    Throw New NotImplementedException(remoteItem.ItemType.ToString)
            End Select

            Dim ParentRemoteDirName As String = Me.ParentDirectoryPath(remoteItem.FullName)
            Dim ParentRemoteDir As CenterDevice.IO.DirectoryInfo
            Try
                ParentRemoteDir = Me.IOClient.RootDirectory.OpenDirectoryPath(ParentRemoteDirName)
            Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(ParentRemoteDirName, ex)
            End Try
            If IsFileItem Then
                ParentRemoteDir.ResetFilesCache()
            Else
                ParentRemoteDir.ResetDirectoriesCache()
            End If
        End Sub

        Public Overrides Function ListAllRemoteItems(remoteFolderPath As String, searchType As SearchItemType) As List(Of DmsResourceItem)
            Dim RemoteDir As CenterDevice.IO.DirectoryInfo = Me.IOClient.RootDirectory.OpenDirectoryPath(remoteFolderPath)
            Dim Result As New List(Of DmsResourceItem)
            Select Case searchType
                Case SearchItemType.Folders, SearchItemType.Collections, SearchItemType.AllItems
                    Dim SubFolders As CenterDevice.IO.DirectoryInfo() = RemoteDir.GetDirectories
                    If SubFolders IsNot Nothing Then
                        For Each SubFolder In SubFolders
                            Select Case searchType
                                Case SearchItemType.Collections
                                    If SubFolder.Type = CenterDevice.IO.DirectoryInfo.DirectoryType.Collection Then
                                        Result.Add(Me.CreateDmsResourceItem(SubFolder))
                                    End If
                                Case SearchItemType.Folders
                                    If SubFolder.Type = CenterDevice.IO.DirectoryInfo.DirectoryType.Folder Then
                                        Result.Add(Me.CreateDmsResourceItem(SubFolder))
                                    End If
                                Case SearchItemType.AllItems
                                    Result.Add(Me.CreateDmsResourceItem(SubFolder))
                            End Select
                        Next
                    End If
            End Select
            Select Case searchType
                Case SearchItemType.Files, SearchItemType.AllItems
                    Dim Files As CenterDevice.IO.FileInfo() = RemoteDir.GetFiles
                    For Each Item In Files
                        Result.Add(Me.CreateDmsResourceItem(Item))
                    Next
            End Select
            Return Result
        End Function

        Public Overrides Function ListAllFolderNames(remoteFolderPath As String) As List(Of String)
            Dim RemoteDir As CenterDevice.IO.DirectoryInfo = Me.IOClient.RootDirectory.OpenDirectoryPath(remoteFolderPath)
            Dim Result As New List(Of String)
            Dim SubFolders As CenterDevice.IO.DirectoryInfo() = RemoteDir.GetDirectories
            For Each SubFolder In SubFolders
                Result.Add(SubFolder.Name)
            Next
            Return Result
        End Function

        Public Overrides Function ListAllFileNames(remoteFolderPath As String) As List(Of String)
            Dim RemoteDir As CenterDevice.IO.DirectoryInfo = Me.IOClient.RootDirectory.OpenDirectoryPath(remoteFolderPath)
            Dim Result As New List(Of String)
            Dim FileItems As CenterDevice.IO.FileInfo() = RemoteDir.GetFiles
            For Each FileItem In FileItems
                Result.Add(FileItem.FileName)
            Next
            Return Result
        End Function

        Public Overrides Sub UploadFile(remoteFilePath As String, localFilePath As String)
            Dim ParentRemoteDirName As String = Me.ParentDirectoryPath(remoteFilePath)
            Dim ParentRemoteDir As CenterDevice.IO.DirectoryInfo
            Try
                ParentRemoteDir = Me.IOClient.RootDirectory.OpenDirectoryPath(ParentRemoteDirName)
            Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(ParentRemoteDirName, ex)
            End Try
            Dim RemoteFileName As String = Me.ItemName(remoteFilePath)
            Dim FoundFileItem As CenterDevice.IO.FileInfo = ParentRemoteDir.TryGetFile(RemoteFileName)
            If FoundFileItem IsNot Nothing Then
                'File exists, upload new file version
                FoundFileItem.UploadNewVersion(localFilePath)
            Else
                'Upload new file
                ParentRemoteDir.UploadAndCreateNewFile(localFilePath, RemoteFileName)
            End If
            ParentRemoteDir.ResetFilesCache()
        End Sub

        Public Overrides Sub UploadFile(remoteFilePath As String, binaryData As Func(Of System.IO.Stream))
            Dim ParentRemoteDirName As String = Me.ParentDirectoryPath(remoteFilePath)
            Dim ParentRemoteDir As CenterDevice.IO.DirectoryInfo
            Try
                ParentRemoteDir = Me.IOClient.RootDirectory.OpenDirectoryPath(ParentRemoteDirName)
            Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(ParentRemoteDirName, ex)
            End Try
            Dim RemoteFileName As String = Me.ItemName(remoteFilePath)
            Dim FoundFileItem As CenterDevice.IO.FileInfo = ParentRemoteDir.TryGetFile(RemoteFileName)
            If FoundFileItem IsNot Nothing Then
                'File exists, upload new file version
                FoundFileItem.UploadNewVersion(binaryData)
            Else
                'Upload new file
                ParentRemoteDir.UploadAndCreateNewFile(binaryData, RemoteFileName)
            End If
            ParentRemoteDir.ResetFilesCache()
        End Sub

        Public Overrides Sub DownloadFile(remoteFilePath As String, localFilePath As String, lastModificationDateOnLocalTime As DateTime?)
            Dim ParentRemoteDirName As String = Me.ParentDirectoryPath(remoteFilePath)
            Dim ParentRemoteDir As CenterDevice.IO.DirectoryInfo
            Try
                ParentRemoteDir = Me.IOClient.RootDirectory.OpenDirectoryPath(ParentRemoteDirName)
            Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(ParentRemoteDirName, ex)
            End Try
            Dim RemoteFileName As String = Me.ItemName(remoteFilePath)
            Dim FoundFileItem As CenterDevice.IO.FileInfo = ParentRemoteDir.GetFile(RemoteFileName)
            FoundFileItem.Download(localFilePath)
            If lastModificationDateOnLocalTime.HasValue AndAlso lastModificationDateOnLocalTime.Value <> Nothing Then System.IO.File.SetLastWriteTime(localFilePath, lastModificationDateOnLocalTime.Value)
        End Sub

        Protected Enum RessourceNotFoundHandling As Byte
            ReturnWithNullIfItemOrParentDirectoryIsNotFound = 0
            ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound = 1
        End Enum

        ''' <summary>
        ''' Get directory item
        ''' </summary>
        ''' <param name="remoteDirectoryPath"></param>
        ''' <param name="handlingIfNotFound"></param>
        ''' <returns></returns>
        Protected Function GetDirectoryItem(remoteDirectoryPath As String, handlingIfNotFound As RessourceNotFoundHandling) As CenterDevice.IO.DirectoryInfo
            Dim FoundDirItem As CenterDevice.IO.DirectoryInfo
            Try
                FoundDirItem = Me.IOClient.RootDirectory.OpenDirectoryPath(remoteDirectoryPath)
            Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                Select Case handlingIfNotFound
                    Case RessourceNotFoundHandling.ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound
                        Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(remoteDirectoryPath, ex)
                    Case RessourceNotFoundHandling.ReturnWithNullIfItemOrParentDirectoryIsNotFound
                        FoundDirItem = Nothing
                    Case Else
                        Throw New ArgumentOutOfRangeException(NameOf(handlingIfNotFound))
                End Select
            End Try
            Return FoundDirItem
        End Function

        ''' <summary>
        ''' Get a file item
        ''' </summary>
        ''' <param name="remoteFilePath"></param>
        ''' <param name="handlingIfNotFound"></param>
        ''' <returns></returns>
        Protected Function GetFileItem(remoteFilePath As String, handlingIfNotFound As RessourceNotFoundHandling) As CenterDevice.IO.FileInfo
            Dim ParentRemoteDirName As String = Me.ParentDirectoryPath(remoteFilePath)
            Dim ParentRemoteDir As CenterDevice.IO.DirectoryInfo = GetDirectoryItem(ParentRemoteDirName, handlingIfNotFound)
            Dim RemoteFileName As String = Me.ItemName(remoteFilePath)
            Return Me.GetFileItem(ParentRemoteDir, RemoteFileName, handlingIfNotFound)
        End Function

        ''' <summary>
        ''' Get a file item
        ''' </summary>
        ''' <param name="parentRemoteDir"></param>
        ''' <param name="remoteFileName"></param>
        ''' <param name="handlingIfNotFound"></param>
        ''' <returns></returns>
        Protected Function GetFileItem(parentRemoteDir As CenterDevice.IO.DirectoryInfo, remoteFileName As String, handlingIfNotFound As RessourceNotFoundHandling) As CenterDevice.IO.FileInfo
            Dim FoundFileItem As CenterDevice.IO.FileInfo
            Try
                FoundFileItem = parentRemoteDir.GetFile(remoteFileName)
            Catch ex As CenterDevice.Model.Exceptions.FileNotFoundException
                Select Case handlingIfNotFound
                    Case RessourceNotFoundHandling.ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound
                        Throw New CompuMaster.Dms.Data.FileNotFoundException(Me.CombinePath(parentRemoteDir.FullName, remoteFileName), ex)
                    Case RessourceNotFoundHandling.ReturnWithNullIfItemOrParentDirectoryIsNotFound
                        FoundFileItem = Nothing
                    Case Else
                        Throw New ArgumentOutOfRangeException(NameOf(handlingIfNotFound))
                End Select
            End Try
            Return FoundFileItem
        End Function

        Protected Overrides Sub CopyFileItem(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?)
            Dim FoundSourceFileItem As CenterDevice.IO.FileInfo = Me.GetFileItem(remoteSourcePath, RessourceNotFoundHandling.ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound)
            Dim ParentRemoteDestinationDirName As String = Me.ParentDirectoryPath(remoteDestinationPath)
            Dim ParentRemoteDestinationDir As CenterDevice.IO.DirectoryInfo = Me.GetDirectoryItem(ParentRemoteDestinationDirName, RessourceNotFoundHandling.ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound)
            Dim RemoteDestinationFileName As String = Me.ItemName(remoteDestinationPath)
            Dim FoundDestinationFileItem As CenterDevice.IO.FileInfo = Me.GetFileItem(ParentRemoteDestinationDir, RemoteDestinationFileName, RessourceNotFoundHandling.ReturnWithNullIfItemOrParentDirectoryIsNotFound)

            If FoundDestinationFileItem IsNot Nothing AndAlso allowOverwrite Then
                'destination file already exists
                'delete remote destination file
                FoundDestinationFileItem.Delete()
            End If
            ParentRemoteDestinationDir.AddCopy(FoundSourceFileItem, RemoteDestinationFileName)
        End Sub

        Protected Overrides Async Function CopyFileItemAsync(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?) As Task
            Await Task.Run(Sub()
                               CopyFileItem(remoteSourcePath, remoteDestinationPath, allowOverwrite)
                           End Sub)
        End Function

        Protected Overrides Sub CopyDirectoryItem(remoteSourcePath As String, remoteDestinationPath As String)
            Dim FoundSourceDirItem As CenterDevice.IO.DirectoryInfo = Me.GetDirectoryItem(remoteSourcePath, RessourceNotFoundHandling.ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound)
            Dim ParentRemoteDestinationDirName As String = Me.ParentDirectoryPath(remoteDestinationPath)
            Dim ParentRemoteDestinationDir As CenterDevice.IO.DirectoryInfo = Me.GetDirectoryItem(ParentRemoteDestinationDirName, RessourceNotFoundHandling.ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound)
            Dim RemoteDestinationSubDirName As String = Me.ItemName(remoteDestinationPath)
            Dim FoundDestinationFileItem As CenterDevice.IO.FileInfo = Me.GetFileItem(ParentRemoteDestinationDir, RemoteDestinationSubDirName, RessourceNotFoundHandling.ReturnWithNullIfItemOrParentDirectoryIsNotFound)

            If FoundDestinationFileItem IsNot Nothing Then
                Throw New DirectoryAlreadyExistsException(remoteDestinationPath)
            Else
                ParentRemoteDestinationDir.AddCopy(FoundSourceDirItem, RemoteDestinationSubDirName)
            End If
        End Sub

        Protected Overrides Async Function CopyDirectoryItemAsync(remoteSourcePath As String, remoteDestinationPath As String) As Task
            Await Task.Run(Sub()
                               CopyDirectoryItem(remoteSourcePath, remoteDestinationPath)
                           End Sub)
        End Function

        Protected Overrides Sub MoveFileItem(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?)
            Dim ParentRemoteSourceDirName As String = Me.ParentDirectoryPath(remoteSourcePath)
            Dim FoundSourceFileItem As CenterDevice.IO.FileInfo = Me.GetFileItem(remoteSourcePath, RessourceNotFoundHandling.ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound)
            Dim ParentRemoteDestinationDirName As String = Me.ParentDirectoryPath(remoteDestinationPath)
            Dim ParentRemoteDestinationDir As CenterDevice.IO.DirectoryInfo = Me.GetDirectoryItem(ParentRemoteDestinationDirName, RessourceNotFoundHandling.ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound)
            Dim RemoteDestinationFileName As String = Me.ItemName(remoteDestinationPath)
            Dim FoundDestinationFileItem As CenterDevice.IO.FileInfo = Me.GetFileItem(ParentRemoteDestinationDir, RemoteDestinationFileName, RessourceNotFoundHandling.ReturnWithNullIfItemOrParentDirectoryIsNotFound)

            'check destination
            If FoundDestinationFileItem IsNot Nothing Then
                'target already exists
                If allowOverwrite = False Then
                    Throw New CompuMaster.Dms.Data.FileAlreadyExistsException(remoteDestinationPath)
                Else
                    FoundDestinationFileItem.Delete()
                End If
            End If

            'move or rename file
            If ParentRemoteSourceDirName = ParentRemoteDestinationDirName Then
                'rename the file
                FoundSourceFileItem.Rename(RemoteDestinationFileName)
            ElseIf FoundSourceFileItem.FileName = RemoteDestinationFileName Then
                'move of file into new directory
                FoundSourceFileItem.Move(ParentRemoteDestinationDir)
            Else
                'move of file into new directory + rename file
                'CenterDevice doesn't provide a 1-stop-process -> workaround required with 2 steps

                'FOLLOWING CODE FOR 3-STOPS-SHOP
                Dim GuidDirName As String = Guid.NewGuid.ToString()
                Dim GuidRemotePath1 As String = Me.CombinePath(ParentRemoteSourceDirName, GuidDirName)
                Dim GuidRemotePath2 As String = Me.CombinePath(ParentRemoteDestinationDirName, GuidDirName)
                FoundSourceFileItem.Rename(GuidDirName) 'Rename in source dir to temp name
                Try
                    FoundSourceFileItem.Move(ParentRemoteDestinationDir) 'Move to destination dir
                Catch ex As Exception
                    'Rollback transaction: rename directory back to origin dir name
                    FoundSourceFileItem.Rename(Me.ItemName(remoteSourcePath)) 'Rename in source dir to temp name
                    Throw New FileActionFailedException("move", remoteSourcePath, remoteDestinationPath)
                End Try
                FoundSourceFileItem.Rename(RemoteDestinationFileName) 'Rename in destination dir to final name

                'FOLLOWING CODE FOR 1-STOPS-SHOP
                'FoundSourceFileItem.Move(ParentRemoteDestinationDir, RemoteDestinationFileName)
            End If
        End Sub

        Protected Overrides Sub MoveDirectoryItem(remoteSourcePath As String, remoteDestinationPath As String)
            Dim ParentRemoteSourceDirName As String = Me.ParentDirectoryPath(remoteSourcePath)
            Dim FoundSourceDirItem As CenterDevice.IO.DirectoryInfo = Me.GetDirectoryItem(remoteSourcePath, RessourceNotFoundHandling.ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound)
            Dim ParentRemoteDestinationDirName As String = Me.ParentDirectoryPath(remoteDestinationPath)
            Dim ParentRemoteDestinationDir As CenterDevice.IO.DirectoryInfo = Me.GetDirectoryItem(ParentRemoteDestinationDirName, RessourceNotFoundHandling.ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound)
            Dim RemoteDestinationDirName As String = Me.ItemName(remoteDestinationPath)

            If ParentRemoteDestinationDir.DirectoryExists(RemoteDestinationDirName) Then
                Throw New Data.DirectoryAlreadyExistsException(remoteDestinationPath)
            ElseIf ParentRemoteSourceDirName = ParentRemoteDestinationDirName Then
                'rename the directory
                FoundSourceDirItem.Rename(RemoteDestinationDirName)
            ElseIf FoundSourceDirItem.Name = RemoteDestinationDirName Then
                'move the directory, keep dir name
                FoundSourceDirItem.Move(ParentRemoteDestinationDir)
            Else
                'move the directory + rename dir
                'CenterDevice doesn't provide a 1-stop-process -> workaround required with 2 steps

                'FOLLOWING CODE FOR 3-STOPS-SHOP
                Dim GuidDirName As String = Guid.NewGuid.ToString()
                Dim GuidRemotePath1 As String = Me.CombinePath(ParentRemoteSourceDirName, GuidDirName)
                Dim GuidRemotePath2 As String = Me.CombinePath(ParentRemoteDestinationDirName, GuidDirName)
                FoundSourceDirItem.Rename(GuidDirName) 'Rename in source dir to temp name
                Try
                    FoundSourceDirItem.Move(ParentRemoteDestinationDir) 'Move to destination dir
                Catch ex As Exception
                    'Rollback transaction: rename directory back to origin dir name
                    FoundSourceDirItem.Rename(Me.ItemName(remoteSourcePath)) 'Rename in source dir to temp name
                    Throw New DirectoryActionFailedException("move", remoteSourcePath, remoteDestinationPath)
                End Try
                FoundSourceDirItem.Rename(RemoteDestinationDirName) 'Rename in destination dir to final name

                'FOLLOWING CODE FOR 2-STOPS-SHOP
                'Dim Trial1RemotePath As String = Me.CombinePath(ParentRemoteDestinationDirName, FoundSourceDirItem.Name)
                'Select Case Me.RemoteItemExistsAs(Trial1RemotePath)
                '    Case DmsResourceItem.FoundItemType.Collection, DmsResourceItem.FoundItemType.Folder
                '        'FoundSourceDirItem.Rename(GuidDirName) 'Rename in source dir to temp name
                '        'FoundSourceDirItem.Move(ParentRemoteDestinationDir) 'Move to destination dir
                '    Case Else
                '        Dim Trial2RemotePath As String = Me.CombinePath(ParentRemoteSourceDirName, RemoteDestinationDirName)
                '        Select Case Me.RemoteItemExistsAs(Trial2RemotePath)
                '            Case DmsResourceItem.FoundItemType.Collection, DmsResourceItem.FoundItemType.Folder
                '                'FoundSourceDirItem.Rename(GuidDirName) 'Rename in source dir to temp name
                '                'FoundSourceDirItem.Move(ParentRemoteDestinationDir) 'Move to destination dir
                '            Case Else
                '                Dim GuidDirName As String = Guid.NewGuid.ToString()
                '                Dim GuidRemotePath1 As String = Me.CombinePath(ParentRemoteSourceDirName, GuidDirName)
                '                Dim GuidRemotePath2 As String = Me.CombinePath(ParentRemoteDestinationDirName, GuidDirName)
                '                FoundSourceDirItem.Rename(GuidDirName) 'Rename in source dir to temp name
                '                FoundSourceDirItem.Move(ParentRemoteDestinationDir) 'Move to destination dir
                '                FoundSourceDirItem.Rename(RemoteDestinationDirName) 'Rename in destination dir to final name
                '        End Select
                'End Select
            End If
        End Sub

        Private Function FindUploadLinkForCollection(collectionID As String) As CenterDevice.Rest.Clients.Link.UploadLink
            If collectionID <> Nothing AndAlso AllUploadLinks.UploadLinksList.ConvertAll(Of String)(Function(item) item.Collection).Contains(collectionID) Then
                Return AllUploadLinks.UploadLinksList.Find(Function(item) item.Collection = collectionID)
            Else
                Return Nothing
            End If
        End Function

        Public Overrides Sub DeleteRemoteItem(remoteFilePath As String)
            Dim ParentRemoteDirName As String = Me.ParentDirectoryPath(remoteFilePath)
            Dim ParentRemoteDir As CenterDevice.IO.DirectoryInfo
            Try
                ParentRemoteDir = Me.IOClient.RootDirectory.OpenDirectoryPath(ParentRemoteDirName)
            Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(ParentRemoteDirName, ex)
            End Try
            Dim RemoteFileName As String = Me.ItemName(remoteFilePath)
            If remoteFilePath.EndsWith(Me.DirectorySeparator) = False AndAlso ParentRemoteDir.FileExists(RemoteFileName) Then
                Dim FoundFileItem As CenterDevice.IO.FileInfo = ParentRemoteDir.GetFile(RemoteFileName)
                If FoundFileItem IsNot Nothing Then
                    FoundFileItem.Delete()
                End If
                ParentRemoteDir.ResetFilesCache()
            Else
                Dim FoundDirItem As CenterDevice.IO.DirectoryInfo
                Try
                    FoundDirItem = ParentRemoteDir.GetDirectory(RemoteFileName)
                Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                    Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(remoteFilePath, ex)
                End Try
                If FoundDirItem.Type = CenterDevice.IO.DirectoryInfo.DirectoryType.Collection Then
                    Dim RelatedUploadLink As CenterDevice.Rest.Clients.Link.UploadLink = FindUploadLinkForCollection(FoundDirItem.CollectionID)
                    If RelatedUploadLink IsNot Nothing Then
                        Me.IOClient.ApiClient.UploadLink.DeleteLink(Me.IOClient.CurrentAuthenticationContextUserID, RelatedUploadLink.Id)
                    End If
                    _AllUploadLinks = Nothing 'Reset cache
                End If
                If FoundDirItem IsNot Nothing Then
                    FoundDirItem.Delete()
                End If
                ParentRemoteDir.ResetDirectoriesCache()
            End If
        End Sub

        Public Overrides Sub DeleteRemoteItem(remoteItem As DmsResourceItem)
            Select Case remoteItem.ItemType
                Case DmsResourceItem.ItemTypes.Root
                    Throw New DmsUserErrorMessageException("Root folder can't be deleted")
                Case DmsResourceItem.ItemTypes.Collection
                    If remoteItem.ExtendedInfosHasLinks Then
                        For Each link As DmsLink In remoteItem.ExtendedInfosLinks
                            If link.AllowUpload Then
                                Me.IOClient.ApiClient.UploadLink.DeleteLink(Me.IOClient.CurrentAuthenticationContextUserID, link.ID)
                                _AllUploadLinks = Nothing 'Reset cache
                            End If
                        Next
                    End If
                    Me.IOClient.ApiClient.Collection.DeleteCollection(Me.IOClient.CurrentAuthenticationContextUserID, remoteItem.ExtendedInfosCollectionID)
                Case DmsResourceItem.ItemTypes.Folder
                    Me.IOClient.ApiClient.Folder.DeleteFolder(Me.IOClient.CurrentAuthenticationContextUserID, remoteItem.ExtendedInfosFolderID)
                Case DmsResourceItem.ItemTypes.File
                    Me.IOClient.ApiClient.Document.DeleteDocument(Me.IOClient.CurrentAuthenticationContextUserID, remoteItem.ExtendedInfosFileID)
                Case Else
                    Throw New NotImplementedException(remoteItem.ItemType.ToString)
            End Select
            Me.ResetParentDirectoryCache(remoteItem)
        End Sub

        Public Overrides Sub CreateFolder(remoteFilePath As String)
            Dim ioExceptionMessage As String = "CreateFolder failed: " & remoteFilePath
            Try
                Dim NewChildDirName As String = Me.ItemName(remoteFilePath)
                Dim ParentRemoteDirName As String = Me.ParentDirectoryPath(remoteFilePath)
                Dim ParentRemoteDir As CenterDevice.IO.DirectoryInfo
                Try
                    ParentRemoteDir = Me.IOClient.RootDirectory.OpenDirectoryPath(ParentRemoteDirName)
                Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                    Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(ParentRemoteDirName, ex)
                End Try
                Try
                    ParentRemoteDir.CreateDirectory(NewChildDirName, CenterDevice.IO.DirectoryInfo.DirectoryType.Folder)
                Catch ex As CenterDevice.Rest.Exceptions.RestClientException
                    If ex.ErrorResponse Is Nothing Then
                        Throw New System.IO.IOException(ioExceptionMessage, ex)
                    Else
                        Throw New System.IO.IOException(ioExceptionMessage, New ResponseStatusCodeException(ex.ErrorResponse.Code, ex.ErrorResponse.Message, ex))
                    End If
                End Try
                ParentRemoteDir.ResetDirectoriesCache()
            Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(ex.RemotePath)
            End Try
        End Sub

        Public Overrides Sub CreateDirectory(remoteDirectoryName As String)
            Dim ioExceptionMessage As String = "CreateDirectory failed: " & remoteDirectoryName
            Try
                Dim NewChildDirName As String = Me.ItemName(remoteDirectoryName)
                Dim ParentRemoteDirName As String = Me.ParentDirectoryPath(remoteDirectoryName)
                Dim ParentRemoteDir As CenterDevice.IO.DirectoryInfo
                Try
                    ParentRemoteDir = Me.IOClient.RootDirectory.OpenDirectoryPath(ParentRemoteDirName)
                Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                    Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(ParentRemoteDirName, ex)
                End Try
                Try
                    ParentRemoteDir.CreateDirectory(NewChildDirName)
                Catch ex As CenterDevice.Rest.Exceptions.RestClientException
                    If ex.ErrorResponse Is Nothing Then
                        Throw New System.IO.IOException(ioExceptionMessage, ex)
                    Else
                        Throw New System.IO.IOException(ioExceptionMessage, New ResponseStatusCodeException(ex.ErrorResponse.Code, ex.ErrorResponse.Message, ex))
                    End If
                End Try
                ParentRemoteDir.ResetDirectoriesCache()
            Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(ex.RemotePath)
            End Try
        End Sub

        Public Overrides Sub CreateCollection(remoteCollectionName As String)
            Dim ioExceptionMessage As String = "CreateCollection failed: " & remoteCollectionName
            Try
                Dim NewChildDirName As String = Me.ItemName(remoteCollectionName)
                Dim ParentRemoteDirName As String = Me.ParentDirectoryPath(remoteCollectionName)
                If ParentRemoteDirName <> Nothing Then
                    'if this situation wouldn't be catched, then otherwise creation of collection in another parent directory would lead
                    'to a new collection below root directory (instead of the expected parent directory)
                    '=> considered as a bug in CenterDevice API / CenterDevice architecture
                    Throw New NotSupportedException("Collections """ & remoteCollectionName & """ must be located in root folder only, but is in """ & ParentRemoteDirName & """")
                End If
                Dim ParentRemoteDir As CenterDevice.IO.DirectoryInfo
                Try
                    ParentRemoteDir = Me.IOClient.RootDirectory.OpenDirectoryPath(ParentRemoteDirName)
                Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                    Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(ParentRemoteDirName, ex)
                End Try
                Try
                    ParentRemoteDir.CreateDirectory(NewChildDirName, CenterDevice.IO.DirectoryInfo.DirectoryType.Collection)
                Catch ex As CenterDevice.Rest.Exceptions.RestClientException
                    If ex.ErrorResponse Is Nothing Then
                        Throw New System.IO.IOException(ioExceptionMessage, ex)
                    Else
                        Throw New System.IO.IOException(ioExceptionMessage, New ResponseStatusCodeException(ex.ErrorResponse.Code, ex.ErrorResponse.Message, ex))
                    End If
                End Try
                ParentRemoteDir.ResetDirectoriesCache()
            Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(ex.RemotePath)
            End Try
        End Sub

        Public Overrides ReadOnly Property DirectorySeparator As Char
            Get
                Return "/"c
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsCollections As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsSharingSetup As Boolean
            Get
                Return True
            End Get
        End Property

        Friend _AllUploadLinks As CenterDevice.Rest.Clients.Link.UploadLinks
        Private Function AllUploadLinks() As CenterDevice.Rest.Clients.Link.UploadLinks
            If _AllUploadLinks Is Nothing Then
                _AllUploadLinks = Me.IOClient.ApiClient.UploadLinks.GetAllUploadLinks(Me.IOClient.CurrentAuthenticationContextUserID)
            End If
            Return _AllUploadLinks
        End Function

        Private Shared Function DateTimeUtcToLocalTime(value As Date?) As Date?
            If value.HasValue AndAlso Not value.Value = Nothing Then
                Return value.Value.ToLocalTime
            Else
                Return Nothing
            End If
        End Function

        Private Shared Function DateTimeLocalToUtcTime(value As Date?) As Date?
            If value.HasValue AndAlso Not value.Value = Nothing Then
                Return value.Value.ToUniversalTime
            Else
                Return Nothing
            End If
        End Function

        ''' <summary>
        ''' Fill DmsResourceItem from CenterDevice.IO.DirectoryInfo
        ''' </summary>
        ''' <param name="res"></param>
        ''' <returns></returns>
        Private Function CreateDmsResourceItem(res As CenterDevice.IO.DirectoryInfo) As DmsResourceItem
            Dim Result As New DmsResourceItem() With {
                        .Name = res.Name,
                            .CreatedOnLocalTime = Nothing,
                            .LastModificationOnLocalTime = Nothing,
                            .IsHidden = False,
                            .ContentLength = Nothing,
                            .ProviderSpecificHashOrETag = Nothing}
            'Assign additional fields
            If res.IsRootDirectory Then
                Result.ItemType = DmsResourceItem.ItemTypes.Root
            ElseIf res.CollectionID <> Nothing Then
                Result.ItemType = DmsResourceItem.ItemTypes.Collection
            ElseIf res.FolderID <> Nothing Then
                Result.ItemType = DmsResourceItem.ItemTypes.Folder
            Else
                Throw New NotImplementedException("Unknown item type, additional implementation required")
            End If
            Result.FullName = res.FullName
            Result.Name = res.Name
            Result.ExtendedInfosFileID = Nothing
            Result.ExtendedInfosCollectionID = res.AssociatedCollection?.CollectionID
            Result.ExtendedInfosFolderID = res.FolderID
            Result.ExtendedInfosAssignedCollectionID = res.ParentDirectory?.AssociatedCollection?.CollectionID
            Result.ExtendedInfosAssignedFolderID = res.ParentDirectory?.FolderID
            Result.ExtendedInfosOwner = New DmsUser() With {.ID = res.Owner, .Provider = Me, .GetName = AddressOf CenterDeviceDmsProviderBase.DelegatedGetUserName, .GetEMailAddress = AddressOf CenterDeviceDmsProviderBase.DelegatedGetUserEMailAddress}
            Result.ExtendedInfosLinks = New List(Of DmsLink)
            If res.Link <> Nothing Then Result.ExtendedInfosLinks.Add(New DmsLink(Result, res.Link, Me, AddressOf CenterDeviceDmsProviderBase.DelegatedFillLinkDetails))
            If res.CollectionID <> Nothing AndAlso AllUploadLinks.UploadLinksList.ConvertAll(Of String)(Function(item) item.Collection).Contains(res.CollectionID) Then
                Dim UploadLink As CenterDevice.Rest.Clients.Link.UploadLink = AllUploadLinks.UploadLinksList.Find(Function(item) item.Collection = res.CollectionID)
                Dim RefreshableDmsLink As New DmsLink(Result, UploadLink.Id, Me, AddressOf DelegatedFillUploadLinkDetails)
                RefreshableDmsLink.Initialize(UploadLink.Password,
                DateTimeUtcToLocalTime(UploadLink.ExpiryDate),
                CType(Nothing, Long?), UploadLink.MaxDocuments, UploadLink.MaxBytes,
                CType(Nothing, Long?), CType(Nothing, Long?), UploadLink.UploadsMade, UploadLink.UploadedBytes,
                UploadLink.Web, CType(Nothing, String), CType(Nothing, String),
                False, False, False, True, False, False
                )
                Result.ExtendedInfosLinks.Add(RefreshableDmsLink)
            End If
            Result.ExtendedInfosLocks = Nothing
            Result.ExtendedInfosLockedByUser = Nothing
            Result.ExtendedInfosArchivedDateLocalTime = DateTimeUtcToLocalTime(res.ArchivedDate)
            Result.ExtendedInfosVersion = Nothing
            Result.ExtendedInfosVersionDateLocalTime = Nothing
            Result.ExtendedInfosCollisionDetected = False
            Result.ExtendedInfosIsPublicCollection = res.Public
            Result.ExtendedInfosIsAuditing = res.Auditing
            Result.ExtendedInfosIsIntelligent = res.IsIntelligent
            Result.ExtendedInfosGroupSharings = New List(Of DmsShareForGroup)
            If res.Groups IsNot Nothing Then
                Result.ExtendedInfosHasGroupSharings = res.Groups.HasSharing
                Result.ExtendedInfosHasHiddenGroupSharings = res.Groups.NotVisibleCount <> 0
                If res.Groups.Visible IsNot Nothing Then
                    For Each ResSharing As String In res.Groups.Visible
                        Result.ExtendedInfosGroupSharings.Add(New DmsShareForGroup(Result, New DmsGroup() With {.ID = ResSharing, .Provider = Me, .GetName = AddressOf CenterDeviceDmsProviderBase.DelegatedGetGroupName}, True, True, True, True, True, True))
                    Next
                End If
            End If
            Result.ExtendedInfosUserSharings = New List(Of DmsShareForUser)
            If res.Users IsNot Nothing Then
                Result.ExtendedInfosHasUserSharings = res.Users.HasSharing
                Result.ExtendedInfosHasHiddenUserSharings = res.Users.NotVisibleCount <> 0
                If res.Users.Visible IsNot Nothing Then
                    For Each ResSharing As String In res.Users.Visible
                        Result.ExtendedInfosUserSharings.Add(New DmsShareForUser(Result, New DmsUser() With {.ID = ResSharing, .Provider = Me, .GetName = AddressOf CenterDeviceDmsProviderBase.DelegatedGetUserName, .GetEMailAddress = AddressOf CenterDeviceDmsProviderBase.DelegatedGetUserEMailAddress}, True, True, True, True, True, True))
                    Next
                End If
            End If
            Result.ExtendedInfosIsShared = res.IsShared OrElse Result.ExtendedInfosLinks.Count > 0

            'Normalize field content
            If Result.FullName.EndsWith(Me.DirectorySeparator) Then
                Result.FullName = Result.FullName.Substring(0, Result.FullName.Length - 1)
            End If

            'Assign calculated fields
            If res.IsRootDirectory Then
                Result.Collection = ""
                Result.Folder = ""
            ElseIf res.ParentDirectory Is Nothing Then
                Result.Collection = Nothing
                Result.Folder = Nothing
            ElseIf res.ParentDirectory.Type = CenterDevice.IO.DirectoryInfo.DirectoryType.Collection Then
                Result.Collection = res.ParentDirectory.FullName
                Result.Folder = Nothing
            Else
                Result.Collection = Nothing
                Result.Folder = res.ParentDirectory.FullName
            End If

            'Require at least empty strings
            Result.Name = Tools.NotNullOrEmptyStringValue(Result.Name)
            Result.Folder = Me.PathWithoutLeadingDirectorySeparator(Tools.NotNullOrEmptyStringValue(Result.Folder))
            Result.Collection = Me.PathWithoutLeadingDirectorySeparator(Tools.NotNullOrEmptyStringValue(Result.Collection))
            Result.FullName = Me.PathWithoutLeadingDirectorySeparator(Tools.NotNullOrEmptyStringValue(Result.FullName))
            Return Result
        End Function

        ''' <summary>
        ''' Fill DmsResourceItem from CenterDevice.IO.FileInfo
        ''' </summary>
        ''' <param name="res"></param>
        ''' <returns></returns>
        Private Function CreateDmsResourceItem(res As CenterDevice.IO.FileInfo) As DmsResourceItem
            Dim Result As New DmsResourceItem() With {
                            .Name = res.FileName,
                            .IsHidden = False,
                            .ItemType = DmsResourceItem.ItemTypes.File,
                            .ContentLength = res.Size,
                            .ProviderSpecificHashOrETag = res.ID}
            Result.CreatedOnLocalTime = DateTimeUtcToLocalTime(res.UploadDate)
            Result.LastModificationOnLocalTime = DateTimeUtcToLocalTime(res.ModificationDate)

            'Assign additional fields
            Result.FullName = res.FullName
            Result.Name = res.FileName
            Result.ExtendedInfosFileID = res.ID
            Result.ExtendedInfosCollectionID = Nothing
            Result.ExtendedInfosFolderID = Nothing
            Result.ExtendedInfosAssignedCollectionID = res.ParentDirectory.AssociatedCollection.CollectionID
            Result.ExtendedInfosAssignedFolderID = res.ParentDirectory.FolderID
            If res.ReferencedFromCollectionIDs IsNot Nothing AndAlso res.ReferencedFromCollectionIDs.HasSharing = True Then
                Result.ExtendedInfosReferencedFromCollectionIDs = New List(Of String)(res.ReferencedFromCollectionIDs.Visible)
            Else
                Result.ExtendedInfosReferencedFromCollectionIDs = New List(Of String)
            End If
            If res.ReferencedFromFolderIDs IsNot Nothing Then
                Result.ExtendedInfosReferencedFromFolderIDs = New List(Of String)(res.ReferencedFromFolderIDs)
            Else
                Result.ExtendedInfosReferencedFromFolderIDs = New List(Of String)
            End If
            Result.ExtendedInfosOwner = New DmsUser() With {.ID = res.Owner, .Provider = Me, .GetName = AddressOf CenterDeviceDmsProviderBase.DelegatedGetUserName, .GetEMailAddress = AddressOf CenterDeviceDmsProviderBase.DelegatedGetUserEMailAddress}
            Result.ExtendedInfosLastModificationUser = New DmsUser() With {.ID = res.Uploader, .Provider = Me, .GetName = AddressOf CenterDeviceDmsProviderBase.DelegatedGetUserName, .GetEMailAddress = AddressOf CenterDeviceDmsProviderBase.DelegatedGetUserEMailAddress}
            Result.ExtendedInfosLinks = New List(Of DmsLink)
            If Not res.Link = Nothing Then Result.ExtendedInfosLinks.Add(New DmsLink(Result, res.Link, Me, AddressOf CenterDeviceDmsProviderBase.DelegatedFillLinkDetails))
            Result.ExtendedInfosLocks = res.Locks
            Result.ExtendedInfosLockedByUser = New DmsUser() With {.ID = res.LockedBy, .Provider = Me, .GetName = AddressOf CenterDeviceDmsProviderBase.DelegatedGetUserName, .GetEMailAddress = AddressOf CenterDeviceDmsProviderBase.DelegatedGetUserEMailAddress}
            Result.ExtendedInfosArchivedDateLocalTime = DateTimeUtcToLocalTime(res.ArchivedDate)
            Result.ExtendedInfosVersion = res.Version.ToString
            Result.ExtendedInfosVersionDateLocalTime = DateTimeUtcToLocalTime(res.VersionDate)
            Result.ExtendedInfosCollisionDetected = res.HasCollidingDuplicateFile
            Result.ExtendedInfosIsShared = res.IsShared
            'Result.ExtendedInfosIsPublicCollection = res.Public
            'Result.ExtendedInfosIsAuditing = res.Auditing
            'Result.ExtendedInfosIsIntelligent = res.IsIntelligent
            Result.ExtendedInfosGroupSharings = New List(Of DmsShareForGroup)
            If res.Groups IsNot Nothing Then
                Result.ExtendedInfosHasGroupSharings = res.Groups.HasSharing
                Result.ExtendedInfosHasHiddenGroupSharings = res.Groups.NotVisibleCount <> 0
                If res.Groups.Visible IsNot Nothing Then
                    For Each ResSharing As String In res.Groups.Visible
                        Result.ExtendedInfosGroupSharings.Add(New DmsShareForGroup(Result, New DmsGroup() With {.ID = ResSharing, .Provider = Me, .GetName = AddressOf CenterDeviceDmsProviderBase.DelegatedGetGroupName}, True, True, True, True, True, True))
                    Next
                End If
            End If
            Result.ExtendedInfosUserSharings = New List(Of DmsShareForUser)
            If res.Users IsNot Nothing Then
                Result.ExtendedInfosHasUserSharings = res.Users.HasSharing
                Result.ExtendedInfosHasHiddenUserSharings = res.Users.NotVisibleCount <> 0
                If res.Users.Visible IsNot Nothing Then
                    For Each ResSharing As String In res.Users.Visible
                        Result.ExtendedInfosUserSharings.Add(New DmsShareForUser(Result, New DmsUser() With {.ID = ResSharing, .Provider = Me, .GetName = AddressOf CenterDeviceDmsProviderBase.DelegatedGetUserName, .GetEMailAddress = AddressOf CenterDeviceDmsProviderBase.DelegatedGetUserEMailAddress}, True, True, True, True, True, True))
                    Next
                End If
            End If

            'Assign calculated fields
            Dim LastDirSeparatorPosition As Integer = Result.FullName.LastIndexOf(Me.DirectorySeparator)
            Dim ParentFolderName As String
            If LastDirSeparatorPosition >= 0 Then
                ParentFolderName = Result.FullName.Substring(0, LastDirSeparatorPosition)
            Else
                ParentFolderName = ""
            End If
            If res.ParentDirectory.IsRootDirectory = False AndAlso res.ParentDirectory.CollectionID = Nothing Then
                Result.Folder = ParentFolderName
            End If
            Result.Collection = res.ParentDirectory.AssociatedCollection.FullName

            'Require at least empty strings
            Result.Name = Tools.NotNullOrEmptyStringValue(Result.Name)
            Result.Folder = Me.PathWithoutLeadingDirectorySeparator(Tools.NotNullOrEmptyStringValue(Result.Folder))
            Result.Collection = Me.PathWithoutLeadingDirectorySeparator(Tools.NotNullOrEmptyStringValue(Result.Collection))
            Result.FullName = Me.PathWithoutLeadingDirectorySeparator(Tools.NotNullOrEmptyStringValue(Result.FullName))
            Return Result
        End Function

        Private Function PathWithoutLeadingDirectorySeparator(path As String) As String
            If path <> Nothing AndAlso path.StartsWith(Me.DirectorySeparator) Then
                Return path.Substring(1)
            Else
                Return path
            End If
        End Function

        Public Shared Sub DelegatedFillLinkDetails(provider As Object, linkId As String, dmsLink As DmsLink)
            Dim LinkData As CenterDevice.Rest.Clients.Link.Link = CType(provider, CenterDeviceDmsProviderBase).IOClient.GetLink(linkId)
            dmsLink.WebUrl = LinkData.Web
            dmsLink.DownloadUrl = LinkData.Download
            dmsLink.RestUrl = LinkData.Rest
            dmsLink.ExpiryDateLocalTime = DateTimeUtcToLocalTime(LinkData.AccessControl.ExpiryDate)
            dmsLink.MaxDownloads = LinkData.AccessControl.MaxDownloads
            dmsLink.AllowView = True
            dmsLink.AllowDownload = Not LinkData.AccessControl.ViewOnly
            dmsLink.Password = LinkData.AccessControl.Password
            dmsLink.DownloadsCount = LinkData.Downloads
            dmsLink.ViewsCount = LinkData.Views
        End Sub

        Public Shared Sub DelegatedFillUploadLinkDetails(provider As Object, uploadLinkId As String, dmsLink As DmsLink)
            Dim UploadLink As CenterDevice.Rest.Clients.Link.UploadLink = CType(provider, CenterDeviceDmsProviderBase).IOClient.GetUploadLink(uploadLinkId)
            dmsLink.AllowDelete = False
            dmsLink.AllowEdit = False
            dmsLink.AllowDownload = False
            dmsLink.AllowView = False
            dmsLink.AllowUpload = True
            dmsLink.AllowShare = False
            dmsLink.Password = UploadLink.Password
            dmsLink.ExpiryDateLocalTime = DateTimeUtcToLocalTime(UploadLink.ExpiryDate)
            dmsLink.WebUrl = UploadLink.Web
            'dmsLink.RestUrl=UploadLink.Rest
            dmsLink.DownloadUrl = Nothing
            dmsLink.MaxDownloads = Nothing
            dmsLink.MaxUploads = UploadLink.MaxDocuments
            dmsLink.MaxBytes = UploadLink.MaxBytes
            dmsLink.UploadedBytes = UploadLink.UploadedBytes
            dmsLink.UploadsCount = UploadLink.UploadsMade
            dmsLink.Name = UploadLink.Name
        End Sub

        Public Shared Function DelegatedGetGroupName(provider As BaseDmsProvider, groupId As String) As String
            Return CType(provider, CenterDeviceDmsProviderBase).IOClient.GroupName(groupId)
        End Function

        Public Shared Function DelegatedGetUserName(provider As BaseDmsProvider, userId As String) As String
            Return CType(provider, CenterDeviceDmsProviderBase).IOClient.UserName(userId)
        End Function

        Public Shared Function DelegatedGetUserEMailAddress(provider As BaseDmsProvider, userId As String) As String
            Return CType(provider, CenterDeviceDmsProviderBase).IOClient.UserEMailAddress(userId)
        End Function

        Public Overrides Function CreateLink(dmsResource As DmsResourceItem, shareInfo As DmsLink) As DmsLink
            If shareInfo.AllowEdit Then Throw New NotSupportedException("AllowEdit not supported by provider")
            If shareInfo.AllowDelete Then Throw New NotSupportedException("AllowDelete not supported by provider")
            If shareInfo.AllowShare Then Throw New NotSupportedException("AllowShare not supported by provider")
            If Not (shareInfo.AllowView Xor shareInfo.AllowUpload) Then
                Throw New ArgumentException("Either AllowView or AllowUpload must be set", NameOf(shareInfo))
            End If
            Try
                If shareInfo.AllowUpload Then
                    'Create upload link
                    If Not dmsResource.ItemType = DmsResourceItem.ItemTypes.Collection OrElse dmsResource.ExtendedInfosCollectionID = Nothing Then
                        Throw New NotSupportedException("Upload links supported only with collections")
                    End If
                    Dim CreatedUploadLink As UploadLinkCreationResponse = Me.IOClient.ApiClient.UploadLinks.CreateCollectionLink(Me.IOClient.CurrentAuthenticationContextUserID, dmsResource.ExtendedInfosCollectionID,
                                                                   Tools.NotNullOrEmptyStringValue(shareInfo.Name),
                                                                   Nothing,
                                                                   DateTimeLocalToUtcTime(shareInfo.ExpiryDateLocalTime),
                                                                   ConvertNarrowingToNullableInt32(shareInfo.MaxUploads),
                                                                   Tools.NotNullOrEmptyStringValue(shareInfo.Password),
                                                                   Nothing
                                                                   )
                    _AllUploadLinks = Nothing 'Reset cache
                    Dim Result As DmsLink
                    Result = New DmsLink(dmsResource, CreatedUploadLink.Id, Me, AddressOf DelegatedFillUploadLinkDetails)
                    dmsResource.ExtendedInfosLinks.Add(Result) 'Update current DmsResourceItem
                    Return Result
                Else
                    'Create view/download link
                    Dim AccessControl As New LinkAccessControl With {
                        .ViewOnly = Not shareInfo.AllowDownload,
                        .Password = shareInfo.Password,
                        .ExpiryDate = DateTimeLocalToUtcTime(shareInfo.ExpiryDateLocalTime),
                        .MaxDownloads = ConvertNarrowingToNullableInt32(shareInfo.MaxDownloads)
                    }
                    Dim CreatedLink As LinkCreationResponse
                    Select Case dmsResource.ItemType
                        Case DmsResourceItem.ItemTypes.Collection
                            CreatedLink = Me.IOClient.ApiClient.Links.CreateCollectionLink(Me.IOClient.CurrentAuthenticationContextUserID, dmsResource.ExtendedInfosCollectionID, AccessControl)
                        Case DmsResourceItem.ItemTypes.Folder
                            CreatedLink = Me.IOClient.ApiClient.Links.CreateFolderLink(Me.IOClient.CurrentAuthenticationContextUserID, dmsResource.ExtendedInfosFolderID, AccessControl)
                        Case DmsResourceItem.ItemTypes.File
                            CreatedLink = Me.IOClient.ApiClient.Links.CreateDocumentLink(Me.IOClient.CurrentAuthenticationContextUserID, dmsResource.ExtendedInfosFileID, AccessControl)
                        Case DmsResourceItem.ItemTypes.Root
                            Throw New NotSupportedException("Sharing for root directory not supported")
                        Case Else
                            Throw New NotImplementedException("Invalid item type: " & dmsResource.ItemType)
                    End Select
                    'Refresh caches + update current DmsResourceItem
                    Me.ResetDirectoryCacheOfParentFolderToForceReloadOfUpdatedSharings(dmsResource)
                    Dim Result As DmsLink
                    Result = New DmsLink(dmsResource, CreatedLink.Id, Me, AddressOf DelegatedFillLinkDetails)
                    dmsResource.ExtendedInfosLinks.Add(Result) 'Update current DmsResourceItem
                    Return Result
                End If
            Catch ex As ForbiddenException
                Throw New Data.DmsUserErrorMessageException("Forbidden: " & ex.ErrorResponse.Message)
            Catch ex As BadRequestException
                If ex.ErrorResponse.Data.ContainsKey("explanation") Then
                    Throw New Data.DmsUserErrorMessageException(CType(ex.ErrorResponse.Data("explanation"), String))
                Else
                    Throw New Data.DmsUserErrorMessageException(ex.ErrorResponse.Message)
                End If
            End Try
        End Function

        Private Shared Function ConvertNarrowingToNullableInt32(value As Long?) As Integer?
            If value.HasValue = False Then
                Return Nothing
            ElseIf value.Value > Integer.MaxValue Then
                Return Integer.MaxValue
            ElseIf value.Value < Integer.MinValue Then
                Return Integer.MinValue
            Else
                Return CType(value.Value, Integer)
            End If
        End Function

        Private Sub ResetDirectoryCacheOfParentFolderToForceReloadOfUpdatedSharings(modifiedDmsResourceItem As DmsResourceItem)
            Me.ResetParentDirectoryCache(modifiedDmsResourceItem)
        End Sub

        Private Overloads Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareBase, listOfAddedGroups As List(Of String), listOfAddedUsers As List(Of String))
            Dim Response As SharingResponse
            Select Case shareInfo.ParentDmsResourceItem.ItemType
                Case DmsResourceItem.ItemTypes.Collection
                    Response = Me.IOClient.ApiClient.Collection.ShareCollection(Me.IOClient.CurrentAuthenticationContextUserID, shareInfo.ParentDmsResourceItem.ExtendedInfosCollectionID,
                                                                   listOfAddedUsers,
                                                                   listOfAddedGroups
                                                                   )
                Case DmsResourceItem.ItemTypes.Folder
                    Response = Me.IOClient.ApiClient.Folder.ShareFolder(Me.IOClient.CurrentAuthenticationContextUserID, shareInfo.ParentDmsResourceItem.ExtendedInfosFolderID,
                                                                   listOfAddedUsers,
                                                                   listOfAddedGroups
                                                                   )
                Case DmsResourceItem.ItemTypes.File
                    Throw New NotSupportedException("Sharing for files not supported")
                Case DmsResourceItem.ItemTypes.Root
                    Throw New NotSupportedException("Sharing for root directory not supported")
                Case Else
                    Throw New NotImplementedException()
            End Select
            If Response?.FailedGroups?.Count = 0 AndAlso Response?.FailedUsers?.Count = 0 Then
                Throw New InvalidOperationException("Failure deleting sharing")
            End If
            Me.ResetDirectoryCacheOfParentFolderToForceReloadOfUpdatedSharings(dmsResource)
        End Sub

        Public Overrides Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForGroup)
            Dim ListOfAddedUsers As List(Of String) = Nothing
            Dim ListOfAddedGroups As New List(Of String)(New String() {shareInfo.Group.ID})
            Me.CreateSharing(dmsResource, shareInfo, ListOfAddedGroups, ListOfAddedUsers)
        End Sub

        Public Overrides Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForUser)
            Dim ListOfAddedUsers As New List(Of String)(New String() {shareInfo.User.ID})
            Dim ListOfAddedGroups As List(Of String) = Nothing
            Me.CreateSharing(dmsResource, shareInfo, ListOfAddedGroups, ListOfAddedUsers)
        End Sub

        Public Overrides Sub UpdateLink(shareInfo As DmsLink)
            If shareInfo.ID = Nothing Then Throw New InvalidOperationException("Update of link requires an ID in DmsLink")
            If shareInfo.AllowEdit Then Throw New NotSupportedException("AllowEdit not supported by provider")
            If shareInfo.AllowDelete Then Throw New NotSupportedException("AllowDelete not supported by provider")
            If shareInfo.AllowShare Then Throw New NotSupportedException("AllowShare not supported by provider")
            If Not (shareInfo.AllowView Xor shareInfo.AllowUpload) Then
                Throw New ArgumentException("Either AllowView or AllowUpload must be set", NameOf(shareInfo))
            End If
            Try
                If shareInfo.AllowUpload Then
                    'Update upload link
                    Me.IOClient.ApiClient.UploadLink.UpdateLink(Me.IOClient.CurrentAuthenticationContextUserID, shareInfo.ID,
                                                        CType(Nothing, String),
                                                        Tools.NotNullOrEmptyStringValue(shareInfo.Name),
                                                        Nothing,
                                                        DateTimeLocalToUtcTime(shareInfo.ExpiryDateLocalTime),
                                                        ConvertNarrowingToNullableInt32(shareInfo.MaxUploads),
                                                        Tools.NotNullOrEmptyStringValue(shareInfo.Password),
                                                        Nothing
                                                        )
                    _AllUploadLinks = Nothing 'Reset cache
                Else
                    'Update view/download link
                    Dim AccessControl As New LinkAccessControl With {
                        .ViewOnly = Not shareInfo.AllowDownload,
                        .Password = shareInfo.Password,
                        .ExpiryDate = DateTimeLocalToUtcTime(shareInfo.ExpiryDateLocalTime),
                        .MaxDownloads = ConvertNarrowingToNullableInt32(shareInfo.MaxDownloads)
                    }
                    Me.IOClient.ApiClient.Link.UpdateLink(Me.IOClient.CurrentAuthenticationContextUserID, shareInfo.ID, AccessControl)
                    Me.ResetDirectoryCacheOfParentFolderToForceReloadOfUpdatedSharings(shareInfo.ParentDmsResourceItem)
                End If
            Catch ex As ForbiddenException
                Throw New Data.DmsUserErrorMessageException(ex.ErrorResponse.Message)
            End Try
        End Sub

        Public Overrides Sub UpdateSharing(shareInfo As DmsShareForGroup)
            Throw New NotSupportedException("Updating of share properties not supported")
        End Sub

        Public Overrides Sub UpdateSharing(shareInfo As DmsShareForUser)
            Throw New NotSupportedException("Updating of share properties not supported")
        End Sub

        Public Overrides Sub DeleteLink(shareInfo As DmsLink)
            If shareInfo.AllowEdit Then Throw New NotSupportedException("AllowEdit not supported by provider")
            If shareInfo.AllowDelete Then Throw New NotSupportedException("AllowDelete not supported by provider")
            If shareInfo.AllowShare Then Throw New NotSupportedException("AllowShare not supported by provider")
            If Not (shareInfo.AllowView Xor shareInfo.AllowUpload) Then
                Throw New ArgumentException("Either AllowView or AllowUpload must be set", NameOf(shareInfo))
            End If
            If shareInfo.AllowUpload Then
                'Delete upload link
                Me.IOClient.ApiClient.UploadLink.DeleteLink(Me.IOClient.CurrentAuthenticationContextUserID, shareInfo.ID)
                _AllUploadLinks = Nothing 'Reset cache
            Else
                'Delete view/download link            
                Me.IOClient.ApiClient.Link.DeleteLink(Me.IOClient.CurrentAuthenticationContextUserID, shareInfo.ID)
                Me.ResetDirectoryCacheOfParentFolderToForceReloadOfUpdatedSharings(shareInfo.ParentDmsResourceItem)
            End If
        End Sub

        Private Overloads Sub DeleteSharing(shareInfo As DmsShareBase, listOfDeletedGroups As List(Of String), listOfDeletedUsers As List(Of String))
            Dim Response As SharingResponse
            Select Case shareInfo.ParentDmsResourceItem.ItemType
                Case DmsResourceItem.ItemTypes.Collection
                    Response = Me.IOClient.ApiClient.Collection.UnshareCollection(Me.IOClient.CurrentAuthenticationContextUserID, shareInfo.ParentDmsResourceItem.ExtendedInfosCollectionID,
                                                                   listOfDeletedUsers,
                                                                   listOfDeletedGroups
                                                                   )
                Case DmsResourceItem.ItemTypes.Folder
                    Response = Me.IOClient.ApiClient.Folder.UnshareFolder(Me.IOClient.CurrentAuthenticationContextUserID, shareInfo.ParentDmsResourceItem.ExtendedInfosFolderID,
                                                                   listOfDeletedUsers,
                                                                   listOfDeletedGroups
                                                                   )
                Case DmsResourceItem.ItemTypes.File
                    Throw New NotSupportedException("Sharing for files not supported")
                Case DmsResourceItem.ItemTypes.Root
                    Throw New NotSupportedException("Sharing for root directory not supported")
                Case Else
                    Throw New NotImplementedException()
            End Select
            If Response?.FailedGroups?.Count = 0 AndAlso Response?.FailedUsers?.Count = 0 Then
                Throw New InvalidOperationException("Failure deleting sharing")
            End If
            Me.ResetDirectoryCacheOfParentFolderToForceReloadOfUpdatedSharings(shareInfo.ParentDmsResourceItem)
        End Sub

        Public Overrides Sub DeleteSharing(shareInfo As DmsShareForGroup)
            Dim ListOfDeletedUsers As List(Of String) = Nothing
            Dim ListOfDeletedGroups As New List(Of String)(New String() {shareInfo.Group.ID})
            Me.DeleteSharing(shareInfo, ListOfDeletedGroups, ListOfDeletedUsers)
        End Sub

        Public Overrides Sub DeleteSharing(shareInfo As DmsShareForUser)
            Dim ListOfDeletedUsers As New List(Of String)(New String() {shareInfo.User.ID})
            Dim ListOfDeletedGroups As List(Of String) = Nothing
            Me.DeleteSharing(shareInfo, ListOfDeletedGroups, ListOfDeletedUsers)
        End Sub

        Public Overrides Function GetAllGroups() As List(Of DmsGroup)
            Dim GroupList As GroupList = Me.IOClient.ApiClient.Groups.GetAllGroups(Me.IOClient.CurrentAuthenticationContextUserID, CenterDevice.Model.Groups.GroupsFilter.AllVisibleGroupsForCurrentUser)
            Dim Result As New List(Of DmsGroup)
            For Each Group In GroupList.Groups
                Result.Add(New DmsGroup() With {
                    .ID = Group.Id,
                    .Name = Group.Name
                   })
            Next
            Return Result
        End Function

        Public Overrides Function GetAllUsers() As List(Of DmsUser)
            Dim UserList As UserList(Of BaseUserData) = Me.IOClient.ApiClient.Users.GetAllUsers(Me.IOClient.CurrentAuthenticationContextUserID, New String() {
            CenterDevice.Rest.Clients.User.UserStatus.ACTIVE
            })
            Dim Result As New List(Of DmsUser)
            For Each User In UserList.Users
                Result.Add(New DmsUser() With {
                    .ID = User.Id,
                    .Name = User.GetFullName,
                    .EMailAddress = User.Email
                   })
            Next
            Return Result
        End Function

        Public Overrides ReadOnly Property CurrentContextUserID As String
            Get
                Return Me.IOClient.CurrentContextUserId
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsSubFolderConfiguration As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsRuntimeAccessToRemoteServer As RuntimeAccessTypes
            Get
                Return RuntimeAccessTypes.ConfigurationAndRuntimeAccess
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsFilesInRootFolder As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsNonUniqueRemoteItems As Boolean
            Get
                Return True
            End Get
        End Property

    End Class

End Namespace