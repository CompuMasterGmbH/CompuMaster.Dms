Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data
Imports System.Threading
Imports System.Threading.Tasks

Namespace Providers

    ''' <summary>
    ''' A DMS provider instance
    ''' </summary>
    Public MustInherit Class BaseDmsProvider

        Private Shared ReadOnly AmbientCancellation As New AsyncLocal(Of CancellationToken)

        ''' <summary>Gets the cancellation token for the current asynchronous item operation.</summary>
        ''' <returns>The token supplied by the caller, or CancellationToken.None.</returns>
        Protected ReadOnly Property CurrentAsyncCancellationToken As CancellationToken
            Get
                Return AmbientCancellation.Value
            End Get
        End Property

        ''' <summary>Indicates whether this provider implements native asynchronous remote I/O.</summary>
        ''' <returns>True when remote requests can be awaited without occupying a worker thread.</returns>
        Public Overridable ReadOnly Property SupportsAsynchronousIo As Boolean
            Get
                Return False
            End Get
        End Property

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
        ''' Lists all direct child directories for a remote path.
        ''' </summary>
        ''' <param name="remoteFolderPath">The remote parent path.</param>
        ''' <returns>All direct child folders and collections.</returns>
        Public Overridable Function ListAllDirectoryItems(remoteFolderPath As String) As List(Of DmsResourceItem)
            Dim Result As New List(Of DmsResourceItem)
            For Each Item As DmsResourceItem In Me.ListAllRemoteItems(remoteFolderPath, SearchItemType.AllItems)
                If Item.ItemType = DmsResourceItem.ItemTypes.Folder OrElse Item.ItemType = DmsResourceItem.ItemTypes.Collection Then
                    Result.Add(Item)
                End If
            Next
            Return Result
        End Function

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

        ''' <summary>Finds a remote item without blocking the calling thread.</summary>
        ''' <param name="remotePath">The remote path to inspect.</param>
        ''' <param name="cancellationToken">Cancels the request and any wait for service capacity.</param>
        ''' <returns>The matching item, or Nothing when the path is absent.</returns>
        ''' <exception cref="NotSupportedException">The provider does not support asynchronous I/O.</exception>
        Public Overridable Function ListRemoteItemAsync(remotePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            Throw New NotSupportedException("This provider does not support asynchronous I/O.")
        End Function

        ''' <summary>Lists remote child items without blocking the calling thread.</summary>
        ''' <param name="remoteFolderPath">The remote parent path.</param>
        ''' <param name="searchType">The child types to include.</param>
        ''' <param name="cancellationToken">Cancels the request and any wait for service capacity.</param>
        ''' <returns>The matching child items.</returns>
        ''' <exception cref="NotSupportedException">The provider does not support asynchronous I/O.</exception>
        Public Overridable Function ListAllRemoteItemsAsync(remoteFolderPath As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Throw New NotSupportedException("This provider does not support asynchronous I/O.")
        End Function

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

        ''' <summary>Uploads a local file without blocking the calling thread.</summary>
        ''' <param name="remoteFilePath">The remote destination path.</param>
        ''' <param name="localFilePath">The local source path.</param>
        ''' <param name="cancellationToken">Cancels the upload and any wait for service capacity.</param>
        ''' <returns>A task that completes when the upload finishes.</returns>
        ''' <exception cref="NotSupportedException">The provider does not support asynchronous I/O.</exception>
        Public Overridable Function UploadFileAsync(remoteFilePath As String, localFilePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Throw New NotSupportedException("This provider does not support asynchronous I/O.")
        End Function

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

        ''' <summary>Downloads a remote file without blocking the calling thread.</summary>
        ''' <param name="remoteFilePath">The remote source path.</param>
        ''' <param name="localFilePath">The local destination path.</param>
        ''' <param name="lastModificationDateOnLocalTime">The optional local timestamp to apply.</param>
        ''' <param name="cancellationToken">Cancels the download and any wait for service capacity.</param>
        ''' <returns>A task that completes when the download finishes.</returns>
        ''' <exception cref="NotSupportedException">The provider does not support asynchronous I/O.</exception>
        Public Overridable Function DownloadFileAsync(remoteFilePath As String, localFilePath As String, lastModificationDateOnLocalTime As DateTime?, Optional cancellationToken As CancellationToken = Nothing) As Task
            Throw New NotSupportedException("This provider does not support asynchronous I/O.")
        End Function

        ''' <summary>
        ''' Downloads a remote DMS file identified by its resource metadata.
        ''' </summary>
        ''' <param name="remoteFile">The remote file to download.</param>
        ''' <param name="localFilePath">The local destination path.</param>
        Public Overridable Sub DownloadFile(remoteFile As DmsResourceItem, localFilePath As String)
            If remoteFile Is Nothing Then Throw New ArgumentNullException(NameOf(remoteFile))
            If remoteFile.ItemType <> DmsResourceItem.ItemTypes.File Then Throw New ArgumentException("The remote resource must be a file.", NameOf(remoteFile))
            Me.DownloadFile(remoteFile.FullName, localFilePath, remoteFile.LastModificationOnLocalTime)
        End Sub


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
        ''' Copies a remote DMS item while preserving its provider-specific identity.
        ''' </summary>
        ''' <param name="remoteSource">The remote source item.</param>
        ''' <param name="remoteDestinationPath">The absolute destination path.</param>
        Public Sub Copy(remoteSource As DmsResourceItem, remoteDestinationPath As String)
            Me.Copy(remoteSource, remoteDestinationPath, False, False)
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
        ''' Copies a remote DMS item asynchronously while preserving its provider-specific identity.
        ''' </summary>
        ''' <param name="remoteSource">The remote source item.</param>
        ''' <param name="remoteDestinationPath">The absolute destination path.</param>
        Public Async Function CopyAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String) As Task
            Await Me.CopyAsync(remoteSource, remoteDestinationPath, False, False)
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
            Me.Copy(Me.ResolveUniqueSourceItem(remoteSourcePath), remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory)
        End Sub

        ''' <summary>
        ''' Copies a remote DMS item while preserving its provider-specific identity.
        ''' </summary>
        ''' <param name="remoteSource">The remote source item.</param>
        ''' <param name="remoteDestinationPath">The absolute destination path.</param>
        ''' <param name="allowOverwrite">True to replace files and merge directories, False to reject existing targets, or Nothing to use the provider default.</param>
        ''' <param name="allowCreationOfRemoteDirectory">True to create a missing destination parent directory.</param>
        Public Sub Copy(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean)
            Me.CopyMoveArgumentsCheck(remoteSource, remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory)
            Me.CopyItem(remoteSource, remoteDestinationPath, allowOverwrite)
            Me.ResetDestinationCaches(remoteSource, remoteDestinationPath)
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
            Await Me.CopyAsync(remoteSourcePath, remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory, CancellationToken.None).ConfigureAwait(False)
        End Function

        ''' <summary>Copies a remote item asynchronously with cancellation when the provider supports native asynchronous I/O.</summary>
        ''' <param name="remoteSourcePath">The remote source path.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        ''' <param name="allowOverwrite">Whether files may be replaced or directories merged.</param>
        ''' <param name="allowCreationOfRemoteDirectory">Whether a missing destination parent may be created.</param>
        ''' <param name="cancellationToken">Cancels queued and active requests for providers with native asynchronous I/O.</param>
        ''' <returns>A task that completes after the copy and cache update.</returns>
        Public Async Function CopyAsync(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean, cancellationToken As CancellationToken) As Task
            Dim previous As CancellationToken = AmbientCancellation.Value
            AmbientCancellation.Value = cancellationToken
            Try
                cancellationToken.ThrowIfCancellationRequested()
                Dim source As DmsResourceItem
                If Me.SupportsAsynchronousIo Then
                    source = Await Me.ResolveUniqueSourceItemAsync(remoteSourcePath).ConfigureAwait(False)
                Else
                    source = Me.ResolveUniqueSourceItem(remoteSourcePath)
                End If
                Await Me.CopyAsync(source, remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory, cancellationToken).ConfigureAwait(False)
            Finally
                AmbientCancellation.Value = previous
            End Try
        End Function

        ''' <summary>
        ''' Copies a remote DMS item asynchronously while preserving its provider-specific identity.
        ''' </summary>
        ''' <param name="remoteSource">The remote source item.</param>
        ''' <param name="remoteDestinationPath">The absolute destination path.</param>
        ''' <param name="allowOverwrite">True to replace files and merge directories, False to reject existing targets, or Nothing to use the provider default.</param>
        ''' <param name="allowCreationOfRemoteDirectory">True to create a missing destination parent directory.</param>
        Public Async Function CopyAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean) As Task
            Await Me.CopyAsync(remoteSource, remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory, CancellationToken.None).ConfigureAwait(False)
        End Function

        ''' <summary>Copies a remote item asynchronously while retaining its provider identity.</summary>
        ''' <param name="remoteSource">The source item.</param>
        ''' <param name="remoteDestinationPath">The destination path.</param>
        ''' <param name="allowOverwrite">Whether files may be replaced or directories merged.</param>
        ''' <param name="allowCreationOfRemoteDirectory">Whether a missing destination parent may be created.</param>
        ''' <param name="cancellationToken">Cancels queued and active requests for providers with native asynchronous I/O.</param>
        ''' <returns>A task that completes after the copy and cache update.</returns>
        Public Async Function CopyAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean, cancellationToken As CancellationToken) As Task
            Dim previous As CancellationToken = AmbientCancellation.Value
            AmbientCancellation.Value = cancellationToken
            Try
                cancellationToken.ThrowIfCancellationRequested()
                If Me.SupportsAsynchronousIo Then
                    Await Me.CopyMoveArgumentsCheckAsync(remoteSource, remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory).ConfigureAwait(False)
                Else
                    Me.CopyMoveArgumentsCheck(remoteSource, remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory)
                End If
                Await Me.CopyItemAsync(remoteSource, remoteDestinationPath, allowOverwrite).ConfigureAwait(False)
                Me.ResetDestinationCaches(remoteSource, remoteDestinationPath)
            Finally
                AmbientCancellation.Value = previous
            End Try
        End Function

        Private Async Function CopyMoveArgumentsCheckAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean) As Task
            If remoteSource Is Nothing Then Throw New ArgumentNullException(NameOf(remoteSource))
            If String.IsNullOrEmpty(remoteSource.FullName) Then Throw New ArgumentException("The source item must provide its full remote path.", NameOf(remoteSource))
            If remoteDestinationPath Is Nothing Then Throw New ArgumentNullException(NameOf(remoteDestinationPath))
            If remoteDestinationPath.EndsWith(Me.DirectorySeparator) Then Throw New ArgumentException("Must be a path without trailing directory separator char: " & remoteDestinationPath, NameOf(remoteDestinationPath))
            If remoteSource.ItemType = DmsResourceItem.ItemTypes.Root Then Throw New NotSupportedException("Root directory can't be the source of a copy or move action")
            If String.Equals(remoteSource.FullName.TrimEnd(Me.DirectorySeparator), remoteDestinationPath.TrimEnd(Me.DirectorySeparator), StringComparison.Ordinal) Then Throw New ArgumentException("Source and destination paths must differ.", NameOf(remoteDestinationPath))
            If remoteSource.ItemType = DmsResourceItem.ItemTypes.Folder OrElse remoteSource.ItemType = DmsResourceItem.ItemTypes.Collection Then
                Dim prefix As String = remoteSource.FullName.TrimEnd(Me.DirectorySeparator) & Me.DirectorySeparator
                If remoteDestinationPath.StartsWith(prefix, StringComparison.Ordinal) Then Throw New ArgumentException("A directory can't be copied or moved into itself.", NameOf(remoteDestinationPath))
            End If

            Dim destination = Await Me.ListRemoteItemAsync(remoteDestinationPath, Me.CurrentAsyncCancellationToken).ConfigureAwait(False)
            If destination IsNot Nothing Then
                If destination.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(remoteDestinationPath)
                If destination.ItemType = DmsResourceItem.ItemTypes.Root Then Throw New NotSupportedException("Root directory can't be the target of a copy or move action")
                Dim matching As Integer
                For Each candidate In Await Me.ListAllRemoteItemsAsync(Me.ParentDirectoryPath(remoteDestinationPath), SearchItemType.AllItems, Me.CurrentAsyncCancellationToken).ConfigureAwait(False)
                    If String.Equals(candidate.Name, Me.ItemName(remoteDestinationPath), StringComparison.Ordinal) Then matching += 1
                Next
                If matching > 1 Then Throw New RemotePathNotUniqueException(remoteDestinationPath)
                If remoteSource.ItemType = DmsResourceItem.ItemTypes.File AndAlso destination.ItemType <> DmsResourceItem.ItemTypes.File Then Throw New DirectoryAlreadyExistsException(remoteDestinationPath)
                If remoteSource.ItemType <> DmsResourceItem.ItemTypes.File AndAlso destination.ItemType = DmsResourceItem.ItemTypes.File Then Throw New FileAlreadyExistsException(remoteDestinationPath)
                If allowOverwrite.HasValue AndAlso Not allowOverwrite.Value Then
                    If destination.ItemType = DmsResourceItem.ItemTypes.File Then Throw New FileAlreadyExistsException(remoteDestinationPath)
                    Throw New DirectoryAlreadyExistsException(remoteDestinationPath)
                End If
            End If

            Dim parent As String = Me.ParentDirectoryPath(remoteDestinationPath)
            If parent <> Nothing Then
                Dim parentItem = Await Me.ListRemoteItemAsync(parent, Me.CurrentAsyncCancellationToken).ConfigureAwait(False)
                If parentItem Is Nothing Then
                    If Not allowCreationOfRemoteDirectory Then Throw New DirectoryNotFoundException(parent)
                    Await Me.CreateFolderAsync(parent, Me.CurrentAsyncCancellationToken).ConfigureAwait(False)
                ElseIf parentItem.ExtendedInfosCollisionDetected Then
                    Throw New RemotePathNotUniqueException(parent)
                ElseIf parentItem.ItemType = DmsResourceItem.ItemTypes.File Then
                    Throw New FileAlreadyExistsException(parent)
                ElseIf parentItem.ItemType <> DmsResourceItem.ItemTypes.Folder AndAlso parentItem.ItemType <> DmsResourceItem.ItemTypes.Collection Then
                    Throw New NotSupportedException("Remote ressource with unsupported type: " & parent)
                End If
            ElseIf remoteSource.ItemType = DmsResourceItem.ItemTypes.File AndAlso Not Me.SupportsFilesInRootFolder Then
                Throw New NotSupportedException("Files in root folder not supported by DMS provider")
            End If
            Select Case remoteSource.ItemType
                Case DmsResourceItem.ItemTypes.File, DmsResourceItem.ItemTypes.Folder, DmsResourceItem.ItemTypes.Collection
                Case Else
                    Throw New ArgumentOutOfRangeException(NameOf(remoteSource), "Unsupported source item type.")
            End Select
        End Function

        Private Async Function ResolveUniqueSourceItemAsync(remoteSourcePath As String) As Task(Of DmsResourceItem)
            If remoteSourcePath Is Nothing Then Throw New ArgumentNullException(NameOf(remoteSourcePath))
            Dim source = Await Me.ListRemoteItemAsync(remoteSourcePath, Me.CurrentAsyncCancellationToken).ConfigureAwait(False)
            If source Is Nothing Then Throw New RessourceNotFoundException(remoteSourcePath)
            If source.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(remoteSourcePath)
            If source.ItemType <> DmsResourceItem.ItemTypes.Root Then
                Dim matching As Integer
                For Each candidate In Await Me.ListAllRemoteItemsAsync(Me.ParentDirectoryPath(remoteSourcePath), SearchItemType.AllItems, Me.CurrentAsyncCancellationToken).ConfigureAwait(False)
                    If String.Equals(candidate.Name, Me.ItemName(remoteSourcePath), StringComparison.Ordinal) Then matching += 1
                Next
                If matching > 1 Then Throw New RemotePathNotUniqueException(remoteSourcePath)
            End If
            Return source
        End Function

        ''' <summary>
        ''' Check input arguments for copy methods
        ''' </summary>
        ''' <param name="remoteSource"></param>
        ''' <param name="remoteDestinationPath"></param>
        ''' <param name="allowOverwrite"></param>
        ''' <param name="allowCreationOfRemoteDirectory"></param>
        Private Sub CopyMoveArgumentsCheck(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean)
            If remoteSource Is Nothing Then Throw New ArgumentNullException(NameOf(remoteSource))
            If String.IsNullOrEmpty(remoteSource.FullName) Then Throw New ArgumentException("The source item must provide its full remote path.", NameOf(remoteSource))
            If remoteDestinationPath = Nothing Then Throw New ArgumentNullException(NameOf(remoteDestinationPath))
            If remoteDestinationPath.EndsWith(Me.DirectorySeparator) Then Throw New ArgumentException("Must be a path without trailing directory separator char: " & remoteDestinationPath, NameOf(remoteDestinationPath))
            If remoteSource.ItemType = DmsResourceItem.ItemTypes.Root Then Throw New NotSupportedException("Root directory can't be the source of a copy or move action")
            If String.Equals(remoteSource.FullName.TrimEnd(Me.DirectorySeparator), remoteDestinationPath.TrimEnd(Me.DirectorySeparator), StringComparison.Ordinal) Then
                Throw New ArgumentException("Source and destination paths must differ.", NameOf(remoteDestinationPath))
            End If

            If remoteSource.ItemType = DmsResourceItem.ItemTypes.Folder OrElse remoteSource.ItemType = DmsResourceItem.ItemTypes.Collection Then
                Dim SourcePrefix As String = remoteSource.FullName.TrimEnd(Me.DirectorySeparator) & Me.DirectorySeparator
                If remoteDestinationPath.StartsWith(SourcePrefix, StringComparison.Ordinal) Then Throw New ArgumentException("A directory can't be copied or moved into itself.", NameOf(remoteDestinationPath))
            End If

            Dim DestinationItem As DmsResourceItem = Me.ListRemoteItem(remoteDestinationPath)
            If DestinationItem IsNot Nothing AndAlso DestinationItem.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(remoteDestinationPath)
            If DestinationItem IsNot Nothing Then
                If DestinationItem.ItemType = DmsResourceItem.ItemTypes.Root Then Throw New NotSupportedException("Root directory can't be the target of a copy or move action")
                Dim MatchingDestinationItems As Integer = 0
                For Each Candidate As DmsResourceItem In Me.ListAllRemoteItems(Me.ParentDirectoryPath(remoteDestinationPath), SearchItemType.AllItems)
                    If String.Equals(Candidate.Name, Me.ItemName(remoteDestinationPath), StringComparison.Ordinal) Then MatchingDestinationItems += 1
                Next
                If MatchingDestinationItems > 1 Then Throw New RemotePathNotUniqueException(remoteDestinationPath)
                If remoteSource.ItemType = DmsResourceItem.ItemTypes.File AndAlso DestinationItem.ItemType <> DmsResourceItem.ItemTypes.File Then Throw New DirectoryAlreadyExistsException(remoteDestinationPath)
                If remoteSource.ItemType <> DmsResourceItem.ItemTypes.File AndAlso DestinationItem.ItemType = DmsResourceItem.ItemTypes.File Then Throw New FileAlreadyExistsException(remoteDestinationPath)
                If allowOverwrite.HasValue AndAlso allowOverwrite.Value = False Then
                    If DestinationItem.ItemType = DmsResourceItem.ItemTypes.File Then
                        Throw New FileAlreadyExistsException(remoteDestinationPath)
                    Else
                        Throw New DirectoryAlreadyExistsException(remoteDestinationPath)
                    End If
                End If
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
                If remoteSource.ItemType = DmsResourceItem.ItemTypes.File AndAlso Me.SupportsFilesInRootFolder = False Then
                    Throw New NotSupportedException("Files in root folder not supported by DMS provider")
                End If
            End If
            Select Case remoteSource.ItemType
                Case DmsResourceItem.ItemTypes.File, DmsResourceItem.ItemTypes.Folder, DmsResourceItem.ItemTypes.Collection
                    'Supported source object.
                Case Else
                    Throw New ArgumentOutOfRangeException(NameOf(remoteSource), "Unsupported source item type.")
            End Select
        End Sub

        Private Function ResolveUniqueSourceItem(remoteSourcePath As String) As DmsResourceItem
            If remoteSourcePath Is Nothing Then Throw New ArgumentNullException(NameOf(remoteSourcePath))
            Dim RemoteSource As DmsResourceItem = Me.ListRemoteItem(remoteSourcePath)
            If RemoteSource Is Nothing Then Throw New RessourceNotFoundException(remoteSourcePath)
            If RemoteSource.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(remoteSourcePath)
            If RemoteSource.ItemType <> DmsResourceItem.ItemTypes.Root Then
                Dim MatchingSourceItems As Integer = 0
                For Each Candidate As DmsResourceItem In Me.ListAllRemoteItems(Me.ParentDirectoryPath(remoteSourcePath), SearchItemType.AllItems)
                    If String.Equals(Candidate.Name, Me.ItemName(remoteSourcePath), StringComparison.Ordinal) Then MatchingSourceItems += 1
                Next
                If MatchingSourceItems > 1 Then Throw New RemotePathNotUniqueException(remoteSourcePath)
            End If
            Return RemoteSource
        End Function

        Private Sub ResetDestinationCaches(remoteSource As DmsResourceItem, remoteDestinationPath As String)
            Dim ParentDirPathDestination As String = Me.ParentDirectoryPath(remoteDestinationPath)
            If remoteSource.ItemType = DmsResourceItem.ItemTypes.File Then
                Me.ResetCachesForRemoteItems(ParentDirPathDestination, SearchItemType.Files)
            Else
                Me.ResetCachesForRemoteItems(ParentDirPathDestination, SearchItemType.Folders)
                Me.ResetCachesForRemoteItems(ParentDirPathDestination, SearchItemType.Collections)
            End If
        End Sub

        Private Sub ResetMoveCaches(remoteSource As DmsResourceItem, remoteDestinationPath As String)
            Dim ParentDirPathSource As String = Me.ParentDirectoryPath(remoteSource.FullName)
            Me.ResetDestinationCaches(remoteSource, remoteDestinationPath)
            If remoteSource.ItemType = DmsResourceItem.ItemTypes.File Then
                Me.ResetCachesForRemoteItems(ParentDirPathSource, SearchItemType.Files)
            Else
                Me.ResetCachesForRemoteItems(ParentDirPathSource, SearchItemType.Folders)
                Me.ResetCachesForRemoteItems(ParentDirPathSource, SearchItemType.Collections)
            End If
        End Sub

        ''' <summary>
        ''' Copies an item while retaining provider-specific item identity. Providers should override this method when paths aren't unique identifiers.
        ''' </summary>
        ''' <param name="remoteSource">The remote source item.</param>
        ''' <param name="remoteDestinationPath">The absolute destination path.</param>
        ''' <param name="allowOverwrite">True to replace files and merge directories, False to reject existing targets, or Nothing to use the provider default.</param>
        Protected Overridable Sub CopyItem(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?)
            If remoteSource.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(remoteSource.FullName)
            Select Case remoteSource.ItemType
                Case DmsResourceItem.ItemTypes.File
                    Me.CopyFileItem(remoteSource.FullName, remoteDestinationPath, allowOverwrite)
                Case DmsResourceItem.ItemTypes.Folder, DmsResourceItem.ItemTypes.Collection
                    Dim DestinationItem As DmsResourceItem = Me.ListRemoteItem(remoteDestinationPath)
                    If DestinationItem IsNot Nothing AndAlso allowOverwrite = True Then
                        Me.MergeDirectoryContents(remoteSource, remoteDestinationPath, False)
                    Else
                        Me.CopyDirectoryItem(remoteSource.FullName, remoteDestinationPath)
                    End If
                Case Else
                    Throw New NotSupportedException("Unsupported source item type: " & remoteSource.ItemType.ToString())
            End Select
        End Sub

        ''' <summary>
        ''' Copies an item asynchronously while retaining provider-specific item identity. Providers should override this method when paths aren't unique identifiers.
        ''' </summary>
        ''' <param name="remoteSource">The remote source item.</param>
        ''' <param name="remoteDestinationPath">The absolute destination path.</param>
        ''' <param name="allowOverwrite">True to replace files and merge directories, False to reject existing targets, or Nothing to use the provider default.</param>
        Protected Overridable Async Function CopyItemAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?) As Task
            If remoteSource.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(remoteSource.FullName)
            Select Case remoteSource.ItemType
                Case DmsResourceItem.ItemTypes.File
                    Await Me.CopyFileItemAsync(remoteSource.FullName, remoteDestinationPath, allowOverwrite)
                Case DmsResourceItem.ItemTypes.Folder, DmsResourceItem.ItemTypes.Collection
                    Dim DestinationItem As DmsResourceItem
                    If Me.SupportsAsynchronousIo Then
                        DestinationItem = Await Me.ListRemoteItemAsync(remoteDestinationPath, Me.CurrentAsyncCancellationToken).ConfigureAwait(False)
                    Else
                        DestinationItem = Me.ListRemoteItem(remoteDestinationPath)
                    End If
                    If DestinationItem IsNot Nothing AndAlso allowOverwrite = True Then
                        If Me.SupportsAsynchronousIo Then
                            Await Me.MergeDirectoryContentsAsync(remoteSource, remoteDestinationPath).ConfigureAwait(False)
                        Else
                            Await Task.Run(Sub() Me.MergeDirectoryContents(remoteSource, remoteDestinationPath, False)).ConfigureAwait(False)
                        End If
                    Else
                        Await Me.CopyDirectoryItemAsync(remoteSource.FullName, remoteDestinationPath)
                    End If
                Case Else
                    Throw New NotSupportedException("Unsupported source item type: " & remoteSource.ItemType.ToString())
            End Select
        End Function

        Private Async Function MergeDirectoryContentsAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String, Optional moveItems As Boolean = False) As Task
            Dim children = Await Me.ListAllRemoteItemsAsync(remoteSource.FullName, SearchItemType.AllItems, Me.CurrentAsyncCancellationToken).ConfigureAwait(False)
            Dim names As New HashSet(Of String)(StringComparer.Ordinal)
            For Each child In children
                If child.ExtendedInfosCollisionDetected OrElse Not names.Add(child.Name) Then Throw New RemotePathNotUniqueException(child.FullName)
            Next
            For Each child In children
                If moveItems Then
                    Await Me.MoveAsync(child, Me.CombinePath(remoteDestinationPath, child.Name), True, False, Me.CurrentAsyncCancellationToken).ConfigureAwait(False)
                Else
                    Await Me.CopyAsync(child, Me.CombinePath(remoteDestinationPath, child.Name), True, False, Me.CurrentAsyncCancellationToken).ConfigureAwait(False)
                End If
            Next
            If moveItems Then Await Me.DeleteRemoteItemAsync(remoteSource.FullName, Me.CurrentAsyncCancellationToken).ConfigureAwait(False)
        End Function

        Private Sub MergeDirectoryContents(remoteSource As DmsResourceItem, remoteDestinationPath As String, moveItems As Boolean)
            Dim SourceChildren As List(Of DmsResourceItem) = Me.ListAllRemoteItems(remoteSource.FullName, SearchItemType.AllItems)
            Dim ChildNames As New HashSet(Of String)(StringComparer.Ordinal)
            For Each SourceChild As DmsResourceItem In SourceChildren
                If SourceChild.ExtendedInfosCollisionDetected OrElse Not ChildNames.Add(SourceChild.Name) Then Throw New RemotePathNotUniqueException(SourceChild.FullName)
            Next
            For Each SourceChild As DmsResourceItem In SourceChildren
                Dim ChildDestinationPath As String = Me.CombinePath(remoteDestinationPath, SourceChild.Name)
                If moveItems Then
                    Me.Move(SourceChild, ChildDestinationPath, True, False)
                Else
                    Me.Copy(SourceChild, ChildDestinationPath, True, False)
                End If
            Next
            If moveItems Then Me.DeleteRemoteItem(remoteSource)
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
            Me.Move(Me.ResolveUniqueSourceItem(remoteSourcePath), remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory)
        End Sub

        ''' <summary>
        ''' Move a remote DMS item
        ''' </summary>
        ''' <param name="remoteSource"></param>
        ''' <param name="remoteDestinationPath"></param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Public Sub Move(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean)
            Me.CopyMoveArgumentsCheck(remoteSource, remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory)
            Me.MoveItem(remoteSource, remoteDestinationPath, allowOverwrite)
            Me.ResetMoveCaches(remoteSource, remoteDestinationPath)
        End Sub

        ''' <summary>Moves a remote item asynchronously without overwriting an existing destination.</summary>
        ''' <param name="remoteSourcePath">The remote source path.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        ''' <param name="cancellationToken">Cancels queued and active requests.</param>
        ''' <returns>A task that completes after the move.</returns>
        ''' <exception cref="NotSupportedException">The provider does not support native asynchronous move operations.</exception>
        Public Function MoveAsync(remoteSourcePath As String, remoteDestinationPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.MoveAsync(remoteSourcePath, remoteDestinationPath, False, False, cancellationToken)
        End Function

        ''' <summary>Moves a remote item asynchronously while retaining its provider identity.</summary>
        ''' <param name="remoteSource">The source item.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        ''' <param name="cancellationToken">Cancels queued and active requests.</param>
        ''' <returns>A task that completes after the move.</returns>
        ''' <exception cref="NotSupportedException">The provider does not support native asynchronous move operations.</exception>
        Public Function MoveAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.MoveAsync(remoteSource, remoteDestinationPath, False, False, cancellationToken)
        End Function

        ''' <summary>Moves a remote item asynchronously with destination options.</summary>
        ''' <param name="remoteSourcePath">The remote source path.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        ''' <param name="allowOverwrite">Whether files may be replaced or directories merged.</param>
        ''' <param name="allowCreationOfRemoteDirectory">Whether a missing destination parent may be created.</param>
        ''' <param name="cancellationToken">Cancels queued and active requests.</param>
        ''' <returns>A task that completes after the move.</returns>
        ''' <exception cref="NotSupportedException">The provider does not support native asynchronous move operations.</exception>
        Public Async Function MoveAsync(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean, Optional cancellationToken As CancellationToken = Nothing) As Task
            If Not Me.SupportsAsynchronousIo Then Throw New NotSupportedException("This provider does not support asynchronous move operations.")
            Dim previous As CancellationToken = AmbientCancellation.Value
            AmbientCancellation.Value = cancellationToken
            Try
                Dim source = Await Me.ResolveUniqueSourceItemAsync(remoteSourcePath).ConfigureAwait(False)
                Await Me.MoveAsync(source, remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory, cancellationToken).ConfigureAwait(False)
            Finally
                AmbientCancellation.Value = previous
            End Try
        End Function

        ''' <summary>Moves a remote item asynchronously with destination options and provider identity.</summary>
        ''' <param name="remoteSource">The source item.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        ''' <param name="allowOverwrite">Whether files may be replaced or directories merged.</param>
        ''' <param name="allowCreationOfRemoteDirectory">Whether a missing destination parent may be created.</param>
        ''' <param name="cancellationToken">Cancels queued and active requests.</param>
        ''' <returns>A task that completes after the move.</returns>
        ''' <exception cref="NotSupportedException">The provider does not support native asynchronous move operations.</exception>
        Public Async Function MoveAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean, Optional cancellationToken As CancellationToken = Nothing) As Task
            If Not Me.SupportsAsynchronousIo Then Throw New NotSupportedException("This provider does not support asynchronous move operations.")
            Dim previous As CancellationToken = AmbientCancellation.Value
            AmbientCancellation.Value = cancellationToken
            Try
                cancellationToken.ThrowIfCancellationRequested()
                Await Me.CopyMoveArgumentsCheckAsync(remoteSource, remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory).ConfigureAwait(False)
                Await Me.MoveItemAsync(remoteSource, remoteDestinationPath, allowOverwrite, cancellationToken).ConfigureAwait(False)
                Me.ResetMoveCaches(remoteSource, remoteDestinationPath)
            Finally
                AmbientCancellation.Value = previous
            End Try
        End Function

        ''' <summary>Moves an item asynchronously while retaining its provider identity.</summary>
        ''' <param name="remoteSource">The source item.</param>
        ''' <param name="remoteDestinationPath">The destination path.</param>
        ''' <param name="allowOverwrite">Whether an existing destination may be replaced or merged.</param>
        ''' <param name="cancellationToken">Cancels queued and active requests.</param>
        ''' <returns>A task that completes after the move.</returns>
        Protected Overridable Async Function MoveItemAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?, cancellationToken As CancellationToken) As Task
            If remoteSource.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(remoteSource.FullName)
            Select Case remoteSource.ItemType
                Case DmsResourceItem.ItemTypes.File
                    Await Me.MoveFileItemAsync(remoteSource.FullName, remoteDestinationPath, allowOverwrite, cancellationToken).ConfigureAwait(False)
                Case DmsResourceItem.ItemTypes.Folder, DmsResourceItem.ItemTypes.Collection
                    Dim destination = Await Me.ListRemoteItemAsync(remoteDestinationPath, cancellationToken).ConfigureAwait(False)
                    If destination IsNot Nothing AndAlso allowOverwrite = True Then
                        Await Me.MergeDirectoryContentsAsync(remoteSource, remoteDestinationPath, True).ConfigureAwait(False)
                    Else
                        Await Me.MoveDirectoryItemAsync(remoteSource.FullName, remoteDestinationPath, cancellationToken).ConfigureAwait(False)
                    End If
                Case Else
                    Throw New NotSupportedException("Unsupported source item type: " & remoteSource.ItemType.ToString())
            End Select
        End Function

        ''' <summary>Moves a file asynchronously in a provider implementation.</summary>
        ''' <param name="remoteSourcePath">The source path.</param>
        ''' <param name="remoteDestinationPath">The destination path.</param>
        ''' <param name="allowOverwrite">Whether an existing file may be replaced.</param>
        ''' <param name="cancellationToken">Cancels the request.</param>
        ''' <returns>A task that completes after the move.</returns>
        Protected Overridable Function MoveFileItemAsync(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?, cancellationToken As CancellationToken) As Task
            Throw New NotSupportedException("This provider does not support asynchronous move operations.")
        End Function

        ''' <summary>Moves a directory asynchronously in a provider implementation.</summary>
        ''' <param name="remoteSourcePath">The source path.</param>
        ''' <param name="remoteDestinationPath">The destination path.</param>
        ''' <param name="cancellationToken">Cancels the request.</param>
        ''' <returns>A task that completes after the move.</returns>
        Protected Overridable Function MoveDirectoryItemAsync(remoteSourcePath As String, remoteDestinationPath As String, cancellationToken As CancellationToken) As Task
            Throw New NotSupportedException("This provider does not support asynchronous move operations.")
        End Function

        ''' <summary>
        ''' Moves an item while retaining provider-specific item identity. Providers should override this method when paths aren't unique identifiers.
        ''' </summary>
        ''' <param name="remoteSource">The remote source item.</param>
        ''' <param name="remoteDestinationPath">The absolute destination path.</param>
        ''' <param name="allowOverwrite">True to replace files and merge directories, False to reject existing targets, or Nothing to use the provider default.</param>
        Protected Overridable Sub MoveItem(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?)
            If remoteSource.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(remoteSource.FullName)
            Select Case remoteSource.ItemType
                Case DmsResourceItem.ItemTypes.File
                    Me.MoveFileItem(remoteSource.FullName, remoteDestinationPath, allowOverwrite)
                Case DmsResourceItem.ItemTypes.Folder, DmsResourceItem.ItemTypes.Collection
                    Dim DestinationItem As DmsResourceItem = Me.ListRemoteItem(remoteDestinationPath)
                    If DestinationItem IsNot Nothing AndAlso allowOverwrite = True Then
                        Me.MergeDirectoryContents(remoteSource, remoteDestinationPath, True)
                    Else
                        Me.MoveDirectoryItem(remoteSource.FullName, remoteDestinationPath)
                    End If
                Case Else
                    Throw New NotSupportedException("Unsupported source item type: " & remoteSource.ItemType.ToString())
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

        ''' <summary>Deletes a remote item without blocking the calling thread.</summary>
        ''' <param name="remotePath">The remote path to delete.</param>
        ''' <param name="cancellationToken">Cancels the request and any wait for service capacity.</param>
        ''' <returns>A task that completes when deletion finishes.</returns>
        ''' <exception cref="NotSupportedException">The provider does not support asynchronous I/O.</exception>
        Public Overridable Function DeleteRemoteItemAsync(remotePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Throw New NotSupportedException("This provider does not support asynchronous I/O.")
        End Function

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

        ''' <summary>Creates a remote folder without blocking the calling thread.</summary>
        ''' <param name="remoteDirectoryPath">The path of the new folder.</param>
        ''' <param name="cancellationToken">Cancels the request and any wait for service capacity.</param>
        ''' <returns>A task that completes when the folder is created.</returns>
        ''' <exception cref="NotSupportedException">The provider does not support asynchronous I/O.</exception>
        Public Overridable Function CreateFolderAsync(remoteDirectoryPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Throw New NotSupportedException("This provider does not support asynchronous I/O.")
        End Function

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
