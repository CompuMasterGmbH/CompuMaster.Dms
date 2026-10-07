Option Explicit On
Option Strict On

Namespace Data

#Disable Warning CA1034 ' Nested types should not be visible
#Disable Warning CA1815 ' Override equals and operator equals on value types

    ''' <summary>Defines common permissions associated with a remote DMS resource.</summary>
    Public MustInherit Class DmsShareBase

        ''' <summary>Initializes the common resource permissions.</summary>
        ''' <param name="parentDmsResourceItem">The remote resource whose permissions are represented.</param>
        ''' <param name="allowView">Whether the view permission is enabled.</param>
        ''' <param name="allowDownload">Whether the download permission is enabled.</param>
        ''' <param name="allowEdit">Whether the edit permission is enabled.</param>
        ''' <param name="allowUpload">Whether the upload permission is enabled.</param>
        ''' <param name="allowDelete">Whether the delete permission is enabled.</param>
        ''' <param name="allowShare">Whether the share permission is enabled.</param>
        Protected Sub New(parentDmsResourceItem As DmsResourceItem, allowView As Boolean, allowDownload As Boolean, allowEdit As Boolean, allowUpload As Boolean, allowDelete As Boolean, allowShare As Boolean)
            Me.ParentDmsResourceItem = parentDmsResourceItem
            Me.AllowView = allowView
            Me.AllowDownload = allowDownload
            Me.AllowEdit = allowEdit
            Me.AllowUpload = allowUpload
            Me.AllowDelete = allowDelete
            Me.AllowShare = allowShare
        End Sub

        ''' <summary>Initializes permission or link details before they are read.</summary>
        Protected MustOverride Sub Initialize()

        ''' <summary>Gets or sets the remote resource to which these permissions belong.</summary>
        Public Property ParentDmsResourceItem As DmsResourceItem

        ''' <summary>Returns stable technical permission tokens for the enabled actions.</summary>
        ''' <returns>The enabled View, Download, Edit, Upload, Delete and Share tokens in that order.</returns>
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
