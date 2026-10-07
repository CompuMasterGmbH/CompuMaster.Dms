Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Providers
Imports System.Threading
Imports System.Threading.Tasks

Namespace Data

    ''' <summary>Represents an external sharing link, its permissions, limits and usage details.</summary>
    ''' <remarks>Reading permissions and link details can synchronously invoke the configured lookup callback when the snapshot has not been initialized. RefreshAsync explicitly reloads details through the provider's async contract.</remarks>
#Disable Warning CA1034 ' Nested types should not be visible
#Disable Warning CA1815 ' Override equals and operator equals on value types
    Public Class DmsLink
        Inherits DmsShareBase
        Implements ICloneable

        ''' <summary>
        ''' Create a new instance of DMS link settings for a new DMS link
        ''' </summary>
        ''' <param name="relatedDmsResourceItem">The remote resource associated with this link.</param>
        ''' <param name="dmsProvider">The provider that owns the link or identity.</param>
        Public Sub New(relatedDmsResourceItem As DmsResourceItem, dmsProvider As BaseDmsProvider)
            MyBase.New(relatedDmsResourceItem, False, False, False, False, False, False)
            Me.ID = Nothing
            Me.DmsProvider = dmsProvider
            Me.FillLinkDetails = Nothing
        End Sub

        ''' <summary>
        ''' Create a new instance of DMS link settings for an existing DMS link
        ''' </summary>
        ''' <param name="relatedDmsResourceItem">The remote resource associated with this link.</param>
        ''' <param name="linkID">Existing link ID</param>
        ''' <param name="dmsProvider">The provider that owns the link or identity.</param>
        ''' <param name="fillLinkDetailsMethod">The optional callback that loads details for an existing link.</param>
        Public Sub New(relatedDmsResourceItem As DmsResourceItem, linkID As String, dmsProvider As BaseDmsProvider, fillLinkDetailsMethod As FillLinkDetailsFromId)
            MyBase.New(relatedDmsResourceItem, False, False, False, False, False, False)
            Me.ID = linkID
            Me.DmsProvider = dmsProvider
            Me.FillLinkDetails = fillLinkDetailsMethod
        End Sub

        ''' <summary>Gets or sets the provider-owned identifier used for remote operations.</summary>
        Public Property ID As String
        ''' <summary>Gets or sets the display name supplied by the provider or caller.</summary>
        Public Property Name As String

        ''' <summary>Gets or sets the provider associated with this object.</summary>
        Public DmsProvider As BaseDmsProvider
        ''' <summary>Gets or sets the optional delegate used to load details for an existing link.</summary>
        Public FillLinkDetails As FillLinkDetailsFromId
        ''' <summary>Loads details into the specified existing link snapshot.</summary>
        ''' <param name="provider">The provider that owns the resource or identity.</param>
        ''' <param name="id">The provider-owned identifier.</param>
        ''' <param name="dmsLink">The link snapshot to populate.</param>
        Public Delegate Sub FillLinkDetailsFromId(provider As Object, id As String, dmsLink As DmsLink)

        Private Initialized As Boolean
        Friend ReadOnly Property DetailsInitialized As Boolean
            Get
                Return Me.Initialized
            End Get
        End Property
        ''' <inheritdoc/>
        Protected Overrides Sub Initialize()
            If Me.Initialized = False AndAlso Me.ID <> Nothing AndAlso Me.DmsProvider IsNot Nothing AndAlso Me.FillLinkDetails IsNot Nothing Then
                Me.Refresh()
                Me.Initialized = True
            End If
        End Sub

        ''' <summary>Initializes permission or link details before they are read.</summary>
        ''' <param name="password">The link password, or Nothing when no password is supplied.</param>
        ''' <param name="expiryDateLocalTime">The expiry timestamp in local time, or Nothing when unavailable.</param>
        ''' <param name="maxDownloads">The maximum download count, or Nothing when no limit is supplied.</param>
        ''' <param name="maxUploads">The maximum upload count, or Nothing when no limit is supplied.</param>
        ''' <param name="maxUploadBytes">The maximum upload-byte count, or Nothing when no limit is supplied.</param>
        ''' <param name="viewsCount">The reported view count, or Nothing when unavailable.</param>
        ''' <param name="downloadsCount">The reported download count, or Nothing when unavailable.</param>
        ''' <param name="uploadsCount">The reported upload count, or Nothing when unavailable.</param>
        ''' <param name="uploadedBytes">The reported uploaded-byte count, or Nothing when unavailable.</param>
        ''' <param name="webUrl">The link URL used by a browser.</param>
        ''' <param name="downloadUrl">The direct download URL, when supplied.</param>
        ''' <param name="restUrl">The provider REST URL, when supplied.</param>
        ''' <param name="allowView">Whether the view permission is enabled.</param>
        ''' <param name="allowDownload">Whether the download permission is enabled.</param>
        ''' <param name="allowEdit">Whether the edit permission is enabled.</param>
        ''' <param name="allowUpload">Whether the upload permission is enabled.</param>
        ''' <param name="allowDelete">Whether the delete permission is enabled.</param>
        ''' <param name="allowShare">Whether the share permission is enabled.</param>
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
            Me.MaxBytes = maxUploadBytes
            Me.MaxDownloads = maxDownloads
            Me.UploadedBytes = uploadedBytes
            Me.UploadsCount = uploadsCount
            Me.ViewsCount = viewsCount
            Me.DownloadsCount = downloadsCount
            Me.Initialized = True
        End Sub

        ''' <summary>Creates a shallow copy of these sharing settings.</summary>
        ''' <returns>A copy retaining references to the provider, parent resource and lookup delegates.</returns>
        Public Function Clone() As Object Implements ICloneable.Clone
            Return Me.MemberwiseClone
        End Function

        ''' <summary>Reloads existing link details through the configured provider callback.</summary>
        ''' <remarks>Without an ID, provider or callback, this method performs no operation.</remarks>
        Public Sub Refresh()
            If Me.ID <> Nothing AndAlso Me.DmsProvider IsNot Nothing AndAlso Me.FillLinkDetails IsNot Nothing Then
                Me.FillLinkDetails(Me.DmsProvider, Me.ID, Me)
                Me.Initialized = True
            End If
        End Sub

        ''' <summary>
        ''' Refreshes the link details without blocking the caller.
        ''' </summary>
        ''' <param name="cancellationToken">Cancels waiting for provider access. Providers with synchronous callbacks cannot interrupt an active callback.</param>
        ''' <returns>A task that completes when the link details have been refreshed.</returns>
        Public Function RefreshAsync(Optional cancellationToken As CancellationToken = Nothing) As Task
            cancellationToken.ThrowIfCancellationRequested()
            If Me.DmsProvider Is Nothing Then Return Task.CompletedTask
            Return Me.DmsProvider.RefreshLinkAsync(Me, cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Function ToString() As String
            If Me.Name = Nothing Then
                Return Me.ID & " (" & String.Join("/", Me.AllowedActions.ToArray) & ")"
            Else
                Return Me.Name & " (" & String.Join("/", Me.AllowedActions.ToArray) & ")"
            End If
        End Function

        Private _WebUrl As String
        ''' <summary>Gets or sets the browser URL of the external link.</summary>
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
        ''' <summary>Gets or sets the direct download URL when supplied.</summary>
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
        ''' <summary>Gets or sets the provider REST URL associated with the link.</summary>
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
        ''' <summary>Gets or sets the expiry timestamp in local time, or Nothing when no limit is supplied.</summary>
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
        ''' <summary>Gets or sets the download limit, or Nothing when no limit is supplied.</summary>
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
        ''' <summary>Gets or sets the credential or link password; it must not be logged.</summary>
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
        ''' <summary>Gets or sets the upload limit, or Nothing when no limit is supplied.</summary>
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
        ''' <summary>Gets or sets the upload-byte limit, or Nothing when no limit is supplied.</summary>
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
        ''' <summary>Gets or sets the reported number of uploaded bytes, or Nothing when unavailable.</summary>
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
        ''' <summary>Gets or sets the reported upload count, or Nothing when unavailable.</summary>
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
        ''' <summary>Gets or sets the reported download count, or Nothing when unavailable.</summary>
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
        ''' <summary>Gets or sets the reported view count, or Nothing when unavailable.</summary>
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
