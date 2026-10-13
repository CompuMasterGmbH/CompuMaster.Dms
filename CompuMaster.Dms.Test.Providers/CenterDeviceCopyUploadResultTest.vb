Option Explicit On
Option Strict On

Imports System.IO
Imports CenterDevice.Rest.Clients.Documents
Imports CenterDevice.Rest.Clients.Documents.Metadata
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class CenterDeviceCopyUploadResultTest
    Private Const CopyName As String = ".compumaster-dms-fixture"
    Private Shared Function CreatedMetadata(id As String) As DocumentFullMetadata
        Return New DocumentFullMetadata With {.Id = id, .Filename = CopyName,
            .Collections = New SharingInfo With {.Visible = New List(Of String) From {"collection"}}, .Folders = New List(Of String) From {"folder"}}
    End Function

    <Test>
    Public Sub SuccessfulUploadResolvesAuthoritativeIdWhenNameListingIsMissing()
        Dim writes As Integer
        Dim reads As New List(Of String)
        Dim created = CenterDeviceCopyUploadResult.Create(
            Function()
                writes += 1
                Return New UploadDocumentResponse With {.Id = "created"}
            End Function,
            Function(id)
                reads.Add(id)
                If id <> "created" Then Throw New System.IO.FileNotFoundException("The copied name is absent from the stale listing.")
                Return CreatedMetadata(id)
            End Function, CopyName, "source", "target", {"source-id", "target-id"}, "collection", "folder")
        Assert.That(created.Id, [Is].EqualTo("created"))
        Assert.That(writes, [Is].EqualTo(1), "A copy write must never replay.")
        Assert.That(reads, [Is].EqualTo(New String() {"created"}))
    End Sub

    <TestCase(Nothing), TestCase(""), TestCase("source-id"), TestCase("target-id")>
    Public Sub MissingOrProtectedUploadIdentityNeverAdmitsMetadataOrReplacement(id As String)
        Dim reads As Integer
        Dim failure = Assert.Throws(Of FileActionFailedException)(Sub()
            CenterDeviceCopyUploadResult.Create(Function() New UploadDocumentResponse With {.Id = id},
                Function(value)
                    reads += 1
                    Return CreatedMetadata(value)
                End Function, CopyName, "source", "target", {"source-id", "target-id"}, "collection", "folder")
        End Sub)
        Assert.That(reads, [Is].Zero)
        Assert.That(failure.InnerException, [Is].TypeOf(Of InvalidDataException))
    End Sub

    <Test>
    Public Sub FailedFolderAttachmentIsNotSuccessfulCopyOrMetadataLookup()
        Dim reads As Integer
        Dim failure = Assert.Throws(Of FileActionFailedException)(Sub()
            CenterDeviceCopyUploadResult.Create(Function() New UploadDocumentResponse With {
                .Id = "created", .UploadActions = New UploadActions With {.AddToFolder = New AddToFolder With {.Failed = New List(Of String) From {"folder"}}}},
                Function(id)
                    reads += 1
                    Return CreatedMetadata(id)
                End Function, CopyName, "source", "target", {"source-id", "target-id"}, "collection", "folder")
        End Sub)
        Assert.That(reads, [Is].Zero)
        Assert.That(failure.Data("CompuMaster.Dms.Copy.DocumentId"), [Is].EqualTo("created"))
        Assert.That(failure.Data("CompuMaster.Dms.Copy.Phase"), [Is].EqualTo("FolderAttachment"))
    End Sub

    <Test>
    Public Sub UnknownUploadCommitmentRetainsOriginalFailureAndNeverReadsOrReplays()
        Dim writes As Integer
        Dim reads As Integer
        Dim original As New System.Net.Http.HttpRequestException("Fixed HTTP 500 fixture failure.")
        Dim failure = Assert.Throws(Of FileActionFailedException)(Sub()
            CenterDeviceCopyUploadResult.Create(
                Function() As UploadDocumentResponse
                    writes += 1
                    Throw original
                End Function,
                Function(id)
                    reads += 1
                    Return CreatedMetadata(id)
                End Function, CopyName, "source", "target", {"source-id", "target-id"}, "collection", "folder")
        End Sub)
        Assert.That(writes, [Is].EqualTo(1))
        Assert.That(reads, [Is].Zero)
        Assert.That(failure.InnerException, [Is].SameAs(original))
        Assert.That(failure.Data("CompuMaster.Dms.Copy.Phase"), [Is].EqualTo("Upload"))
        Assert.That(failure.Data.Contains("CompuMaster.Dms.Copy.DocumentId"), [Is].False)
    End Sub

    <Test>
    Public Sub MetadataFailureRetainsCreatedIdWithoutAnotherWrite()
        Dim writes As Integer
        Dim original As New System.IO.FileNotFoundException("Fixed ID lookup failure.")
        Dim failure = Assert.Throws(Of FileActionFailedException)(Sub()
            CenterDeviceCopyUploadResult.Create(
                Function()
                    writes += 1
                    Return New UploadDocumentResponse With {.Id = "created"}
                End Function,
                Function(id) As DocumentFullMetadata
                    Assert.That(id, [Is].EqualTo("created"))
                    Throw original
                End Function, CopyName, "source", "target", {"source-id", "target-id"}, "collection", "folder")
        End Sub)
        Assert.That(writes, [Is].EqualTo(1))
        Assert.That(failure.InnerException, [Is].SameAs(original))
        Assert.That(failure.Data("CompuMaster.Dms.Copy.DocumentId"), [Is].EqualTo("created"))
        Assert.That(failure.Data("CompuMaster.Dms.Copy.TemporaryName"), [Is].EqualTo(CopyName))
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub AuthenticationAndCancellationRetainTheirTypedBoundary(cancellation As Boolean)
        Dim writes As Integer
        Dim original As Exception = If(cancellation, CType(New OperationCanceledException("Fixed cancellation."), Exception),
            New DmsUserAuthenticationException("Fixed authentication failure."))
        Dim failure = Assert.Throws(original.GetType(), Sub()
            CenterDeviceCopyUploadResult.Create(
                Function() As UploadDocumentResponse
                    writes += 1
                    Throw original
                End Function, Function(id) CreatedMetadata(id), CopyName, "source", "target", {"source-id", "target-id"}, "collection", "folder")
        End Sub)
        Assert.That(writes, [Is].EqualTo(1))
        Assert.That(failure, [Is].SameAs(original))
        Assert.That(failure.Data("CompuMaster.Dms.Copy.Phase"), [Is].EqualTo("Upload"))
    End Sub

    <TestCase("id"), TestCase("name"), TestCase("folder"), TestCase("collection"), TestCase("missing")>
    Public Sub InconsistentMetadataDoesNotAuthorizeReplacement(field As String)
        Dim failure = Assert.Throws(Of FileActionFailedException)(Sub()
            CenterDeviceCopyUploadResult.Create(Function() New UploadDocumentResponse With {.Id = "created"},
                Function(id)
                    Dim result = CreatedMetadata(id)
                    Select Case field
                        Case "id" : result.Id = "other"
                        Case "name" : result.Filename = "other"
                        Case "folder" : result.Folders.Clear()
                        Case "collection" : result.Collections.Visible.Clear()
                        Case "missing" : result = Nothing
                    End Select
                    Return result
                End Function, CopyName, "source", "target", {"source-id", "target-id"}, "collection", "folder")
        End Sub)
        Assert.That(failure.InnerException, [Is].TypeOf(Of InvalidDataException))
        Assert.That(failure.Data("CompuMaster.Dms.Copy.Phase"), [Is].EqualTo("Metadata"))
    End Sub
End Class
