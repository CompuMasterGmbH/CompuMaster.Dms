Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data

Namespace Providers

    ''' <summary>
    ''' A DMS provider instance
    ''' </summary>
    Public MustInherit Class BaseDmsProvider

        Public Enum DmsProviders As Integer
            <System.ComponentModel.Description("URL (manueller Transfer)")>
            ManualUrl = -1
            None = 0
            <System.ComponentModel.Description("WebDAV (OwnCloud, NextCloud, etc.)")>
            WebDAV = 1
            <System.ComponentModel.Description("Scopevisio Teamwork")>
            Scopevisio = 20
            CenterDevice = 21
        End Enum

        ''' <summary>
        ''' The unique ID of the provider
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property DmsProviderID As DmsProviders

        ''' <summary>
        ''' The name of the provider
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property Name As String

        ''' <summary>
        ''' The url to access the web API endpoint 
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property WebApiDefaultUrl As String

        ''' <summary>
        ''' Available configuration options for url of DMS service
        ''' </summary>
        Public Enum UrlCustomizationType As Byte
            ''' <summary>
            ''' DMS webservice endpoint exactly matches to WebApiDefaultUrl
            ''' </summary>
            WebApiUrlNotCustomizable = 1
            ''' <summary>
            ''' DMS webservice requires an url to login at a customer instance
            ''' </summary>
            WebApiUrlMustBeCustomized = 2
            ''' <summary>
            ''' DMS webservice provides an url field to optionally login at a customer instance
            ''' </summary>
            WebApiUrlCanBeCustomized = 3
        End Enum

        ''' <summary>
        ''' Configuration options for url of DMS service
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property WebApiUrlCustomization As UrlCustomizationType

        ''' <summary>
        ''' Available configuration options for customer reference in user credentials
        ''' </summary>
        Public Enum UserCustomerReferenceType As Byte
            ''' <summary>
            ''' DMS webservice endpoint doesn't provide login fields for selecting a customer instance on the remote DMS server
            ''' </summary>
            WithoutCustomerReference = 1
            ''' <summary>
            ''' The DMS webservice endpoint definition requires an information on the requested customer instance, e.g. a client no.
            ''' </summary>
            CustomerReferenceRequired = 2
            ''' <summary>
            ''' The DMS webservice endpoint definition provides a field to optionally select a customer instance, e.g. a client no.
            ''' </summary>
            CustomerReferenceOptional = 3
        End Enum

        ''' <summary>
        ''' Configuration options for customer reference in user credentials
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property WebApiUserCustomerReferenceRequirement As UserCustomerReferenceType

        ''' <summary>
        ''' A work url for accessing DMS remote file system
        ''' </summary>
        ''' <param name="loginCredentials">The work url is sometimes depending on the login user</param>
        ''' <returns></returns>
        Protected MustOverride Function CustomizedWebApiUrl(loginCredentials As BaseDmsLoginCredentials) As String

        ''' <summary>
        ''' Search filter criteria based on item type
        ''' </summary>
        Public Enum SearchItemType As Byte
            AllItems = 0
            Collections = 1
            Folders = 2
            Files = 3
        End Enum

        ''' <summary>
        ''' Open a remote item (file/folder/collection) or null/Nothing if the remote item doesn't exist
        ''' </summary>
        ''' <param name="remotePath"></param>
        ''' <returns></returns>
        Public MustOverride Function ListRemoteItem(remotePath As String) As DmsResourceItem

        ''' <summary>
        ''' An existance check for a remote item
        ''' </summary>
        ''' <param name="remotePath"></param>
        ''' <returns></returns>
        Public Overridable Function RemoteItemExists(remotePath As String) As Boolean
            Dim RemoteItem As DmsResourceItem
            RemoteItem = Me.ListRemoteItem(remotePath)
            Return RemoteItem IsNot Nothing
        End Function

        ''' <summary>
        ''' An existance check for a remote item
        ''' </summary>
        ''' <param name="remotePath"></param>
        ''' <returns></returns>
        Public Overridable Function RemoteItemExistsAs(remotePath As String) As DmsResourceItem.FoundItemType
            Dim RemoteItem As DmsResourceItem
            RemoteItem = Me.ListRemoteItem(remotePath)
            If RemoteItem Is Nothing Then
                Return DmsResourceItem.FoundItemType.NotFound
            Else
                Return CType(CType(RemoteItem.ItemType, Byte), DmsResourceItem.FoundItemType)
            End If
        End Function

        ''' <summary>
        ''' An existance check for a remote item (collissions with remote items under the very same name are checked)
        ''' </summary>
        ''' <param name="remotePath"></param>
        ''' <returns></returns>
        Public Overridable Function RemoteItemExistsUniquelyAs(remotePath As String) As DmsResourceItem.FoundItemResult
            Dim RemoteItem As DmsResourceItem
            RemoteItem = Me.ListRemoteItem(remotePath)
            If RemoteItem Is Nothing Then
                Return DmsResourceItem.FoundItemResult.NotFound
            ElseIf RemoteItem.ExtendedInfosCollisionDetected Then
                Return DmsResourceItem.FoundItemResult.WithNameCollisions
            Else
                Return CType(CType(RemoteItem.ItemType, Byte), DmsResourceItem.FoundItemResult)
            End If
        End Function

        ''' <summary>
        ''' Reset file system cache and force refresh on next access
        ''' </summary>
        ''' <param name="remoteItem">A directory which might contain lists of children items</param>
        ''' <param name="searchType"></param>
        Public Sub ResetCachesForRemoteItems(remoteItem As DmsResourceItem, searchType As SearchItemType)
            Select Case remoteItem.ItemType
                Case DmsResourceItem.ItemTypes.Collection, DmsResourceItem.ItemTypes.Folder, DmsResourceItem.ItemTypes.Root

                Case DmsResourceItem.ItemTypes.File
                    Throw New NotSupportedException("Files don't contain directory caches")
                Case Else
                    Throw New NotImplementedException
            End Select
        End Sub

        ''' <summary>
        ''' Reset file system cache and force refresh on next access
        ''' </summary>
        ''' <param name="remoteFolderPath"></param>
        ''' <param name="searchType"></param>
        Public MustOverride Sub ResetCachesForRemoteItems(remoteFolderPath As String, searchType As SearchItemType)

        ''' <summary>
        ''' List all child items (files/folders/collections) for a remote path
        ''' </summary>
        ''' <param name="remoteFolderPath"></param>
        ''' <param name="searchType"></param>
        ''' <returns></returns>
        Public MustOverride Function ListAllRemoteItems(remoteFolderPath As String, searchType As SearchItemType) As List(Of DmsResourceItem)

        ''' <summary>
        ''' List all child collections for a remote path
        ''' </summary>
        ''' <param name="remoteFolderPath"></param>
        ''' <returns></returns>
        Public Overridable Function ListAllCollectionItems(remoteFolderPath As String) As List(Of DmsResourceItem)
            Dim Result As New List(Of DmsResourceItem)
            For Each Item In Me.ListAllRemoteItems(remoteFolderPath, SearchItemType.Collections)
                If Item.ItemType = DmsResourceItem.ItemTypes.Collection Then
                    Result.Add(Item)
                End If
            Next
            Return Result
        End Function

        ''' <summary>
        ''' List all child folders for a remote path
        ''' </summary>
        ''' <param name="remoteFolderPath"></param>
        ''' <returns></returns>
        Public Overridable Function ListAllFolderItems(remoteFolderPath As String) As List(Of DmsResourceItem)
            Dim Result As New List(Of DmsResourceItem)
            For Each Item In Me.ListAllRemoteItems(remoteFolderPath, SearchItemType.Folders)
                If Item.ItemType = DmsResourceItem.ItemTypes.Folder Then
                    Result.Add(Item)
                End If
            Next
            Return Result
        End Function

        ''' <summary>
        ''' List all child files for a remote path
        ''' </summary>
        ''' <param name="remoteFolderPath"></param>
        ''' <returns></returns>
        Public Overridable Function ListAllFileItems(remoteFolderPath As String) As List(Of DmsResourceItem)
            Dim Result As New List(Of DmsResourceItem)
            For Each Item In Me.ListAllRemoteItems(remoteFolderPath, SearchItemType.Files)
                If Item.ItemType = DmsResourceItem.ItemTypes.File Then
                    Result.Add(Item)
                End If
            Next
            Return Result
        End Function

        ''' <summary>
        ''' List all child collection names for a remote path
        ''' </summary>
        ''' <param name="remoteFolderPath"></param>
        ''' <returns></returns>
        Public Overridable Function ListAllCollectionNames(remoteFolderPath As String) As List(Of String)
            Dim Result As New List(Of String)
            For Each Item In Me.ListAllCollectionItems(remoteFolderPath)
                If Item.ItemType = DmsResourceItem.ItemTypes.Collection Then
                    Result.Add(Item.Name)
                End If
            Next
            Return Result
        End Function

        ''' <summary>
        ''' List all child folder names for a remote path
        ''' </summary>
        ''' <param name="remoteFolderPath"></param>
        ''' <returns></returns>
        Public Overridable Function ListAllFolderNames(remoteFolderPath As String) As List(Of String)
            Dim Result As New List(Of String)
            For Each Item In Me.ListAllFolderItems(remoteFolderPath)
                If Item.ItemType = DmsResourceItem.ItemTypes.Folder Then
                    Result.Add(Item.Name)
                End If
            Next
            Return Result
        End Function

        ''' <summary>
        ''' List all child file names for a remote path
        ''' </summary>
        ''' <param name="remoteFolderPath"></param>
        ''' <returns></returns>
        Public Overridable Function ListAllFileNames(remoteFolderPath As String) As List(Of String)
            Dim Result As New List(Of String)
            For Each Item In Me.ListAllFileItems(remoteFolderPath)
                If Item.ItemType = DmsResourceItem.ItemTypes.File Then
                    Result.Add(Item.Name)
                End If
            Next
            Return Result
        End Function

        ''' <summary>
        ''' Load a remote collection item based on its ID
        ''' </summary>
        ''' <param name="id"></param>
        ''' <returns></returns>
        Public MustOverride Function FindCollectionById(id As String) As DmsResourceItem

        ''' <summary>
        ''' Load a remote folder item based on its ID
        ''' </summary>
        ''' <param name="id"></param>
        ''' <returns></returns>
        Public MustOverride Function FindFolderById(id As String) As DmsResourceItem

        ''' <summary>
        ''' Load a remote file item based on its ID
        ''' </summary>
        ''' <param name="id"></param>
        ''' <returns></returns>
        <Obsolete("Use FindFileById instead"), System.ComponentModel.EditorBrowsable(ComponentModel.EditorBrowsableState.Never)>
        Public Function FindDocumentById(id As String) As DmsResourceItem
            Return Me.FindFileById(id)
        End Function

        ''' <summary>
        ''' Load a remote file item based on its ID
        ''' </summary>
        ''' <param name="id"></param>
        ''' <returns></returns>
        Public MustOverride Function FindFileById(id As String) As DmsResourceItem

        ''' <summary>
        ''' Create a provider-specific credentials instance for further customization
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride Function CreateNewCredentialsInstance() As BaseDmsLoginCredentials

        ''' <summary>
        ''' The desired exception type in case of errors
        ''' </summary>
        Protected Enum ExceptionTypeForItemType As Byte
            Unspecified = 0
            Directory = 1
            File = 2
        End Enum

        ''' <summary>
        ''' Upload a local file to the remote DMS, if applicable: create a new version to an existing file
        ''' </summary>
        ''' <param name="remoteFilePath"></param>
        ''' <param name="localFilePath"></param>
        Public MustOverride Sub UploadFile(remoteFilePath As String, localFilePath As String)

        ''' <summary>
        ''' Upload a local file to the remote DMS, if applicable: create a new version to an existing file
        ''' </summary>
        ''' <param name="remoteFilePath"></param>
        ''' <param name="binaryData"></param>
        Public MustOverride Sub UploadFile(remoteFilePath As String, binaryData As Func(Of System.IO.Stream))

        ''' <summary>
        ''' Upload a local file to the remote DMS, if applicable: create a new version to an existing file
        ''' </summary>
        ''' <param name="remoteFilePath"></param>
        ''' <param name="binaryData"></param>
        Public Overridable Sub UploadFile(remoteFilePath As String, binaryData As Byte())
            Me.UploadFile(remoteFilePath, Function()
                                              Return New System.IO.MemoryStream(binaryData)
                                          End Function)
        End Sub

        ''' <summary>
        ''' Upload a local file to the remote DMS, if applicable: create a new version to an existing file
        ''' </summary>
        ''' <param name="remoteFilePath"></param>
        ''' <param name="localFilePath"></param>
        Public Sub UploadFile(remoteFilePath As String, localFilePath As String, createDirectoryStructureIfMissing As Boolean)
            If remoteFilePath = Nothing Then Throw New ArgumentNullException(NameOf(remoteFilePath))
            Dim ParentDirectory As String = Me.ParentDirectoryPath(remoteFilePath)
            If ParentDirectory <> Nothing AndAlso Me.RemoteItemExists(ParentDirectory) = False Then
                Me.CreateFolder(ParentDirectory)
            End If
            Me.UploadFile(remoteFilePath, localFilePath)
        End Sub


        ''' <summary>
        ''' Upload a local file to the remote DMS, if applicable: create a new version to an existing file
        ''' </summary>
        ''' <param name="remoteFilePath"></param>
        ''' <param name="binaryData"></param>
        Public Sub UploadFile(remoteFilePath As String, binaryData As Func(Of System.IO.Stream), createDirectoryStructureIfMissing As Boolean)
            If remoteFilePath = Nothing Then Throw New ArgumentNullException(NameOf(remoteFilePath))
            Dim ParentDirectory As String = Me.ParentDirectoryPath(remoteFilePath)
            If ParentDirectory <> Nothing AndAlso Me.RemoteItemExists(ParentDirectory) = False Then
                Me.CreateFolder(ParentDirectory)
            End If
            Me.UploadFile(remoteFilePath, binaryData)
        End Sub

        ''' <summary>
        ''' Upload a local file to the remote DMS, if applicable: create a new version to an existing file
        ''' </summary>
        ''' <param name="remoteFilePath"></param>
        ''' <param name="binaryData"></param>
        Public Sub UploadFile(remoteFilePath As String, binaryData As Byte(), createDirectoryStructureIfMissing As Boolean)
            If remoteFilePath = Nothing Then Throw New ArgumentNullException(NameOf(remoteFilePath))
            Dim ParentDirectory As String = Me.ParentDirectoryPath(remoteFilePath)
            If ParentDirectory <> Nothing AndAlso Me.RemoteItemExists(ParentDirectory) = False Then
                Me.CreateFolder(ParentDirectory, True)
            End If
            Me.UploadFile(remoteFilePath, binaryData)
        End Sub

        ''' <summary>
        ''' Download a remote DMS file
        ''' </summary>
        ''' <param name="remoteFilePath"></param>
        ''' <param name="localFilePath"></param>
        ''' <param name="lastModificationDateOnLocalTime"></param>
        Public MustOverride Sub DownloadFile(remoteFilePath As String, localFilePath As String, lastModificationDateOnLocalTime As DateTime?)


        ''' <summary>
        ''' Copy a remote DMS item (overwriting forbidden, destination directory must exist)
        ''' </summary>
        ''' <param name="remoteSourcePath"></param>
        ''' <param name="remoteDestinationPath"></param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Public Sub Copy(remoteSourcePath As String, remoteDestinationPath As String)
            Me.Copy(remoteSourcePath, remoteDestinationPath, False, False)
        End Sub

        ''' <summary>
        ''' Copy a remote DMS item (overwriting forbidden, destination directory must exist)
        ''' </summary>
        ''' <param name="remoteSourcePath"></param>
        ''' <param name="remoteDestinationPath"></param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Public Async Function CopyAsync(remoteSourcePath As String, remoteDestinationPath As String) As Task
            Await Me.CopyAsync(remoteSourcePath, remoteDestinationPath, False, False)
        End Function

        ''' <summary>
        ''' Copy a remote DMS item
        ''' </summary>
        ''' <param name="remoteSourcePath">Absolute source path</param>
        ''' <param name="remoteDestinationPath">Absolute destination path</param>
        ''' <param name="allowOverwrite">True to allow overwriting, False to forbid overwriting, null/Nothing to use provider specific default</param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Public Sub Copy(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean)
            Dim FoundRemoteSourceItem As DmsResourceItem.FoundItemResult = Me.RemoteItemExistsUniquelyAs(remoteSourcePath)
            Me.CopyMoveArgumentsCheck(FoundRemoteSourceItem, remoteSourcePath, remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory)
            Dim ParentDirPathDestination As String = Me.ParentDirectoryPath(remoteDestinationPath)
            Select Case FoundRemoteSourceItem
                Case DmsResourceItem.FoundItemResult.File
                    Me.CopyFileItem(remoteSourcePath, remoteDestinationPath, allowOverwrite)
                    Me.ResetCachesForRemoteItems(ParentDirPathDestination, SearchItemType.Files)
                Case DmsResourceItem.FoundItemResult.Folder, DmsResourceItem.FoundItemResult.Collection
                    Me.CopyDirectoryItem(remoteSourcePath, remoteDestinationPath)
                    Me.ResetCachesForRemoteItems(ParentDirPathDestination, SearchItemType.Folders)
                    Me.ResetCachesForRemoteItems(ParentDirPathDestination, SearchItemType.Collections)
                Case Else
                    Throw New NotImplementedException
            End Select
        End Sub

        ''' <summary>
        ''' Copy a remote DMS item
        ''' </summary>
        ''' <param name="remoteSourcePath">The path of a file or directory, e.g. &quot;source/file.tmp&quot; or &quot;source/directory&quot;</param>
        ''' <param name="remoteDestinationPath">The full destination path of a file or directory, e.g. &quot;cloned/directory&quot;</param>
        ''' <param name="allowOverwrite">True to overwrite, False to throw exception if target already exists, null/Nothing to use provider specific default</param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Public Async Function CopyAsync(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean) As Task
            Dim FoundRemoteSourceItem As DmsResourceItem.FoundItemResult = Me.RemoteItemExistsUniquelyAs(remoteSourcePath)
            Me.CopyMoveArgumentsCheck(FoundRemoteSourceItem, remoteSourcePath, remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory)
            Dim ParentDirPathDestination As String = Me.ParentDirectoryPath(remoteDestinationPath)
            Select Case FoundRemoteSourceItem
                Case DmsResourceItem.FoundItemResult.File
                    Await Me.CopyFileItemAsync(remoteSourcePath, remoteDestinationPath, allowOverwrite)
                    Me.ResetCachesForRemoteItems(ParentDirPathDestination, SearchItemType.Files)
                Case DmsResourceItem.FoundItemResult.Folder, DmsResourceItem.FoundItemResult.Collection
                    Await Me.CopyDirectoryItemAsync(remoteSourcePath, remoteDestinationPath)
                    Me.ResetCachesForRemoteItems(ParentDirPathDestination, SearchItemType.Folders)
                    Me.ResetCachesForRemoteItems(ParentDirPathDestination, SearchItemType.Collections)
                Case Else
                    Throw New NotImplementedException
            End Select
        End Function

        ''' <summary>
        ''' Check input arguments for copy methods
        ''' </summary>
        ''' <param name="foundSourceRemoteItemType"></param>
        ''' <param name="remoteSourcePath"></param>
        ''' <param name="remoteDestinationPath"></param>
        ''' <param name="allowOverwrite"></param>
        ''' <param name="allowCreationOfRemoteDirectory"></param>
        Private Sub CopyMoveArgumentsCheck(foundSourceRemoteItemType As DmsResourceItem.FoundItemResult, remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean)
            If remoteSourcePath = Nothing Then Throw New ArgumentNullException(NameOf(remoteSourcePath))
            If remoteDestinationPath = Nothing Then Throw New ArgumentNullException(NameOf(remoteDestinationPath))
            If remoteDestinationPath.EndsWith(Me.DirectorySeparator) Then Throw New ArgumentException("Must be a path without trailing directory separator char: " & remoteDestinationPath, NameOf(remoteDestinationPath))
            If allowOverwrite.HasValue AndAlso allowOverwrite.Value = False Then
                Select Case Me.RemoteItemExistsUniquelyAs(remoteDestinationPath)
                    Case DmsResourceItem.FoundItemResult.WithNameCollisions
                        Throw New RemotePathNotUniqueException(remoteDestinationPath)
                    Case DmsResourceItem.FoundItemResult.File
                        Throw New FileAlreadyExistsException(remoteDestinationPath)
                    Case DmsResourceItem.FoundItemResult.Collection, DmsResourceItem.FoundItemResult.Folder
                        Throw New DirectoryAlreadyExistsException(remoteDestinationPath)
                    Case DmsResourceItem.FoundItemResult.Root
                        Throw New NotSupportedException("Root directory can't be the target of a copy action")
                    Case DmsResourceItem.FoundItemResult.NotFound
                        'ok
                    Case Else
                        Throw New NotImplementedException
                End Select
            End If
            Dim ParentDir As String = Me.ParentDirectoryPath(remoteDestinationPath)
            If ParentDir <> Nothing Then
                Select Case Me.RemoteItemExistsUniquelyAs(ParentDir)
                    Case DmsResourceItem.FoundItemResult.Collection, DmsResourceItem.FoundItemResult.Folder
                        'Ok
                    Case DmsResourceItem.FoundItemResult.File
                        Throw New FileAlreadyExistsException(ParentDir)
                    Case DmsResourceItem.FoundItemResult.NotFound
                        If allowCreationOfRemoteDirectory Then
                            Me.CreateFolder(ParentDir)
                        Else
                            Throw New DirectoryNotFoundException(ParentDir)
                        End If
                    Case DmsResourceItem.FoundItemResult.WithNameCollisions
                        Throw New RemotePathNotUniqueException(ParentDir)
                    Case Else
                        Throw New NotSupportedException("Remote ressource with unsupported type: " & ParentDir)
                End Select
            Else 'If ParentDir = "" -> root dir
                If Me.SupportsFilesInRootFolder = False Then
                    Throw New NotSupportedException("Files in root folder not supported by DMS provider")
                End If
            End If
            Select Case foundSourceRemoteItemType
                Case DmsResourceItem.FoundItemResult.WithNameCollisions
                    Throw New NotSupportedException("Not a unique item: " & remoteSourcePath)
                Case DmsResourceItem.FoundItemResult.NotFound
                    Throw New RessourceNotFoundException(remoteSourcePath)
                Case DmsResourceItem.FoundItemResult.Root
                    Throw New NotSupportedException("Root directory can't be source of a copy action")
                Case DmsResourceItem.FoundItemResult.Folder, DmsResourceItem.FoundItemResult.Collection
                    If allowOverwrite.HasValue = True AndAlso allowOverwrite.Value = False Then
                        'Supported source object (file or directory)
                    Else
                        Throw New NotImplementedException("Source is directory (folder or collection); current implementation only supports AllowOverwrite=False")
                    End If
                Case DmsResourceItem.FoundItemResult.File
                    'Supported source object (file or directory)
                Case Else
                    Throw New ArgumentOutOfRangeException(NameOf(remoteSourcePath), "Invalid value: " & remoteSourcePath.ToString)
            End Select
        End Sub

        ''' <summary>
        ''' Copy a remote DMS item
        ''' </summary>
        ''' <param name="remoteSourcePath"></param>
        ''' <param name="remoteDestinationPath"></param>
        ''' <param name="allowOverwrite">True to overwrite, False to throw exception if target already exists, null/Nothing to use provider specific default</param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Protected MustOverride Sub CopyFileItem(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?)

        ''' <summary>
        ''' Copy a remote DMS item
        ''' </summary>
        ''' <param name="remoteSourcePath"></param>
        ''' <param name="remoteDestinationPath"></param>
        ''' <param name="allowOverwrite">True to overwrite, False to throw exception if target already exists, null/Nothing to use provider specific default</param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Protected MustOverride Async Function CopyFileItemAsync(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?) As Task

        ''' <summary>
        ''' Copy a remote DMS item
        ''' </summary>
        ''' <param name="remoteSourcePath"></param>
        ''' <param name="remoteDestinationPath"></param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Protected MustOverride Sub CopyDirectoryItem(remoteSourcePath As String, remoteDestinationPath As String)

        ''' <summary>
        ''' Copy a remote DMS item
        ''' </summary>
        ''' <param name="remoteSourcePath"></param>
        ''' <param name="remoteDestinationPath"></param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Protected MustOverride Async Function CopyDirectoryItemAsync(remoteSourcePath As String, remoteDestinationPath As String) As Task

        ''' <summary>
        ''' Move a remote DMS item (overwriting forbidden, destination directory must exist)
        ''' </summary>
        ''' <param name="remoteSourcePath"></param>
        ''' <param name="remoteDestinationPath"></param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Public Sub Move(remoteSourcePath As String, remoteDestinationPath As String)
            Me.Move(remoteSourcePath, remoteDestinationPath, False, False)
        End Sub

        ''' <summary>
        ''' Move a remote DMS item (overwriting forbidden, destination directory must exist)
        ''' </summary>
        ''' <param name="remoteSource"></param>
        ''' <param name="remoteDestinationPath"></param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Public Sub Move(remoteSource As DmsResourceItem, remoteDestinationPath As String)
            Me.Move(remoteSource, remoteDestinationPath, False, False)
        End Sub

        ''' <summary>
        ''' Move a remote DMS item
        ''' </summary>
        ''' <param name="remoteSourcePath"></param>
        ''' <param name="remoteDestinationPath"></param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Public Sub Move(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean)
            Dim FoundRemoteSourceItem As DmsResourceItem.FoundItemResult = Me.RemoteItemExistsUniquelyAs(remoteSourcePath)
            Me.CopyMoveArgumentsCheck(FoundRemoteSourceItem, remoteSourcePath, remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory)
            Dim ParentDirPathSource As String = Me.ParentDirectoryPath(remoteSourcePath)
            Dim ParentDirPathDestination As String = Me.ParentDirectoryPath(remoteDestinationPath)
            Select Case FoundRemoteSourceItem
                Case DmsResourceItem.FoundItemResult.File
                    Me.MoveFileItem(remoteSourcePath, remoteDestinationPath, allowOverwrite)
                    Me.ResetCachesForRemoteItems(ParentDirPathSource, SearchItemType.Files)
                    Me.ResetCachesForRemoteItems(ParentDirPathDestination, SearchItemType.Files)
                Case DmsResourceItem.FoundItemResult.Folder, DmsResourceItem.FoundItemResult.Collection
                    Me.MoveDirectoryItem(remoteSourcePath, remoteDestinationPath)
                    Me.ResetCachesForRemoteItems(ParentDirPathSource, SearchItemType.Folders)
                    Me.ResetCachesForRemoteItems(ParentDirPathDestination, SearchItemType.Folders)
                    Me.ResetCachesForRemoteItems(ParentDirPathSource, SearchItemType.Collections)
                    Me.ResetCachesForRemoteItems(ParentDirPathDestination, SearchItemType.Collections)
                Case Else
                    Throw New NotImplementedException
            End Select
        End Sub

        ''' <summary>
        ''' Move a remote DMS item
        ''' </summary>
        ''' <param name="remoteSource"></param>
        ''' <param name="remoteDestinationPath"></param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Public Sub Move(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean)
            Dim FoundRemoteSourceItem As DmsResourceItem.FoundItemResult = Me.RemoteItemExistsUniquelyAs(remoteSource.FullName)
            Me.CopyMoveArgumentsCheck(FoundRemoteSourceItem, remoteSource.FullName, remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory)
            Dim ParentDirPathSource As String = Me.ParentDirectoryPath(remoteSource.FullName)
            Dim ParentDirPathDestination As String = Me.ParentDirectoryPath(remoteDestinationPath)
            Select Case FoundRemoteSourceItem
                Case DmsResourceItem.FoundItemResult.File
                    Me.MoveFileItem(remoteSource.FullName, remoteDestinationPath, allowOverwrite)
                    Me.ResetCachesForRemoteItems(ParentDirPathSource, SearchItemType.Files)
                    Me.ResetCachesForRemoteItems(ParentDirPathDestination, SearchItemType.Files)
                Case DmsResourceItem.FoundItemResult.Folder, DmsResourceItem.FoundItemResult.Collection
                    Me.MoveDirectoryItem(remoteSource.FullName, remoteDestinationPath)
                    Me.ResetCachesForRemoteItems(ParentDirPathSource, SearchItemType.Folders)
                    Me.ResetCachesForRemoteItems(ParentDirPathDestination, SearchItemType.Folders)
                    Me.ResetCachesForRemoteItems(ParentDirPathSource, SearchItemType.Collections)
                    Me.ResetCachesForRemoteItems(ParentDirPathDestination, SearchItemType.Collections)
                Case Else
                    Throw New NotImplementedException
            End Select
        End Sub

        ''' <summary>
        ''' Move a remote DMS item
        ''' </summary>
        ''' <param name="remoteSourcePath"></param>
        ''' <param name="remoteDestinationPath"></param>
        ''' <param name="allowOverwrite"></param>
        Protected MustOverride Sub MoveFileItem(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?)

        ''' <summary>
        ''' Move a remote DMS item
        ''' </summary>
        ''' <param name="remoteSourcePath"></param>
        ''' <param name="remoteDestinationPath"></param>
        Protected MustOverride Sub MoveDirectoryItem(remoteSourcePath As String, remoteDestinationPath As String)

        ''' <summary>
        ''' Delete a remote item (folder, collection or file)
        ''' </summary>
        ''' <param name="remotePath"></param>
        Public MustOverride Sub DeleteRemoteItem(remotePath As String)

        ''' <summary>
        ''' Delete a remote item if its item type matches with the expected item type
        ''' </summary>
        ''' <param name="remotePath"></param>
        ''' <param name="expectedItemType"></param>
        Public Overridable Sub DeleteRemoteItem(remotePath As String, expectedItemType As DmsResourceItem.ItemTypes)
            Dim Item As DmsResourceItem = Me.ListRemoteItem(remotePath)
            Me.DeleteRemoteItem(Item, expectedItemType)
        End Sub

        ''' <summary>
        ''' Delete a remote item if its item type matches with the expected item type
        ''' </summary>
        ''' <param name="remotePath"></param>
        ''' <param name="expectedItemType"></param>
        ''' <param name="alternativeExpectedItemType"></param>
        Public Overridable Sub DeleteRemoteItem(remotePath As String, expectedItemType As DmsResourceItem.ItemTypes, alternativeExpectedItemType As DmsResourceItem.ItemTypes)
            Dim Item As DmsResourceItem = Me.ListRemoteItem(remotePath)
            Me.DeleteRemoteItem(Item, expectedItemType, alternativeExpectedItemType)
        End Sub

        ''' <summary>
        ''' Delete a remote item (folder, collection or file)
        ''' </summary>
        ''' <param name="remoteItem"></param>
        Public MustOverride Sub DeleteRemoteItem(remoteItem As DmsResourceItem)

        ''' <summary>
        ''' Delete a remote item if its item type matches with the expected item type
        ''' </summary>
        ''' <param name="remoteItem"></param>
        ''' <param name="expectedItemType"></param>
        Public Overridable Sub DeleteRemoteItem(remoteItem As DmsResourceItem, expectedItemType As DmsResourceItem.ItemTypes)
            If expectedItemType = Nothing Then Throw New ArgumentNullException(NameOf(expectedItemType))
            If remoteItem.ItemType <> expectedItemType Then
                Throw New ArgumentException("ItemType " & expectedItemType.ToString & " expected, but was " & remoteItem.ItemType.ToString, NameOf(remoteItem))
            Else
                Me.DeleteRemoteItem(remoteItem)
            End If
        End Sub

        ''' <summary>
        ''' Delete a remote item if its item type matches with the expected item type
        ''' </summary>
        ''' <param name="remoteItem"></param>
        ''' <param name="expectedItemType"></param>
        ''' <param name="alternativeExpectedItemType"></param>
        Public Overridable Sub DeleteRemoteItem(remoteItem As DmsResourceItem, expectedItemType As DmsResourceItem.ItemTypes, alternativeExpectedItemType As DmsResourceItem.ItemTypes)
            If expectedItemType = Nothing Then Throw New ArgumentNullException(NameOf(expectedItemType))
            If alternativeExpectedItemType = Nothing Then Throw New ArgumentNullException(NameOf(alternativeExpectedItemType))
            If remoteItem.ItemType <> expectedItemType AndAlso remoteItem.ItemType <> alternativeExpectedItemType Then
                Throw New ArgumentException("ItemType " & expectedItemType.ToString & " or " & alternativeExpectedItemType.ToString & " expected, but was " & remoteItem.ItemType.ToString, NameOf(remoteItem))
            Else
                Me.DeleteRemoteItem(remoteItem)
            End If
        End Sub

        ''' <summary>
        ''' Create a new folder on remote DMS
        ''' </summary>
        ''' <param name="remoteDirectoryPath"></param>
        Public MustOverride Sub CreateFolder(remoteDirectoryPath As String)

        ''' <summary>
        ''' Create a new folder on remote DMS
        ''' </summary>
        ''' <param name="remoteDirectoryPath"></param>
        Public Sub CreateFolder(remoteDirectoryPath As String, createParentFolders As Boolean)
            If remoteDirectoryPath = Nothing Then Throw New ArgumentNullException(NameOf(remoteDirectoryPath))
            Dim ParentDirectory As String = Me.ParentDirectoryPath(remoteDirectoryPath)
            If ParentDirectory <> Nothing AndAlso Me.RemoteItemExists(ParentDirectory) = False Then
                If createParentFolders Then
                    Me.CreateFolder(ParentDirectory, True)
                Else
                    Throw New DirectoryNotFoundException(ParentDirectory)
                End If
            End If
            Me.CreateFolder(remoteDirectoryPath)
        End Sub

        ''' <summary>
        ''' Create a new collection or folder on remote DMS
        ''' </summary>
        ''' <param name="remoteDirectoryName"></param>
        ''' <remarks>The provider decides itself to create either a collection or a folder</remarks>
        Public MustOverride Sub CreateDirectory(remoteDirectoryName As String)

        ''' <summary>
        ''' Create a new collection or folder on remote DMS
        ''' </summary>
        ''' <param name="remoteDirectoryPath"></param>
        ''' <remarks>The provider decides itself to create either a collection or a folder</remarks>
        Public Sub CreateDirectory(remoteDirectoryPath As String, createParentFolders As Boolean)
            If remoteDirectoryPath = Nothing Then Throw New ArgumentNullException(NameOf(remoteDirectoryPath))
            Dim ParentDirectory As String = Me.ParentDirectoryPath(remoteDirectoryPath)
            If ParentDirectory <> Nothing AndAlso Me.RemoteItemExists(ParentDirectory) = False Then
                If createParentFolders Then
                    Me.CreateFolder(ParentDirectory, True)
                Else
                    Throw New DirectoryNotFoundException(ParentDirectory)
                End If
            End If
            Me.CreateFolder(remoteDirectoryPath)
        End Sub

        ''' <summary>
        ''' Create a new collection on remote DMS
        ''' </summary>
        ''' <param name="remoteCollectionName"></param>
        Public MustOverride Sub CreateCollection(remoteCollectionName As String)

        ''' <summary>
        ''' The directory separator used by the DMS provider
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property DirectorySeparator As Char

        ''' <summary>
        ''' The name of the root element to request a folder/collection listing at root
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property BrowseInRootFolderName() As String

        ''' <summary>
        ''' Login to the remote DMS
        ''' </summary>
        ''' <param name="dmsProfile"></param>
        Public MustOverride Sub Authorize(dmsProfile As IDmsLoginProfile)

        ''' <summary>
        ''' Combines folder names to a path
        ''' </summary>
        ''' <param name="basePath"></param>
        ''' <param name="paths"></param>
        ''' <returns></returns>
        Public Overridable Function CombinePath(basePath As String, ParamArray paths As String()) As String
            Dim Result As String = basePath
            For Each Path In paths
                If Path = Me.DirectorySeparator Then
                    Result = ""
                ElseIf Path.StartsWith(Me.DirectorySeparator) Then
                    Result = Path.Substring(1)
                ElseIf Result = "" Then
                    Result = Path
                ElseIf Result.EndsWith(Me.DirectorySeparator) Then
                    Result &= Path
                Else
                    Result &= Me.DirectorySeparator & Path
                End If
            Next
            Return Result
        End Function

        ''' <summary>
        ''' The parent folder name for a path
        ''' </summary>
        ''' <param name="absolutePath"></param>
        ''' <returns></returns>
        Public Overridable Function ParentDirectoryPath(absolutePath As String) As String
            If absolutePath = Nothing OrElse absolutePath = Me.DirectorySeparator Then
                Return Nothing
            ElseIf absolutePath.Contains(Me.DirectorySeparator) = False Then
                Return ""
            ElseIf absolutePath.EndsWith(Me.DirectorySeparator) Then
                Return ParentDirectoryPath(absolutePath.Substring(0, absolutePath.LastIndexOf(Me.DirectorySeparator, absolutePath.Length - 1)))
            Else
                Return absolutePath.Substring(0, absolutePath.LastIndexOf(Me.DirectorySeparator))
            End If
        End Function

        ''' <summary>
        ''' The item name (directory name or file name) in an absolute path
        ''' </summary>
        ''' <param name="absolutePath"></param>
        ''' <returns></returns>
        Public Overridable Function ItemName(absolutePath As String) As String
            If absolutePath Is Nothing Then
                Throw New ArgumentNullException(NameOf(absolutePath))
            ElseIf absolutePath = Nothing OrElse absolutePath = Me.DirectorySeparator Then
                Return ""
            ElseIf absolutePath.EndsWith(Me.DirectorySeparator) Then
                Return ItemName(absolutePath.Substring(0, absolutePath.LastIndexOf(Me.DirectorySeparator, absolutePath.Length - 1)))
            Else
                Dim ParentPath As String = Me.ParentDirectoryPath(absolutePath)
                Dim Result As String = absolutePath.Substring(ParentPath.Length)
                If Result.StartsWith(Me.DirectorySeparator) Then Result = Result.Substring(1)
                Return Result
            End If
        End Function

        ''' <summary>
        ''' Check existance of a remote collection
        ''' </summary>
        ''' <param name="remoteFolderPath"></param>
        ''' <returns></returns>
        Public Function CollectionExists(remoteFolderPath As String) As Boolean
            Dim ParentPath As String = Me.ParentDirectoryPath(remoteFolderPath)
            Dim ItemName As String = Me.ItemName(remoteFolderPath)
            Dim AllFoldersInParentFolder As List(Of String) = Me.ListAllCollectionNames(ParentPath)
            Return AllFoldersInParentFolder.Contains(ItemName, StringComparer.Create(System.Globalization.CultureInfo.InvariantCulture, True))
        End Function

        ''' <summary>
        ''' Check existance of a remote folder
        ''' </summary>
        ''' <param name="remoteFolderPath"></param>
        ''' <returns></returns>
        Public Function FolderExists(remoteFolderPath As String) As Boolean
            Dim ParentPath As String = Me.ParentDirectoryPath(remoteFolderPath)
            Dim ItemName As String = Me.ItemName(remoteFolderPath)
            Dim AllFoldersInParentFolder As List(Of String) = Me.ListAllFolderNames(ParentPath)
            Return AllFoldersInParentFolder.Contains(ItemName, StringComparer.Create(System.Globalization.CultureInfo.InvariantCulture, True))
        End Function

        ''' <summary>
        ''' DMS provider supports collections (concept of collections can be understood as "intelligent folders", see CenterDevice/Scopevisio Teamwork)
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property SupportsCollections As Boolean

        ''' <summary>
        ''' DMS provider supports sharing API
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property SupportsSharingSetup As Boolean

        ''' <summary>
        ''' DMS provider supports configuration of root folder and subfolders for the different purposes (input files, reports)
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property SupportsSubFolderConfiguration As Boolean

        ''' <summary>
        ''' DMS provider supports remote items with very same names (e.g. 2 files with the very same name are uniquely accessible only by their DmsItem respectively by their ID)
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property SupportsNonUniqueRemoteItems As Boolean

        ''' <summary>
        ''' DMS provider supports files in root folder (or only folders/collections)
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property SupportsFilesInRootFolder As Boolean

        ''' <summary>
        ''' DMS providers implement different sets of features
        ''' </summary>
        Public Enum RuntimeAccessTypes As Byte
            ''' <summary>
            ''' No configuration, no runtime access
            ''' </summary>
            None = 0
            ''' <summary>
            ''' Configuration available, but no runtime access
            ''' </summary>
            ConfigurationOnly = 1
            ''' <summary>
            ''' Configuration available with access on runtime to remote server using API calls
            ''' </summary>
            ConfigurationAndRuntimeAccess = 2
            ''' <summary>
            ''' Configuration available with manual access on runtime to remote server (starts e.g. a browser to open a 3rd party remote DMS system)
            ''' </summary>
            ConfigurationAndStartSeparateProcessWithDmsServerAddress = 3
        End Enum

        ''' <summary>
        ''' DMS provider supports authentication and transfer of files on runtime (False indicates a configuration-only provider)
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property SupportsRuntimeAccessToRemoteServer As RuntimeAccessTypes

        ''' <summary>
        ''' Create a link share for a remote DMS item
        ''' </summary>
        ''' <param name="dmsResource"></param>
        ''' <param name="shareInfo"></param>
        ''' <returns></returns>
        Public MustOverride Function CreateLink(dmsResource As DmsResourceItem, shareInfo As DmsLink) As DmsLink
        ''' <summary>
        ''' Update an existing link share
        ''' </summary>
        ''' <param name="shareInfo"></param>
        Public MustOverride Sub UpdateLink(shareInfo As DmsLink)
        ''' <summary>
        ''' Remove an existing link share
        ''' </summary>
        ''' <param name="shareInfo"></param>
        Public MustOverride Sub DeleteLink(shareInfo As DmsLink)

        ''' <summary>
        ''' Create a share for a group on remote DMS
        ''' </summary>
        ''' <param name="dmsResource"></param>
        ''' <param name="shareInfo"></param>
        Public MustOverride Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForGroup)
        ''' <summary>
        ''' Create a share for a user on remote DMS
        ''' </summary>
        ''' <param name="dmsResource"></param>
        ''' <param name="shareInfo"></param>
        Public MustOverride Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForUser)

        ''' <summary>
        ''' Update a group share on remote DMS
        ''' </summary>
        ''' <param name="shareInfo"></param>
        Public MustOverride Sub UpdateSharing(shareInfo As DmsShareForGroup)
        ''' <summary>
        ''' Remove a group share on remote DMS
        ''' </summary>
        ''' <param name="shareInfo"></param>
        Public MustOverride Sub DeleteSharing(shareInfo As DmsShareForGroup)
        ''' <summary>
        ''' Update a user share on remote DMS
        ''' </summary>
        ''' <param name="shareInfo"></param>
        Public MustOverride Sub UpdateSharing(shareInfo As DmsShareForUser)
        ''' <summary>
        ''' Remove a user share on remote DMS
        ''' </summary>
        ''' <param name="shareInfo"></param>
        Public MustOverride Sub DeleteSharing(shareInfo As DmsShareForUser)

        ''' <summary>
        ''' Load a list of groups on remote DMS (which are visible to the current login user)
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride Function GetAllGroups() As List(Of DmsGroup)
        ''' <summary>
        ''' Load a list of users on remote DMS (which are visible to the current login user)
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride Function GetAllUsers() As List(Of DmsUser)

        ''' <summary>
        ''' A runtime variable which contains the user ID after login at remote DMS system
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property CurrentContextUserID As String

    End Class

End Namespace