Option Explicit On
Option Strict On

Imports CenterDevice.Rest.Clients.Collections
Imports CenterDevice.Rest.Clients.Folders
Imports CenterDevice.Rest.Clients.Documents
Imports CenterDevice.Rest.Clients.Documents.Metadata
Imports System.Globalization
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.RegularExpressions

'Only fixed historical scopes and GET-style delegates are available to this inspector.
Friend NotInheritable Class HistoricalReadOnlyPreflight
    Friend Const CopyRoot As String = "ZZZ_UnitTests_Dms_TestFolder"
    Friend Const FixtureRoot As String = "ZZZ_UnitTests_CM.Dms"
    Friend Const MaximumDocuments As Integer = 20
    Private Shared ReadOnly WitnessPattern As New Regex("\A\.compumaster-dms-[0-9a-f]{32}\z", RegexOptions.CultureInvariant)

    Friend Shared Function ValidateWitness(value As String) As String
        If value Is Nothing OrElse value = "" Then Return Nothing
        If Not WitnessPattern.IsMatch(value) Then Throw New InvalidOperationException("Invalid exact historical temporary-name witness.")
        Return value
    End Function

    Friend Shared Function Fingerprint(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then Return "unknown"
        Using algorithm = SHA256.Create()
            Return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant()
        End Using
    End Function

    Friend Shared Sub Inspect(collections As IEnumerable(Of Collection), witness As String,
                              readCollection As Func(Of String, Collection),
                              readFolders As Func(Of String, String, List(Of Folder)),
                              readDocuments As Func(Of String, String, DocumentSearchResults(Of DocumentFullMetadata)),
                              writeObservation As Action(Of String))
        witness = ValidateWitness(witness)
        If collections Is Nothing Then Throw New IO.InvalidDataException("Collection inventory unavailable.")
        For Each rootName In {CopyRoot, FixtureRoot}
            Dim matches = collections.Where(Function(item) item IsNot Nothing AndAlso String.Equals(item.Name, rootName, StringComparison.Ordinal)).Take(2).ToArray()
            writeObservation("Historical root=" & rootName & "; indexedMatches=" & matches.Length.ToString(CultureInfo.InvariantCulture) & "; collision=" & (matches.Length > 1).ToString() & ".")
            If matches.Length = 0 Then
                writeObservation("Historical root=" & rootName & "; indexed root absent; global/trash/orphan absence unverified; no descendant read.")
                Continue For
            End If
            If matches.Length <> 1 OrElse String.IsNullOrWhiteSpace(matches(0).Id) Then Throw New IO.InvalidDataException("Historical root is ambiguous or lacks identity.")
            Dim collection = readCollection(matches(0).Id)
            If collection Is Nothing OrElse Not String.Equals(collection.Id, matches(0).Id, StringComparison.Ordinal) OrElse
                Not String.Equals(collection.Name, rootName, StringComparison.Ordinal) Then Throw New IO.InvalidDataException("Historical root identity/name changed.")
            writeObservation("Historical root=" & rootName & "; idFingerprint=" & Fingerprint(collection.Id) & "; ownerFingerprint=" & Fingerprint(collection.Owner) & "; shared=" & collection.IsShared.ToString() & "; ownershipUnproven=True.")
            InspectParent(rootName, collection.Id, Nothing, witness, readDocuments, writeObservation)
            Dim firstChild = If(rootName = CopyRoot, "copy-sub-target", "Folder")
            Dim folder = SelectFolder(rootName & "/" & firstChild, collection.Id, Nothing, firstChild, readFolders, writeObservation)
            If folder Is Nothing Then Continue For
            InspectParent(rootName & "/" & firstChild, collection.Id, folder.Id, witness, readDocuments, writeObservation)
            If rootName = FixtureRoot Then
                Dim nested = SelectFolder(rootName & "/Folder/Sub", collection.Id, folder.Id, "Sub", readFolders, writeObservation)
                If nested IsNot Nothing Then InspectParent(rootName & "/Folder/Sub", collection.Id, nested.Id, witness, readDocuments, writeObservation)
            End If
        Next
        writeObservation("Historical limits: indexed observations only; global/trash/orphan absence unverified; duplicate suffix unknown; loop/SDK-parent consistency unresolved; no ownership or cleanup authorization; content not read (copy fixture is seven bytes).")
    End Sub

    Private Shared Function SelectFolder(label As String, collectionId As String, parentId As String, name As String,
                                         readFolders As Func(Of String, String, List(Of Folder)), writeObservation As Action(Of String)) As Folder
        Dim folders = readFolders(collectionId, parentId)
        If folders Is Nothing Then Throw New IO.InvalidDataException("Folder inventory unavailable.")
        Dim matches = folders.Where(Function(item) item IsNot Nothing AndAlso String.Equals(item.Name, name, StringComparison.Ordinal)).Take(2).ToArray()
        writeObservation("Historical parent=" & label & "; indexedMatches=" & matches.Length.ToString(CultureInfo.InvariantCulture) & "; collision=" & (matches.Length > 1).ToString() & ".")
        If matches.Length = 0 Then
            writeObservation("Historical parent=" & label & "; indexed folder absent; no descendant read; global/trash/orphan absence unverified.")
            Return Nothing
        End If
        If matches.Length <> 1 Then Throw New IO.InvalidDataException("Historical folder is ambiguous.")
        Dim folder = matches(0)
        Dim expectedParent = If(parentId, CenterDevice.Rest.RestApiConstants.NONE)
        If String.IsNullOrWhiteSpace(folder.Id) OrElse Not String.Equals(folder.Collection, collectionId, StringComparison.Ordinal) OrElse
            Not String.Equals(folder.Parent, expectedParent, StringComparison.Ordinal) Then Throw New IO.InvalidDataException("Historical folder parent/collection is inconsistent.")
        writeObservation("Historical parent=" & label & "; idFingerprint=" & Fingerprint(folder.Id) & "; collectionFingerprint=" & Fingerprint(collectionId) & "; parentFingerprint=" & Fingerprint(expectedParent) & "; ownershipUnproven=True.")
        Return folder
    End Function

    Private Shared Sub InspectParent(label As String, collectionId As String, folderId As String, witness As String,
                                     readDocuments As Func(Of String, String, DocumentSearchResults(Of DocumentFullMetadata)), writeObservation As Action(Of String))
        Dim page = readDocuments(collectionId, folderId)
        If page Is Nothing OrElse page.Documents Is Nothing OrElse page.Hits < 0 OrElse page.Documents.Count > MaximumDocuments + 1 OrElse page.Hits < page.Documents.Count Then
            Throw New IO.InvalidDataException("Invalid bounded document inventory.")
        End If
        Dim complete = page.Hits = page.Documents.Count AndAlso page.Documents.Count <= MaximumDocuments
        writeObservation("Historical parent=" & label & "; indexedHits=" & page.Hits.ToString(CultureInfo.InvariantCulture) & "; observed=" & Math.Min(MaximumDocuments, page.Documents.Count).ToString(CultureInfo.InvariantCulture) & "; pageComplete=" & complete.ToString() & ".")
        If Not label.StartsWith(CopyRoot, StringComparison.Ordinal) Then
            writeObservation("Historical parent=" & label & "; duplicate suffix unknown; no prefix/name selection or content read; ownershipUnproven=True.")
            Return
        End If
        For Each itemLabel In {"Source", "Target", "TemporaryWitness"}
            Dim name As String
            Select Case itemLabel
                Case "Source" : name = "copy-source-test-file.tmp"
                Case "Target" : name = "copy-target-test-file.tmp"
                Case Else : name = witness
            End Select
            If name Is Nothing Then
                writeObservation("Historical parent=" & label & "; selection=" & itemLabel & "; witness not supplied; exact-match assessment unavailable.")
                Continue For
            End If
            Dim selected = page.Documents.Take(MaximumDocuments).Where(Function(item) item IsNot Nothing AndAlso String.Equals(item.Filename, name, StringComparison.Ordinal)).ToArray()
            writeObservation("Historical parent=" & label & "; selection=" & itemLabel & "; exactMatches=" & selected.Length.ToString(CultureInfo.InvariantCulture) & "; collision=" & (selected.Length > 1).ToString() & "; indexedAbsence=" & (complete AndAlso selected.Length = 0).ToString() & "; globalAbsenceUnverified=True.")
            For Each document In selected
                Dim collectionReferences = If(document.Collections?.Visible, New List(Of String))
                Dim folderReferences = If(document.Folders, New List(Of String))
                Dim collectionConfirmed = document.Collections IsNot Nothing AndAlso collectionReferences.Contains(collectionId, StringComparer.Ordinal)
                Dim folderConfirmed = If(folderId Is Nothing, document.Folders IsNot Nothing AndAlso folderReferences.Count = 0,
                    folderReferences.Contains(folderId, StringComparer.Ordinal))
                writeObservation("Historical parent=" & label & "; selection=" & itemLabel & "; idFingerprint=" & Fingerprint(document.Id) & "; ownerFingerprint=" & Fingerprint(document.Owner) & "; uploaderFingerprint=" & Fingerprint(document.Uploader) &
                                 "; size=" & document.Size.ToString(CultureInfo.InvariantCulture) & "; sevenByteSize=" & (document.Size = 7).ToString() &
                                 "; selectedCollectionConfirmed=" & collectionConfirmed.ToString() & "; selectedFolderConfirmed=" & folderConfirmed.ToString() &
                                 "; visibleCollectionReferences=" & collectionReferences.Count.ToString(CultureInfo.InvariantCulture) & "; hiddenCollectionReferences=" & If(document.Collections Is Nothing, "unknown", document.Collections.NotVisibleCount.ToString(CultureInfo.InvariantCulture)) &
                                 "; folderReferences=" & folderReferences.Count.ToString(CultureInfo.InvariantCulture) & "; ownershipUnproven=True; contentNotRead=True.")
            Next
        Next
    End Sub
End Class
