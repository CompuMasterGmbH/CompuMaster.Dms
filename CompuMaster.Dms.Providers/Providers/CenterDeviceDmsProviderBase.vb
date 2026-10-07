Option Explicit On
Option Strict On

Imports System.IO
Imports System.Reflection
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
    Partial Public MustInherit Class CenterDeviceDmsProviderBase
        Inherits Providers.BaseDmsProvider

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property WebApiUrlCustomization As UrlCustomizationType
            Get
                Return UrlCustomizationType.WebApiUrlNotCustomizable
            End Get
        End Property

        ''' <inheritdoc/>
        Protected Overrides Function CustomizedWebApiUrl(loginCredentials As BaseDmsLoginCredentials) As String
            Return Me.WebApiDefaultUrl
        End Function

        'Protected Property ApiToken As TokenResponse
        'Protected Property CenterDeviceClient As CenterDevice.Rest.Clients.CenterDeviceClient
        ''' <summary>Gets or sets the authorized SDK I/O client used by derived provider implementations.</summary>
        Protected Friend Property IOClient As CenterDevice.IO.IOClientBase

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property BrowseInRootFolderName() As String = "/"

        ''' <inheritdoc/>
        Public Overrides Function FindCollectionById(id As String) As DmsResourceItem
            Return Me.CreateDmsResourceItem(New CenterDevice.IO.DirectoryInfo(Me.IOClient, Nothing, Me.IOClient.ApiClient.Collection.GetCollection(Me.IOClient.CurrentAuthenticationContextUserID, id)))
        End Function

        ''' <inheritdoc/>
        Public Overrides Function FindFolderById(id As String) As DmsResourceItem
            Return Me.CreateDmsResourceItem(New CenterDevice.IO.DirectoryInfo(Me.IOClient, Nothing, Me.IOClient.ApiClient.Folder.GetFolder(Me.IOClient.CurrentAuthenticationContextUserID, id, Nothing)))
        End Function

        ''' <inheritdoc/>
        Public Overrides Function FindFileById(id As String) As DmsResourceItem
            Return Me.CreateDmsResourceItem(New CenterDevice.IO.FileInfo(Me.IOClient, Nothing, Me.IOClient.ApiClient.Document.GetDocumentMetadata(Me.IOClient.CurrentAuthenticationContextUserID, id)))
        End Function

        ''' <inheritdoc/>
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

        ''' <inheritdoc/>
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
        ''' <param name="remoteItem">The selected remote resource snapshot.</param>
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

        ''' <inheritdoc/>
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

        ''' <inheritdoc/>
        Public Overrides Function ListAllDirectoryItems(remoteFolderPath As String) As List(Of DmsResourceItem)
            Dim RemoteDir As CenterDevice.IO.DirectoryInfo = Me.IOClient.RootDirectory.OpenDirectoryPath(remoteFolderPath)
            Dim Result As New List(Of DmsResourceItem)
            If RemoteDir.IsRootDirectory Then
                Dim SubFolders As CenterDevice.IO.DirectoryInfo() = RemoteDir.GetDirectories()
                If SubFolders IsNot Nothing Then
                    For Each SubFolder As CenterDevice.IO.DirectoryInfo In SubFolders
                        Result.Add(Me.CreateDmsResourceItem(SubFolder))
                    Next
                End If
            Else
                'The IO client's folder listing omits has-subfolders. Request it with the
                'other listing fields so leaf folders can be shown without a false expander.
                Dim ParentFolderId As String = If(RemoteDir.CollectionID IsNot Nothing, CenterDevice.Rest.RestApiConstants.NONE, RemoteDir.FolderID)
                Dim Folders = CenterDeviceFolderMetadataClient.Create(Me.IOClient.ApiClient).GetFoldersWithMetadata(
                    Me.IOClient.CurrentAuthenticationContextUserID, RemoteDir.CollectionID, ParentFolderId)
                For Each Folder In Folders
                    Dim SubFolder As New CenterDevice.IO.DirectoryInfo(Me.IOClient, RemoteDir, Folder)
                    Dim Item As DmsResourceItem = Me.CreateDmsResourceItem(SubFolder)
                    Item.HasChildDirectories = Folder.HasSubFoldersMetadata
                    Result.Add(Item)
                Next
            End If
            Return Result
        End Function

        ''' <inheritdoc/>
        Public Overrides Function ListAllFolderNames(remoteFolderPath As String) As List(Of String)
            Dim RemoteDir As CenterDevice.IO.DirectoryInfo = Me.IOClient.RootDirectory.OpenDirectoryPath(remoteFolderPath)
            Dim Result As New List(Of String)
            Dim SubFolders As CenterDevice.IO.DirectoryInfo() = RemoteDir.GetDirectories
            For Each SubFolder In SubFolders
                Result.Add(SubFolder.Name)
            Next
            Return Result
        End Function

        ''' <inheritdoc/>
        Public Overrides Function ListAllFileNames(remoteFolderPath As String) As List(Of String)
            Dim RemoteDir As CenterDevice.IO.DirectoryInfo = Me.IOClient.RootDirectory.OpenDirectoryPath(remoteFolderPath)
            Dim Result As New List(Of String)
            Dim FileItems As CenterDevice.IO.FileInfo() = RemoteDir.GetFiles
            For Each FileItem In FileItems
                Result.Add(FileItem.FileName)
            Next
            Return Result
        End Function

        ''' <inheritdoc/>
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

        ''' <inheritdoc/>
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

        ''' <inheritdoc/>
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

        ''' <inheritdoc/>
        Public Overrides Sub DownloadFile(remoteFile As DmsResourceItem, localFilePath As String)
            If remoteFile Is Nothing Then Throw New ArgumentNullException(NameOf(remoteFile))
            If remoteFile.ItemType <> DmsResourceItem.ItemTypes.File Then Throw New ArgumentException(ProviderStrings.GetText("TheRemoteResourceMustBeAFile"), NameOf(remoteFile))
            If String.IsNullOrEmpty(remoteFile.ExtendedInfosFileID) Then
                MyBase.DownloadFile(remoteFile, localFilePath)
                Return
            End If

            Dim FoundFileItem As New CenterDevice.IO.FileInfo(Me.IOClient, Nothing, Me.IOClient.ApiClient.Document.GetDocumentMetadata(Me.IOClient.CurrentAuthenticationContextUserID, remoteFile.ExtendedInfosFileID))
            FoundFileItem.Download(localFilePath)
            If remoteFile.LastModificationOnLocalTime.HasValue AndAlso remoteFile.LastModificationOnLocalTime.Value <> Nothing Then System.IO.File.SetLastWriteTime(localFilePath, remoteFile.LastModificationOnLocalTime.Value)
        End Sub

        ''' <summary>Specifies whether a missing resource returns no result or raises an exception.</summary>
        Protected Enum RessourceNotFoundHandling As Byte
            ''' <summary>Returns Nothing if the requested resource or its parent directory is missing.</summary>
            ReturnWithNullIfItemOrParentDirectoryIsNotFound = 0
            ''' <summary>Raises a resource-not-found exception if the resource or its parent directory is missing.</summary>
            ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound = 1
        End Enum

        ''' <summary>
        ''' Get directory item
        ''' </summary>
        ''' <param name="remoteDirectoryPath">The remote directory path.</param>
        ''' <param name="handlingIfNotFound">Whether a missing item returns Nothing or raises a not-found exception.</param>
        ''' <returns>The SDK directory, or Nothing when the selected missing-resource policy permits it.</returns>
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
        ''' <param name="remoteFilePath">The remote file path.</param>
        ''' <param name="handlingIfNotFound">Whether a missing item returns Nothing or raises a not-found exception.</param>
        ''' <returns>The SDK file, or Nothing when the selected missing-resource policy permits it.</returns>
        Protected Function GetFileItem(remoteFilePath As String, handlingIfNotFound As RessourceNotFoundHandling) As CenterDevice.IO.FileInfo
            Dim ParentRemoteDirName As String = Me.ParentDirectoryPath(remoteFilePath)
            Dim ParentRemoteDir As CenterDevice.IO.DirectoryInfo = GetDirectoryItem(ParentRemoteDirName, handlingIfNotFound)
            Dim RemoteFileName As String = Me.ItemName(remoteFilePath)
            Return Me.GetFileItem(ParentRemoteDir, RemoteFileName, handlingIfNotFound)
        End Function

        ''' <summary>
        ''' Get a file item
        ''' </summary>
        ''' <param name="parentRemoteDir">The resolved provider directory containing the requested file.</param>
        ''' <param name="remoteFileName">The remote file name within its parent directory.</param>
        ''' <param name="handlingIfNotFound">Whether a missing item returns Nothing or raises a not-found exception.</param>
        ''' <returns>The SDK file, or Nothing when the selected missing-resource policy permits it.</returns>
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

        Private Function GetParentDirectory(remoteItem As DmsResourceItem) As CenterDevice.IO.DirectoryInfo
            If remoteItem.ExtendedInfosAssignedFolderID <> Nothing Then
                Return Me.GetFolderDirectoryById(remoteItem.ExtendedInfosAssignedFolderID, remoteItem.ExtendedInfosAssignedCollectionID, New HashSet(Of String)(StringComparer.Ordinal))
            ElseIf remoteItem.ExtendedInfosAssignedCollectionID <> Nothing Then
                Return New CenterDevice.IO.DirectoryInfo(Me.IOClient, Me.IOClient.RootDirectory, Me.IOClient.ApiClient.Collection.GetCollection(Me.IOClient.CurrentAuthenticationContextUserID, remoteItem.ExtendedInfosAssignedCollectionID))
            Else
                Return Me.IOClient.RootDirectory
            End If
        End Function

        Private Function GetFolderDirectoryById(folderId As String, expectedCollectionId As String, visitedFolderIds As HashSet(Of String)) As CenterDevice.IO.DirectoryInfo
            If Not visitedFolderIds.Add(folderId) Then Throw New InvalidOperationException(ProviderStrings.Format("ACycleWasFoundInTheCenterDeviceFolder", folderId))

            Dim Folder = Me.IOClient.ApiClient.Folder.GetFolder(Me.IOClient.CurrentAuthenticationContextUserID, folderId, Nothing)
            If Not String.IsNullOrEmpty(expectedCollectionId) AndAlso Not String.IsNullOrEmpty(Folder.Collection) AndAlso Not String.Equals(Folder.Collection, expectedCollectionId, StringComparison.Ordinal) Then
                Throw New InvalidOperationException(ProviderStrings.Format("FolderIDNoLongerBelongsToItsExpected", folderId))
            End If

            Dim CollectionId As String = If(String.IsNullOrEmpty(Folder.Collection), expectedCollectionId, Folder.Collection)
            Dim ParentDirectory As CenterDevice.IO.DirectoryInfo
            If String.IsNullOrEmpty(Folder.Parent) OrElse String.Equals(Folder.Parent, CenterDevice.Rest.RestApiConstants.NONE, StringComparison.Ordinal) Then
                If String.IsNullOrEmpty(CollectionId) Then Throw New InvalidOperationException(ProviderStrings.Format("TheCollectionOfCenterDeviceFolderIDIsUnknown", folderId))
                ParentDirectory = New CenterDevice.IO.DirectoryInfo(Me.IOClient, Me.IOClient.RootDirectory, Me.IOClient.ApiClient.Collection.GetCollection(Me.IOClient.CurrentAuthenticationContextUserID, CollectionId))
            Else
                ParentDirectory = Me.GetFolderDirectoryById(Folder.Parent, CollectionId, visitedFolderIds)
            End If

            Return New CenterDevice.IO.DirectoryInfo(Me.IOClient, ParentDirectory, Folder)
        End Function

        Private Function GetFileItem(remoteItem As DmsResourceItem) As CenterDevice.IO.FileInfo
            If remoteItem.ExtendedInfosFileID = Nothing Then
                If remoteItem.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(remoteItem.FullName)
                Return Me.GetFileItem(remoteItem.FullName, RessourceNotFoundHandling.ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound)
            End If

            Dim ParentDirectory As CenterDevice.IO.DirectoryInfo = Me.GetParentDirectory(remoteItem)
            For Each File As CenterDevice.IO.FileInfo In ParentDirectory.GetFiles()
                If File.ID = remoteItem.ExtendedInfosFileID Then Return File
            Next
            Throw New CompuMaster.Dms.Data.FileNotFoundException(remoteItem.FullName)
        End Function

        Private Function GetDirectoryItem(remoteItem As DmsResourceItem) As CenterDevice.IO.DirectoryInfo
            If remoteItem.ItemType = DmsResourceItem.ItemTypes.Collection AndAlso remoteItem.ExtendedInfosCollectionID <> Nothing Then
                For Each Directory As CenterDevice.IO.DirectoryInfo In Me.IOClient.RootDirectory.GetDirectories()
                    If Directory.CollectionID = remoteItem.ExtendedInfosCollectionID Then Return Directory
                Next
            ElseIf remoteItem.ItemType = DmsResourceItem.ItemTypes.Folder AndAlso remoteItem.ExtendedInfosFolderID <> Nothing Then
                Dim ParentDirectory As CenterDevice.IO.DirectoryInfo = Me.GetParentDirectory(remoteItem)
                For Each Directory As CenterDevice.IO.DirectoryInfo In ParentDirectory.GetDirectories()
                    If Directory.FolderID = remoteItem.ExtendedInfosFolderID Then Return Directory
                Next
            ElseIf Not remoteItem.ExtendedInfosCollisionDetected Then
                Return Me.GetDirectoryItem(remoteItem.FullName, RessourceNotFoundHandling.ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound)
            End If
            Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(remoteItem.FullName)
        End Function

        ''' <inheritdoc/>
        Protected Overrides Sub CopyItem(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?)
            Select Case remoteSource.ItemType
                Case DmsResourceItem.ItemTypes.File
                    Dim SourceFile As CenterDevice.IO.FileInfo = Me.GetFileItem(remoteSource)
                    Dim DestinationParent As CenterDevice.IO.DirectoryInfo = Me.GetDirectoryItem(Me.ParentDirectoryPath(remoteDestinationPath), RessourceNotFoundHandling.ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound)
                    Me.CopyFileExact(SourceFile, DestinationParent, Me.ItemName(remoteDestinationPath), remoteSource.FullName, remoteDestinationPath, allowOverwrite)
                Case DmsResourceItem.ItemTypes.Folder
                    If Me.ParentDirectoryPath(remoteDestinationPath) = Nothing Then Throw New NotSupportedException(ProviderStrings.GetText("CenterDeviceFoldersMustRemainInsideACollectionOr"))
                    Dim SourceDirectory As CenterDevice.IO.DirectoryInfo = Me.GetDirectoryItem(remoteSource)
                    Dim ExistingDestination As DmsResourceItem = Me.ListRemoteItem(remoteDestinationPath)
                    If ExistingDestination IsNot Nothing Then
                        If allowOverwrite <> True Then Throw New DirectoryAlreadyExistsException(remoteDestinationPath)
                        Me.MergeDirectoryContents(SourceDirectory, Me.GetDirectoryItem(remoteDestinationPath, RessourceNotFoundHandling.ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound), False, remoteSource.FullName, remoteDestinationPath)
                    Else
                        Dim DestinationParent As CenterDevice.IO.DirectoryInfo = Me.GetDirectoryItem(Me.ParentDirectoryPath(remoteDestinationPath), RessourceNotFoundHandling.ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound)
                        Me.CopyDirectoryTree(SourceDirectory, DestinationParent, Me.ItemName(remoteDestinationPath), remoteSource.FullName, remoteDestinationPath)
                    End If
                Case DmsResourceItem.ItemTypes.Collection
                    Throw New NotSupportedException(ProviderStrings.GetText("CenterDeviceCollectionsCanTBeCopiedBecauseThey"))
                Case Else
                    Throw New NotSupportedException(ProviderStrings.Format("UnsupportedSourceItemType2", remoteSource.ItemType.ToString()))
            End Select
        End Sub

        ''' <inheritdoc/>
        ''' <remarks>Source mode uses native asynchronous requests; the default package build retains the serialized synchronous compatibility path.</remarks>
        Protected Overrides Function CopyItemAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?) As Task
