Imports System.IO
Imports CompuMaster.Dms.Data

Namespace Providers
    Partial Public MustInherit Class BaseDmsProvider
        ''' <summary>Indicates whether the provider implements listing file-version history.</summary>
        ''' <remarks>Server support alone is insufficient. Caller permissions and per-file server authorization remain independent; an enabled workflow may return an empty or pruned history.</remarks>
        Public Overridable ReadOnly Property SupportsFileVersionHistory As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>Indicates whether the provider implements historical file downloads.</summary>
        Public Overridable ReadOnly Property SupportsFileVersionDownload As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>Indicates whether the provider implements restoration of historical file content.</summary>
        ''' <remarks>Independent of history, upload and deletion permissions. Server authorization still applies.</remarks>
        Public Overridable ReadOnly Property SupportsFileVersionRestore As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>Indicates whether the provider implements removal of individual historical versions.</summary>
        ''' <remarks>False includes unknown or unimplemented support; it does not imply that a server administrator cannot prune history.</remarks>
        Public Overridable ReadOnly Property SupportsFileVersionRemoval As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>Lists available historical versions of the selected file.</summary>
        ''' <param name="remoteFile">The selected file snapshot, including stable identity where available.</param>
        ''' <returns>Available versions; an empty list represents no retained history.</returns>
        ''' <remarks>Missing files, missing permissions and unavailable version services remain errors, never an empty history. This focused workflow is synchronous; general async API expansion remains separate.</remarks>
        ''' <exception cref="NotSupportedException">The provider does not implement this workflow.</exception>
        Public Overridable Function ListFileVersions(remoteFile As DmsResourceItem) As List(Of DmsFileVersion)
            Throw New NotSupportedException("File-version history is not supported by this provider.")
        End Function

        ''' <summary>Streams the exact historical content into the caller's destination.</summary>
        ''' <param name="version">A version listed by this provider and account.</param>
        ''' <param name="destination">The writable caller-owned destination, left open even after failure.</param>
        ''' <remarks>Failures may leave partial content. History permission does not grant download permission.</remarks>
        ''' <exception cref="NotSupportedException">The provider does not implement historical downloads.</exception>
        Public Overridable Sub DownloadFileVersion(version As DmsFileVersion, destination As Stream)
            Throw New NotSupportedException("Historical downloads are not supported by this provider.")
        End Sub

        ''' <summary>Restores a historical version to the same current file identity.</summary>
        ''' <param name="version">A version listed by this provider and account.</param>
        ''' <remarks>Rejects stale or foreign identity rather than replacing another file. Refresh current metadata and history after restoration. Does not grant upload or deletion rights. Ambiguous transport failures must not be replayed automatically.</remarks>
        ''' <exception cref="NotSupportedException">The provider does not implement restoration.</exception>
        Public Overridable Sub RestoreFileVersion(version As DmsFileVersion)
            Throw New NotSupportedException("File-version restoration is not supported by this provider.")
        End Sub

        ''' <summary>Removes one historical version without deleting the current file.</summary>
        ''' <param name="version">A version listed by this provider and account.</param>
        ''' <remarks>Requires separate caller permission and server authorization. Refresh history after success; do not replay ambiguous failures automatically.</remarks>
        ''' <exception cref="NotSupportedException">The provider does not implement individual removal.</exception>
        Public Overridable Sub RemoveFileVersion(version As DmsFileVersion)
            Throw New NotSupportedException("Individual file-version removal is not supported by this provider.")
        End Sub
    End Class
End Namespace
