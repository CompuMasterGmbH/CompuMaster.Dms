Option Explicit On
Option Strict On

Imports System.IO
Imports CenterDevice.Rest.Clients.Documents
Imports CenterDevice.Rest.Clients.Documents.Metadata
Imports CompuMaster.Dms.Data

Namespace Providers
    'Consumes an authoritative copy-upload result before any replacement or rename.
    'A failed/uncertain write is never replayed or reconciled by deleting a name match.
    Friend NotInheritable Class CenterDeviceCopyUploadResult
        Friend Shared Function Create(upload As Func(Of UploadDocumentResponse), metadata As Func(Of String, DocumentFullMetadata), name As String,
                                      sourcePath As String, destinationPath As String, protectedIds As IEnumerable(Of String), collectionId As String, folderId As String) As DocumentFullMetadata
            Dim phase As String = "Upload"
            Dim createdId As String = Nothing
            Try
                Dim response = upload()
                phase = "UploadIdentity"
                If response Is Nothing OrElse String.IsNullOrWhiteSpace(response.Id) OrElse
                    (protectedIds IsNot Nothing AndAlso protectedIds.Contains(response.Id, StringComparer.Ordinal)) Then
                    Throw New InvalidDataException("The copy upload did not return a distinct document identity.")
                End If
                createdId = response.Id
                phase = "FolderAttachment"
                If response.HasFailedFolders Then Throw New InvalidDataException("The copy upload reported a failed destination-folder attachment.")
                phase = "Metadata"
                Dim document = metadata(createdId)
                If document Is Nothing OrElse Not String.Equals(document.Id, createdId, StringComparison.Ordinal) OrElse
                    Not String.Equals(document.Filename, name, StringComparison.Ordinal) Then
                    Throw New InvalidDataException("The created copy metadata does not match its uploaded identity and name.")
                End If
                If Not String.IsNullOrEmpty(collectionId) AndAlso (document.Collections Is Nothing OrElse document.Collections.Visible Is Nothing OrElse
                    Not document.Collections.Visible.Contains(collectionId, StringComparer.Ordinal)) Then
                    Throw New InvalidDataException("The created copy is not confirmed in its destination collection.")
                End If
                If Not String.IsNullOrEmpty(folderId) AndAlso (document.Folders Is Nothing OrElse Not document.Folders.Contains(folderId, StringComparer.Ordinal)) Then
                    Throw New InvalidDataException("The created copy is not confirmed in its destination folder.")
                End If
                Return document
            Catch original As Exception
                Dim failure As Exception
                If TypeOf original Is DmsUserAuthenticationException OrElse TypeOf original Is OperationCanceledException Then
                    'Keep the existing authentication/cancellation control-flow boundary.
                    failure = original
                Else
                    failure = New FileActionFailedException("copy", sourcePath, destinationPath, original)
                End If
                failure.Data("CompuMaster.Dms.Copy.Phase") = phase
                failure.Data("CompuMaster.Dms.Copy.TemporaryName") = name
                failure.Data("CompuMaster.Dms.Copy.RequiresReconciliation") = True
                If createdId IsNot Nothing Then failure.Data("CompuMaster.Dms.Copy.DocumentId") = createdId
                If failure Is original Then Throw
                Throw failure
            End Try
        End Function
    End Class
End Namespace
