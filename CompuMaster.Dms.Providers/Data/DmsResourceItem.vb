Option Explicit On
Option Strict On

Namespace Data

    ''' <summary>
    ''' Meta data for a remote item
    ''' </summary>
    Public Class DmsResourceItem

        ''' <summary>Initializes the resource snapshot with empty sharing/link collections.</summary>
        Public Sub New()
        End Sub

        ''' <summary>
        ''' Types of remote items
        ''' </summary>
        Public Enum ItemTypes As Byte
            ''' <summary>
            ''' A file
            ''' </summary>
            File = 1
            ''' <summary>
            ''' A regular directory
            ''' </summary>
            Folder = 2
            ''' <summary>
            ''' A collection directory, usually implementing additional special features on remote DMS
            ''' </summary>
            Collection = 3
            ''' <summary>
            ''' The root directory of the remote DMS
            ''' </summary>
            Root = 4
        End Enum

        ''' <summary>
        ''' Lookup results for remote items
        ''' </summary>
        Public Enum FoundItemType As Byte
            ''' <summary>
            ''' Ressource doesn't exist
            ''' </summary>
            NotFound = 0
            ''' <summary>
            ''' A file
            ''' </summary>
            File = 1
            ''' <summary>
            ''' A regular directory
            ''' </summary>
            Folder = 2
            ''' <summary>
            ''' A collection directory, usually implementing additional special features on remote DMS
            ''' </summary>
            Collection = 3
            ''' <summary>
            ''' The root directory of the remote DMS
            ''' </summary>
            Root = 4
        End Enum

        ''' <summary>
        ''' Lookup results for remote items
        ''' </summary>
        Public Enum FoundItemResult As Byte
            ''' <summary>
            ''' Ressource doesn't exist
            ''' </summary>
            NotFound = 0
            ''' <summary>
            ''' A file
            ''' </summary>
            File = 1
            ''' <summary>
            ''' A regular directory
            ''' </summary>
            Folder = 2
            ''' <summary>
            ''' A collection directory, usually implementing additional special features on remote DMS
            ''' </summary>
            Collection = 3
            ''' <summary>
            ''' The root directory of the remote DMS
            ''' </summary>
            Root = 4
            ''' <summary>
            ''' There is more than 1 file or folder with the very same name
            ''' </summary>
            WithNameCollisions = 255
        End Enum

        ''' <summary>
        ''' Item name
        ''' </summary>
        Public Property Name As String
        ''' <summary>
        ''' Full path of parent folder
        ''' </summary>
        Public Property Folder As String
        ''' <summary>
        ''' Full path of parent collection
        ''' </summary>
        Public Property Collection As String
        ''' <summary>
        ''' Full path of remote item
        ''' </summary>
        Public Property FullName As String
        ''' <summary>
        ''' The remote item's last modification date/time
        ''' </summary>
        Public Property LastModificationOnLocalTime As DateTime?
        ''' <summary>
        ''' The remote item creation date/time
        ''' </summary>
        Public Property CreatedOnLocalTime As DateTime?
        ''' <summary>
        ''' The item is attributed as hidden
        ''' </summary>
        Public Property IsHidden As Boolean
        ''' <summary>
        ''' The length of the file
        ''' </summary>
        Public Property ContentLength As Long
        ''' <summary>
        ''' Gets or sets the known number of direct child directories, including folders and collections.
        ''' </summary>
        ''' <returns>The child directory count, or <see langword="Nothing"/> when the provider did not supply an exact count.</returns>
        Public Property ChildDirectoryCount As Integer?
        ''' <summary>
        ''' Gets or sets whether the item has direct child directories, including folders and collections.
        ''' </summary>
        ''' <returns><see langword="Nothing"/> when the provider did not supply this metadata.</returns>
        Public Property HasChildDirectories As Boolean?
        ''' <summary>
        ''' This item represents a folder item
        ''' </summary>
        ''' <returns></returns>
        Private Property IsFolder As Boolean
        ''' <summary>
        ''' This item represents a collection item
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks>Collections behave similar to folders, but they usually implement additional special features on remote DMS systems</remarks>
        Private Property IsCollection As Boolean
        ''' <summary>
        ''' This item represents the root directory item
        ''' </summary>
        ''' <returns></returns>
        Private Property IsRoot As Boolean
        ''' <summary>
        ''' A hash or similar check value of the remote file (item) data
        ''' </summary>
        Public Property ProviderSpecificHashOrETag As String
        ''' <summary>
        ''' The remote DMS contains 2 or more items with the very same item name
        ''' </summary>
        ''' <remarks>
        ''' <para>Attention is requested if 2 or more items could be the operation target of an action (e.g. open or delete a remote file): the action might be related to the wrong remote item.</para>
        ''' <para>Most often, the additional file was created by uploading a file for a 2nd time instead of creating a new version of the existing file, but this issue depends on the remote DMS type/provider.</para>
        ''' <para>In case of duplicate items on remote DMS, the file/folder/collection ID should be considered to act on the correct remote item.</para>
        ''' </remarks>
        Public Property ExtendedInfosCollisionDetected As Boolean
        ''' <summary>
        ''' The unique ID of a file
        ''' </summary>
        Public Property ExtendedInfosFileID As String
        ''' <summary>
        ''' The unique ID of a folder
        ''' </summary>
        Public Property ExtendedInfosFolderID As String
        ''' <summary>
        ''' The unique ID of a collection
        ''' </summary>
        Public Property ExtendedInfosCollectionID As String
        ''' <summary>
        ''' The unique ID of the parent folder
        ''' </summary>
        Public Property ExtendedInfosAssignedFolderID As String
        ''' <summary>
        ''' The unique ID of the parent collection
        ''' </summary>
        Public Property ExtendedInfosAssignedCollectionID As String
        ''' <summary>Gets or sets optional provider-specific backing data; common workflows should use the typed resource properties.</summary>
        Public Property ExtendedInfosData As Object
        ''' <summary>
        ''' The owner of the remote item
        ''' </summary>
        Public Property ExtendedInfosOwner As DmsUser
        ''' <summary>
        ''' The user who wrote the last modification
        ''' </summary>
        Public Property ExtendedInfosLastModificationUser As DmsUser
        ''' <summary>Gets or sets the links supplied with this resource snapshot.</summary>
        Public Property ExtendedInfosLinks As List(Of DmsLink)
        ''' <summary>Gets or sets the lock identifiers supplied by the provider.</summary>
        Public Property ExtendedInfosLocks As List(Of String)
        ''' <summary>Gets or sets the user identified as holding a lock, when available.</summary>
        Public Property ExtendedInfosLockedByUser As DmsUser
        ''' <summary>Gets or sets the archive timestamp in local time, or Nothing when unavailable.</summary>
        Public Property ExtendedInfosArchivedDateLocalTime As Date?
        ''' <summary>Gets or sets the provider-supplied version identifier.</summary>
        Public Property ExtendedInfosVersion As String
        ''' <summary>Gets or sets the version timestamp in local time, or Nothing when unavailable.</summary>
        Public Property ExtendedInfosVersionDateLocalTime As Date?
        ''' <summary>
        ''' The remote item is shared by links or shared for users/groups
        ''' </summary>
        Public Property ExtendedInfosIsShared As Boolean
        ''' <summary>Gets or sets whether the provider identifies this collection as public.</summary>
        Public Property ExtendedInfosIsPublicCollection As Boolean
        ''' <summary>Gets or sets whether the provider identifies auditing as enabled.</summary>
        Public Property ExtendedInfosIsAuditing As Boolean
        ''' <summary>
        ''' The remote item (collection) has got some smart components, e.g. is a query on remote file system
        ''' </summary>
        Public Property ExtendedInfosIsIntelligent As Boolean
        ''' <summary>
        ''' The remote item is shared for groups
        ''' </summary>
        Public Property ExtendedInfosHasGroupSharings As Boolean
        ''' <summary>
        ''' The remote item is shared for groups which are not visible to the current user
        ''' </summary>
        Public Property ExtendedInfosHasHiddenGroupSharings As Boolean
        ''' <summary>
        ''' The sharing entries for groups
        ''' </summary>
        Public Property ExtendedInfosGroupSharings As List(Of DmsShareForGroup)
        ''' <summary>
        ''' The remote item is shared for users
        ''' </summary>
        Public Property ExtendedInfosHasUserSharings As Boolean
        ''' <summary>
        ''' The remote item is shared for users which are not visible to the current user
        ''' </summary>
        Public Property ExtendedInfosHasHiddenUserSharings As Boolean
        ''' <summary>
        ''' The sharing entries for users
        ''' </summary>
        Public Property ExtendedInfosUserSharings As List(Of DmsShareForUser)
        ''' <summary>
        ''' References by other folders to this remote item
        ''' </summary>
        Public Property ExtendedInfosReferencedFromFolderIDs As List(Of String)
        ''' <summary>
        ''' References by other collections to this remote item
        ''' </summary>
        Public Property ExtendedInfosReferencedFromCollectionIDs As List(Of String)
        ''' <summary>
        ''' The remote item is shared by links
        ''' </summary>
        Public ReadOnly Property ExtendedInfosHasLinks As Boolean
            Get
                If Me.ExtendedInfosLinks Is Nothing OrElse Me.ExtendedInfosLinks.Count = 0 Then
                    Return False
                Else
                    Return True
                End If
            End Get
        End Property

        ''' <summary>
        ''' The type of the remote item
        ''' </summary>
        Public Property ItemType As ItemTypes
            Get
                If IsRoot Then
                    If IsCollection OrElse IsFolder Then
                        Throw New InvalidOperationException(ProviderStrings.GetText("InvalidItemTypeStatusForIsRoot"))
                    End If
                    Return ItemTypes.Root
                ElseIf IsFolder AndAlso Not IsCollection Then
                    Return ItemTypes.Folder
                ElseIf Not IsFolder AndAlso IsCollection Then
                    Return ItemTypes.Collection
                ElseIf Not IsFolder AndAlso Not IsCollection Then
                    Return ItemTypes.File
                Else
                    Throw New InvalidOperationException(ProviderStrings.GetText("InvalidItemTypeStatus"))
                End If
            End Get
            Set(value As ItemTypes)
                Select Case value
                    Case ItemTypes.File
                        Me.IsCollection = False
                        Me.IsFolder = False
                        Me.IsRoot = False
                    Case ItemTypes.Folder
                        Me.IsCollection = False
                        Me.IsFolder = True
                        Me.IsRoot = False
                    Case ItemTypes.Collection
                        Me.IsCollection = True
                        Me.IsFolder = False
                        Me.IsRoot = False
                    Case ItemTypes.Root
                        Me.IsCollection = False
                        Me.IsFolder = False
                        Me.IsRoot = True
                    Case Else
                        Throw New ArgumentOutOfRangeException(NameOf(value))
                End Select
            End Set
        End Property

        ''' <inheritdoc/>
        ''' <summary>
        ''' The full path of the remote item
        ''' </summary>
        ''' <returns>The resource display text.</returns>
        Public Overrides Function ToString() As String
            Return Me.FullName
        End Function

    End Class

End Namespace
