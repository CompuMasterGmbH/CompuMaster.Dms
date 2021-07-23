Option Explicit On
Option Strict On

Namespace Data

#Disable Warning CA1034 ' Nested types should not be visible
#Disable Warning CA1815 ' Override equals and operator equals on value types

    Public MustInherit Class DmsShareBase

        Protected Sub New(parentDmsResourceItem As DmsResourceItem, allowView As Boolean, allowDownload As Boolean, allowEdit As Boolean, allowUpload As Boolean, allowDelete As Boolean, allowShare As Boolean)
            Me.ParentDmsResourceItem = parentDmsResourceItem
            Me.AllowView = allowView
            Me.AllowDownload = allowDownload
            Me.AllowEdit = allowEdit
            Me.AllowUpload = allowUpload
            Me.AllowDelete = allowDelete
            Me.AllowShare = allowShare
        End Sub

        Protected MustOverride Sub Initialize()

        Public Property ParentDmsResourceItem As DmsResourceItem

        Public Function AllowedActions() As List(Of String)
            Dim Result As New List(Of String)
            If AllowView Then Result.Add("View")
            If AllowDownload Then Result.Add("Download")
            If AllowEdit Then Result.Add("Edit")
            If AllowUpload Then Result.Add("Upload")
            If AllowDelete Then Result.Add("Delete")
            If AllowShare Then Result.Add("Share")
            Return Result
        End Function

        Private _AllowView As Boolean
        ''' <summary>
        ''' Allow view (=view only, no download)
        ''' </summary>
        ''' <returns></returns>
        Public Property AllowView As Boolean
            Get
                Me.Initialize()
                Return Me._AllowView
            End Get
            Set(value As Boolean)
                Me._AllowView = value
            End Set
        End Property

        Private _AllowShare As Boolean
        ''' <summary>
        ''' Allow re-sharing
        ''' </summary>
        ''' <returns></returns>
        Public Property AllowShare As Boolean
            Get
                Me.Initialize()
                Return Me._AllowShare
            End Get
            Set(value As Boolean)
                Me._AllowShare = value
            End Set
        End Property

        Private _AllowDownload As Boolean
        ''' <summary>
        ''' Allow downloads
        ''' </summary>
        ''' <returns></returns>
        Public Property AllowDownload As Boolean
            Get
                Me.Initialize()
                Return Me._AllowDownload
            End Get
            Set(value As Boolean)
                Me._AllowDownload = value
            End Set
        End Property

        Private _AllowEdit As Boolean
        ''' <summary>
        ''' Allow edit/update of files or folders
        ''' </summary>
        ''' <returns></returns>
        Public Property AllowEdit As Boolean
            Get
                Me.Initialize()
                Return Me._AllowEdit
            End Get
            Set(value As Boolean)
                Me._AllowEdit = value
            End Set
        End Property

        Private _AllowUpload As Boolean
        ''' <summary>
        ''' Allow uploads
        ''' </summary>
        ''' <returns></returns>
        Public Property AllowUpload As Boolean
            Get
                Me.Initialize()
                Return Me._AllowUpload
            End Get
            Set(value As Boolean)
                Me._AllowUpload = value
            End Set
        End Property

        Private _AllowDelete As Boolean
        ''' <summary>
        ''' Allow deletions
        ''' </summary>
        ''' <returns></returns>
        Public Property AllowDelete As Boolean
            Get
                Me.Initialize()
                Return Me._AllowDelete
            End Get
            Set(value As Boolean)
                Me._AllowDelete = value
            End Set
        End Property

    End Class

#Enable Warning CA1815 ' Override equals and operator equals on value types
#Enable Warning CA1034 ' Nested types should not be visible

End Namespace