Namespace Data
    ''' <summary>Describes a historical file version with immutable file and version identity.</summary>
    ''' <remarks>Version IDs are opaque and differ from dates and labels. Optional metadata remains unknown when unavailable. Use snapshots returned by the same provider/account for actions.</remarks>
    Public NotInheritable Class DmsFileVersion
        ''' <summary>Initializes an immutable historical version snapshot.</summary>
        ''' <param name="file">The selected file snapshot; its identity and name are copied.</param>
        ''' <param name="versionID">The opaque version ID.</param>
        ''' <param name="modifiedUtc">The optional historical UTC modification time.</param>
        ''' <param name="contentLength">The optional historical length in bytes.</param>
        ''' <param name="authorID">The optional author's stable user ID.</param>
        ''' <param name="authorDisplayName">The optional author's display name.</param>
        ''' <param name="label">The optional version label or tag.</param>
        ''' <param name="contentType">The optional historical media type.</param>
        ''' <param name="eTag">The optional historical entity tag.</param>
        ''' <exception cref="ArgumentException">The selected item is not a file or the version ID is empty.</exception>
        Public Sub New(file As DmsResourceItem, versionID As String, Optional modifiedUtc As DateTime? = Nothing,
                       Optional contentLength As Long? = Nothing, Optional authorID As String = Nothing,
                       Optional authorDisplayName As String = Nothing, Optional label As String = Nothing,
                       Optional contentType As String = Nothing, Optional eTag As String = Nothing)
            If file Is Nothing Then Throw New ArgumentNullException(NameOf(file))
            If file.ItemType <> DmsResourceItem.ItemTypes.File OrElse String.IsNullOrWhiteSpace(versionID) Then Throw New ArgumentException("A file and opaque version ID are required.")
            If contentLength.HasValue AndAlso contentLength.Value < 0 Then Throw New ArgumentOutOfRangeException(NameOf(contentLength))
            Me.FileID = file.ExtendedInfosFileID
            Me.FileFullName = file.FullName
            Me.FileName = file.Name
            Me.VersionID = versionID
            Me.LastModificationUtc = If(modifiedUtc.HasValue, CType(If(modifiedUtc.Value.Kind = DateTimeKind.Unspecified, DateTime.SpecifyKind(modifiedUtc.Value, DateTimeKind.Utc), modifiedUtc.Value.ToUniversalTime()), DateTime?), Nothing)
            Me.ContentLength = contentLength
            Me.AuthorID = authorID
            Me.AuthorDisplayName = authorDisplayName
            Me.Label = label
            Me.ContentType = contentType
            Me.ProviderSpecificHashOrETag = eTag
        End Sub
        ''' <summary>Gets the stable file ID, or nothing when a provider has no stable ID.</summary>
        Public ReadOnly Property FileID As String
        ''' <summary>Gets the file path observed during listing.</summary>
        Public ReadOnly Property FileFullName As String
        ''' <summary>Gets the file name observed during listing.</summary>
        Public ReadOnly Property FileName As String
        ''' <summary>Gets the opaque version ID.</summary>
        Public ReadOnly Property VersionID As String
        ''' <summary>Gets the historical UTC modification time, or nothing when unknown.</summary>
        Public ReadOnly Property LastModificationUtc As DateTime?
        ''' <summary>Gets the historical byte length, or nothing when unknown.</summary>
        Public ReadOnly Property ContentLength As Long?
        ''' <summary>Gets the author's stable user ID, or nothing when unknown.</summary>
        Public ReadOnly Property AuthorID As String
        ''' <summary>Gets the author's display name, or nothing when unknown.</summary>
        Public ReadOnly Property AuthorDisplayName As String
        ''' <summary>Gets the optional version label or tag.</summary>
        Public ReadOnly Property Label As String
        ''' <summary>Gets the historical media type, or nothing when unknown.</summary>
        Public ReadOnly Property ContentType As String
        ''' <summary>Gets the historical provider hash or entity tag, or nothing when unknown.</summary>
        Public ReadOnly Property ProviderSpecificHashOrETag As String
        Friend Property ProviderState As Object
    End Class
End Namespace
