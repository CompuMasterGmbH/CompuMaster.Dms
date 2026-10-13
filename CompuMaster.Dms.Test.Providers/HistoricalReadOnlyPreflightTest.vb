Option Explicit On
Option Strict On

Imports NUnit.Framework
Imports CenterDevice.Rest.Clients.Collections
Imports CenterDevice.Rest.Clients.Folders
Imports CenterDevice.Rest.Clients.Documents
Imports CenterDevice.Rest.Clients.Documents.Metadata

<TestFixture>
Public NotInheritable Class HistoricalReadOnlyPreflightTest
    Private Shared ReadOnly Witness As String = ".compumaster-dms-" & New String("a"c, 32)

    <TestCase(" "), TestCase(".compumaster-dms-"), TestCase(".compumaster-dms-*"), TestCase("../outside"), TestCase("/ZZZ_UnitTests_Dms_TestFolder/name"), TestCase(".compumaster-dms-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"), TestCase(".compumaster-dms-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/child"), TestCase(".compumaster-dms-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" & vbLf)>
    Public Sub RejectsPathsPrefixesWhitespaceAndUnboundedWitnesses(value As String)
        Assert.Throws(Of InvalidOperationException)(Sub() HistoricalReadOnlyPreflight.ValidateWitness(value))
    End Sub

    <Test>
    Public Sub AcceptsOnlyAnOptionalExactLowercaseWitness()
        Assert.That(HistoricalReadOnlyPreflight.ValidateWitness(Nothing), [Is].Null)
        Assert.That(HistoricalReadOnlyPreflight.ValidateWitness(""), [Is].Null)
        Assert.That(HistoricalReadOnlyPreflight.ValidateWitness(Witness), [Is].EqualTo(Witness))
    End Sub

    <Test>
    Public Sub MissingIndexedRootsNeverReadDescendantsOrImplyGlobalAbsence()
        Dim fake As New ReadFixture With {.Roots = New List(Of Collection) From {Root("unrelated-root", "foreign-id")}}
        fake.Run()
        Assert.That(fake.Calls, [Is].Empty)
        Assert.That(fake.Output.Where(Function(line) line.Contains("indexed root absent")).Count(), [Is].EqualTo(2))
        Assert.That(fake.Output.All(Function(line) Not line.Contains("foreign")), [Is].True)
        Assert.That(fake.Output.Last(), Does.Contain("global/trash/orphan absence unverified"))
    End Sub

    <Test>
    Public Sub InvalidWitnessFailsBeforeAnyReadDelegate()
        Dim fake As New ReadFixture
        Assert.Throws(Of InvalidOperationException)(Sub() fake.Run("../other"))
        Assert.That(fake.Calls, [Is].Empty)
    End Sub

    <Test>
    Public Sub AmbiguousRootNeverChoosesTheFirstIdentity()
        Dim fake As New ReadFixture
        fake.Roots.Add(Root(HistoricalReadOnlyPreflight.CopyRoot, "second-id"))
        Assert.Throws(Of IO.InvalidDataException)(Sub() fake.Run())
        Assert.That(fake.Calls, [Is].Empty)
        Assert.That(fake.Output.Single(), Does.Contain("collision=True"))
    End Sub

    <TestCase(True), TestCase(False)>
    Public Sub ChangedRootIdentityOrNameStopsBeforeChildRead(changeId As Boolean)
        Dim fake As New ReadFixture
        fake.CollectionRead = Function(id) Root(If(changeId, HistoricalReadOnlyPreflight.CopyRoot, "foreign-root"), If(changeId, "foreign-id", id))
        Assert.Throws(Of IO.InvalidDataException)(Sub() fake.Run())
        Assert.That(fake.Calls, Has.Count.EqualTo(1))
    End Sub

    <Test>
    Public Sub OnlyFixedRootAndNestedParentsAreInspected()
        Dim fake As New ReadFixture
        fake.Roots.Add(Root(HistoricalReadOnlyPreflight.FixtureRoot, "fixture-id"))
        fake.FolderPages("copy-id/none") = New List(Of Folder) From {Child("copy-sub-target", "copy-child", "copy-id", "none"), Child("unrelated", "unrelated", "copy-id", "none")}
        fake.FolderPages("fixture-id/none") = New List(Of Folder) From {Child("Folder", "fixture-child", "fixture-id", "none")}
        fake.FolderPages("fixture-id/fixture-child") = New List(Of Folder) From {Child("Sub", "fixture-sub", "fixture-id", "fixture-child")}
        fake.Run()
        Assert.That(fake.Calls, [Is].EqualTo({"collection:copy-id", "documents:copy-id/none", "folders:copy-id/none", "documents:copy-id/copy-child",
                    "collection:fixture-id", "documents:fixture-id/none", "folders:fixture-id/none", "documents:fixture-id/fixture-child", "folders:fixture-id/fixture-child", "documents:fixture-id/fixture-sub"}))
        Assert.That(fake.Output.Any(Function(line) line.Contains("duplicate suffix unknown")), [Is].True)
    End Sub

    <TestCase("wrong-collection", "none"), TestCase("copy-id", "wrong-parent")>
    Public Sub InconsistentFolderMetadataStopsBeforeItsDocumentRead(collection As String, parent As String)
        Dim fake As New ReadFixture
        fake.FolderPages("copy-id/none") = New List(Of Folder) From {Child("copy-sub-target", "child-id", collection, parent)}
        Assert.Throws(Of IO.InvalidDataException)(Sub() fake.Run())
        Assert.That(fake.Calls.Any(Function(value) value.Contains("documents:copy-id/child-id")), [Is].False)
    End Sub

    <Test>
    Public Sub FolderCollisionDoesNotReadEitherCandidate()
        Dim fake As New ReadFixture
        fake.FolderPages("copy-id/none") = New List(Of Folder) From {Child("copy-sub-target", "one", "copy-id", "none"), Child("copy-sub-target", "two", "copy-id", "none")}
        Assert.Throws(Of IO.InvalidDataException)(Sub() fake.Run())
        Assert.That(fake.Calls.Where(Function(value) value.StartsWith("documents:")).Count(), [Is].EqualTo(1))
    End Sub

    <TestCase(0, True), TestCase(1, False), TestCase(1000, False)>
    Public Sub IndexedAbsenceRequiresACompleteBoundedPage(hits As Integer, expectedAbsent As Boolean)
        Dim fake As New ReadFixture
        fake.DocumentPages("copy-id/none") = New DocumentSearchResults(Of DocumentFullMetadata) With {.Hits = hits, .Documents = New List(Of DocumentFullMetadata)}
        fake.Run(Witness)
        Dim observation = fake.Output.Single(Function(line) line.Contains("selection=TemporaryWitness; exactMatches="))
        Assert.That(observation, Does.Contain("indexedAbsence=" & expectedAbsent.ToString()))
        Assert.That(observation, Does.Contain("globalAbsenceUnverified=True"))
    End Sub

    <Test>
    Public Sub TwentyFirstRowNeverBecomesASelectedCandidateOrACompletePage()
        Dim fake As New ReadFixture
        Dim documents = Enumerable.Range(0, 20).Select(Function(index) New DocumentFullMetadata With {.Filename = "unrelated-" & index.ToString()}).ToList()
        documents.Add(New DocumentFullMetadata With {.Filename = Witness, .Id = "must-not-report"})
        fake.DocumentPages("copy-id/none") = New DocumentSearchResults(Of DocumentFullMetadata) With {.Hits = 21, .Documents = documents}
        fake.Run(Witness)
        Assert.That(String.Join(vbLf, fake.Output), Does.Contain("pageComplete=False"))
        Assert.That(String.Join(vbLf, fake.Output), Does.Contain("selection=TemporaryWitness; exactMatches=0; collision=False; indexedAbsence=False"))
        Assert.That(String.Join(vbLf, fake.Output), Does.Not.Contain("must-not-report"))
    End Sub

    <TestCase(-1, 0), TestCase(0, 1), TestCase(22, 22)>
    Public Sub InvalidOrOversizedPageFailsWithoutInferringAbsence(hits As Integer, count As Integer)
        Dim fake As New ReadFixture
        fake.DocumentPages("copy-id/none") = New DocumentSearchResults(Of DocumentFullMetadata) With {.Hits = hits, .Documents = Enumerable.Range(0, count).Select(Function(index) New DocumentFullMetadata()).ToList()}
        Assert.Throws(Of IO.InvalidDataException)(Sub() fake.Run())
        Assert.That(fake.Output.Any(Function(line) line.Contains("indexedAbsence=")), [Is].False)
    End Sub

    <Test>
    Public Sub ExactSelectionReportsCollisionsAndReferenceFactsWithoutRawValues()
        Dim fake As New ReadFixture
        Dim first As New DocumentFullMetadata With {.Id = "secret-document-id", .Filename = Witness, .Owner = "secret-owner", .Uploader = "secret-uploader", .Size = 7,
            .Collections = New SharingInfo With {.Visible = New List(Of String) From {"copy-id", "foreign-id"}, .NotVisibleCount = 1}, .Folders = New List(Of String)}
        Dim second As New DocumentFullMetadata With {.Id = "second-secret-id", .Filename = Witness}
        fake.DocumentPages("copy-id/none") = New DocumentSearchResults(Of DocumentFullMetadata) With {.Hits = 3, .Documents = New List(Of DocumentFullMetadata) From {first, second, New DocumentFullMetadata With {.Filename = Witness & "extra"}}}
        fake.Run(Witness)
        Dim output = String.Join(vbLf, fake.Output)
        Assert.That(output, Does.Contain("selection=TemporaryWitness; exactMatches=2; collision=True"))
        Assert.That(output, Does.Contain("sevenByteSize=True; selectedCollectionConfirmed=True; selectedFolderConfirmed=True; visibleCollectionReferences=2; hiddenCollectionReferences=1"))
        Assert.That(output, Does.Contain("selectedCollectionConfirmed=False; selectedFolderConfirmed=False"))
        For Each raw In {Witness, "secret-document-id", "secret-owner", "secret-uploader", "second-secret-id", "foreign-id"}
            Assert.That(output, Does.Not.Contain(raw))
        Next
        Assert.That(output, Does.Contain("ownershipUnproven=True; contentNotRead=True"))
    End Sub

    <Test>
    Public Sub FingerprintsAreStableAndDoNotPrintControlCharacters()
        Dim raw = "secret" & vbCrLf & "injected"
        Dim hashed = HistoricalReadOnlyPreflight.Fingerprint(raw)
        Assert.That(hashed, Does.Match("\A[0-9a-f]{64}\z"))
        Assert.That(hashed, [Is].EqualTo(HistoricalReadOnlyPreflight.Fingerprint(raw)))
        Assert.That(HistoricalReadOnlyPreflight.Fingerprint(Nothing), [Is].EqualTo("unknown"))
    End Sub

    Private Shared Function Root(name As String, id As String) As Collection
        Return New Collection With {.Name = name, .Id = id, .Owner = "secret-root-owner"}
    End Function

    Private Shared Function Child(name As String, id As String, collection As String, parent As String) As Folder
        Return New Folder With {.Name = name, .Id = id, .Collection = collection, .Parent = parent}
    End Function

    Private NotInheritable Class ReadFixture
        Friend Roots As New List(Of Collection) From {Root(HistoricalReadOnlyPreflight.CopyRoot, "copy-id")}
        Friend CollectionRead As Func(Of String, Collection)
        Friend FolderPages As New Dictionary(Of String, List(Of Folder))
        Friend DocumentPages As New Dictionary(Of String, DocumentSearchResults(Of DocumentFullMetadata))
        Friend Calls As New List(Of String)
        Friend Output As New List(Of String)

        Friend Sub Run(Optional witness As String = Nothing)
            HistoricalReadOnlyPreflight.Inspect(Roots, witness,
                Function(id)
                    Calls.Add("collection:" & id)
                    If CollectionRead IsNot Nothing Then Return CollectionRead(id)
                    Return Roots.Single(Function(item) item.Id = id)
                End Function,
                Function(collectionId, parentId)
                    Dim key = collectionId & "/" & If(parentId, "none")
                    Calls.Add("folders:" & key)
                    Return If(FolderPages.ContainsKey(key), FolderPages(key), New List(Of Folder))
                End Function,
                Function(collectionId, folderId)
                    Dim key = collectionId & "/" & If(folderId, "none")
                    Calls.Add("documents:" & key)
                    Return If(DocumentPages.ContainsKey(key), DocumentPages(key), New DocumentSearchResults(Of DocumentFullMetadata) With {.Hits = 0, .Documents = New List(Of DocumentFullMetadata)})
                End Function, AddressOf Output.Add)
        End Sub
    End Class
End Class