#If NATIVE_ASYNC Then
            Return Me.CopyMoveNativeItemAsync(remoteSource, remoteDestinationPath, allowOverwrite, False, Me.CurrentAsyncCancellationToken)
#Else
            Return Me.RunSynchronousFallbackAsync(Sub() Me.CopyItem(remoteSource, remoteDestinationPath, allowOverwrite), Me.CurrentAsyncCancellationToken)
#End If
        End Function

        ''' <inheritdoc/>
        Protected Overrides Sub MoveItem(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?)
            Select Case remoteSource.ItemType
                Case DmsResourceItem.ItemTypes.File
                    Dim SourceFile As CenterDevice.IO.FileInfo = Me.GetFileItem(remoteSource)
                    Dim DestinationParent As CenterDevice.IO.DirectoryInfo = Me.GetDirectoryItem(Me.ParentDirectoryPath(remoteDestinationPath), RessourceNotFoundHandling.ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound)
                    Me.MoveFileExact(SourceFile, DestinationParent, Me.ItemName(remoteDestinationPath), remoteSource.FullName, remoteDestinationPath, allowOverwrite)
                Case DmsResourceItem.ItemTypes.Folder
                    If Me.ParentDirectoryPath(remoteDestinationPath) = Nothing Then Throw New NotSupportedException(ProviderStrings.GetText("CenterDeviceFoldersMustRemainInsideACollectionOr"))
                    Dim SourceDirectory As CenterDevice.IO.DirectoryInfo = Me.GetDirectoryItem(remoteSource)
                    Dim ExistingDestination As DmsResourceItem = Me.ListRemoteItem(remoteDestinationPath)
                    If ExistingDestination IsNot Nothing Then
                        If allowOverwrite <> True Then Throw New DirectoryAlreadyExistsException(remoteDestinationPath)
                        Me.MergeDirectoryContents(SourceDirectory, Me.GetDirectoryItem(remoteDestinationPath, RessourceNotFoundHandling.ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound), True, remoteSource.FullName, remoteDestinationPath)
                        SourceDirectory.Delete()
                    Else
                        Dim DestinationParent As CenterDevice.IO.DirectoryInfo = Me.GetDirectoryItem(Me.ParentDirectoryPath(remoteDestinationPath), RessourceNotFoundHandling.ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound)
                        Me.MoveDirectoryExact(SourceDirectory, DestinationParent, Me.ItemName(remoteDestinationPath), remoteSource.FullName, remoteDestinationPath)
                    End If
                Case DmsResourceItem.ItemTypes.Collection
                    If Me.ParentDirectoryPath(remoteSource.FullName) <> Nothing OrElse Me.ParentDirectoryPath(remoteDestinationPath) <> Nothing Then
                        Throw New NotSupportedException(ProviderStrings.GetText("CenterDeviceCollectionsCanOnlyBeRenamedWhileRemaining"))
                    End If
                    If Me.ListRemoteItem(remoteDestinationPath) IsNot Nothing Then Throw New DirectoryAlreadyExistsException(remoteDestinationPath)
                    Me.GetDirectoryItem(remoteSource).Rename(Me.ItemName(remoteDestinationPath))
                Case Else
                    Throw New NotSupportedException(ProviderStrings.Format("UnsupportedSourceItemType2", remoteSource.ItemType.ToString()))
            End Select
        End Sub

        Private Sub CopyDirectoryTree(sourceDirectory As CenterDevice.IO.DirectoryInfo, destinationParent As CenterDevice.IO.DirectoryInfo, destinationName As String, sourcePath As String, destinationPath As String)
            Me.ValidateUniqueChildNames(sourceDirectory, sourcePath)
            destinationParent.CreateDirectory(destinationName)
            destinationParent.ResetDirectoriesCache()
            Dim DestinationDirectory As CenterDevice.IO.DirectoryInfo = destinationParent.GetDirectory(destinationName)
            Me.MergeDirectoryContents(sourceDirectory, DestinationDirectory, False, sourcePath, destinationPath)
        End Sub

        Private Sub MergeDirectoryContents(sourceDirectory As CenterDevice.IO.DirectoryInfo, destinationDirectory As CenterDevice.IO.DirectoryInfo, moveItems As Boolean, sourcePath As String, destinationPath As String)
            Me.ValidateUniqueChildNames(sourceDirectory, sourcePath)
            Me.ValidateUniqueChildNames(destinationDirectory, destinationPath)

            Try
                For Each SourceFile As CenterDevice.IO.FileInfo In sourceDirectory.GetFiles()
                    Dim ChildSourcePath As String = Me.CombinePath(sourcePath, SourceFile.FileName)
                    Dim ChildDestinationPath As String = Me.CombinePath(destinationPath, SourceFile.FileName)
                    If destinationDirectory.DirectoryExists(SourceFile.FileName) Then Throw New DirectoryAlreadyExistsException(ChildDestinationPath)
                    If moveItems Then
                        Me.MoveFileExact(SourceFile, destinationDirectory, SourceFile.FileName, ChildSourcePath, ChildDestinationPath, True)
                    Else
                        Me.CopyFileExact(SourceFile, destinationDirectory, SourceFile.FileName, ChildSourcePath, ChildDestinationPath, True)
                    End If
                Next

                For Each SourceChildDirectory As CenterDevice.IO.DirectoryInfo In sourceDirectory.GetDirectories()
                    Dim ChildSourcePath As String = Me.CombinePath(sourcePath, SourceChildDirectory.Name)
                    Dim ChildDestinationPath As String = Me.CombinePath(destinationPath, SourceChildDirectory.Name)
                    If destinationDirectory.FileExists(SourceChildDirectory.Name) Then Throw New FileAlreadyExistsException(ChildDestinationPath)
                    If destinationDirectory.DirectoryExists(SourceChildDirectory.Name) Then
                        Dim DestinationChildDirectory As CenterDevice.IO.DirectoryInfo = destinationDirectory.GetDirectory(SourceChildDirectory.Name)
                        Me.MergeDirectoryContents(SourceChildDirectory, DestinationChildDirectory, moveItems, ChildSourcePath, ChildDestinationPath)
                        If moveItems Then SourceChildDirectory.Delete()
                    ElseIf moveItems Then
                        Me.MoveDirectoryExact(SourceChildDirectory, destinationDirectory, SourceChildDirectory.Name, ChildSourcePath, ChildDestinationPath)
                    Else
                        Me.CopyDirectoryTree(SourceChildDirectory, destinationDirectory, SourceChildDirectory.Name, ChildSourcePath, ChildDestinationPath)
                    End If
                Next
            Finally
                sourceDirectory.ResetFilesCache()
                sourceDirectory.ResetDirectoriesCache()
                destinationDirectory.ResetFilesCache()
                destinationDirectory.ResetDirectoriesCache()
            End Try
        End Sub

        Private Sub ValidateUniqueChildNames(directory As CenterDevice.IO.DirectoryInfo, directoryPath As String)
            Dim Names As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For Each File As CenterDevice.IO.FileInfo In directory.GetFiles()
                If Not Names.Add(File.FileName) Then Throw New RemotePathNotUniqueException(Me.CombinePath(directoryPath, File.FileName))
            Next
            For Each ChildDirectory As CenterDevice.IO.DirectoryInfo In directory.GetDirectories()
                If Not Names.Add(ChildDirectory.Name) Then Throw New RemotePathNotUniqueException(Me.CombinePath(directoryPath, ChildDirectory.Name))
            Next
        End Sub

        Private Function UniqueTemporaryName(directory As CenterDevice.IO.DirectoryInfo) As String
            Dim Candidate As String
            Do
                Candidate = ".compumaster-dms-" & Guid.NewGuid().ToString("N")
            Loop While directory.FileExists(Candidate) OrElse directory.DirectoryExists(Candidate)
            Return Candidate
        End Function

        Private Sub CopyFileExact(sourceFile As CenterDevice.IO.FileInfo, destinationParent As CenterDevice.IO.DirectoryInfo, destinationName As String, sourcePath As String, destinationPath As String, allowOverwrite As Boolean?)
            Dim ExistingDestination As CenterDevice.IO.FileInfo = destinationParent.TryGetFile(destinationName)
            If ExistingDestination IsNot Nothing Then RejectSameDocumentAction(sourceFile.ID, ExistingDestination.ID, destinationPath)
            If ExistingDestination Is Nothing Then
                destinationParent.AddCopy(sourceFile, destinationName)
                Return
            End If
            If allowOverwrite <> True Then Throw New FileAlreadyExistsException(destinationPath)

            Dim TemporaryCopyName As String = Me.UniqueTemporaryName(destinationParent)
            Dim BackupName As String = Me.UniqueTemporaryName(destinationParent)
            destinationParent.AddCopy(sourceFile, TemporaryCopyName)
            destinationParent.ResetFilesCache()
            Dim TemporaryCopy As CenterDevice.IO.FileInfo = destinationParent.GetFile(TemporaryCopyName)
            Try
                ExistingDestination.Rename(BackupName)
            Catch ex As Exception
                Try
                    TemporaryCopy.Delete()
                Catch rollbackException As Exception
                    Throw New FileActionFailedException("copy", sourcePath, destinationPath, New AggregateException(ex, rollbackException))
                End Try
                Throw New FileActionFailedException("copy", sourcePath, destinationPath, ex)
            End Try
            Try
                TemporaryCopy.Rename(destinationName)
            Catch ex As Exception
                Dim RollbackErrors As New List(Of Exception) From {ex}
                Try
                    ExistingDestination.Rename(destinationName)
                Catch rollbackException As Exception
                    RollbackErrors.Add(rollbackException)
                End Try
                Try
                    TemporaryCopy.Delete()
                Catch rollbackException As Exception
                    RollbackErrors.Add(rollbackException)
                End Try
                Throw New FileActionFailedException("copy", sourcePath, destinationPath, New AggregateException(ProviderStrings.GetText("TheCopiedFileCouldnTBePromotedTo"), RollbackErrors))
            End Try
            Try
                ExistingDestination.Delete()
            Catch ex As Exception
                Throw New FileActionFailedException("copy", sourcePath, destinationPath, New InvalidOperationException(ProviderStrings.Format("TheCopySucceededButTheReplacedFileWith", ExistingDestination.ID, BackupName), ex))
            End Try
        End Sub

        Private Sub MoveFileExact(sourceFile As CenterDevice.IO.FileInfo, destinationParent As CenterDevice.IO.DirectoryInfo, destinationName As String, sourcePath As String, destinationPath As String, allowOverwrite As Boolean?)
            Dim ExistingDestination As CenterDevice.IO.FileInfo = destinationParent.TryGetFile(destinationName)
            If ExistingDestination IsNot Nothing Then RejectSameDocumentAction(sourceFile.ID, ExistingDestination.ID, destinationPath)
            If ExistingDestination IsNot Nothing AndAlso allowOverwrite <> True Then Throw New FileAlreadyExistsException(destinationPath)
            Dim BackupName As String = Nothing
            If ExistingDestination IsNot Nothing Then
                BackupName = Me.UniqueTemporaryName(destinationParent)
                ExistingDestination.Rename(BackupName)
            End If

            Try
                Me.MoveFileToFinalName(sourceFile, destinationParent, destinationName, sourcePath, destinationPath)
            Catch ex As Exception
                If ExistingDestination IsNot Nothing Then
                    Try
                        ExistingDestination.Rename(destinationName)
                    Catch rollbackException As Exception
                        Throw New FileActionFailedException("move", sourcePath, destinationPath, New AggregateException(ex, rollbackException))
                    End Try
                End If
                Throw
            End Try

            If ExistingDestination IsNot Nothing Then
                Try
                    ExistingDestination.Delete()
                Catch ex As Exception
                    Throw New FileActionFailedException("move", sourcePath, destinationPath, New InvalidOperationException(ProviderStrings.Format("TheMoveSucceededButTheReplacedFileWith", ExistingDestination.ID, BackupName), ex))
                End Try
            End If
        End Sub

        Private Shared Sub RejectSameDocumentAction(sourceId As String, destinationId As String, destinationPath As String)
            If Not String.IsNullOrEmpty(sourceId) AndAlso String.Equals(sourceId, destinationId, StringComparison.Ordinal) Then
                Throw New ArgumentException(ProviderStrings.GetText("SourceAndDestinationIdentifyTheSameDocumentReplacement"), NameOf(destinationPath))
            End If
        End Sub

        Private Sub MoveFileToFinalName(sourceFile As CenterDevice.IO.FileInfo, destinationParent As CenterDevice.IO.DirectoryInfo, destinationName As String, sourcePath As String, destinationPath As String)
            Dim OriginalName As String = sourceFile.FileName
            Dim OriginalParent As CenterDevice.IO.DirectoryInfo = sourceFile.ParentDirectory
            Dim SameParent As Boolean = Me.IsSameDirectory(OriginalParent, destinationParent)
            If SameParent Then
                sourceFile.Rename(destinationName)
                Return
            ElseIf OriginalName = destinationName Then
                sourceFile.Move(destinationParent)
                Return
            End If

            Dim TemporaryName As String = Me.UniqueTemporaryName(OriginalParent)
            Dim WasMoved As Boolean = False
            sourceFile.Rename(TemporaryName)
            Try
                sourceFile.Move(destinationParent)
                WasMoved = True
                sourceFile.Rename(destinationName)
            Catch ex As Exception
                Dim RollbackErrors As New List(Of Exception) From {ex}
                If WasMoved Then
                    Try
                        sourceFile.Move(OriginalParent)
                    Catch rollbackException As Exception
                        RollbackErrors.Add(rollbackException)
                    End Try
                End If
                Try
                    sourceFile.Rename(OriginalName)
                Catch rollbackException As Exception
                    RollbackErrors.Add(rollbackException)
                End Try
                Throw New FileActionFailedException("move", sourcePath, destinationPath, New AggregateException(ProviderStrings.Format("MoveFailedForFileIDRollbackWasAttempted", sourceFile.ID), RollbackErrors))
            End Try
        End Sub

        Private Sub MoveDirectoryExact(sourceDirectory As CenterDevice.IO.DirectoryInfo, destinationParent As CenterDevice.IO.DirectoryInfo, destinationName As String, sourcePath As String, destinationPath As String)
            Dim OriginalName As String = sourceDirectory.Name
            Dim OriginalParent As CenterDevice.IO.DirectoryInfo = sourceDirectory.ParentDirectory
            Dim SameParent As Boolean = Me.IsSameDirectory(OriginalParent, destinationParent)
            If SameParent Then
                sourceDirectory.Rename(destinationName)
                Return
            ElseIf OriginalName = destinationName Then
                sourceDirectory.Move(destinationParent)
                Return
            End If

            Dim TemporaryName As String = Me.UniqueTemporaryName(OriginalParent)
            Dim WasMoved As Boolean = False
            sourceDirectory.Rename(TemporaryName)
            Try
                sourceDirectory.Move(destinationParent)
                WasMoved = True
                sourceDirectory.Rename(destinationName)
            Catch ex As Exception
                Dim RollbackErrors As New List(Of Exception) From {ex}
                If WasMoved Then
                    Try
                        sourceDirectory.Move(OriginalParent)
                    Catch rollbackException As Exception
                        RollbackErrors.Add(rollbackException)
                    End Try
                End If
                Try
                    sourceDirectory.Rename(OriginalName)
                Catch rollbackException As Exception
                    RollbackErrors.Add(rollbackException)
                End Try
                Throw New DirectoryActionFailedException("move", sourcePath, destinationPath, New AggregateException(ProviderStrings.Format("MoveFailedForFolderIDRollbackWasAttempted", sourceDirectory.FolderID), RollbackErrors))
            End Try
        End Sub

        Private Function IsSameDirectory(first As CenterDevice.IO.DirectoryInfo, second As CenterDevice.IO.DirectoryInfo) As Boolean
            If first Is Nothing OrElse second Is Nothing Then Return False
            If first.IsRootDirectory OrElse second.IsRootDirectory Then Return first.IsRootDirectory AndAlso second.IsRootDirectory
            If first.FolderID <> Nothing OrElse second.FolderID <> Nothing Then Return first.FolderID = second.FolderID
            Return first.CollectionID = second.CollectionID
        End Function

        ''' <inheritdoc/>
        Protected Overrides Sub CopyFileItem(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?)
            Dim Source As DmsResourceItem = Me.RequireLegacySourceItem(remoteSourcePath, DmsResourceItem.ItemTypes.File)
            Me.CopyItem(Source, remoteDestinationPath, allowOverwrite)
        End Sub

        ''' <inheritdoc/>
        Protected Overrides Function CopyFileItemAsync(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.CopyFileItem(remoteSourcePath, remoteDestinationPath, allowOverwrite), Me.CurrentAsyncCancellationToken)
        End Function

        ''' <inheritdoc/>
        Protected Overrides Sub CopyDirectoryItem(remoteSourcePath As String, remoteDestinationPath As String)
            Dim Source As DmsResourceItem = Me.RequireLegacySourceItem(remoteSourcePath, DmsResourceItem.ItemTypes.Folder)
            Me.CopyItem(Source, remoteDestinationPath, False)
        End Sub

        ''' <inheritdoc/>
        Protected Overrides Function CopyDirectoryItemAsync(remoteSourcePath As String, remoteDestinationPath As String) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.CopyDirectoryItem(remoteSourcePath, remoteDestinationPath), Me.CurrentAsyncCancellationToken)
        End Function

        ''' <inheritdoc/>
        Protected Overrides Sub MoveFileItem(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?)
            Dim Source As DmsResourceItem = Me.RequireLegacySourceItem(remoteSourcePath, DmsResourceItem.ItemTypes.File)
            Me.MoveItem(Source, remoteDestinationPath, allowOverwrite)
        End Sub

        ''' <inheritdoc/>
        Protected Overrides Sub MoveDirectoryItem(remoteSourcePath As String, remoteDestinationPath As String)
            Dim Source As DmsResourceItem = Me.ListRemoteItem(remoteSourcePath)
            If Source Is Nothing Then Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(remoteSourcePath)
            If Source.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(remoteSourcePath)
            If Source.ItemType <> DmsResourceItem.ItemTypes.Folder AndAlso Source.ItemType <> DmsResourceItem.ItemTypes.Collection Then Throw New ArgumentException(ProviderStrings.GetText("TheSourceItemIsnTADirectory"), NameOf(remoteSourcePath))
            Me.MoveItem(Source, remoteDestinationPath, False)
        End Sub

        Private Function RequireLegacySourceItem(remoteSourcePath As String, expectedType As DmsResourceItem.ItemTypes) As DmsResourceItem
            Dim Source As DmsResourceItem = Me.ListRemoteItem(remoteSourcePath)
            If Source Is Nothing Then
                If expectedType = DmsResourceItem.ItemTypes.File Then
                    Throw New CompuMaster.Dms.Data.FileNotFoundException(remoteSourcePath)
                Else
                    Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(remoteSourcePath)
                End If
            End If
            If Source.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(remoteSourcePath)
            If Source.ItemType <> expectedType Then Throw New ArgumentException(ProviderStrings.GetText("TheSourceItemHasAnUnexpectedResourceType"), NameOf(remoteSourcePath))
            Return Source
        End Function

        Private Function FindUploadLinkForCollection(collectionID As String) As CenterDevice.Rest.Clients.Link.UploadLink
            If collectionID <> Nothing AndAlso AllUploadLinks.UploadLinksList.ConvertAll(Of String)(Function(item) item.Collection).Contains(collectionID) Then
                Return AllUploadLinks.UploadLinksList.Find(Function(item) item.Collection = collectionID)
            Else
                Return Nothing
            End If
        End Function

        ''' <inheritdoc/>
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

        ''' <inheritdoc/>
        Public Overrides Sub DeleteRemoteItem(remoteItem As DmsResourceItem)
            Select Case remoteItem.ItemType
                Case DmsResourceItem.ItemTypes.Root
                    Throw New DmsUserErrorMessageException(ProviderStrings.GetText("RootFolderCanTBeDeleted"))
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

        ''' <inheritdoc/>
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

        ''' <inheritdoc/>
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

        ''' <inheritdoc/>
        Public Overrides Sub CreateCollection(remoteCollectionName As String)
            Dim ioExceptionMessage As String = "CreateCollection failed: " & remoteCollectionName
            Try
                Dim NewChildDirName As String = Me.ItemName(remoteCollectionName)
                Dim ParentRemoteDirName As String = Me.ParentDirectoryPath(remoteCollectionName)
                If ParentRemoteDirName <> Nothing Then
                    'if this situation wouldn't be catched, then otherwise creation of collection in another parent directory would lead
                    'to a new collection below root directory (instead of the expected parent directory)
                    '=> considered as a bug in CenterDevice API / CenterDevice architecture
                    Throw New NotSupportedException(ProviderStrings.Format("CollectionsMustBeLocatedInRootFolderOnly", remoteCollectionName, ParentRemoteDirName))
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

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property DirectorySeparator As Char
            Get
                Return "/"c
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsCollections As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <inheritdoc/>
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
            Dim links = If(res.CollectionID <> Nothing, AllUploadLinks(), Nothing)
            Dim collision = res.ParentDirectory IsNot Nothing AndAlso res.HasCollidingDuplicateDirectory
            Return Me.CreateDmsResourceItem(res, links, collision)
        End Function

        'Snapshot inputs let native async callers avoid synchronous lookups during conversion.
        Private Function CreateDmsResourceItem(res As CenterDevice.IO.DirectoryInfo, uploadLinks As UploadLinks, collision As Boolean) As DmsResourceItem
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
                Throw New NotImplementedException(ProviderStrings.GetText("UnknownItemTypeAdditionalImplementationRequired"))
            End If
            Result.FullName = res.FullName
            Result.Name = res.Name
            Result.ExtendedInfosFileID = Nothing
            Result.ExtendedInfosCollectionID = res.AssociatedCollection?.CollectionID
            Result.ExtendedInfosFolderID = res.FolderID
            Result.ExtendedInfosAssignedCollectionID = res.ParentDirectory?.AssociatedCollection?.CollectionID
            Result.ExtendedInfosAssignedFolderID = res.ParentDirectory?.FolderID
            Result.HasChildDirectories = ReadHasChildDirectoriesMetadata(res)
            Result.ExtendedInfosOwner = New DmsUser() With {.ID = res.Owner, .Provider = Me, .GetDisplayName = AddressOf CenterDeviceDmsProviderBase.DelegatedGetDisplayName, .GetEMailAddress = AddressOf CenterDeviceDmsProviderBase.DelegatedGetUserEMailAddress}
            Result.ExtendedInfosLinks = New List(Of DmsLink)
            If res.Link <> Nothing Then Result.ExtendedInfosLinks.Add(New DmsLink(Result, res.Link, Me, AddressOf CenterDeviceDmsProviderBase.DelegatedFillLinkDetails))
            If res.CollectionID <> Nothing AndAlso uploadLinks.UploadLinksList.ConvertAll(Of String)(Function(item) item.Collection).Contains(res.CollectionID) Then
                Dim UploadLink As CenterDevice.Rest.Clients.Link.UploadLink = uploadLinks.UploadLinksList.Find(Function(item) item.Collection = res.CollectionID)
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
            'Objects loaded directly by ID have no parent directory in the IO wrapper;
            'its collision property requires that parent and cannot be evaluated here.
            Result.ExtendedInfosCollisionDetected = collision
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
                        Result.ExtendedInfosUserSharings.Add(New DmsShareForUser(Result, New DmsUser() With {.ID = ResSharing, .Provider = Me, .GetDisplayName = AddressOf CenterDeviceDmsProviderBase.DelegatedGetDisplayName, .GetEMailAddress = AddressOf CenterDeviceDmsProviderBase.DelegatedGetUserEMailAddress}, True, True, True, True, True, True))
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

        Private Shared Function ReadHasChildDirectoriesMetadata(res As CenterDevice.IO.DirectoryInfo) As Boolean?
            'The IO wrapper keeps the server-provided flags on its response objects without exposing them publicly.
            'Read them opportunistically and fall back to unknown when a future package version changes that shape.
            Const InstanceFields As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
            If res.CollectionID <> Nothing Then
                Dim RestCollectionField As FieldInfo = GetType(CenterDevice.IO.DirectoryInfo).GetField("restCollection", InstanceFields)
                Return ReadNullableBooleanProperty(RestCollectionField?.GetValue(res), "HasFolders")
            Else
                Dim RestFolderField As FieldInfo = GetType(CenterDevice.IO.DirectoryInfo).GetField("restFolder", InstanceFields)
                Return ReadNullableBooleanProperty(RestFolderField?.GetValue(res), "HasSubFolders")
            End If
        End Function

        Private Shared Function ReadNullableBooleanProperty(instance As Object, propertyName As String) As Boolean?
            If instance Is Nothing Then Return Nothing
            Const InstanceProperties As BindingFlags = BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic
            Dim Value As Object = instance.GetType().GetProperty(propertyName, InstanceProperties)?.GetValue(instance)
            If Value Is Nothing Then Return Nothing
            Return CType(Value, Boolean?)
        End Function

        ''' <summary>
        ''' Fill DmsResourceItem from CenterDevice.IO.FileInfo
        ''' </summary>
        ''' <param name="res"></param>
        ''' <returns></returns>
        Private Function CreateDmsResourceItem(res As CenterDevice.IO.FileInfo) As DmsResourceItem
            Return Me.CreateDmsResourceItem(res, res.ParentDirectory IsNot Nothing AndAlso res.HasCollidingDuplicateFile)
        End Function

        Private Function CreateDmsResourceItem(res As CenterDevice.IO.FileInfo, collision As Boolean) As DmsResourceItem
            Dim Result As New DmsResourceItem() With {
                            .Name = res.FileName,
                            .IsHidden = False,
                            .ItemType = DmsResourceItem.ItemTypes.File,
                            .ContentLength = res.Size,
                            .ProviderSpecificHashOrETag = res.ID}
            Result.CreatedOnLocalTime = DateTimeUtcToLocalTime(res.UploadDate)
            Result.LastModificationOnLocalTime = DateTimeUtcToLocalTime(res.ModificationDate)

            'Assign additional fields
            'A direct identifier lookup has no selected parent path; retain the name and references.
            Result.FullName = If(res.ParentDirectory Is Nothing, res.FileName, res.FullName)
            Result.Name = res.FileName
            Result.ExtendedInfosFileID = res.ID
            Result.ExtendedInfosCollectionID = Nothing
            Result.ExtendedInfosFolderID = Nothing
            Result.ExtendedInfosAssignedCollectionID = res.ParentDirectory?.AssociatedCollection?.CollectionID
            Result.ExtendedInfosAssignedFolderID = res.ParentDirectory?.FolderID
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
            Result.ExtendedInfosOwner = New DmsUser() With {.ID = res.Owner, .Provider = Me, .GetDisplayName = AddressOf CenterDeviceDmsProviderBase.DelegatedGetDisplayName, .GetEMailAddress = AddressOf CenterDeviceDmsProviderBase.DelegatedGetUserEMailAddress}
            Result.ExtendedInfosLastModificationUser = New DmsUser() With {.ID = res.Uploader, .Provider = Me, .GetDisplayName = AddressOf CenterDeviceDmsProviderBase.DelegatedGetDisplayName, .GetEMailAddress = AddressOf CenterDeviceDmsProviderBase.DelegatedGetUserEMailAddress}
            Result.ExtendedInfosLinks = New List(Of DmsLink)
            If Not res.Link = Nothing Then Result.ExtendedInfosLinks.Add(New DmsLink(Result, res.Link, Me, AddressOf CenterDeviceDmsProviderBase.DelegatedFillLinkDetails))
            Result.ExtendedInfosLocks = res.Locks
            Result.ExtendedInfosLockedByUser = New DmsUser() With {.ID = res.LockedBy, .Provider = Me, .GetDisplayName = AddressOf CenterDeviceDmsProviderBase.DelegatedGetDisplayName, .GetEMailAddress = AddressOf CenterDeviceDmsProviderBase.DelegatedGetUserEMailAddress}
            Result.ExtendedInfosArchivedDateLocalTime = DateTimeUtcToLocalTime(res.ArchivedDate)
            Result.ExtendedInfosVersion = res.Version.ToString
            Result.ExtendedInfosVersionDateLocalTime = DateTimeUtcToLocalTime(res.VersionDate)
            Result.ExtendedInfosCollisionDetected = collision
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
                        Result.ExtendedInfosUserSharings.Add(New DmsShareForUser(Result, New DmsUser() With {.ID = ResSharing, .Provider = Me, .GetDisplayName = AddressOf CenterDeviceDmsProviderBase.DelegatedGetDisplayName, .GetEMailAddress = AddressOf CenterDeviceDmsProviderBase.DelegatedGetUserEMailAddress}, True, True, True, True, True, True))
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
            If res.ParentDirectory IsNot Nothing AndAlso res.ParentDirectory.IsRootDirectory = False AndAlso res.ParentDirectory.CollectionID = Nothing Then
                Result.Folder = ParentFolderName
            End If
            Result.Collection = res.ParentDirectory?.AssociatedCollection?.FullName

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

        ''' <summary>Loads the details of an existing download link into the supplied snapshot.</summary>
        ''' <param name="provider">The provider that owns the resource or identity.</param>
        ''' <param name="linkId">The existing download-link identifier.</param>
        ''' <param name="dmsLink">The link snapshot to populate.</param>
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

        ''' <summary>Loads the details of an existing upload link into the supplied snapshot.</summary>
        ''' <param name="provider">The provider that owns the resource or identity.</param>
        ''' <param name="uploadLinkId">The existing upload-link identifier.</param>
        ''' <param name="dmsLink">The link snapshot to populate.</param>
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

        ''' <summary>Resolves a group name through the supplied CenterDevice-based provider.</summary>
        ''' <param name="provider">The provider that owns the resource or identity.</param>
        ''' <param name="groupId">The provider-owned group identifier.</param>
        ''' <returns>The group display name returned by the provider.</returns>
        Public Shared Function DelegatedGetGroupName(provider As BaseDmsProvider, groupId As String) As String
            Dim centerDeviceProvider As CenterDeviceDmsProviderBase = CType(provider, CenterDeviceDmsProviderBase)
            Return centerDeviceProvider.NormalizeGroupDisplayName(groupId, centerDeviceProvider.IOClient.GroupName(groupId))
        End Function

        ''' <summary>Resolves the display name of a CenterDevice group from its identifier and SDK name.</summary>
        ''' <param name="groupId">The provider-specific group ID.</param>
        ''' <param name="groupName">The group name returned by the SDK, if available.</param>
        ''' <returns>The name to show for the group, or the original SDK name when no alternative is available.</returns>
        Protected Overridable Function NormalizeGroupDisplayName(groupId As String, groupName As String) As String
            Return groupName
        End Function

        ''' <summary>Resolves the display name of a CenterDevice user.</summary>
        ''' <param name="provider">The CenterDevice provider instance.</param>
        ''' <param name="userId">The provider-specific user ID.</param>
        ''' <returns>The trimmed display name, or an empty string when no name is available.</returns>
        Public Shared Function DelegatedGetDisplayName(provider As BaseDmsProvider, userId As String) As String
            Dim displayName As String = CType(provider, CenterDeviceDmsProviderBase).LookupUserDisplayName(userId)
            Return If(displayName, String.Empty).Trim()
        End Function

        ''' <summary>Resolves a CenterDevice user's display name for this provider.</summary>
        ''' <param name="userId">The provider-specific user ID.</param>
        ''' <returns>The display name, or an empty string when no name is available.</returns>
        Protected Overridable Function LookupUserDisplayName(userId As String) As String
            Return Me.IOClient.UserName(userId)
        End Function

        ''' <summary>Resolves a CenterDevice user's display name through the legacy method.</summary>
        ''' <param name="provider">The CenterDevice provider instance.</param>
        ''' <param name="userId">The provider-specific user ID.</param>
        ''' <returns>The trimmed display name, or an empty string when no name is available.</returns>
        <Obsolete("Use DelegatedGetDisplayName instead.", True), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>
        Public Shared Function DelegatedGetUserName(provider As BaseDmsProvider, userId As String) As String
            Return DelegatedGetDisplayName(provider, userId)
        End Function

        ''' <summary>Resolves the legacy user-email display value through the supplied provider.</summary>
        ''' <param name="provider">The provider that owns the resource or identity.</param>
        ''' <param name="userId">The provider-owned user identifier.</param>
        ''' <returns>The provider-supplied user email address, when available.</returns>
        Public Shared Function DelegatedGetUserEMailAddress(provider As BaseDmsProvider, userId As String) As String
            Return CType(provider, CenterDeviceDmsProviderBase).IOClient.UserEMailAddress(userId)
        End Function

        ''' <inheritdoc/>
        ''' <remarks>Upload links target collections. Their <see cref="DmsLink.MaxBytes"/> limit must be a positive multiple of 1 GiB.</remarks>
        Public Overrides Function CreateLink(dmsResource As DmsResourceItem, shareInfo As DmsLink) As DmsLink
            If shareInfo.AllowEdit Then Throw New NotSupportedException(ProviderStrings.GetText("AllowEditNotSupportedByProvider"))
            If shareInfo.AllowDelete Then Throw New NotSupportedException(ProviderStrings.GetText("AllowDeleteNotSupportedByProvider"))
            If shareInfo.AllowShare Then Throw New NotSupportedException(ProviderStrings.GetText("AllowShareNotSupportedByProvider"))
            If Not (shareInfo.AllowView Xor shareInfo.AllowUpload) Then
                Throw New ArgumentException(ProviderStrings.GetText("EitherAllowViewOrAllowUploadMustBeSet"), NameOf(shareInfo))
            End If
            Try
                If shareInfo.AllowUpload Then
                    'Create upload link
                    If Not dmsResource.ItemType = DmsResourceItem.ItemTypes.Collection OrElse dmsResource.ExtendedInfosCollectionID = Nothing Then
                        Throw New NotSupportedException(ProviderStrings.GetText("UploadLinksSupportedOnlyWithCollections"))
                    End If
                    ValidateUploadLinkMaxBytes(shareInfo.MaxBytes)
                    Dim CreatedUploadLink As UploadLinkCreationResponse
                    If shareInfo.MaxBytes.HasValue Then
                        CreatedUploadLink = CenterDeviceUploadLinkBytesClient.Create(Me.IOClient.ApiClient).CreateCollectionLink(
                            Me.IOClient.CurrentAuthenticationContextUserID, dmsResource.ExtendedInfosCollectionID,
                            Tools.NotNullOrEmptyStringValue(shareInfo.Name),
                            DateTimeLocalToUtcTime(shareInfo.ExpiryDateLocalTime),
                            ConvertNarrowingToNullableInt32(shareInfo.MaxUploads),
                            shareInfo.MaxBytes.Value,
                            Tools.NotNullOrEmptyStringValue(shareInfo.Password))
                    Else
                        CreatedUploadLink = Me.IOClient.ApiClient.UploadLinks.CreateCollectionLink(Me.IOClient.CurrentAuthenticationContextUserID, dmsResource.ExtendedInfosCollectionID,
                                                                       Tools.NotNullOrEmptyStringValue(shareInfo.Name),
                                                                       Nothing,
                                                                       DateTimeLocalToUtcTime(shareInfo.ExpiryDateLocalTime),
                                                                       ConvertNarrowingToNullableInt32(shareInfo.MaxUploads),
                                                                       Tools.NotNullOrEmptyStringValue(shareInfo.Password),
                                                                       Nothing)
                    End If
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
                            Throw New NotSupportedException(ProviderStrings.GetText("SharingForRootDirectoryNotSupported"))
                        Case Else
                            Throw New NotImplementedException(ProviderStrings.Format("InvalidItemType", dmsResource.ItemType))
                    End Select
                    'Refresh caches + update current DmsResourceItem
                    Me.ResetDirectoryCacheOfParentFolderToForceReloadOfUpdatedSharings(dmsResource)
                    Dim Result As DmsLink
                    Result = New DmsLink(dmsResource, CreatedLink.Id, Me, AddressOf DelegatedFillLinkDetails)
                    dmsResource.ExtendedInfosLinks.Add(Result) 'Update current DmsResourceItem
                    Return Result
                End If
            Catch ex As ForbiddenException
                Throw New Data.DmsUserErrorMessageException(ProviderStrings.Format("Forbidden", ex.ErrorResponse.Message))
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

        Friend Shared Sub ValidateUploadLinkMaxBytes(maxBytes As Long?)
            'CenterDevice REST API v2.29, section 5.7.1: max-bytes is a positive multiple of 1 GiB.
            Const Gibibyte As Long = 1073741824L
            If maxBytes.HasValue AndAlso (maxBytes.Value <= 0 OrElse maxBytes.Value Mod Gibibyte <> 0) Then
                Throw New ArgumentOutOfRangeException(NameOf(maxBytes), ProviderStrings.GetText("UploadLinkMaxBytesMustBeAPositiveMultiple"))
            End If
        End Sub

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
                    Throw New NotSupportedException(ProviderStrings.GetText("SharingForFilesNotSupported"))
                Case DmsResourceItem.ItemTypes.Root
                    Throw New NotSupportedException(ProviderStrings.GetText("SharingForRootDirectoryNotSupported"))
                Case Else
                    Throw New NotImplementedException()
            End Select
            ValidateSharingResponse(Response)
            Me.ResetDirectoryCacheOfParentFolderToForceReloadOfUpdatedSharings(dmsResource)
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForGroup)
            Dim ListOfAddedUsers As List(Of String) = Nothing
            Dim ListOfAddedGroups As New List(Of String)(New String() {shareInfo.Group.ID})
            Me.CreateSharing(dmsResource, shareInfo, ListOfAddedGroups, ListOfAddedUsers)
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForUser)
            Dim ListOfAddedUsers As New List(Of String)(New String() {shareInfo.User.ID})
            Dim ListOfAddedGroups As List(Of String) = Nothing
            Me.CreateSharing(dmsResource, shareInfo, ListOfAddedGroups, ListOfAddedUsers)
        End Sub

        ''' <inheritdoc/>
        ''' <remarks>Upload-link <see cref="DmsLink.MaxBytes"/> values must be positive multiples of 1 GiB.</remarks>
        Public Overrides Sub UpdateLink(shareInfo As DmsLink)
            If shareInfo.ID = Nothing Then Throw New InvalidOperationException(ProviderStrings.GetText("UpdateOfLinkRequiresAnIDInDmsLink"))
            If shareInfo.AllowEdit Then Throw New NotSupportedException(ProviderStrings.GetText("AllowEditNotSupportedByProvider"))
            If shareInfo.AllowDelete Then Throw New NotSupportedException(ProviderStrings.GetText("AllowDeleteNotSupportedByProvider"))
            If shareInfo.AllowShare Then Throw New NotSupportedException(ProviderStrings.GetText("AllowShareNotSupportedByProvider"))
            If Not (shareInfo.AllowView Xor shareInfo.AllowUpload) Then
                Throw New ArgumentException(ProviderStrings.GetText("EitherAllowViewOrAllowUploadMustBeSet"), NameOf(shareInfo))
            End If
            Try
                If shareInfo.AllowUpload Then
                    'Update upload link
                    ValidateUploadLinkMaxBytes(shareInfo.MaxBytes)
                    If shareInfo.MaxBytes.HasValue Then
                        CenterDeviceUploadLinkBytesClient.Create(Me.IOClient.ApiClient).UpdateLink(
                            Me.IOClient.CurrentAuthenticationContextUserID, shareInfo.ID,
                            Tools.NotNullOrEmptyStringValue(shareInfo.Name),
                            DateTimeLocalToUtcTime(shareInfo.ExpiryDateLocalTime),
                            ConvertNarrowingToNullableInt32(shareInfo.MaxUploads),
                            shareInfo.MaxBytes.Value,
                            Tools.NotNullOrEmptyStringValue(shareInfo.Password))
                    Else
                        Me.IOClient.ApiClient.UploadLink.UpdateLink(Me.IOClient.CurrentAuthenticationContextUserID, shareInfo.ID,
                                                            CType(Nothing, String),
                                                            Tools.NotNullOrEmptyStringValue(shareInfo.Name),
                                                            Nothing,
                                                            DateTimeLocalToUtcTime(shareInfo.ExpiryDateLocalTime),
                                                            ConvertNarrowingToNullableInt32(shareInfo.MaxUploads),
                                                            Tools.NotNullOrEmptyStringValue(shareInfo.Password),
                                                            Nothing)
                    End If
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

        ''' <inheritdoc/>
        Public Overrides Sub UpdateSharing(shareInfo As DmsShareForGroup)
            Throw New NotSupportedException(ProviderStrings.GetText("UpdatingOfSharePropertiesNotSupported"))
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub UpdateSharing(shareInfo As DmsShareForUser)
            Throw New NotSupportedException(ProviderStrings.GetText("UpdatingOfSharePropertiesNotSupported"))
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub DeleteLink(shareInfo As DmsLink)
            If shareInfo.AllowEdit Then Throw New NotSupportedException(ProviderStrings.GetText("AllowEditNotSupportedByProvider"))
            If shareInfo.AllowDelete Then Throw New NotSupportedException(ProviderStrings.GetText("AllowDeleteNotSupportedByProvider"))
            If shareInfo.AllowShare Then Throw New NotSupportedException(ProviderStrings.GetText("AllowShareNotSupportedByProvider"))
            If Not (shareInfo.AllowView Xor shareInfo.AllowUpload) Then
                Throw New ArgumentException(ProviderStrings.GetText("EitherAllowViewOrAllowUploadMustBeSet"), NameOf(shareInfo))
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
                    Throw New NotSupportedException(ProviderStrings.GetText("SharingForFilesNotSupported"))
                Case DmsResourceItem.ItemTypes.Root
                    Throw New NotSupportedException(ProviderStrings.GetText("SharingForRootDirectoryNotSupported"))
                Case Else
                    Throw New NotImplementedException()
            End Select
            ValidateSharingResponse(Response)
            Me.ResetDirectoryCacheOfParentFolderToForceReloadOfUpdatedSharings(shareInfo.ParentDmsResourceItem)
        End Sub

        Friend Shared Sub ValidateSharingResponse(response As SharingResponse)
            If response Is Nothing Then Return 'The API also accepts a successful 204 response without content.
            If (response.FailedGroups IsNot Nothing AndAlso response.FailedGroups.Count > 0) OrElse
                (response.FailedUsers IsNot Nothing AndAlso response.FailedUsers.Count > 0) Then
                Throw New InvalidOperationException(ProviderStrings.GetText("TheSharingOperationFailedForOneOrMore"))
            End If
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub DeleteSharing(shareInfo As DmsShareForGroup)
            Dim ListOfDeletedUsers As List(Of String) = Nothing
            Dim ListOfDeletedGroups As New List(Of String)(New String() {shareInfo.Group.ID})
            Me.DeleteSharing(shareInfo, ListOfDeletedGroups, ListOfDeletedUsers)
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub DeleteSharing(shareInfo As DmsShareForUser)
            Dim ListOfDeletedUsers As New List(Of String)(New String() {shareInfo.User.ID})
            Dim ListOfDeletedGroups As List(Of String) = Nothing
            Me.DeleteSharing(shareInfo, ListOfDeletedGroups, ListOfDeletedUsers)
        End Sub

        ''' <inheritdoc/>
        Public Overrides Function GetAllGroups() As List(Of DmsGroup)
            Dim GroupList As GroupList = Me.IOClient.ApiClient.Groups.GetAllGroups(Me.IOClient.CurrentAuthenticationContextUserID, CenterDevice.Model.Groups.GroupsFilter.AllVisibleGroupsForCurrentUser)
            Dim Result As New List(Of DmsGroup)
            For Each Group In GroupList.Groups
                Result.Add(New DmsGroup() With {
                    .ID = Group.Id,
                    .Name = Me.NormalizeGroupDisplayName(Group.Id, Group.Name)
                   })
            Next
            Return Result
        End Function

        ''' <inheritdoc/>
        Public Overrides Function GetAllUsers() As List(Of DmsUser)
            Dim UserList As UserList(Of BaseUserData) = Me.IOClient.ApiClient.Users.GetAllUsers(Me.IOClient.CurrentAuthenticationContextUserID, New String() {
            CenterDevice.Rest.Clients.User.UserStatus.ACTIVE
            })
            Dim Result As New List(Of DmsUser)
            For Each User In UserList.Users
                Result.Add(New DmsUser() With {
                    .ID = User.Id,
                    .DisplayName = If(User.GetFullName, String.Empty).Trim(),
                    .EMailAddress = User.Email
                   })
            Next
            Return Result
        End Function

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property CurrentContextUserID As String
            Get
                Return Me.IOClient.CurrentContextUserId
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsSubFolderConfiguration As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsRuntimeAccessToRemoteServer As RuntimeAccessTypes
            Get
                Return RuntimeAccessTypes.ConfigurationAndRuntimeAccess
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsFilesInRootFolder As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsNonUniqueRemoteItems As Boolean
            Get
                Return True
            End Get
        End Property

    End Class

End Namespace
