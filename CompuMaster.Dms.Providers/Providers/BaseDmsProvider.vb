Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data
Imports System.Collections.Concurrent
Imports System.Threading
Imports System.Threading.Tasks

Namespace Providers

    ''' <summary>
    ''' A DMS provider instance
    ''' </summary>
    Public MustInherit Class BaseDmsProvider

        Private Shared ReadOnly AmbientCancellation As New AsyncLocal(Of CancellationToken)
        Private Shared ReadOnly SynchronousFallbackGates As New ConcurrentDictionary(Of DmsProviders, SemaphoreSlim)

        Private Async Function RunSynchronousFallbackAsync(Of TResult)(operation As Func(Of TResult), cancellationToken As CancellationToken) As Task(Of TResult)
            Dim gate As SemaphoreSlim = SynchronousFallbackGates.GetOrAdd(Me.DmsProviderID, Function(key) New SemaphoreSlim(1, 1))
            Await gate.WaitAsync(cancellationToken).ConfigureAwait(False)
            Try
                cancellationToken.ThrowIfCancellationRequested()
                Return Await Task.Run(operation).ConfigureAwait(False)
            Finally
                gate.Release()
            End Try
        End Function

        ''' <summary>Runs a synchronous provider operation without blocking the calling thread.</summary>
        ''' <param name="operation">The synchronous operation to execute.</param>
        ''' <param name="cancellationToken">Cancels a queued operation; an active synchronous call cannot be interrupted.</param>
        ''' <returns>A task that completes when the operation finishes.</returns>
        ''' <remarks>Operations using this helper are serialized across provider instances of the same backend in this process.</remarks>
        Protected Async Function RunSynchronousFallbackAsync(operation As Action, cancellationToken As CancellationToken) As Task
            Await Me.RunSynchronousFallbackAsync(Function()
                                                    operation()
                                                    Return True
                                                End Function, cancellationToken).ConfigureAwait(False)
        End Function

        ''' <summary>Gets the cancellation token for the current asynchronous item operation.</summary>
        ''' <returns>The token supplied by the caller, or CancellationToken.None.</returns>
        Protected ReadOnly Property CurrentAsyncCancellationToken As CancellationToken
            Get
                Return AmbientCancellation.Value
            End Get
        End Property

        ''' <summary>Indicates whether this provider implements native asynchronous remote I/O.</summary>
        ''' <returns>True when remote requests can be awaited without occupying a worker thread; the Task-based API also supports a serialized worker fallback.</returns>
        Public Overridable ReadOnly Property SupportsAsynchronousIo As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>Identifies the built-in provider implementations.</summary>
        Public Enum DmsProviders As Integer
            ''' <summary>Selects manual URL entry without a remote provider.</summary>
            <System.ComponentModel.Description("URL (manueller Transfer)")>
            ManualUrl = -1
            ''' <summary>Selects no DMS provider.</summary>
            None = 0
            ''' <summary>Selects the WebDAV provider, including supported ownCloud and Nextcloud servers.</summary>
            <System.ComponentModel.Description("WebDAV (OwnCloud, NextCloud, etc.)")>
            WebDAV = 1
            ''' <summary>Selects Scopevisio Teamwork.</summary>
            <System.ComponentModel.Description("Scopevisio Teamwork")>
            Scopevisio = 20
            ''' <summary>Selects the legacy direct CenterDevice provider; initial login remains incomplete.</summary>
            CenterDevice = 21
        End Enum

        ''' <summary>
        ''' The unique ID of the provider
        ''' </summary>
        Public MustOverride ReadOnly Property DmsProviderID As DmsProviders

        ''' <summary>
        ''' The name of the provider
        ''' </summary>
        Public MustOverride ReadOnly Property Name As String

        ''' <summary>
        ''' The url to access the web API endpoint 
        ''' </summary>
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
            ''' <summary>Includes files, folders and collections.</summary>
            AllItems = 0
            ''' <summary>Includes provider collections.</summary>
            Collections = 1
            ''' <summary>Includes ordinary folders.</summary>
            Folders = 2
            ''' <summary>Includes files.</summary>
            Files = 3
        End Enum

        ''' <summary>
        ''' Open a remote item (file/folder/collection) or null/Nothing if the remote item doesn't exist
        ''' </summary>
        ''' <param name="remotePath">The remote resource path.</param>
        ''' <returns>The matching resource snapshot, or Nothing when the provider reports no result.</returns>
        Public MustOverride Function ListRemoteItem(remotePath As String) As DmsResourceItem

        ''' <summary>
        ''' An existance check for a remote item
        ''' </summary>
        ''' <param name="remotePath">The remote resource path.</param>
        ''' <returns></returns>
        Public Overridable Function RemoteItemExists(remotePath As String) As Boolean
            Dim RemoteItem As DmsResourceItem
            RemoteItem = Me.ListRemoteItem(remotePath)
            Return RemoteItem IsNot Nothing
        End Function

        ''' <summary>
        ''' An existance check for a remote item
        ''' </summary>
        ''' <param name="remotePath">The remote resource path.</param>
        ''' <returns>The kind of resource found at the path.</returns>
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
        ''' <param name="remotePath">The remote resource path.</param>
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
        ''' <param name="searchType">The resource kinds included in the listing or cache reset.</param>
        Public Sub ResetCachesForRemoteItems(remoteItem As DmsResourceItem, searchType As SearchItemType)
            Select Case remoteItem.ItemType
                Case DmsResourceItem.ItemTypes.Collection, DmsResourceItem.ItemTypes.Folder, DmsResourceItem.ItemTypes.Root

                Case DmsResourceItem.ItemTypes.File
                    Throw New NotSupportedException(ProviderStrings.GetText("FilesDonTContainDirectoryCaches"))
                Case Else
                    Throw New NotImplementedException
            End Select
        End Sub

        ''' <summary>
        ''' Reset file system cache and force refresh on next access
        ''' </summary>
        ''' <param name="remoteFolderPath">The remote folder or collection path.</param>
        ''' <param name="searchType">The resource kinds included in the listing or cache reset.</param>
        Public MustOverride Sub ResetCachesForRemoteItems(remoteFolderPath As String, searchType As SearchItemType)

        ''' <summary>Invalidates cached remote children without blocking the calling thread.</summary>
        ''' <param name="remoteFolderPath">The remote parent path.</param>
        ''' <param name="searchType">The child types whose cache should be reset.</param>
        ''' <param name="cancellationToken">Cancels a queued cache operation.</param>
        ''' <returns>A task that completes when the cache is reset.</returns>
        Public Overridable Function ResetCachesForRemoteItemsAsync(remoteFolderPath As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.ResetCachesForRemoteItems(remoteFolderPath, searchType), cancellationToken)
        End Function

        ''' <summary>Resets cached information for a selected remote directory asynchronously.</summary>
        ''' <param name="remoteItem">The selected remote directory.</param>
        ''' <param name="searchType">The cached child types to reset.</param>
        ''' <param name="cancellationToken">Cancels a queued reset.</param>
        ''' <returns>A task that completes after the cache reset.</returns>
        Public Overridable Function ResetCachesForRemoteItemsAsync(remoteItem As DmsResourceItem, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.ResetCachesForRemoteItems(remoteItem, searchType), cancellationToken)
        End Function

        ''' <summary>
        ''' List all child items (files/folders/collections) for a remote path
        ''' </summary>
        ''' <param name="remoteFolderPath">The remote folder or collection path.</param>
        ''' <param name="searchType">The resource kinds included in the listing or cache reset.</param>
        ''' <returns>The matching child resource snapshots supplied by the provider.</returns>
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
        ''' <param name="remoteFolderPath">The remote folder or collection path.</param>
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
        ''' <param name="remoteFolderPath">The remote folder or collection path.</param>
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
        ''' <param name="remoteFolderPath">The remote folder or collection path.</param>
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
        ''' <param name="remoteFolderPath">The remote folder or collection path.</param>
        ''' <returns>The names of matching child collections.</returns>
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
        ''' <param name="remoteFolderPath">The remote folder or collection path.</param>
        ''' <returns>The names of matching child folders.</returns>
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
        ''' <param name="remoteFolderPath">The remote folder or collection path.</param>
        ''' <returns>The names of matching child files.</returns>
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
        ''' <param name="id">The provider-owned resource identifier.</param>
        ''' <returns>The collection snapshot returned by the provider for the specified identifier.</returns>
        Public MustOverride Function FindCollectionById(id As String) As DmsResourceItem

        ''' <summary>
        ''' Load a remote folder item based on its ID
        ''' </summary>
        ''' <param name="id">The provider-owned resource identifier.</param>
        ''' <returns>The folder snapshot returned by the provider for the specified identifier.</returns>
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
        ''' <param name="id">The provider-owned resource identifier.</param>
        ''' <returns>The file snapshot returned by the provider for the specified identifier.</returns>
        Public MustOverride Function FindFileById(id As String) As DmsResourceItem

        ''' <summary>Finds a remote item without blocking the calling thread.</summary>
        ''' <param name="remotePath">The remote path to inspect.</param>
        ''' <param name="cancellationToken">Cancels the request and any wait for service capacity.</param>
        ''' <returns>The matching item, or Nothing when the path is absent.</returns>
        ''' <remarks>Providers without native asynchronous I/O serialize this operation across provider instances for the same backend. Cancellation stops a queued operation, but cannot interrupt an active synchronous request.</remarks>
        Public Overridable Function ListRemoteItemAsync(remotePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            Return Me.RunSynchronousFallbackAsync(Function() Me.ListRemoteItem(remotePath), cancellationToken)
        End Function

        ''' <summary>Lists remote child items without blocking the calling thread.</summary>
        ''' <param name="remoteFolderPath">The remote parent path.</param>
        ''' <param name="searchType">The child types to include.</param>
        ''' <param name="cancellationToken">Cancels the request and any wait for service capacity.</param>
        ''' <returns>The matching child items.</returns>
        ''' <remarks>Providers without native asynchronous I/O serialize this operation across provider instances for the same backend. Cancellation stops a queued operation, but cannot interrupt an active synchronous request.</remarks>
        Public Overridable Function ListAllRemoteItemsAsync(remoteFolderPath As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return Me.RunSynchronousFallbackAsync(Function() Me.ListAllRemoteItems(remoteFolderPath, searchType), cancellationToken)
        End Function

        ''' <summary>Checks whether a remote item exists asynchronously.</summary>
        ''' <param name="remotePath">The remote path to check.</param>
        ''' <param name="cancellationToken">Cancels a queued or native request.</param>
        ''' <returns>True when an item exists at the path.</returns>
        Public Overridable Async Function RemoteItemExistsAsync(remotePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of Boolean)
            If Not Me.SupportsAsynchronousIo Then Return Await Me.RunSynchronousFallbackAsync(Function() Me.RemoteItemExists(remotePath), cancellationToken).ConfigureAwait(False)
            Return Await Me.ListRemoteItemAsync(remotePath, cancellationToken).ConfigureAwait(False) IsNot Nothing
        End Function

        ''' <summary>Checks the type of a remote item asynchronously.</summary>
        ''' <param name="remotePath">The remote path to check.</param>
        ''' <param name="cancellationToken">Cancels a queued or native request.</param>
        ''' <returns>The item type or NotFound.</returns>
        Public Overridable Async Function RemoteItemExistsAsAsync(remotePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem.FoundItemType)
            If Not Me.SupportsAsynchronousIo Then Return Await Me.RunSynchronousFallbackAsync(Function() Me.RemoteItemExistsAs(remotePath), cancellationToken).ConfigureAwait(False)
            Dim item = Await Me.ListRemoteItemAsync(remotePath, cancellationToken).ConfigureAwait(False)
            Return If(item Is Nothing, DmsResourceItem.FoundItemType.NotFound, CType(CType(item.ItemType, Byte), DmsResourceItem.FoundItemType))
        End Function

        ''' <summary>Checks a remote item's type and name collisions asynchronously.</summary>
        ''' <param name="remotePath">The remote path to check.</param>
        ''' <param name="cancellationToken">Cancels a queued or native request.</param>
        ''' <returns>The item result, including name collisions.</returns>
        Public Overridable Async Function RemoteItemExistsUniquelyAsAsync(remotePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem.FoundItemResult)
            If Not Me.SupportsAsynchronousIo Then Return Await Me.RunSynchronousFallbackAsync(Function() Me.RemoteItemExistsUniquelyAs(remotePath), cancellationToken).ConfigureAwait(False)
            Dim item = Await Me.ListRemoteItemAsync(remotePath, cancellationToken).ConfigureAwait(False)
            If item Is Nothing Then Return DmsResourceItem.FoundItemResult.NotFound
            If item.ExtendedInfosCollisionDetected Then Return DmsResourceItem.FoundItemResult.WithNameCollisions
            Return CType(CType(item.ItemType, Byte), DmsResourceItem.FoundItemResult)
        End Function

        ''' <summary>Lists immediate directory and file entries for browsing asynchronously.</summary>
        ''' <param name="remoteFolderPath">The remote parent path.</param>
        ''' <param name="cancellationToken">Cancels queued or active listing requests.</param>
        ''' <returns>The child directory and file entries, including their browsing metadata.</returns>
        ''' <exception cref="OperationCanceledException">The listing is canceled.</exception>
        ''' <remarks>The default implementation combines the existing directory and file entry workflows, omitting files in the browsing root when the provider does not support them. Providers may retrieve both kinds in one request. Detailed item lookups may still be required for properties or sharing dialogs. Results are not cached by this method.</remarks>
        Public Overridable Async Function ListEntriesAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            cancellationToken.ThrowIfCancellationRequested()
            Dim directories = Await Me.ListDirectoryEntriesAsync(remoteFolderPath, cancellationToken).ConfigureAwait(False)
            cancellationToken.ThrowIfCancellationRequested()
            If Not Me.SupportsFilesInRootFolder AndAlso String.Equals(If(remoteFolderPath, "").Trim(Me.DirectorySeparator), If(Me.BrowseInRootFolderName, "").Trim(Me.DirectorySeparator), StringComparison.Ordinal) Then
                Return New List(Of DmsResourceItem)(directories)
            End If
            Dim files = Await Me.ListFileEntriesAsync(remoteFolderPath, cancellationToken).ConfigureAwait(False)
            cancellationToken.ThrowIfCancellationRequested()
            Dim result As New List(Of DmsResourceItem)(directories)
            result.AddRange(files)
            Return result
        End Function

        ''' <summary>Lists immediate directory entries for browsing asynchronously.</summary>
        ''' <param name="remoteFolderPath">The remote parent path.</param>
        ''' <param name="cancellationToken">Cancels queued or active listing requests.</param>
        ''' <returns>The child directory identities, names, paths, sharing indicators and optional child-directory metadata.</returns>
        ''' <exception cref="OperationCanceledException">The listing is canceled.</exception>
        ''' <remarks>Providers may defer detailed principal and link lookups to avoid per-entry requests. Sharing indicators and link identifiers are retained for browsing. Retrieve the selected resource through an item or identifier lookup before showing its properties or sharing details. The default implementation delegates to <see cref="ListAllDirectoryItemsAsync"/> for compatibility with derived providers.</remarks>
        Public Overridable Function ListDirectoryEntriesAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return Me.ListAllDirectoryItemsAsync(remoteFolderPath, cancellationToken)
        End Function

        ''' <summary>Lists immediate file entries for browsing asynchronously.</summary>
        ''' <param name="remoteFolderPath">The remote parent path.</param>
        ''' <param name="cancellationToken">Cancels queued or active listing requests.</param>
        ''' <returns>The file identities, names, paths, sizes, timestamps and sharing indicators.</returns>
        ''' <exception cref="OperationCanceledException">The listing is canceled.</exception>
        ''' <remarks>Providers may defer detailed principal and link metadata to avoid per-entry requests. Retrieve the selected resource through an item or identifier lookup before showing its properties or sharing details. The default implementation delegates to <see cref="ListAllFileItemsAsync"/> for compatibility with derived providers.</remarks>
        Public Overridable Function ListFileEntriesAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return Me.ListAllFileItemsAsync(remoteFolderPath, cancellationToken)
        End Function

        ''' <summary>Lists child folders and collections asynchronously.</summary>
        ''' <param name="remoteFolderPath">The remote parent path.</param>
        ''' <param name="cancellationToken">Cancels a queued or native request.</param>
        ''' <returns>The direct child directories.</returns>
        Public Overridable Async Function ListAllDirectoryItemsAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            If Not Me.SupportsAsynchronousIo Then Return Await Me.RunSynchronousFallbackAsync(Function() Me.ListAllDirectoryItems(remoteFolderPath), cancellationToken).ConfigureAwait(False)
            Dim items = Await Me.ListAllRemoteItemsAsync(remoteFolderPath, SearchItemType.AllItems, cancellationToken).ConfigureAwait(False)
            Return items.FindAll(Function(item) item.ItemType = DmsResourceItem.ItemTypes.Folder OrElse item.ItemType = DmsResourceItem.ItemTypes.Collection)
        End Function

        ''' <summary>Lists child collections asynchronously.</summary>
        ''' <param name="remoteFolderPath">The remote parent path.</param>
        ''' <param name="cancellationToken">Cancels a queued or native request.</param>
        ''' <returns>The direct child collections.</returns>
        Public Overridable Async Function ListAllCollectionItemsAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            If Not Me.SupportsAsynchronousIo Then Return Await Me.RunSynchronousFallbackAsync(Function() Me.ListAllCollectionItems(remoteFolderPath), cancellationToken).ConfigureAwait(False)
            Dim items = Await Me.ListAllRemoteItemsAsync(remoteFolderPath, SearchItemType.Collections, cancellationToken).ConfigureAwait(False)
            Return items.FindAll(Function(item) item.ItemType = DmsResourceItem.ItemTypes.Collection)
        End Function

        ''' <summary>Lists child folders asynchronously.</summary>
        ''' <param name="remoteFolderPath">The remote parent path.</param>
        ''' <param name="cancellationToken">Cancels a queued or native request.</param>
        ''' <returns>The direct child folders.</returns>
        Public Overridable Async Function ListAllFolderItemsAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            If Not Me.SupportsAsynchronousIo Then Return Await Me.RunSynchronousFallbackAsync(Function() Me.ListAllFolderItems(remoteFolderPath), cancellationToken).ConfigureAwait(False)
            Dim items = Await Me.ListAllRemoteItemsAsync(remoteFolderPath, SearchItemType.Folders, cancellationToken).ConfigureAwait(False)
            Return items.FindAll(Function(item) item.ItemType = DmsResourceItem.ItemTypes.Folder)
        End Function

        ''' <summary>Lists child files asynchronously.</summary>
        ''' <param name="remoteFolderPath">The remote parent path.</param>
        ''' <param name="cancellationToken">Cancels a queued or native request.</param>
        ''' <returns>The direct child files.</returns>
        Public Overridable Async Function ListAllFileItemsAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            If Not Me.SupportsAsynchronousIo Then Return Await Me.RunSynchronousFallbackAsync(Function() Me.ListAllFileItems(remoteFolderPath), cancellationToken).ConfigureAwait(False)
            Dim items = Await Me.ListAllRemoteItemsAsync(remoteFolderPath, SearchItemType.Files, cancellationToken).ConfigureAwait(False)
            Return items.FindAll(Function(item) item.ItemType = DmsResourceItem.ItemTypes.File)
        End Function

        ''' <summary>Lists child collection names asynchronously.</summary>
        ''' <param name="remoteFolderPath">The remote parent path.</param>
        ''' <param name="cancellationToken">Cancels a queued or native request.</param>
        ''' <returns>The collection names.</returns>
        Public Overridable Async Function ListAllCollectionNamesAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of String))
            If Not Me.SupportsAsynchronousIo Then Return Await Me.RunSynchronousFallbackAsync(Function() Me.ListAllCollectionNames(remoteFolderPath), cancellationToken).ConfigureAwait(False)
            Return (Await Me.ListAllCollectionItemsAsync(remoteFolderPath, cancellationToken).ConfigureAwait(False)).ConvertAll(Function(item) item.Name)
        End Function

        ''' <summary>Lists child folder names asynchronously.</summary>
        ''' <param name="remoteFolderPath">The remote parent path.</param>
        ''' <param name="cancellationToken">Cancels a queued or native request.</param>
        ''' <returns>The folder names.</returns>
        Public Overridable Async Function ListAllFolderNamesAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of String))
            If Not Me.SupportsAsynchronousIo Then Return Await Me.RunSynchronousFallbackAsync(Function() Me.ListAllFolderNames(remoteFolderPath), cancellationToken).ConfigureAwait(False)
            Return (Await Me.ListAllFolderItemsAsync(remoteFolderPath, cancellationToken).ConfigureAwait(False)).ConvertAll(Function(item) item.Name)
        End Function

        ''' <summary>Lists child file names asynchronously.</summary>
        ''' <param name="remoteFolderPath">The remote parent path.</param>
        ''' <param name="cancellationToken">Cancels a queued or native request.</param>
        ''' <returns>The file names.</returns>
        Public Overridable Async Function ListAllFileNamesAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of String))
            If Not Me.SupportsAsynchronousIo Then Return Await Me.RunSynchronousFallbackAsync(Function() Me.ListAllFileNames(remoteFolderPath), cancellationToken).ConfigureAwait(False)
            Return (Await Me.ListAllFileItemsAsync(remoteFolderPath, cancellationToken).ConfigureAwait(False)).ConvertAll(Function(item) item.Name)
        End Function

        ''' <summary>Finds a collection by identifier without blocking the calling thread.</summary>
        ''' <param name="id">The provider-specific collection identifier.</param>
        ''' <param name="cancellationToken">Cancels a queued lookup.</param>
        ''' <returns>The collection.</returns>
        Public Overridable Function FindCollectionByIdAsync(id As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            Return Me.RunSynchronousFallbackAsync(Function() Me.FindCollectionById(id), cancellationToken)
        End Function

        ''' <summary>Finds a folder by identifier without blocking the calling thread.</summary>
        ''' <param name="id">The provider-specific folder identifier.</param>
        ''' <param name="cancellationToken">Cancels a queued lookup.</param>
        ''' <returns>The folder.</returns>
        Public Overridable Function FindFolderByIdAsync(id As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            Return Me.RunSynchronousFallbackAsync(Function() Me.FindFolderById(id), cancellationToken)
        End Function

        ''' <summary>Finds a file by identifier without blocking the calling thread.</summary>
        ''' <param name="id">The provider-specific file identifier.</param>
        ''' <param name="cancellationToken">Cancels a queued lookup.</param>
        ''' <returns>The file.</returns>
        Public Overridable Function FindFileByIdAsync(id As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            Return Me.RunSynchronousFallbackAsync(Function() Me.FindFileById(id), cancellationToken)
        End Function

        ''' <summary>Finds a document by identifier without blocking the calling thread.</summary>
        ''' <param name="id">The provider-specific document identifier.</param>
        ''' <param name="cancellationToken">Cancels a queued lookup.</param>
        ''' <returns>The document.</returns>
        <Obsolete("Use FindFileByIdAsync instead")>
        Public Overridable Function FindDocumentByIdAsync(id As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            Return Me.FindFileByIdAsync(id, cancellationToken)
        End Function

        ''' <summary>
        ''' Create a provider-specific credentials instance for further customization
        ''' </summary>
        ''' <returns>A new credential object configured for this provider.</returns>
        Public MustOverride Function CreateNewCredentialsInstance() As BaseDmsLoginCredentials

        ''' <summary>
        ''' The desired exception type in case of errors
        ''' </summary>
        Protected Enum ExceptionTypeForItemType As Byte
            ''' <summary>Uses the generic resource exception contract.</summary>
            Unspecified = 0
            ''' <summary>Uses the directory exception contract.</summary>
            Directory = 1
            ''' <summary>Uses the file exception contract.</summary>
            File = 2
        End Enum

        ''' <summary>
        ''' Upload a local file to the remote DMS, if applicable: create a new version to an existing file
        ''' </summary>
        ''' <param name="remoteFilePath">The remote file path.</param>
        ''' <param name="localFilePath">The path of the local source or destination file.</param>
        ''' <exception cref="System.IO.IOException">The local source cannot be read. A path conflict can include a directory occupying the file path; the exact exception mapping depends on the operating system, runtime, and file system.</exception>
        ''' <exception cref="UnauthorizedAccessException">Access to the local path is denied, or the platform reports a directory at the file path as access denied.</exception>
        ''' <exception cref="System.IO.DirectoryNotFoundException">A required local parent directory does not exist.</exception>
        ''' <exception cref="System.IO.PathTooLongException">The local path exceeds the limits of the operating system or runtime.</exception>
        ''' <exception cref="ArgumentException">The local path is empty or invalid for the runtime.</exception>
        ''' <exception cref="NotSupportedException">The runtime does not support the local path format.</exception>
        ''' <exception cref="System.IO.FileNotFoundException">The local source file does not exist.</exception>
        Public MustOverride Sub UploadFile(remoteFilePath As String, localFilePath As String)

        ''' <summary>Uploads a local file without blocking the calling thread.</summary>
        ''' <param name="remoteFilePath">The remote destination path.</param>
        ''' <param name="localFilePath">The local source path.</param>
        ''' <param name="cancellationToken">Cancels the upload and any wait for service capacity.</param>
        ''' <returns>A task that completes when the upload finishes.</returns>
        ''' <remarks>Providers without native asynchronous I/O serialize this operation across provider instances for the same backend. Cancellation stops a queued upload, but cannot interrupt an active synchronous upload.</remarks>
        ''' <exception cref="System.IO.IOException">The local source cannot be read. A path conflict can include a directory occupying the file path; the exact exception mapping depends on the operating system, runtime, and file system.</exception>
        ''' <exception cref="UnauthorizedAccessException">Access to the local path is denied, or the platform reports a directory at the file path as access denied.</exception>
        ''' <exception cref="System.IO.DirectoryNotFoundException">A required local parent directory does not exist.</exception>
        ''' <exception cref="System.IO.PathTooLongException">The local path exceeds the limits of the operating system or runtime.</exception>
        ''' <exception cref="ArgumentException">The local path is empty or invalid for the runtime.</exception>
        ''' <exception cref="NotSupportedException">The runtime does not support the local path format.</exception>
        ''' <exception cref="System.IO.FileNotFoundException">The local source file does not exist.</exception>
        Public Overridable Function UploadFileAsync(remoteFilePath As String, localFilePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.UploadFile(remoteFilePath, localFilePath), cancellationToken)
        End Function

        ''' <summary>Uploads bytes to a remote file without blocking the calling thread.</summary>
        ''' <param name="remoteFilePath">The remote destination path.</param>
        ''' <param name="binaryData">The file contents.</param>
        ''' <param name="cancellationToken">Cancels a queued upload.</param>
        ''' <returns>A task that completes when the upload finishes.</returns>
        Public Overridable Function UploadFileAsync(remoteFilePath As String, binaryData As Byte(), Optional cancellationToken As CancellationToken = Nothing) As Task
            If Me.SupportsAsynchronousIo Then Return Me.UploadFileAsync(remoteFilePath, Function() New System.IO.MemoryStream(binaryData), cancellationToken)
            Return Me.RunSynchronousFallbackAsync(Sub() Me.UploadFile(remoteFilePath, binaryData), cancellationToken)
        End Function

        ''' <summary>Uploads stream contents to a remote file without blocking the calling thread.</summary>
        ''' <param name="remoteFilePath">The remote destination path.</param>
        ''' <param name="binaryData">A factory for the input stream.</param>
        ''' <param name="cancellationToken">Cancels a queued upload.</param>
        ''' <returns>A task that completes when the upload finishes.</returns>
        Public Overridable Function UploadFileAsync(remoteFilePath As String, binaryData As Func(Of System.IO.Stream), Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.UploadFile(remoteFilePath, binaryData), cancellationToken)
        End Function

        ''' <summary>Uploads a local file with optional progress reporting.</summary>
        ''' <param name="remoteFilePath">The remote destination path.</param>
        ''' <param name="localFilePath">The local source path.</param>
        ''' <param name="progress">Receives immutable source-byte/phase snapshots; Nothing disables reporting.</param>
        ''' <param name="cancellationToken">Cancels the request where supported by the provider.</param>
        ''' <returns>A task that completes only after the provider confirms success.</returns>
        ''' <remarks>The default implementation preserves a derived provider's existing local-file override and reports unknown byte counts. Providers supporting stream progress override this method. Existing UploadFileAsync overloads retain their signatures and behavior. A completed source-byte count does not imply server success.</remarks>
        ''' <exception cref="System.IO.IOException">The source cannot be read or the transfer fails.</exception>
        ''' <exception cref="UnauthorizedAccessException">Access to the local source is denied.</exception>
        ''' <exception cref="OperationCanceledException">The supported upload operation is cancelled.</exception>
        Public Overridable Async Function UploadFileWithProgressAsync(remoteFilePath As String, localFilePath As String, progress As IProgress(Of DmsTransferProgress), Optional cancellationToken As CancellationToken = Nothing) As Task
            cancellationToken.ThrowIfCancellationRequested()
            progress?.Report(New DmsTransferProgress(Nothing, Nothing, DmsTransferPhase.Transferring))
            Await Me.UploadFileAsync(remoteFilePath, localFilePath, cancellationToken).ConfigureAwait(False)
            progress?.Report(New DmsTransferProgress(Nothing, Nothing, DmsTransferPhase.Completed))
        End Function

        ''' <summary>Uploads source-stream contents with optional progress reporting.</summary>
        ''' <param name="remoteFilePath">The remote destination path.</param>
        ''' <param name="binaryData">Creates a readable stream whose ownership transfers to the provider.</param>
        ''' <param name="progress">Receives source bytes consumed by the transport, optional totals, and provider phases.</param>
        ''' <param name="cancellationToken">Cancels the supported request or queued fallback.</param>
        ''' <returns>A task that completes only after the upload succeeds.</returns>
        ''' <remarks>Unknown stream lengths remain Nothing. Callbacks must not throw. The existing provider's stream-factory implementation and active-cancellation limitations are preserved. Source bytes exclude protocol overhead and do not prove remote persistence.</remarks>
        ''' <exception cref="ArgumentNullException">The stream factory is Nothing.</exception>
        ''' <exception cref="System.IO.IOException">Reading the stream or transferring its contents fails.</exception>
        ''' <exception cref="OperationCanceledException">The supported operation is cancelled.</exception>
        Public Overridable Async Function UploadFileWithProgressAsync(remoteFilePath As String, binaryData As Func(Of System.IO.Stream), progress As IProgress(Of DmsTransferProgress), Optional cancellationToken As CancellationToken = Nothing) As Task
            If binaryData Is Nothing Then Throw New ArgumentNullException(NameOf(binaryData))
            cancellationToken.ThrowIfCancellationRequested()
            progress?.Report(New DmsTransferProgress(Nothing, Nothing, DmsTransferPhase.Transferring))
            Dim tracked As UploadProgressStream = Nothing
            Await Me.UploadFileAsync(remoteFilePath, Function()
                                                       tracked = New UploadProgressStream(binaryData(), progress)
                                                       Return tracked
                                                   End Function, cancellationToken).ConfigureAwait(False)
            progress?.Report(If(tracked Is Nothing, New DmsTransferProgress(Nothing, Nothing, DmsTransferPhase.Completed), tracked.Snapshot(DmsTransferPhase.Completed)))
        End Function

        Private Async Function EnsureUploadParentAsync(remoteFilePath As String, recursive As Boolean, cancellationToken As CancellationToken) As Task
            If remoteFilePath Is Nothing Then Throw New ArgumentNullException(NameOf(remoteFilePath))
            Dim parentDirectory As String = Me.ParentDirectoryPath(remoteFilePath)
            If parentDirectory IsNot Nothing AndAlso Not Await Me.RemoteItemExistsAsync(parentDirectory, cancellationToken).ConfigureAwait(False) Then
                If recursive Then
                    Await Me.CreateFolderAsync(parentDirectory, True, cancellationToken).ConfigureAwait(False)
                Else
                    Await Me.CreateFolderAsync(parentDirectory, cancellationToken).ConfigureAwait(False)
                End If
            End If
        End Function

        ''' <summary>Uploads a local file and optionally creates its remote parent directory asynchronously.</summary>
        ''' <param name="remoteFilePath">The remote destination path.</param>
        ''' <param name="localFilePath">The local source path.</param>
        ''' <param name="createDirectoryStructureIfMissing">Creates missing remote parent folders when true.</param>
        ''' <param name="cancellationToken">Cancels a queued upload.</param>
        ''' <returns>A task that completes when the upload finishes.</returns>
        ''' <exception cref="System.IO.IOException">The local source cannot be read. A path conflict can include a directory occupying the file path; the exact exception mapping depends on the operating system, runtime, and file system.</exception>
        ''' <exception cref="UnauthorizedAccessException">Access to the local path is denied, or the platform reports a directory at the file path as access denied.</exception>
        ''' <exception cref="System.IO.DirectoryNotFoundException">A required local parent directory does not exist.</exception>
        ''' <exception cref="System.IO.PathTooLongException">The local path exceeds the limits of the operating system or runtime.</exception>
        ''' <exception cref="ArgumentException">The local path is empty or invalid for the runtime.</exception>
        ''' <exception cref="NotSupportedException">The runtime does not support the local path format.</exception>
        ''' <exception cref="System.IO.FileNotFoundException">The local source file does not exist.</exception>
        Public Overridable Async Function UploadFileAsync(remoteFilePath As String, localFilePath As String, createDirectoryStructureIfMissing As Boolean, Optional cancellationToken As CancellationToken = Nothing) As Task
            If Not createDirectoryStructureIfMissing Then
                Await Me.UploadFileAsync(remoteFilePath, localFilePath, cancellationToken).ConfigureAwait(False)
                Return
            End If
            If Not Me.SupportsAsynchronousIo Then
                Await Me.RunSynchronousFallbackAsync(Sub() Me.UploadFile(remoteFilePath, localFilePath, createDirectoryStructureIfMissing), cancellationToken).ConfigureAwait(False)
                Return
            End If
            Await Me.EnsureUploadParentAsync(remoteFilePath, False, cancellationToken).ConfigureAwait(False)
            Await Me.UploadFileAsync(remoteFilePath, localFilePath, cancellationToken).ConfigureAwait(False)
        End Function

        ''' <summary>Uploads stream contents and optionally creates their remote parent directory asynchronously.</summary>
        ''' <param name="remoteFilePath">The remote destination path.</param>
        ''' <param name="binaryData">A factory for the input stream.</param>
        ''' <param name="createDirectoryStructureIfMissing">Creates missing remote parent folders when true.</param>
        ''' <param name="cancellationToken">Cancels a queued upload.</param>
        ''' <returns>A task that completes when the upload finishes.</returns>
        Public Overridable Async Function UploadFileAsync(remoteFilePath As String, binaryData As Func(Of System.IO.Stream), createDirectoryStructureIfMissing As Boolean, Optional cancellationToken As CancellationToken = Nothing) As Task
            If Not createDirectoryStructureIfMissing Then
                Await Me.UploadFileAsync(remoteFilePath, binaryData, cancellationToken).ConfigureAwait(False)
                Return
            End If
            If Not Me.SupportsAsynchronousIo Then
                Await Me.RunSynchronousFallbackAsync(Sub() Me.UploadFile(remoteFilePath, binaryData, createDirectoryStructureIfMissing), cancellationToken).ConfigureAwait(False)
                Return
            End If
            Await Me.EnsureUploadParentAsync(remoteFilePath, False, cancellationToken).ConfigureAwait(False)
            Await Me.UploadFileAsync(remoteFilePath, binaryData, cancellationToken).ConfigureAwait(False)
        End Function

        ''' <summary>Uploads bytes and optionally creates their remote parent directory asynchronously.</summary>
        ''' <param name="remoteFilePath">The remote destination path.</param>
        ''' <param name="binaryData">The file contents.</param>
        ''' <param name="createDirectoryStructureIfMissing">Creates missing remote parent folders when true.</param>
        ''' <param name="cancellationToken">Cancels a queued upload.</param>
        ''' <returns>A task that completes when the upload finishes.</returns>
        Public Overridable Async Function UploadFileAsync(remoteFilePath As String, binaryData As Byte(), createDirectoryStructureIfMissing As Boolean, Optional cancellationToken As CancellationToken = Nothing) As Task
            If Not createDirectoryStructureIfMissing Then
                Await Me.UploadFileAsync(remoteFilePath, binaryData, cancellationToken).ConfigureAwait(False)
                Return
            End If
            If Not Me.SupportsAsynchronousIo Then
                Await Me.RunSynchronousFallbackAsync(Sub() Me.UploadFile(remoteFilePath, binaryData, createDirectoryStructureIfMissing), cancellationToken).ConfigureAwait(False)
                Return
            End If
            Await Me.EnsureUploadParentAsync(remoteFilePath, True, cancellationToken).ConfigureAwait(False)
            Await Me.UploadFileAsync(remoteFilePath, binaryData, cancellationToken).ConfigureAwait(False)
        End Function

        ''' <summary>
        ''' Upload a local file to the remote DMS, if applicable: create a new version to an existing file
        ''' </summary>
        ''' <param name="remoteFilePath">The remote file path.</param>
        ''' <param name="binaryData">The source bytes or factory for a readable stream; the provider owns a stream returned by the factory.</param>
        Public MustOverride Sub UploadFile(remoteFilePath As String, binaryData As Func(Of System.IO.Stream))

        ''' <summary>
        ''' Upload a local file to the remote DMS, if applicable: create a new version to an existing file
        ''' </summary>
        ''' <param name="remoteFilePath">The remote file path.</param>
        ''' <param name="binaryData">The source bytes or factory for a readable stream; the provider owns a stream returned by the factory.</param>
        Public Overridable Sub UploadFile(remoteFilePath As String, binaryData As Byte())
            Me.UploadFile(remoteFilePath, Function()
                                              Return New System.IO.MemoryStream(binaryData)
                                          End Function)
        End Sub

        ''' <summary>
        ''' Upload a local file to the remote DMS, if applicable: create a new version to an existing file
        ''' </summary>
        ''' <param name="remoteFilePath">The remote file path.</param>
        ''' <param name="localFilePath">The path of the local source or destination file.</param>
        ''' <exception cref="System.IO.IOException">The local source cannot be read. A path conflict can include a directory occupying the file path; the exact exception mapping depends on the operating system, runtime, and file system.</exception>
        ''' <exception cref="UnauthorizedAccessException">Access to the local path is denied, or the platform reports a directory at the file path as access denied.</exception>
        ''' <exception cref="System.IO.DirectoryNotFoundException">A required local parent directory does not exist.</exception>
        ''' <exception cref="System.IO.PathTooLongException">The local path exceeds the limits of the operating system or runtime.</exception>
        ''' <exception cref="ArgumentException">The local path is empty or invalid for the runtime.</exception>
        ''' <exception cref="NotSupportedException">The runtime does not support the local path format.</exception>
        ''' <exception cref="System.IO.FileNotFoundException">The local source file does not exist.</exception>
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
        ''' <param name="remoteFilePath">The remote file path.</param>
        ''' <param name="binaryData">The source bytes or factory for a readable stream; the provider owns a stream returned by the factory.</param>
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
        ''' <param name="remoteFilePath">The remote file path.</param>
        ''' <param name="binaryData">The source bytes or factory for a readable stream; the provider owns a stream returned by the factory.</param>
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
        ''' <param name="remoteFilePath">The remote file path.</param>
        ''' <param name="localFilePath">The path of the local source or destination file.</param>
        ''' <param name="lastModificationDateOnLocalTime">The local-time modification timestamp to apply to the downloaded file, or Nothing to leave it unchanged.</param>
        ''' <exception cref="System.IO.IOException">The local destination, staging file, replacement, or timestamp cannot be written. A path conflict can include a directory occupying the file path; the exact exception mapping depends on the operating system, runtime, and file system.</exception>
        ''' <exception cref="UnauthorizedAccessException">Access to the local path is denied, or the platform reports a directory at the file path as access denied.</exception>
        ''' <exception cref="System.IO.DirectoryNotFoundException">A required local parent directory does not exist.</exception>
        ''' <exception cref="System.IO.PathTooLongException">The local path exceeds the limits of the operating system or runtime.</exception>
        ''' <exception cref="ArgumentException">The local path is empty or invalid for the runtime.</exception>
        ''' <exception cref="NotSupportedException">The runtime does not support the local path format.</exception>
        Public MustOverride Sub DownloadFile(remoteFilePath As String, localFilePath As String, lastModificationDateOnLocalTime As DateTime?)

        ''' <summary>Downloads a remote file without blocking the calling thread.</summary>
        ''' <param name="remoteFilePath">The remote source path.</param>
        ''' <param name="localFilePath">The local destination path.</param>
        ''' <param name="lastModificationDateOnLocalTime">The optional local timestamp to apply.</param>
        ''' <param name="cancellationToken">Cancels the download and any wait for service capacity.</param>
        ''' <returns>A task that completes when the download finishes.</returns>
        ''' <remarks>Providers without native asynchronous I/O serialize this operation across provider instances for the same backend. Cancellation stops a queued download, but cannot interrupt an active synchronous download.</remarks>
        ''' <exception cref="System.IO.IOException">The local destination, staging file, replacement, or timestamp cannot be written. A path conflict can include a directory occupying the file path; the exact exception mapping depends on the operating system, runtime, and file system.</exception>
        ''' <exception cref="UnauthorizedAccessException">Access to the local path is denied, or the platform reports a directory at the file path as access denied.</exception>
        ''' <exception cref="System.IO.DirectoryNotFoundException">A required local parent directory does not exist.</exception>
        ''' <exception cref="System.IO.PathTooLongException">The local path exceeds the limits of the operating system or runtime.</exception>
        ''' <exception cref="ArgumentException">The local path is empty or invalid for the runtime.</exception>
        ''' <exception cref="NotSupportedException">The runtime does not support the local path format.</exception>
        Public Overridable Function DownloadFileAsync(remoteFilePath As String, localFilePath As String, lastModificationDateOnLocalTime As DateTime?, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.DownloadFile(remoteFilePath, localFilePath, lastModificationDateOnLocalTime), cancellationToken)
        End Function

        ''' <summary>Downloads a selected remote file asynchronously while preserving its resource identity.</summary>
        ''' <param name="remoteFile">The selected remote file.</param>
        ''' <param name="localFilePath">The local destination path.</param>
        ''' <param name="cancellationToken">Cancels a queued or native download.</param>
        ''' <returns>A task that completes when the download finishes.</returns>
        ''' <exception cref="System.IO.IOException">The local destination, staging file, replacement, or timestamp cannot be written. A path conflict can include a directory occupying the file path; the exact exception mapping depends on the operating system, runtime, and file system.</exception>
        ''' <exception cref="UnauthorizedAccessException">Access to the local path is denied, or the platform reports a directory at the file path as access denied.</exception>
        ''' <exception cref="System.IO.DirectoryNotFoundException">A required local parent directory does not exist.</exception>
        ''' <exception cref="System.IO.PathTooLongException">The local path exceeds the limits of the operating system or runtime.</exception>
        ''' <exception cref="ArgumentException">The local path is empty or invalid for the runtime.</exception>
        ''' <exception cref="NotSupportedException">The runtime does not support the local path format.</exception>
        Public Overridable Async Function DownloadFileAsync(remoteFile As DmsResourceItem, localFilePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            If remoteFile Is Nothing Then Throw New ArgumentNullException(NameOf(remoteFile))
            If remoteFile.ItemType <> DmsResourceItem.ItemTypes.File Then Throw New ArgumentException(ProviderStrings.GetText("TheRemoteResourceMustBeAFile"), NameOf(remoteFile))
            If Not Me.SupportsAsynchronousIo OrElse Me.SupportsNonUniqueRemoteItems Then
                Await Me.RunSynchronousFallbackAsync(Sub() Me.DownloadFile(remoteFile, localFilePath), cancellationToken).ConfigureAwait(False)
            Else
                Await Me.DownloadFileAsync(remoteFile.FullName, localFilePath, remoteFile.LastModificationOnLocalTime, cancellationToken).ConfigureAwait(False)
            End If
        End Function

        ''' <summary>
        ''' Downloads a remote DMS file identified by its resource metadata.
        ''' </summary>
        ''' <param name="remoteFile">The remote file to download.</param>
        ''' <param name="localFilePath">The local destination path.</param>
        ''' <exception cref="System.IO.IOException">The local destination, staging file, replacement, or timestamp cannot be written. A path conflict can include a directory occupying the file path; the exact exception mapping depends on the operating system, runtime, and file system.</exception>
        ''' <exception cref="UnauthorizedAccessException">Access to the local path is denied, or the platform reports a directory at the file path as access denied.</exception>
        ''' <exception cref="System.IO.DirectoryNotFoundException">A required local parent directory does not exist.</exception>
        ''' <exception cref="System.IO.PathTooLongException">The local path exceeds the limits of the operating system or runtime.</exception>
        ''' <exception cref="ArgumentException">The local path is empty or invalid for the runtime.</exception>
        ''' <exception cref="NotSupportedException">The runtime does not support the local path format.</exception>
        Public Overridable Sub DownloadFile(remoteFile As DmsResourceItem, localFilePath As String)
            If remoteFile Is Nothing Then Throw New ArgumentNullException(NameOf(remoteFile))
            If remoteFile.ItemType <> DmsResourceItem.ItemTypes.File Then Throw New ArgumentException(ProviderStrings.GetText("TheRemoteResourceMustBeAFile"), NameOf(remoteFile))
            Me.DownloadFile(remoteFile.FullName, localFilePath, remoteFile.LastModificationOnLocalTime)
        End Sub


        ''' <summary>
        ''' Copy a remote DMS item (overwriting forbidden, destination directory must exist)
        ''' </summary>
        ''' <param name="remoteSourcePath">The remote source path.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
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
        ''' <param name="remoteSourcePath">The remote source path.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Public Async Function CopyAsync(remoteSourcePath As String, remoteDestinationPath As String) As Task
            Await Me.CopyAsync(remoteSourcePath, remoteDestinationPath, False, False)
        End Function

        ''' <summary>Copies a remote item without overwriting an existing destination.</summary>
        ''' <param name="remoteSourcePath">The remote source path.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        ''' <param name="cancellationToken">Cancels queued and native active requests; active synchronous fallback calls cannot be interrupted.</param>
        ''' <returns>A task that completes after the copy.</returns>
        Public Function CopyAsync(remoteSourcePath As String, remoteDestinationPath As String, cancellationToken As CancellationToken) As Task
            Return Me.CopyAsync(remoteSourcePath, remoteDestinationPath, False, False, cancellationToken)
        End Function

        ''' <summary>
        ''' Copies a remote DMS item asynchronously while preserving its provider-specific identity.
        ''' </summary>
        ''' <param name="remoteSource">The remote source item.</param>
        ''' <param name="remoteDestinationPath">The absolute destination path.</param>
        Public Async Function CopyAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String) As Task
            Await Me.CopyAsync(remoteSource, remoteDestinationPath, False, False)
        End Function

        ''' <summary>Copies a selected remote item without overwriting an existing destination.</summary>
        ''' <param name="remoteSource">The selected source item.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        ''' <param name="cancellationToken">Cancels queued and native active requests; active synchronous fallback calls cannot be interrupted.</param>
        ''' <returns>A task that completes after the copy.</returns>
        Public Function CopyAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String, cancellationToken As CancellationToken) As Task
            Return Me.CopyAsync(remoteSource, remoteDestinationPath, False, False, cancellationToken)
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

        ''' <summary>Copies a remote item without blocking the calling thread.</summary>
        ''' <param name="remoteSourcePath">The remote source path.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        ''' <param name="allowOverwrite">Whether files may be replaced or directories merged.</param>
        ''' <param name="allowCreationOfRemoteDirectory">Whether a missing destination parent may be created.</param>
        ''' <param name="cancellationToken">Cancels queued and native active requests; active synchronous fallback calls cannot be interrupted.</param>
        ''' <returns>A task that completes after the copy and cache update.</returns>
        Public Async Function CopyAsync(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean, cancellationToken As CancellationToken) As Task
            If Not Me.SupportsAsynchronousCopyMove Then
                Await Me.RunSynchronousFallbackAsync(Sub() Me.Copy(remoteSourcePath, remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory), cancellationToken).ConfigureAwait(False)
                Return
            End If
            Dim previous As CancellationToken = AmbientCancellation.Value
            AmbientCancellation.Value = cancellationToken
            Try
                cancellationToken.ThrowIfCancellationRequested()
                Dim source As DmsResourceItem = Await Me.ResolveUniqueSourceItemAsync(remoteSourcePath).ConfigureAwait(False)
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
        ''' <param name="cancellationToken">Cancels queued and native active requests; active synchronous fallback calls cannot be interrupted.</param>
        ''' <returns>A task that completes after the copy and cache update.</returns>
        Public Async Function CopyAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean, cancellationToken As CancellationToken) As Task
            If Not Me.SupportsAsynchronousCopyMove Then
                Await Me.RunSynchronousFallbackAsync(Sub() Me.Copy(remoteSource, remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory), cancellationToken).ConfigureAwait(False)
                Return
            End If
            Dim previous As CancellationToken = AmbientCancellation.Value
            AmbientCancellation.Value = cancellationToken
            Try
                cancellationToken.ThrowIfCancellationRequested()
                Await Me.CopyMoveArgumentsCheckAsync(remoteSource, remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory).ConfigureAwait(False)
                Await Me.CopyItemAsync(remoteSource, remoteDestinationPath, allowOverwrite).ConfigureAwait(False)
                Await Me.ResetDestinationCachesAsync(remoteSource, remoteDestinationPath, cancellationToken).ConfigureAwait(False)
            Finally
                AmbientCancellation.Value = previous
            End Try
        End Function

        Private Async Function CopyMoveArgumentsCheckAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean) As Task
            If remoteSource Is Nothing Then Throw New ArgumentNullException(NameOf(remoteSource))
            If String.IsNullOrEmpty(remoteSource.FullName) Then Throw New ArgumentException(ProviderStrings.GetText("TheSourceItemMustProvideItsFullRemote"), NameOf(remoteSource))
            If remoteDestinationPath Is Nothing Then Throw New ArgumentNullException(NameOf(remoteDestinationPath))
            If remoteDestinationPath.EndsWith(Me.DirectorySeparator) Then Throw New ArgumentException(ProviderStrings.Format("MustBeAPathWithoutTrailingDirectorySeparator", remoteDestinationPath), NameOf(remoteDestinationPath))
            If remoteSource.ItemType = DmsResourceItem.ItemTypes.Root Then Throw New NotSupportedException(ProviderStrings.GetText("RootDirectoryCanTBeTheSourceOf"))
            If String.Equals(remoteSource.FullName.TrimEnd(Me.DirectorySeparator), remoteDestinationPath.TrimEnd(Me.DirectorySeparator), StringComparison.Ordinal) Then Throw New ArgumentException(ProviderStrings.GetText("SourceAndDestinationPathsMustDiffer"), NameOf(remoteDestinationPath))
            If remoteSource.ItemType = DmsResourceItem.ItemTypes.Folder OrElse remoteSource.ItemType = DmsResourceItem.ItemTypes.Collection Then
                Dim prefix As String = remoteSource.FullName.TrimEnd(Me.DirectorySeparator) & Me.DirectorySeparator
                If remoteDestinationPath.StartsWith(prefix, StringComparison.Ordinal) Then Throw New ArgumentException(ProviderStrings.GetText("ADirectoryCanTBeCopiedOrMoved"), NameOf(remoteDestinationPath))
            End If

            Await Me.ValidateCopyMoveIdentityAsync(remoteSource, remoteDestinationPath, Me.CurrentAsyncCancellationToken).ConfigureAwait(False)
            Dim destination = Await Me.ListRemoteItemAsync(remoteDestinationPath, Me.CurrentAsyncCancellationToken).ConfigureAwait(False)
            If destination IsNot Nothing Then
                If destination.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(remoteDestinationPath)
                If destination.ItemType = DmsResourceItem.ItemTypes.Root Then Throw New NotSupportedException(ProviderStrings.GetText("RootDirectoryCanTBeTheTargetOf"))
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
                    Throw New NotSupportedException(ProviderStrings.Format("RemoteRessourceWithUnsupportedType", parent))
                End If
            ElseIf remoteSource.ItemType = DmsResourceItem.ItemTypes.File AndAlso Not Me.SupportsFilesInRootFolder Then
                Throw New NotSupportedException(ProviderStrings.GetText("FilesInRootFolderNotSupportedByDMS"))
            End If
            Select Case remoteSource.ItemType
                Case DmsResourceItem.ItemTypes.File, DmsResourceItem.ItemTypes.Folder, DmsResourceItem.ItemTypes.Collection
                Case Else
                    Throw New ArgumentOutOfRangeException(NameOf(remoteSource), ProviderStrings.GetText("UnsupportedSourceItemType"))
            End Select
        End Function

        ''' <summary>Indicates whether copy and move compose asynchronous lookup, mutation, and cache operations.</summary>
        ''' <returns>True when copy and move can use asynchronous provider hooks; otherwise the complete operation uses the serialized synchronous fallback.</returns>
        ''' <remarks>Providers may override this independently while other asynchronous operations still require a fallback.</remarks>
        Protected Overridable ReadOnly Property SupportsAsynchronousCopyMove As Boolean
            Get
                Return Me.SupportsAsynchronousIo
            End Get
        End Property

        ''' <summary>Validates provider identities before optional destination-parent creation.</summary>
        ''' <param name="remoteSource">The selected source item.</param>
        ''' <param name="remoteDestinationPath">The destination path.</param>
        ''' <param name="cancellationToken">Cancels native identity validation.</param>
        ''' <returns>A task that completes after provider identity checks.</returns>
        ''' <remarks>The default implementation performs no additional provider checks.</remarks>
        Protected Overridable Function ValidateCopyMoveIdentityAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String, cancellationToken As CancellationToken) As Task
            Return Task.CompletedTask
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
            If String.IsNullOrEmpty(remoteSource.FullName) Then Throw New ArgumentException(ProviderStrings.GetText("TheSourceItemMustProvideItsFullRemote"), NameOf(remoteSource))
            If remoteDestinationPath = Nothing Then Throw New ArgumentNullException(NameOf(remoteDestinationPath))
            If remoteDestinationPath.EndsWith(Me.DirectorySeparator) Then Throw New ArgumentException(ProviderStrings.Format("MustBeAPathWithoutTrailingDirectorySeparator", remoteDestinationPath), NameOf(remoteDestinationPath))
            If remoteSource.ItemType = DmsResourceItem.ItemTypes.Root Then Throw New NotSupportedException(ProviderStrings.GetText("RootDirectoryCanTBeTheSourceOf"))
            If String.Equals(remoteSource.FullName.TrimEnd(Me.DirectorySeparator), remoteDestinationPath.TrimEnd(Me.DirectorySeparator), StringComparison.Ordinal) Then
                Throw New ArgumentException(ProviderStrings.GetText("SourceAndDestinationPathsMustDiffer"), NameOf(remoteDestinationPath))
            End If

            If remoteSource.ItemType = DmsResourceItem.ItemTypes.Folder OrElse remoteSource.ItemType = DmsResourceItem.ItemTypes.Collection Then
                Dim SourcePrefix As String = remoteSource.FullName.TrimEnd(Me.DirectorySeparator) & Me.DirectorySeparator
                If remoteDestinationPath.StartsWith(SourcePrefix, StringComparison.Ordinal) Then Throw New ArgumentException(ProviderStrings.GetText("ADirectoryCanTBeCopiedOrMoved"), NameOf(remoteDestinationPath))
            End If

            Dim DestinationItem As DmsResourceItem = Me.ListRemoteItem(remoteDestinationPath)
            If DestinationItem IsNot Nothing AndAlso DestinationItem.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(remoteDestinationPath)
            If DestinationItem IsNot Nothing Then
                If DestinationItem.ItemType = DmsResourceItem.ItemTypes.Root Then Throw New NotSupportedException(ProviderStrings.GetText("RootDirectoryCanTBeTheTargetOf"))
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
                        Throw New NotSupportedException(ProviderStrings.Format("RemoteRessourceWithUnsupportedType", ParentDir))
                End Select
            Else 'If ParentDir = "" -> root dir
                If remoteSource.ItemType = DmsResourceItem.ItemTypes.File AndAlso Me.SupportsFilesInRootFolder = False Then
                    Throw New NotSupportedException(ProviderStrings.GetText("FilesInRootFolderNotSupportedByDMS"))
                End If
            End If
            Select Case remoteSource.ItemType
                Case DmsResourceItem.ItemTypes.File, DmsResourceItem.ItemTypes.Folder, DmsResourceItem.ItemTypes.Collection
                    'Supported source object.
                Case Else
                    Throw New ArgumentOutOfRangeException(NameOf(remoteSource), ProviderStrings.GetText("UnsupportedSourceItemType"))
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

        Private Async Function ResetDestinationCachesAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String, cancellationToken As CancellationToken) As Task
            Dim parent = Me.ParentDirectoryPath(remoteDestinationPath)
            If remoteSource.ItemType = DmsResourceItem.ItemTypes.File Then
                Await Me.ResetCachesForRemoteItemsAsync(parent, SearchItemType.Files, cancellationToken).ConfigureAwait(False)
            Else
                Await Me.ResetCachesForRemoteItemsAsync(parent, SearchItemType.Folders, cancellationToken).ConfigureAwait(False)
                Await Me.ResetCachesForRemoteItemsAsync(parent, SearchItemType.Collections, cancellationToken).ConfigureAwait(False)
            End If
        End Function

        Private Async Function ResetMoveCachesAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String, cancellationToken As CancellationToken) As Task
            Await Me.ResetDestinationCachesAsync(remoteSource, remoteDestinationPath, cancellationToken).ConfigureAwait(False)
            Dim parent = Me.ParentDirectoryPath(remoteSource.FullName)
            If remoteSource.ItemType = DmsResourceItem.ItemTypes.File Then
                Await Me.ResetCachesForRemoteItemsAsync(parent, SearchItemType.Files, cancellationToken).ConfigureAwait(False)
            Else
                Await Me.ResetCachesForRemoteItemsAsync(parent, SearchItemType.Folders, cancellationToken).ConfigureAwait(False)
                Await Me.ResetCachesForRemoteItemsAsync(parent, SearchItemType.Collections, cancellationToken).ConfigureAwait(False)
            End If
        End Function

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
                    Throw New NotSupportedException(ProviderStrings.Format("UnsupportedSourceItemType2", remoteSource.ItemType.ToString()))
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
                            Await Me.RunSynchronousFallbackAsync(Sub() Me.MergeDirectoryContents(remoteSource, remoteDestinationPath, False), Me.CurrentAsyncCancellationToken).ConfigureAwait(False)
                        End If
                    Else
                        Await Me.CopyDirectoryItemAsync(remoteSource.FullName, remoteDestinationPath)
                    End If
                Case Else
                    Throw New NotSupportedException(ProviderStrings.Format("UnsupportedSourceItemType2", remoteSource.ItemType.ToString()))
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
        ''' <param name="remoteSourcePath">The remote source path.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        ''' <param name="allowOverwrite">True to overwrite, False to throw exception if target already exists, null/Nothing to use provider specific default</param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Protected MustOverride Sub CopyFileItem(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?)

        ''' <summary>
        ''' Copy a remote DMS item
        ''' </summary>
        ''' <param name="remoteSourcePath">The remote source path.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        ''' <param name="allowOverwrite">True to overwrite, False to throw exception if target already exists, null/Nothing to use provider specific default</param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Protected MustOverride Async Function CopyFileItemAsync(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?) As Task

        ''' <summary>
        ''' Copy a remote DMS item
        ''' </summary>
        ''' <param name="remoteSourcePath">The remote source path.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Protected MustOverride Sub CopyDirectoryItem(remoteSourcePath As String, remoteDestinationPath As String)

        ''' <summary>
        ''' Copy a remote DMS item
        ''' </summary>
        ''' <param name="remoteSourcePath">The remote source path.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Protected MustOverride Async Function CopyDirectoryItemAsync(remoteSourcePath As String, remoteDestinationPath As String) As Task

        ''' <summary>
        ''' Move a remote DMS item (overwriting forbidden, destination directory must exist)
        ''' </summary>
        ''' <param name="remoteSourcePath">The remote source path.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Public Sub Move(remoteSourcePath As String, remoteDestinationPath As String)
            Me.Move(remoteSourcePath, remoteDestinationPath, False, False)
        End Sub

        ''' <summary>
        ''' Move a remote DMS item (overwriting forbidden, destination directory must exist)
        ''' </summary>
        ''' <param name="remoteSource">The selected source resource snapshot.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Public Sub Move(remoteSource As DmsResourceItem, remoteDestinationPath As String)
            Me.Move(remoteSource, remoteDestinationPath, False, False)
        End Sub

        ''' <summary>
        ''' Move a remote DMS item
        ''' </summary>
        ''' <param name="remoteSourcePath">The remote source path.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        ''' <exception cref="FileAlreadyExistsException" />
        ''' <exception cref="DirectoryAlreadyExistsException" />
        Public Sub Move(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean)
            Me.Move(Me.ResolveUniqueSourceItem(remoteSourcePath), remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory)
        End Sub

        ''' <summary>
        ''' Move a remote DMS item
        ''' </summary>
        ''' <param name="remoteSource">The selected source resource snapshot.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
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
        ''' <param name="cancellationToken">Cancels queued and native active requests; active synchronous fallback calls cannot be interrupted.</param>
        ''' <returns>A task that completes after the move.</returns>
        Public Function MoveAsync(remoteSourcePath As String, remoteDestinationPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.MoveAsync(remoteSourcePath, remoteDestinationPath, False, False, cancellationToken)
        End Function

        ''' <summary>Moves a remote item asynchronously while retaining its provider identity.</summary>
        ''' <param name="remoteSource">The source item.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        ''' <param name="cancellationToken">Cancels queued and native active requests; active synchronous fallback calls cannot be interrupted.</param>
        ''' <returns>A task that completes after the move.</returns>
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
        ''' <remarks>Providers without native asynchronous support for this operation use a serialized worker fallback.</remarks>
        Public Async Function MoveAsync(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean, Optional cancellationToken As CancellationToken = Nothing) As Task
            If Not Me.SupportsAsynchronousCopyMove Then
                Await Me.RunSynchronousFallbackAsync(Sub() Me.Move(remoteSourcePath, remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory), cancellationToken).ConfigureAwait(False)
                Return
            End If
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
        ''' <remarks>Providers without native asynchronous support for this operation use a serialized worker fallback.</remarks>
        Public Async Function MoveAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?, allowCreationOfRemoteDirectory As Boolean, Optional cancellationToken As CancellationToken = Nothing) As Task
            If Not Me.SupportsAsynchronousCopyMove Then
                Await Me.RunSynchronousFallbackAsync(Sub() Me.Move(remoteSource, remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory), cancellationToken).ConfigureAwait(False)
                Return
            End If
            Dim previous As CancellationToken = AmbientCancellation.Value
            AmbientCancellation.Value = cancellationToken
            Try
                cancellationToken.ThrowIfCancellationRequested()
                Await Me.CopyMoveArgumentsCheckAsync(remoteSource, remoteDestinationPath, allowOverwrite, allowCreationOfRemoteDirectory).ConfigureAwait(False)
                Await Me.MoveItemAsync(remoteSource, remoteDestinationPath, allowOverwrite, cancellationToken).ConfigureAwait(False)
                Await Me.ResetMoveCachesAsync(remoteSource, remoteDestinationPath, cancellationToken).ConfigureAwait(False)
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
                    Throw New NotSupportedException(ProviderStrings.Format("UnsupportedSourceItemType2", remoteSource.ItemType.ToString()))
            End Select
        End Function

        ''' <summary>Moves a file asynchronously in a provider implementation.</summary>
        ''' <param name="remoteSourcePath">The source path.</param>
        ''' <param name="remoteDestinationPath">The destination path.</param>
        ''' <param name="allowOverwrite">Whether an existing file may be replaced.</param>
        ''' <param name="cancellationToken">Cancels the request.</param>
        ''' <returns>A task that completes after the move.</returns>
        Protected Overridable Function MoveFileItemAsync(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?, cancellationToken As CancellationToken) As Task
            Throw New NotSupportedException(ProviderStrings.GetText("ThisProviderDoesNotSupportAsynchronousMoveOperations"))
        End Function

        ''' <summary>Moves a directory asynchronously in a provider implementation.</summary>
        ''' <param name="remoteSourcePath">The source path.</param>
        ''' <param name="remoteDestinationPath">The destination path.</param>
        ''' <param name="cancellationToken">Cancels the request.</param>
        ''' <returns>A task that completes after the move.</returns>
        Protected Overridable Function MoveDirectoryItemAsync(remoteSourcePath As String, remoteDestinationPath As String, cancellationToken As CancellationToken) As Task
            Throw New NotSupportedException(ProviderStrings.GetText("ThisProviderDoesNotSupportAsynchronousMoveOperations"))
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
                    Throw New NotSupportedException(ProviderStrings.Format("UnsupportedSourceItemType2", remoteSource.ItemType.ToString()))
            End Select
        End Sub

        ''' <summary>
        ''' Move a remote DMS item
        ''' </summary>
        ''' <param name="remoteSourcePath">The remote source path.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        ''' <param name="allowOverwrite">Whether an existing destination file may be replaced.</param>
        Protected MustOverride Sub MoveFileItem(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?)

        ''' <summary>
        ''' Move a remote DMS item
        ''' </summary>
        ''' <param name="remoteSourcePath">The remote source path.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        Protected MustOverride Sub MoveDirectoryItem(remoteSourcePath As String, remoteDestinationPath As String)

        ''' <summary>
        ''' Delete a remote item (folder, collection or file)
        ''' </summary>
        ''' <param name="remotePath">The remote resource path.</param>
        Public MustOverride Sub DeleteRemoteItem(remotePath As String)

        ''' <summary>Deletes a remote item without blocking the calling thread.</summary>
        ''' <param name="remotePath">The remote path to delete.</param>
        ''' <param name="cancellationToken">Cancels the request and any wait for service capacity.</param>
        ''' <returns>A task that completes when deletion finishes.</returns>
        ''' <remarks>Providers without native asynchronous I/O serialize this operation across provider instances for the same backend. Cancellation stops a queued delete, but cannot interrupt an active synchronous delete.</remarks>
        Public Overridable Function DeleteRemoteItemAsync(remotePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.DeleteRemoteItem(remotePath), cancellationToken)
        End Function

        ''' <summary>Deletes a selected remote item asynchronously while preserving its resource identity.</summary>
        ''' <param name="remoteItem">The selected remote item.</param>
        ''' <param name="cancellationToken">Cancels a queued or native deletion.</param>
        ''' <returns>A task that completes when the item is deleted.</returns>
        Public Overridable Async Function DeleteRemoteItemAsync(remoteItem As DmsResourceItem, Optional cancellationToken As CancellationToken = Nothing) As Task
            If remoteItem Is Nothing Then Throw New ArgumentNullException(NameOf(remoteItem))
            If Not Me.SupportsAsynchronousIo OrElse Me.SupportsNonUniqueRemoteItems Then
                Await Me.RunSynchronousFallbackAsync(Sub() Me.DeleteRemoteItem(remoteItem), cancellationToken).ConfigureAwait(False)
            Else
                Await Me.DeleteRemoteItemAsync(remoteItem.FullName, cancellationToken).ConfigureAwait(False)
            End If
        End Function

        ''' <summary>Deletes a remote item when its type matches the expected type asynchronously.</summary>
        ''' <param name="remotePath">The remote item path.</param>
        ''' <param name="expectedItemType">The required item type.</param>
        ''' <param name="cancellationToken">Cancels a queued deletion.</param>
        ''' <returns>A task that completes after deletion.</returns>
        Public Overridable Function DeleteRemoteItemAsync(remotePath As String, expectedItemType As DmsResourceItem.ItemTypes, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.DeleteRemoteItem(remotePath, expectedItemType), cancellationToken)
        End Function

        ''' <summary>Deletes a remote item when its type matches either expected type asynchronously.</summary>
        ''' <param name="remotePath">The remote item path.</param>
        ''' <param name="expectedItemType">The first accepted item type.</param>
        ''' <param name="alternativeExpectedItemType">The second accepted item type.</param>
        ''' <param name="cancellationToken">Cancels a queued deletion.</param>
        ''' <returns>A task that completes after deletion.</returns>
        Public Overridable Function DeleteRemoteItemAsync(remotePath As String, expectedItemType As DmsResourceItem.ItemTypes, alternativeExpectedItemType As DmsResourceItem.ItemTypes, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.DeleteRemoteItem(remotePath, expectedItemType, alternativeExpectedItemType), cancellationToken)
        End Function

        ''' <summary>Deletes a selected remote item when its type matches the expected type asynchronously.</summary>
        ''' <param name="remoteItem">The selected remote item.</param>
        ''' <param name="expectedItemType">The required item type.</param>
        ''' <param name="cancellationToken">Cancels a queued deletion.</param>
        ''' <returns>A task that completes after deletion.</returns>
        Public Overridable Function DeleteRemoteItemAsync(remoteItem As DmsResourceItem, expectedItemType As DmsResourceItem.ItemTypes, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.DeleteRemoteItem(remoteItem, expectedItemType), cancellationToken)
        End Function

        ''' <summary>Deletes a selected remote item when its type matches either expected type asynchronously.</summary>
        ''' <param name="remoteItem">The selected remote item.</param>
        ''' <param name="expectedItemType">The first accepted item type.</param>
        ''' <param name="alternativeExpectedItemType">The second accepted item type.</param>
        ''' <param name="cancellationToken">Cancels a queued deletion.</param>
        ''' <returns>A task that completes after deletion.</returns>
        Public Overridable Function DeleteRemoteItemAsync(remoteItem As DmsResourceItem, expectedItemType As DmsResourceItem.ItemTypes, alternativeExpectedItemType As DmsResourceItem.ItemTypes, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.DeleteRemoteItem(remoteItem, expectedItemType, alternativeExpectedItemType), cancellationToken)
        End Function

        ''' <summary>
        ''' Delete a remote item if its item type matches with the expected item type
        ''' </summary>
        ''' <param name="remotePath">The remote resource path.</param>
        ''' <param name="expectedItemType">The expected resource kind used to select the not-found exception.</param>
        Public Overridable Sub DeleteRemoteItem(remotePath As String, expectedItemType As DmsResourceItem.ItemTypes)
            Dim Item As DmsResourceItem = Me.ListRemoteItem(remotePath)
            Me.DeleteRemoteItem(Item, expectedItemType)
        End Sub

        ''' <summary>
        ''' Delete a remote item if its item type matches with the expected item type
        ''' </summary>
        ''' <param name="remotePath">The remote resource path.</param>
        ''' <param name="expectedItemType">The expected resource kind used to select the not-found exception.</param>
        ''' <param name="alternativeExpectedItemType">The alternative accepted resource kind.</param>
        Public Overridable Sub DeleteRemoteItem(remotePath As String, expectedItemType As DmsResourceItem.ItemTypes, alternativeExpectedItemType As DmsResourceItem.ItemTypes)
            Dim Item As DmsResourceItem = Me.ListRemoteItem(remotePath)
            Me.DeleteRemoteItem(Item, expectedItemType, alternativeExpectedItemType)
        End Sub

        ''' <summary>
        ''' Delete a remote item (folder, collection or file)
        ''' </summary>
        ''' <param name="remoteItem">The selected remote resource snapshot.</param>
        Public MustOverride Sub DeleteRemoteItem(remoteItem As DmsResourceItem)

        ''' <summary>
        ''' Delete a remote item if its item type matches with the expected item type
        ''' </summary>
        ''' <param name="remoteItem">The selected remote resource snapshot.</param>
        ''' <param name="expectedItemType">The expected resource kind used to select the not-found exception.</param>
        Public Overridable Sub DeleteRemoteItem(remoteItem As DmsResourceItem, expectedItemType As DmsResourceItem.ItemTypes)
            If expectedItemType = Nothing Then Throw New ArgumentNullException(NameOf(expectedItemType))
            If remoteItem.ItemType <> expectedItemType Then
                Throw New ArgumentException(ProviderStrings.Format("ItemTypeExpectedButWas", expectedItemType.ToString, remoteItem.ItemType.ToString), NameOf(remoteItem))
            Else
                Me.DeleteRemoteItem(remoteItem)
            End If
        End Sub

        ''' <summary>
        ''' Delete a remote item if its item type matches with the expected item type
        ''' </summary>
        ''' <param name="remoteItem">The selected remote resource snapshot.</param>
        ''' <param name="expectedItemType">The expected resource kind used to select the not-found exception.</param>
        ''' <param name="alternativeExpectedItemType">The alternative accepted resource kind.</param>
        Public Overridable Sub DeleteRemoteItem(remoteItem As DmsResourceItem, expectedItemType As DmsResourceItem.ItemTypes, alternativeExpectedItemType As DmsResourceItem.ItemTypes)
            If expectedItemType = Nothing Then Throw New ArgumentNullException(NameOf(expectedItemType))
            If alternativeExpectedItemType = Nothing Then Throw New ArgumentNullException(NameOf(alternativeExpectedItemType))
            If remoteItem.ItemType <> expectedItemType AndAlso remoteItem.ItemType <> alternativeExpectedItemType Then
                Throw New ArgumentException(ProviderStrings.Format("ItemTypeOrExpectedButWas", expectedItemType.ToString, alternativeExpectedItemType.ToString, remoteItem.ItemType.ToString), NameOf(remoteItem))
            Else
                Me.DeleteRemoteItem(remoteItem)
            End If
        End Sub

        ''' <summary>
        ''' Create a new folder on remote DMS
        ''' </summary>
        ''' <param name="remoteDirectoryPath">The remote directory path.</param>
        Public MustOverride Sub CreateFolder(remoteDirectoryPath As String)

        ''' <summary>Creates a remote folder without blocking the calling thread.</summary>
        ''' <param name="remoteDirectoryPath">The path of the new folder.</param>
        ''' <param name="cancellationToken">Cancels the request and any wait for service capacity.</param>
        ''' <returns>A task that completes when the folder is created.</returns>
        ''' <remarks>Providers without native asynchronous I/O serialize this operation across provider instances for the same backend. Cancellation stops a queued creation, but cannot interrupt an active synchronous request.</remarks>
        Public Overridable Function CreateFolderAsync(remoteDirectoryPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.CreateFolder(remoteDirectoryPath), cancellationToken)
        End Function

        ''' <summary>Creates a remote folder and optionally its missing parent folders asynchronously.</summary>
        ''' <param name="remoteDirectoryPath">The remote folder path.</param>
        ''' <param name="createParentFolders">Creates missing parent folders when true.</param>
        ''' <param name="cancellationToken">Cancels a queued creation.</param>
        ''' <returns>A task that completes when the folder is created.</returns>
        Public Overridable Async Function CreateFolderAsync(remoteDirectoryPath As String, createParentFolders As Boolean, Optional cancellationToken As CancellationToken = Nothing) As Task
            If Not Me.SupportsAsynchronousIo Then
                Await Me.RunSynchronousFallbackAsync(Sub() Me.CreateFolder(remoteDirectoryPath, createParentFolders), cancellationToken).ConfigureAwait(False)
                Return
            End If
            If remoteDirectoryPath Is Nothing Then Throw New ArgumentNullException(NameOf(remoteDirectoryPath))
            Dim parentDirectory As String = Me.ParentDirectoryPath(remoteDirectoryPath)
            If parentDirectory IsNot Nothing AndAlso Not Await Me.RemoteItemExistsAsync(parentDirectory, cancellationToken).ConfigureAwait(False) Then
                If Not createParentFolders Then Throw New DirectoryNotFoundException(parentDirectory)
                Await Me.CreateFolderAsync(parentDirectory, True, cancellationToken).ConfigureAwait(False)
            End If
            Await Me.CreateFolderAsync(remoteDirectoryPath, cancellationToken).ConfigureAwait(False)
        End Function

        ''' <summary>Creates a remote directory asynchronously.</summary>
        ''' <param name="remoteDirectoryPath">The remote directory path.</param>
        ''' <param name="cancellationToken">Cancels a queued or native creation.</param>
        ''' <returns>A task that completes when the directory is created.</returns>
        Public Overridable Function CreateDirectoryAsync(remoteDirectoryPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            If Me.SupportsAsynchronousIo Then Return Me.CreateFolderAsync(remoteDirectoryPath, cancellationToken)
            Return Me.RunSynchronousFallbackAsync(Sub() Me.CreateDirectory(remoteDirectoryPath), cancellationToken)
        End Function

        ''' <summary>Creates a remote directory and optionally its missing parents asynchronously.</summary>
        ''' <param name="remoteDirectoryPath">The remote directory path.</param>
        ''' <param name="createParentFolders">Creates missing parent directories when true.</param>
        ''' <param name="cancellationToken">Cancels a queued creation.</param>
        ''' <returns>A task that completes when the directory is created.</returns>
        Public Overridable Function CreateDirectoryAsync(remoteDirectoryPath As String, createParentFolders As Boolean, Optional cancellationToken As CancellationToken = Nothing) As Task
            If Me.SupportsAsynchronousIo Then Return Me.CreateFolderAsync(remoteDirectoryPath, createParentFolders, cancellationToken)
            Return Me.RunSynchronousFallbackAsync(Sub() Me.CreateDirectory(remoteDirectoryPath, createParentFolders), cancellationToken)
        End Function

        ''' <summary>Creates a remote collection asynchronously.</summary>
        ''' <param name="remoteCollectionName">The remote collection name.</param>
        ''' <param name="cancellationToken">Cancels a queued creation.</param>
        ''' <returns>A task that completes when the collection is created.</returns>
        Public Overridable Function CreateCollectionAsync(remoteCollectionName As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.CreateCollection(remoteCollectionName), cancellationToken)
        End Function

        ''' <summary>
        ''' Create a new folder on remote DMS
        ''' </summary>
        ''' <param name="remoteDirectoryPath">The remote directory path.</param>
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
        ''' <param name="remoteDirectoryName">The remote directory name.</param>
        ''' <remarks>The provider decides itself to create either a collection or a folder</remarks>
        Public MustOverride Sub CreateDirectory(remoteDirectoryName As String)

        ''' <summary>
        ''' Create a new collection or folder on remote DMS
        ''' </summary>
        ''' <param name="remoteDirectoryPath">The remote directory path.</param>
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
        ''' <param name="remoteCollectionName">The remote collection name.</param>
        Public MustOverride Sub CreateCollection(remoteCollectionName As String)

        ''' <summary>
        ''' The directory separator used by the DMS provider
        ''' </summary>
        Public MustOverride ReadOnly Property DirectorySeparator As Char

        ''' <summary>
        ''' The name of the root element to request a folder/collection listing at root
        ''' </summary>
        Public MustOverride ReadOnly Property BrowseInRootFolderName() As String

        ''' <summary>
        ''' Login to the remote DMS
        ''' </summary>
        ''' <param name="dmsProfile">The login profile used to authorize the provider.</param>
        Public MustOverride Sub Authorize(dmsProfile As IDmsLoginProfile)

        ''' <summary>Authorizes access to the remote DMS without blocking the calling thread.</summary>
        ''' <param name="dmsProfile">The login profile to authorize.</param>
        ''' <param name="cancellationToken">Cancels a queued authorization.</param>
        ''' <returns>A task that completes after authorization.</returns>
        Public Overridable Function AuthorizeAsync(dmsProfile As IDmsLoginProfile, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.Authorize(dmsProfile), cancellationToken)
        End Function

        ''' <summary>
        ''' Combines folder names to a path
        ''' </summary>
        ''' <param name="basePath">The base path used to resolve or compare the resource.</param>
        ''' <param name="paths">The additional path segments to combine using the provider separator.</param>
        ''' <returns>The combined path using the provider separator.</returns>
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
        ''' <param name="absolutePath">The complete path to make relative.</param>
        ''' <returns>The parent directory path, or Nothing when the input has no parent.</returns>
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
        ''' <param name="absolutePath">The complete path to make relative.</param>
        ''' <returns>The final file or directory name in the path.</returns>
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

        ''' <summary>Checks whether a remote collection exists.</summary>
        ''' <param name="remoteFolderPath">The remote folder or collection path.</param>
        ''' <returns>True when the path identifies a collection; False when the collection or its parent does not exist.</returns>
        ''' <remarks>Authentication, permission, transport, server, and unsupported-operation failures are propagated.</remarks>
        Public Function CollectionExists(remoteFolderPath As String) As Boolean
            Dim ParentPath As String = Me.ParentDirectoryPath(remoteFolderPath)
            Dim ItemName As String = Me.ItemName(remoteFolderPath)
            Try
                Dim AllFoldersInParentFolder As List(Of String) = Me.ListAllCollectionNames(ParentPath)
                Return AllFoldersInParentFolder.Contains(ItemName, StringComparer.Create(System.Globalization.CultureInfo.InvariantCulture, True))
            Catch ex As Data.DirectoryNotFoundException
                Return False
            End Try
        End Function

        ''' <summary>Checks whether a remote folder exists.</summary>
        ''' <param name="remoteFolderPath">The remote folder or collection path.</param>
        ''' <returns>True when the provider lists the path as a folder; False when the folder or its parent does not exist.</returns>
        ''' <remarks>Authentication, permission, transport, server, and unsupported-operation failures are propagated.</remarks>
        Public Function FolderExists(remoteFolderPath As String) As Boolean
            Dim ParentPath As String = Me.ParentDirectoryPath(remoteFolderPath)
            Dim ItemName As String = Me.ItemName(remoteFolderPath)
            Try
                Dim AllFoldersInParentFolder As List(Of String) = Me.ListAllFolderNames(ParentPath)
                Return AllFoldersInParentFolder.Contains(ItemName, StringComparer.Create(System.Globalization.CultureInfo.InvariantCulture, True))
            Catch ex As Data.DirectoryNotFoundException
                Return False
            End Try
        End Function

        ''' <summary>Checks whether a remote collection exists asynchronously.</summary>
        ''' <param name="remoteFolderPath">The collection path.</param>
        ''' <param name="cancellationToken">Cancels a queued or native lookup.</param>
        ''' <returns>True when the collection exists; False when the collection or its parent does not exist.</returns>
        ''' <remarks>Authentication, permission, transport, server, and unsupported-operation failures are propagated.</remarks>
        ''' <exception cref="OperationCanceledException">The lookup is canceled.</exception>
        Public Overridable Async Function CollectionExistsAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of Boolean)
            Dim parentPath = Me.ParentDirectoryPath(remoteFolderPath)
            Dim name = Me.ItemName(remoteFolderPath)
            Try
                Dim names = Await Me.ListAllCollectionNamesAsync(parentPath, cancellationToken).ConfigureAwait(False)
                Return names.Contains(name, StringComparer.Create(System.Globalization.CultureInfo.InvariantCulture, True))
            Catch ex As Data.DirectoryNotFoundException
                cancellationToken.ThrowIfCancellationRequested()
                Return False
            End Try
        End Function

        ''' <summary>Checks whether a remote folder exists asynchronously.</summary>
        ''' <param name="remoteFolderPath">The folder path.</param>
        ''' <param name="cancellationToken">Cancels a queued or native lookup.</param>
        ''' <returns>True when the provider lists the path as a folder; False when the folder or its parent does not exist.</returns>
        ''' <remarks>Authentication, permission, transport, server, and unsupported-operation failures are propagated.</remarks>
        ''' <exception cref="OperationCanceledException">The lookup is canceled.</exception>
        Public Overridable Async Function FolderExistsAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of Boolean)
            Dim parentPath = Me.ParentDirectoryPath(remoteFolderPath)
            Dim name = Me.ItemName(remoteFolderPath)
            Try
                Dim names = Await Me.ListAllFolderNamesAsync(parentPath, cancellationToken).ConfigureAwait(False)
                Return names.Contains(name, StringComparer.Create(System.Globalization.CultureInfo.InvariantCulture, True))
            Catch ex As Data.DirectoryNotFoundException
                cancellationToken.ThrowIfCancellationRequested()
                Return False
            End Try
        End Function

        ''' <summary>
        ''' DMS provider supports collections (concept of collections can be understood as "intelligent folders", see CenterDevice/Scopevisio Teamwork)
        ''' </summary>
        Public MustOverride ReadOnly Property SupportsCollections As Boolean

        ''' <summary>
        ''' DMS provider supports sharing API
        ''' </summary>
        Public MustOverride ReadOnly Property SupportsSharingSetup As Boolean

        ''' <summary>
        ''' DMS provider supports configuration of root folder and subfolders for the different purposes (input files, reports)
        ''' </summary>
        Public MustOverride ReadOnly Property SupportsSubFolderConfiguration As Boolean

        ''' <summary>
        ''' DMS provider supports remote items with very same names (e.g. 2 files with the very same name are uniquely accessible only by their DmsItem respectively by their ID)
        ''' </summary>
        Public MustOverride ReadOnly Property SupportsNonUniqueRemoteItems As Boolean

        ''' <summary>
        ''' DMS provider supports files in root folder (or only folders/collections)
        ''' </summary>
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
        Public MustOverride ReadOnly Property SupportsRuntimeAccessToRemoteServer As RuntimeAccessTypes

        ''' <summary>
        ''' Create a link share for a remote DMS item
        ''' </summary>
        ''' <param name="dmsResource">The remote resource associated with the share or link.</param>
        ''' <param name="shareInfo">The requested sharing settings; provider capability restrictions apply.</param>
        ''' <returns>The link created by the provider, including its identifier and supplied details.</returns>
        Public MustOverride Function CreateLink(dmsResource As DmsResourceItem, shareInfo As DmsLink) As DmsLink
        ''' <summary>
        ''' Update an existing link share
        ''' </summary>
        ''' <param name="shareInfo">The requested sharing settings; provider capability restrictions apply.</param>
        Public MustOverride Sub UpdateLink(shareInfo As DmsLink)
        ''' <summary>
        ''' Remove an existing link share
        ''' </summary>
        ''' <param name="shareInfo">The requested sharing settings; provider capability restrictions apply.</param>
        Public MustOverride Sub DeleteLink(shareInfo As DmsLink)

        ''' <summary>
        ''' Create a share for a group on remote DMS
        ''' </summary>
        ''' <param name="dmsResource">The remote resource associated with the share or link.</param>
        ''' <param name="shareInfo">The requested sharing settings; provider capability restrictions apply.</param>
        Public MustOverride Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForGroup)
        ''' <summary>
        ''' Create a share for a user on remote DMS
        ''' </summary>
        ''' <param name="dmsResource">The remote resource associated with the share or link.</param>
        ''' <param name="shareInfo">The requested sharing settings; provider capability restrictions apply.</param>
        Public MustOverride Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForUser)

        ''' <summary>
        ''' Update a group share on remote DMS
        ''' </summary>
        ''' <param name="shareInfo">The requested sharing settings; provider capability restrictions apply.</param>
        Public MustOverride Sub UpdateSharing(shareInfo As DmsShareForGroup)
        ''' <summary>
        ''' Remove a group share on remote DMS
        ''' </summary>
        ''' <param name="shareInfo">The requested sharing settings; provider capability restrictions apply.</param>
        Public MustOverride Sub DeleteSharing(shareInfo As DmsShareForGroup)
        ''' <summary>
        ''' Update a user share on remote DMS
        ''' </summary>
        ''' <param name="shareInfo">The requested sharing settings; provider capability restrictions apply.</param>
        Public MustOverride Sub UpdateSharing(shareInfo As DmsShareForUser)
        ''' <summary>
        ''' Remove a user share on remote DMS
        ''' </summary>
        ''' <param name="shareInfo">The requested sharing settings; provider capability restrictions apply.</param>
        Public MustOverride Sub DeleteSharing(shareInfo As DmsShareForUser)

        ''' <summary>
        ''' Load a list of groups on remote DMS (which are visible to the current login user)
        ''' </summary>
        ''' <returns>The group snapshots visible to the authorized account.</returns>
        Public MustOverride Function GetAllGroups() As List(Of DmsGroup)
        ''' <summary>
        ''' Load a list of users on remote DMS (which are visible to the current login user)
        ''' </summary>
        ''' <returns>The user snapshots visible to the authorized account.</returns>
        Public MustOverride Function GetAllUsers() As List(Of DmsUser)

        ''' <summary>Creates a link share without blocking the calling thread.</summary>
        ''' <param name="dmsResource">The resource to share.</param>
        ''' <param name="shareInfo">The requested link settings.</param>
        ''' <param name="cancellationToken">Cancels a queued operation.</param>
        ''' <returns>The created link.</returns>
        Public Overridable Function CreateLinkAsync(dmsResource As DmsResourceItem, shareInfo As DmsLink, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsLink)
            Return Me.RunSynchronousFallbackAsync(Function() Me.CreateLink(dmsResource, shareInfo), cancellationToken)
        End Function

        ''' <summary>
        ''' Refreshes a link through the provider's asynchronous access path.
        ''' </summary>
        ''' <param name="shareInfo">The link whose details are refreshed.</param>
        ''' <param name="cancellationToken">Cancels waiting for provider access. The synchronous fallback cannot interrupt an active callback.</param>
        ''' <returns>A task that completes when the link details have been refreshed.</returns>
        Public Overridable Function RefreshLinkAsync(shareInfo As DmsLink, Optional cancellationToken As CancellationToken = Nothing) As Task
            If shareInfo Is Nothing Then Throw New ArgumentNullException(NameOf(shareInfo))
            Return Me.RunSynchronousFallbackAsync(Sub() shareInfo.Refresh(), cancellationToken)
        End Function

        ''' <summary>Updates a link share without blocking the calling thread.</summary>
        ''' <param name="shareInfo">The link to update.</param>
        ''' <param name="cancellationToken">Cancels a queued operation.</param>
        ''' <returns>A task that completes after the update.</returns>
        Public Overridable Function UpdateLinkAsync(shareInfo As DmsLink, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.UpdateLink(shareInfo), cancellationToken)
        End Function

        ''' <summary>Deletes a link share without blocking the calling thread.</summary>
        ''' <param name="shareInfo">The link to delete.</param>
        ''' <param name="cancellationToken">Cancels a queued operation.</param>
        ''' <returns>A task that completes after deletion.</returns>
        Public Overridable Function DeleteLinkAsync(shareInfo As DmsLink, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.DeleteLink(shareInfo), cancellationToken)
        End Function

        ''' <summary>Creates a group share without blocking the calling thread.</summary>
        ''' <param name="dmsResource">The resource to share.</param>
        ''' <param name="shareInfo">The group share settings.</param>
        ''' <param name="cancellationToken">Cancels a queued operation.</param>
        ''' <returns>A task that completes after creation.</returns>
        Public Overridable Function CreateSharingAsync(dmsResource As DmsResourceItem, shareInfo As DmsShareForGroup, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.CreateSharing(dmsResource, shareInfo), cancellationToken)
        End Function

        ''' <summary>Creates a user share without blocking the calling thread.</summary>
        ''' <param name="dmsResource">The resource to share.</param>
        ''' <param name="shareInfo">The user share settings.</param>
        ''' <param name="cancellationToken">Cancels a queued operation.</param>
        ''' <returns>A task that completes after creation.</returns>
        Public Overridable Function CreateSharingAsync(dmsResource As DmsResourceItem, shareInfo As DmsShareForUser, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.CreateSharing(dmsResource, shareInfo), cancellationToken)
        End Function

        ''' <summary>Updates a group share without blocking the calling thread.</summary>
        ''' <param name="shareInfo">The group share to update.</param>
        ''' <param name="cancellationToken">Cancels a queued operation.</param>
        ''' <returns>A task that completes after the update.</returns>
        Public Overridable Function UpdateSharingAsync(shareInfo As DmsShareForGroup, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.UpdateSharing(shareInfo), cancellationToken)
        End Function

        ''' <summary>Updates a user share without blocking the calling thread.</summary>
        ''' <param name="shareInfo">The user share to update.</param>
        ''' <param name="cancellationToken">Cancels a queued operation.</param>
        ''' <returns>A task that completes after the update.</returns>
        Public Overridable Function UpdateSharingAsync(shareInfo As DmsShareForUser, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.UpdateSharing(shareInfo), cancellationToken)
        End Function

        ''' <summary>Deletes a group share without blocking the calling thread.</summary>
        ''' <param name="shareInfo">The group share to delete.</param>
        ''' <param name="cancellationToken">Cancels a queued operation.</param>
        ''' <returns>A task that completes after deletion.</returns>
        Public Overridable Function DeleteSharingAsync(shareInfo As DmsShareForGroup, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.DeleteSharing(shareInfo), cancellationToken)
        End Function

        ''' <summary>Deletes a user share without blocking the calling thread.</summary>
        ''' <param name="shareInfo">The user share to delete.</param>
        ''' <param name="cancellationToken">Cancels a queued operation.</param>
        ''' <returns>A task that completes after deletion.</returns>
        Public Overridable Function DeleteSharingAsync(shareInfo As DmsShareForUser, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.DeleteSharing(shareInfo), cancellationToken)
        End Function

        ''' <summary>Lists visible groups without blocking the calling thread.</summary>
        ''' <param name="cancellationToken">Cancels a queued lookup.</param>
        ''' <returns>The visible groups.</returns>
        Public Overridable Function GetAllGroupsAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsGroup))
            Return Me.RunSynchronousFallbackAsync(Function() Me.GetAllGroups(), cancellationToken)
        End Function

        ''' <summary>Lists visible users without blocking the calling thread.</summary>
        ''' <param name="cancellationToken">Cancels a queued lookup.</param>
        ''' <returns>The visible users.</returns>
        Public Overridable Function GetAllUsersAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsUser))
            Return Me.RunSynchronousFallbackAsync(Function() Me.GetAllUsers(), cancellationToken)
        End Function

        ''' <summary>
        ''' A runtime variable which contains the user ID after login at remote DMS system
        ''' </summary>
        Public MustOverride ReadOnly Property CurrentContextUserID As String

        ''' <summary>
        ''' Retrieves the current authenticated user's identifier without blocking the caller.
        ''' </summary>
        ''' <param name="cancellationToken">Cancels waiting for provider access. The synchronous fallback cannot interrupt an active request.</param>
        ''' <returns>The provider's identifier for the current authenticated user.</returns>
        Public Overridable Function GetCurrentContextUserIDAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of String)
            Return Me.RunSynchronousFallbackAsync(Function() Me.CurrentContextUserID, cancellationToken)
        End Function

    End Class

End Namespace
