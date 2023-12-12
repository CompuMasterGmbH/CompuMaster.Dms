Option Explicit On
Option Strict On

Namespace Data

    ''' <summary>
    ''' Meta data for a remote item
    ''' </summary>
    Public Class DmsResourceItem

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
        ''' <returns></returns>
        Public Property Name As String
        ''' <summary>
        ''' Full path of parent folder
        ''' </summary>
        ''' <returns></returns>
        Public Property Folder As String
        ''' <summary>
        ''' Full path of parent collection
        ''' </summary>
        ''' <returns></returns>
        Public Property Collection As String
        ''' <summary>
        ''' Full path of remote item
        ''' </summary>
        ''' <returns></returns>
        Public Property FullName As String
        ''' <summary>
        ''' The remote item's last modification date/time
        ''' </summary>
        ''' <returns></returns>
        Public Property LastModificationOnLocalTime As DateTime?
        ''' <summary>
        ''' The remote item creation date/time
        ''' </summary>
        ''' <returns></returns>
        Public Property CreatedOnLocalTime As DateTime?
        ''' <summary>
        ''' The item is attributed as hidden
        ''' </summary>
        ''' <returns></returns>
        Public Property IsHidden As Boolean
        ''' <summary>
        ''' The length of the file
        ''' </summary>
        ''' <returns></returns>
        Public Property ContentLength As Long
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
        ''' <returns></returns>
        Public Property ProviderSpecificHashOrETag As String
        ''' <summary>
        ''' The remote DMS contains 2 or more items with the very same item name
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks>
        ''' <para>Attention is requested if 2 or more items could be the operation target of an action (e.g. open or delete a remote file): the action might be related to the wrong remote item.</para>
        ''' <para>Most often, the additional file was created by uploading a file for a 2nd time instead of creating a new version of the existing file, but this issue depends on the remote DMS type/provider.</para>
        ''' <para>In case of duplicate items on remote DMS, the file/folder/collection ID should be considered to act on the correct remote item.</para>
        ''' </remarks>
        Public Property ExtendedInfosCollisionDetected As Boolean
        ''' <summary>
        ''' The unique ID of a file
        ''' </summary>
        ''' <returns></returns>
        Public Property ExtendedInfosFileID As String
        ''' <summary>
        ''' The unique ID of a folder
        ''' </summary>
        ''' <returns></returns>
        Public Property ExtendedInfosFolderID As String
        ''' <summary>
        ''' The unique ID of a collection
        ''' </summary>
        ''' <returns></returns>
        Public Property ExtendedInfosCollectionID As String
        ''' <summary>
        ''' The unique ID of the parent folder
        ''' </summary>
        ''' <returns></returns>
        Public Property ExtendedInfosAssignedFolderID As String
        ''' <summary>
        ''' The unique ID of the parent collection
        ''' </summary>
        ''' <returns></returns>
        Public Property ExtendedInfosAssignedCollectionID As String
        Public Property ExtendedInfosData As Object
        ''' <summary>
        ''' The owner of the remote item
        ''' </summary>
        ''' <returns></returns>
        Public Property ExtendedInfosOwner As DmsUser
        ''' <summary>
        ''' The user who wrote the last modification
        ''' </summary>
        ''' <returns></returns>
        Public Property ExtendedInfosLastModificationUser As DmsUser
        Public Property ExtendedInfosLocks As List(Of String)
        Public Property ExtendedInfosLockedByUser As DmsUser
        Public Property ExtendedInfosArchivedDateLocalTime As Date?
        Public Property ExtendedInfosVersion As String
        Public Property ExtendedInfosVersionDateLocalTime As Date?

        Private _ExtendedInfosIsShared As Boolean?
        ''' <summary>
        ''' The remote item is shared by links or shared for users/groups
        ''' </summary>
        ''' <returns></returns>
        Public Property ExtendedInfosIsShared As Boolean
            Get
                If _ExtendedInfosIsShared.HasValue = False AndAlso FillSharingInfos IsNot Nothing Then
                    Me.FillSharingInfos(Me)
                ElseIf _ExtendedInfosIsShared.HasValue = False Then
                    Return Me.ExtendedInfosHasGroupSharings OrElse Me.ExtendedInfosHasHiddenGroupSharings OrElse Me.ExtendedInfosHasUserSharings OrElse Me.ExtendedInfosHasHiddenUserSharings OrElse Me.ExtendedInfosHasLinks
                End If
                Return _ExtendedInfosIsShared.Value
            End Get
            Set(value As Boolean)
                _ExtendedInfosIsShared = value
            End Set
        End Property

        Public Property ExtendedInfosIsPublicCollection As Boolean

        Public Property ExtendedInfosIsAuditing As Boolean

        ''' <summary>
        ''' The remote item (collection) has got some smart components, e.g. is a query on remote file system
        ''' </summary>
        ''' <returns></returns>
        Public Property ExtendedInfosIsIntelligent As Boolean

        Private _ExtendedInfosHasGroupSharings As Boolean?
        ''' <summary>
        ''' The remote item is shared for groups
        ''' </summary>
        ''' <returns></returns>
        Public Property ExtendedInfosHasGroupSharings As Boolean
            Get
                If _ExtendedInfosHasGroupSharings.HasValue = False AndAlso Me.FillSharingInfos IsNot Nothing Then
                    Me.FillSharingInfos(Me)
                End If
                Return _ExtendedInfosHasGroupSharings.Value
            End Get
            Set(value As Boolean)
                _ExtendedInfosHasGroupSharings = value
            End Set
        End Property

        Private _ExtendedInfosHasHiddenGroupSharings As Boolean?
        ''' <summary>
        ''' The remote item is shared for groups which are not visible to the current user
        ''' </summary>
        ''' <returns></returns>
        Public Property ExtendedInfosHasHiddenGroupSharings As Boolean
            Get
                If _ExtendedInfosHasHiddenGroupSharings.HasValue = False AndAlso Me.FillSharingInfos IsNot Nothing Then
                    Me.FillSharingInfos(Me)
                End If
                Return _ExtendedInfosHasHiddenGroupSharings.Value
            End Get
            Set(value As Boolean)
                _ExtendedInfosHasHiddenGroupSharings = value
            End Set
        End Property

        Private _ExtendedInfosGroupSharings As List(Of DmsShareForGroup)
        ''' <summary>
        ''' The sharing entries for groups
        ''' </summary>
        ''' <returns></returns>
        Public Property ExtendedInfosGroupSharings As List(Of DmsShareForGroup)
            Get
                If _ExtendedInfosGroupSharings Is Nothing AndAlso Me.FillSharingInfos IsNot Nothing Then
                    Me.FillSharingInfos(Me)
                End If
                Return _ExtendedInfosGroupSharings
            End Get
            Set(value As List(Of DmsShareForGroup))
                _ExtendedInfosGroupSharings = value
            End Set
        End Property

        Private _ExtendedInfosHasUserSharings As Boolean?
        ''' <summary>
        ''' The remote item is shared for users
        ''' </summary>
        ''' <returns></returns>
        Public Property ExtendedInfosHasUserSharings As Boolean
            Get
                If _ExtendedInfosHasUserSharings.HasValue = False AndAlso Me.FillSharingInfos IsNot Nothing Then
                    Me.FillSharingInfos(Me)
                End If
                Return _ExtendedInfosHasUserSharings.Value
            End Get
            Set(value As Boolean)
                _ExtendedInfosHasUserSharings = value
            End Set
        End Property

        Private _ExtendedInfosHasHiddenUserSharings As Boolean?
        ''' <summary>
        ''' The remote item is shared for users which are not visible to the current user
        ''' </summary>
        ''' <returns></returns>
        Public Property ExtendedInfosHasHiddenUserSharings As Boolean
            Get
                If _ExtendedInfosHasHiddenUserSharings.HasValue = False AndAlso Me.FillSharingInfos IsNot Nothing Then
                    Me.FillSharingInfos(Me)
                End If
                Return _ExtendedInfosHasHiddenUserSharings.Value
            End Get
            Set(value As Boolean)
                _ExtendedInfosHasHiddenUserSharings = value
            End Set
        End Property

        Private _ExtendedInfosUserSharings As List(Of DmsShareForUser)
        ''' <summary>
        ''' The sharing entries for users
        ''' </summary>
        ''' <returns></returns>
        Public Property ExtendedInfosUserSharings As List(Of DmsShareForUser)
            Get
                If _ExtendedInfosUserSharings Is Nothing AndAlso Me.FillSharingInfos IsNot Nothing Then
                    Me.FillSharingInfos(Me)
                End If
                Return _ExtendedInfosUserSharings
            End Get
            Set(value As List(Of DmsShareForUser))
                _ExtendedInfosUserSharings = value
            End Set
        End Property

        ''' <summary>
        ''' References by other folders to this remote item
        ''' </summary>
        ''' <returns></returns>
        Public Property ExtendedInfosReferencedFromFolderIDs As List(Of String)

        ''' <summary>
        ''' References by other collections to this remote item
        ''' </summary>
        ''' <returns></returns>
        Public Property ExtendedInfosReferencedFromCollectionIDs As List(Of String)

        ''' <summary>
        ''' The remote item is shared by links
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property ExtendedInfosHasLinks As Boolean
            Get
                If Me.ExtendedInfosLinks Is Nothing OrElse Me.ExtendedInfosLinks.Count = 0 Then
                    Return False
                Else
                    Return True
                End If
            End Get
        End Property

        Private _ExtendedInfosLinks As List(Of DmsLink)
        ''' <summary>
        ''' Link sharings
        ''' </summary>
        ''' <returns></returns>
        Public Property ExtendedInfosLinks As List(Of DmsLink)
            Get
                If _ExtendedInfosLinks Is Nothing AndAlso Me.FillSharingInfos IsNot Nothing Then
                    Me.FillSharingInfos(Me)
                End If
                Return _ExtendedInfosLinks
            End Get
            Set(value As List(Of DmsLink))
                _ExtendedInfosLinks = value
            End Set
        End Property

        ''' <summary>
        ''' Fill sharing data just-in-time from server
        ''' </summary>
        Friend FillSharingInfos As InitialFillSharingInfos

        ''' <summary>
        ''' Fill sharing data just-in-time from server
        ''' </summary>
        ''' <param name="item"></param>
        Friend Delegate Sub InitialFillSharingInfos(item As DmsResourceItem)

        ''' <summary>
        ''' The type of the remote item
        ''' </summary>
        ''' <returns></returns>
        Public Property ItemType As ItemTypes
            Get
                If IsRoot Then
                    If IsCollection OrElse IsFolder Then
                        Throw New InvalidOperationException("Invalid item type status for IsRoot")
                    End If
                    Return ItemTypes.Root
                ElseIf IsFolder AndAlso Not IsCollection Then
                    Return ItemTypes.Folder
                ElseIf Not IsFolder AndAlso IsCollection Then
                    Return ItemTypes.Collection
                ElseIf Not IsFolder AndAlso Not IsCollection Then
                    Return ItemTypes.File
                Else
                    Throw New InvalidOperationException("Invalid item type status")
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

        ''' <summary>
        ''' The full path of the remote item
        ''' </summary>
        ''' <returns></returns>
        Public Overrides Function ToString() As String
            Return Me.FullName
        End Function

    End Class

End Namespace