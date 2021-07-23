Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Providers

Namespace Data

#Disable Warning CA1034 ' Nested types should not be visible
#Disable Warning CA1815 ' Override equals and operator equals on value types
    Public Class DmsLink
        Inherits DmsShareBase
        Implements ICloneable

        Public Sub New(parentDmsResourceItem As DmsResourceItem, id As String, dmsProvider As BaseDmsProvider, fillLinkDetailsMethod As FillLinkDetailsFromId)
            MyBase.New(parentDmsResourceItem, False, False, False, False, False, False)
            Me.ID = id
            Me.DmsProvider = dmsProvider
            Me.FillLinkDetails = fillLinkDetailsMethod
        End Sub

        Public Property ID As String
        Public Property Name As String

        Public DmsProvider As BaseDmsProvider
        Public FillLinkDetails As FillLinkDetailsFromId
        Public Delegate Sub FillLinkDetailsFromId(provider As Object, id As String, dmsLink As DmsLink)

        Private Initialized As Boolean
        Protected Overrides Sub Initialize()
            If Me.Initialized = False AndAlso Me.ID <> Nothing AndAlso Me.DmsProvider IsNot Nothing AndAlso Me.FillLinkDetails IsNot Nothing Then
                Me.Refresh()
                Me.Initialized = True
            End If
        End Sub

        Public Overloads Sub Initialize(password As String, expiryDateLocalTime As DateTime?,
                                        maxDownloads As Long?, maxUploads As Long?, maxUploadBytes As Long?,
                                        viewsCount As Long?, downloadsCount As Long?, uploadsCount As Long?, uploadedBytes As Long?,
                                        webUrl As String, downloadUrl As String, restUrl As String,
                                        allowView As Boolean, allowDownload As Boolean, allowEdit As Boolean, allowUpload As Boolean, allowDelete As Boolean, allowShare As Boolean)
            Me.AllowView = allowView
            Me.AllowDownload = allowDownload
            Me.AllowEdit = allowEdit
            Me.AllowUpload = allowUpload
            Me.AllowDelete = allowDelete
            Me.AllowShare = allowShare
            Me.Password = password
            Me.ExpiryDateLocalTime = expiryDateLocalTime
            Me.WebUrl = webUrl
            Me.DownloadUrl = downloadUrl
            Me.RestUrl = restUrl
            Me.MaxUploads = maxUploads
            Me.MaxBytes = MaxBytes
            Me.MaxDownloads = maxDownloads
            Me.UploadedBytes = uploadedBytes
            Me.UploadsCount = uploadsCount
            Me.ViewsCount = viewsCount
            Me.DownloadsCount = downloadsCount
            Me.Initialized = True
        End Sub

        Public Function Clone() As Object Implements ICloneable.Clone
            Return Me.MemberwiseClone
        End Function

        Public Sub Refresh()
            If Me.ID <> Nothing AndAlso Me.DmsProvider IsNot Nothing AndAlso Me.FillLinkDetails IsNot Nothing Then
                Me.FillLinkDetails(Me.DmsProvider, Me.ID, Me)
            End If
        End Sub

        Public Overrides Function ToString() As String
            If Me.Name = Nothing Then
                Return Me.ID & " (" & String.Join("/", Me.AllowedActions.ToArray) & ")"
            Else
                Return Me.Name & " (" & String.Join("/", Me.AllowedActions.ToArray) & ")"
            End If
        End Function

        Private _WebUrl As String
        Public Property WebUrl As String
            Get
                Me.Initialize()
                Return Me._WebUrl
            End Get
            Set(value As String)
                Me._WebUrl = value
            End Set
        End Property

        Private _DownloadUrl As String
        Public Property DownloadUrl As String
            Get
                Me.Initialize()
                Return Me._DownloadUrl
            End Get
            Set(value As String)
                Me._DownloadUrl = value
            End Set
        End Property

        Private _RestUrl As String
        Public Property RestUrl As String
            Get
                Me.Initialize()
                Return Me._RestUrl
            End Get
            Set(value As String)
                Me._RestUrl = value
            End Set
        End Property

        Private _ExpiryDateLocalTime As DateTime?
        Public Property ExpiryDateLocalTime As DateTime?
            Get
                Me.Initialize()
                Return Me._ExpiryDateLocalTime
            End Get
            Set(value As DateTime?)
                Me._ExpiryDateLocalTime = value
            End Set
        End Property

        Private _MaxDownloads As Long?
        Public Property MaxDownloads As Long?
            Get
                Me.Initialize()
                Return Me._MaxDownloads
            End Get
            Set(value As Long?)
                Me._MaxDownloads = value
            End Set
        End Property

        Private _Password As String
        Public Property Password As String
            Get
                Me.Initialize()
                Return Me._Password
            End Get
            Set(value As String)
                Me._Password = value
            End Set
        End Property

        Private _MaxUploads As Long?
        Public Property MaxUploads As Long?
            Get
                Me.Initialize()
                Return Me._MaxUploads
            End Get
            Set(value As Long?)
                Me._MaxUploads = value
            End Set
        End Property

        Private _MaxBytes As Long?
        Public Property MaxBytes As Long?
            Get
                Me.Initialize()
                Return Me._MaxBytes
            End Get
            Set(value As Long?)
                Me._MaxBytes = value
            End Set
        End Property

        Private _UploadedBytes As Long?
        Public Property UploadedBytes As Long?
            Get
                Me.Initialize()
                Return Me._UploadedBytes
            End Get
            Set(value As Long?)
                Me._UploadedBytes = value
            End Set
        End Property

        Private _UploadsCount As Long?
        Public Property UploadsCount As Long?
            Get
                Me.Initialize()
                Return Me._UploadsCount
            End Get
            Set(value As Long?)
                Me._UploadsCount = value
            End Set
        End Property

        Private _DownloadsCount As Long?
        Public Property DownloadsCount As Long?
            Get
                Me.Initialize()
                Return Me._DownloadsCount
            End Get
            Set(value As Long?)
                Me._DownloadsCount = value
            End Set
        End Property

        Private _ViewsCount As Long?
        Public Property ViewsCount As Long?
            Get
                Me.Initialize()
                Return Me._ViewsCount
            End Get
            Set(value As Long?)
                Me._ViewsCount = value
            End Set
        End Property

    End Class
#Enable Warning CA1815 ' Override equals and operator equals on value types
#Enable Warning CA1034 ' Nested types should not be visible

End Namespace