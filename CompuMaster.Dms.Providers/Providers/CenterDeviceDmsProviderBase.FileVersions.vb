Imports System.Globalization
Imports System.IO
Imports CenterDevice.Rest.Clients.Documents.Metadata
Imports CompuMaster.Dms.Data

Namespace Providers
    Partial Public MustInherit Class CenterDeviceDmsProviderBase
        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsFileVersionHistory As Boolean = True
        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsFileVersionDownload As Boolean = True
        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsFileVersionRestore As Boolean = True
        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsFileVersionRemoval As Boolean = True

        Friend Overridable Function CreateFileVersionBackend() As ICenterDeviceFileVersionBackend
            If IOClient Is Nothing Then Throw New InvalidOperationException("The provider is not authorized.")
            Return New CenterDeviceFileVersionBackend(IOClient)
        End Function

        ''' <inheritdoc/>
        Public Overrides Function ListFileVersions(remoteFile As DmsResourceItem) As List(Of DmsFileVersion)
            If remoteFile Is Nothing Then Throw New ArgumentNullException(NameOf(remoteFile))
            If remoteFile.ItemType <> DmsResourceItem.ItemTypes.File OrElse String.IsNullOrWhiteSpace(remoteFile.ExtendedInfosFileID) Then
                Throw New ArgumentException("A selected file with a stable document ID is required.", NameOf(remoteFile))
            End If
            Try
                Dim backend = CreateFileVersionBackend()
                Dim history = ReadVersionHistory(backend, remoteFile.ExtendedInfosFileID)
                Dim result As New List(Of DmsFileVersion)
                For Each detail In history.VersionDetails
                    Dim uploader = If(String.Equals(detail.Uploader, "not-visible", StringComparison.Ordinal), Nothing, detail.Uploader)
                    Dim snapshot As New DmsFileVersion(remoteFile, detail.Version.Value.ToString(CultureInfo.InvariantCulture), Nothing,
                                                       detail.Size, uploader, Nothing, detail.Title, detail.MimeType, Nothing, detail.VersionDate)
                    snapshot.ProviderState = New CenterDeviceVersionBinding(Me, backend, snapshot.FileID, detail.Version.Value)
                    result.Add(snapshot)
                Next
                Return result
            Catch ex As Exception
                Throw New FileActionFailedException("list versions", remoteFile.FullName, ex)
            End Try
        End Function

        ''' <inheritdoc/>
        Public Overrides Sub DownloadFileVersion(version As DmsFileVersion, destination As Stream)
            If destination Is Nothing Then Throw New ArgumentNullException(NameOf(destination))
            If Not destination.CanWrite Then Throw New ArgumentException("The caller's destination must be writable.", NameOf(destination))
            Dim binding = ValidateVersionBinding(version)
            Try
                binding.Backend.Download(binding.FileID, binding.Version, destination)
            Catch ex As Exception
                Throw New FileActionFailedException("download version", version.FileFullName, ex)
            End Try
        End Sub

        ''' <inheritdoc/>
        ''' <remarks>Restoration creates a new current version without changing the document ID. Ambiguous failures are never retried automatically.</remarks>
        Public Overrides Sub RestoreFileVersion(version As DmsFileVersion)
            Dim binding = ValidateVersionBinding(version)
            Try
                Dim history = ReadVersionHistory(binding.Backend, binding.FileID)
                If Not history.VersionDetails.Any(Function(detail) detail.Version.Value = binding.Version) Then Throw New InvalidOperationException("The selected version is no longer available.")
                binding.Backend.Restore(binding.FileID, binding.Version)
            Catch ex As Exception
                Throw New FileActionFailedException("restore version", version.FileFullName, ex)
            End Try
        End Sub

        ''' <inheritdoc/>
        ''' <remarks>Removes only the selected version and retains at least one version. The server selects the greatest remaining version as current.</remarks>
        Public Overrides Sub RemoveFileVersion(version As DmsFileVersion)
            Dim binding = ValidateVersionBinding(version)
            Try
                Dim history = ReadVersionHistory(binding.Backend, binding.FileID)
                If Not history.VersionDetails.Any(Function(detail) detail.Version.Value = binding.Version) Then Throw New InvalidOperationException("The selected version is no longer available.")
                If history.VersionDetails.Count <= 1 Then Throw New InvalidOperationException("At least one document version must remain.")
                binding.Backend.Remove(binding.FileID, binding.Version)
            Catch ex As Exception
                Throw New FileActionFailedException("remove version", version.FileFullName, ex)
            End Try
        End Sub

        Private Function ValidateVersionBinding(version As DmsFileVersion) As CenterDeviceVersionBinding
            If version Is Nothing Then Throw New ArgumentNullException(NameOf(version))
            Dim binding = TryCast(version.ProviderState, CenterDeviceVersionBinding)
            Dim current = CreateFileVersionBackend()
            If binding Is Nothing OrElse binding.Owner IsNot Me OrElse binding.AuthenticationContext IsNot current.AuthenticationContext OrElse
               Not String.Equals(binding.UserID, current.UserID, StringComparison.Ordinal) OrElse
               Not String.Equals(binding.FileID, version.FileID, StringComparison.Ordinal) OrElse
               Not String.Equals(binding.Version.ToString(CultureInfo.InvariantCulture), version.VersionID, StringComparison.Ordinal) Then
                Throw New ArgumentException("The version snapshot belongs to another provider, account or file.", NameOf(version))
            End If
            Return binding
        End Function

        Private Shared Function ReadVersionHistory(backend As ICenterDeviceFileVersionBackend, fileID As String) As DocumentVersionHistoryMetadata
            Dim history = backend.ReadHistory(fileID)
            If history Is Nothing OrElse Not String.Equals(history.Id, fileID, StringComparison.Ordinal) Then Throw New InvalidDataException("The server returned a different document identity.")
            If history.VersionDetails Is Nothing Then Throw New InvalidDataException("The server did not return visible version history; this does not establish an empty history.")
            If history.VersionDetails.Any(Function(detail) detail Is Nothing OrElse Not detail.Version.HasValue OrElse detail.Version.Value <= 0) OrElse
               history.VersionDetails.Select(Function(detail) detail.Version.Value).Distinct().Count() <> history.VersionDetails.Count Then
                Throw New InvalidDataException("The server returned invalid version identities.")
            End If
            Return history
        End Function

        Private NotInheritable Class CenterDeviceVersionBinding
            Friend ReadOnly Owner As CenterDeviceDmsProviderBase
            Friend ReadOnly Backend As ICenterDeviceFileVersionBackend
            Friend ReadOnly AuthenticationContext As Object
            Friend ReadOnly UserID As String
            Friend ReadOnly FileID As String
            Friend ReadOnly Version As Long
            Friend Sub New(owner As CenterDeviceDmsProviderBase, backend As ICenterDeviceFileVersionBackend, fileID As String, version As Long)
                Me.Owner = owner
                Me.Backend = backend
                Me.AuthenticationContext = backend.AuthenticationContext
                Me.UserID = backend.UserID
                Me.FileID = fileID
                Me.Version = version
            End Sub
        End Class
    End Class

    Friend Interface ICenterDeviceFileVersionBackend
        ReadOnly Property AuthenticationContext As Object
        ReadOnly Property UserID As String
        Function ReadHistory(fileID As String) As DocumentVersionHistoryMetadata
        Sub Download(fileID As String, version As Long, destination As Stream)
        Sub Restore(fileID As String, version As Long)
        Sub Remove(fileID As String, version As Long)
    End Interface

    Friend NotInheritable Class CenterDeviceFileVersionBackend
        Implements ICenterDeviceFileVersionBackend
        Private ReadOnly Client As Global.CenterDevice.IO.IOClientBase
        Private ReadOnly ContextUserID As String
        Friend Sub New(client As Global.CenterDevice.IO.IOClientBase)
            Me.Client = client
            Me.ContextUserID = client.CurrentAuthenticationContextUserID
        End Sub
        Public ReadOnly Property AuthenticationContext As Object Implements ICenterDeviceFileVersionBackend.AuthenticationContext
            Get
                Return Client
            End Get
        End Property
        Public ReadOnly Property UserID As String Implements ICenterDeviceFileVersionBackend.UserID
            Get
                Return ContextUserID
            End Get
        End Property
        Public Function ReadHistory(fileID As String) As DocumentVersionHistoryMetadata Implements ICenterDeviceFileVersionBackend.ReadHistory
            Return Client.ApiClient.Document.GetDocumentVersionHistory(UserID, fileID)
        End Function
        Public Sub Download(fileID As String, version As Long, destination As Stream) Implements ICenterDeviceFileVersionBackend.Download
            Using source = Client.ApiClient.Document.DownloadDocument(UserID, fileID, version, Nothing)
                source.CopyTo(destination)
            End Using
        End Sub
        Public Sub Restore(fileID As String, version As Long) Implements ICenterDeviceFileVersionBackend.Restore
            Client.ApiClient.Document.RevertDocumentVersion(UserID, fileID, version)
        End Sub
        Public Sub Remove(fileID As String, version As Long) Implements ICenterDeviceFileVersionBackend.Remove
            Client.ApiClient.Document.RemoveDocumentVersions(UserID, fileID, {version})
        End Sub
    End Class
End Namespace
