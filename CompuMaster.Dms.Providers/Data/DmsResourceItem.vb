Option Explicit On
Option Strict On

Namespace Data

    Public Class DmsResourceItem

        Public Sub New()
        End Sub

        Public Enum ItemTypes As Byte
            File = 1
            Folder = 2
            Collection = 3
            Root = 4
        End Enum

        Public Property Name As String
        Public Property Folder As String
        Public Property Collection As String
        Public Property FullName As String
        Public Property LastModificationOnLocalTime As DateTime?
        Public Property CreatedOnLocalTime As DateTime?
        Public Property IsHidden As Boolean
        Public Property ContentLength As Long
        Private Property IsFolder As Boolean
        Private Property IsCollection As Boolean
        Private Property IsRoot As Boolean
        Public Property ProviderSpecificHashOrETag As String
        Public Property ExtendedInfosCollisionDetected As Boolean
        Public Property ExtendedInfosFileID As String
        Public Property ExtendedInfosFolderID As String
        Public Property ExtendedInfosCollectionID As String
        Public Property ExtendedInfosAssignedFolderID As String
        Public Property ExtendedInfosAssignedCollectionID As String
        Public Property ExtendedInfosData As Object
        Public Property ExtendedInfosOwner As DmsUser
        Public Property ExtendedInfosLastModificationUser As DmsUser
        Public Property ExtendedInfosLinks As List(Of DmsLink)
        Public Property ExtendedInfosLocks As List(Of String)
        Public Property ExtendedInfosLockedByUser As DmsUser
        Public Property ExtendedInfosArchivedDateLocalTime As Date?
        Public Property ExtendedInfosVersion As String
        Public Property ExtendedInfosVersionDateLocalTime As Date?
        Public Property ExtendedInfosIsShared As Boolean
        Public Property ExtendedInfosIsPublicCollection As Boolean
        Public Property ExtendedInfosIsAuditing As Boolean
        Public Property ExtendedInfosIsIntelligent As Boolean
        Public Property ExtendedInfosHasGroupSharings As Boolean
        Public Property ExtendedInfosHasHiddenGroupSharings As Boolean
        Public Property ExtendedInfosGroupSharings As List(Of DmsShareForGroup)
        Public Property ExtendedInfosHasUserSharings As Boolean
        Public Property ExtendedInfosHasHiddenUserSharings As Boolean
        Public Property ExtendedInfosUserSharings As List(Of DmsShareForUser)
        Public Property ExtendedInfosReferencedFromFolderIDs As List(Of String)
        Public Property ExtendedInfosReferencedFromCollectionIDs As List(Of String)
        Public ReadOnly Property ExtendedInfosHasLinks As Boolean
            Get
                If Me.ExtendedInfosLinks Is Nothing OrElse Me.ExtendedInfosLinks.Count = 0 Then
                    Return False
                Else
                    Return True
                End If
            End Get
        End Property

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

        Public Overrides Function ToString() As String
            Return Me.FullName
        End Function

    End Class

End Namespace