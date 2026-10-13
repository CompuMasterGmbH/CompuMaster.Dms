Option Explicit On
Option Strict On

Imports System.IO
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports CenterDevice.Rest.Clients.Documents
Imports CenterDevice.Rest.Clients.Documents.Metadata
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class CenterDeviceCopyProviderTransactionTest
    <TestCase(0), TestCase(1), TestCase(2)>
    Public Sub ActualOverwriteUsesCreatedIdWhenItsNameIsAbsentFromListing(folderDepth As Integer)
        Dim fixture As New TransactionFixture(folderDepth, True)
        fixture.RunCopyCase(True)
        Assert.That(fixture.UploadCalls, [Is].EqualTo(1))
        Assert.That(fixture.UploadCollection, [Is].EqualTo("collection"))
        Assert.That(fixture.UploadFolder, [Is].EqualTo(If(folderDepth = 0, Nothing, "folder-" & folderDepth)))
        Assert.That(fixture.MetadataReads, [Is].EqualTo(New String() {"created"}))
        Assert.That(fixture.Events, [Is].EqualTo(New String() {"upload", "rename:target:backup", "rename:created:final", "delete:target"}))
        Assert.That(fixture.Deleted, Does.Not.Contain("source"))
    End Sub

    <TestCase("partial"), TestCase("protected"), TestCase("mismatch"), TestCase("metadata-failure"), TestCase("upload-failure")>
    Public Sub ActualOverwriteNeverRenamesOrDeletesAfterUnconfirmedUpload(mode As String)
        Dim fixture As New TransactionFixture(2, True) With {.Mode = mode}
        Dim failure = Assert.Throws(Of FileActionFailedException)(Sub() fixture.RunCopyCase(True))
        Assert.That(fixture.UploadCalls, [Is].EqualTo(1))
        Assert.That(fixture.Events, [Is].EqualTo(New String() {"upload"}))
        Assert.That(fixture.Deleted, [Is].Empty)
        Assert.That(failure.InnerException, [Is].Not.Null)
        If mode = "partial" OrElse mode = "protected" OrElse mode = "upload-failure" Then Assert.That(fixture.MetadataReads, [Is].Empty)
    End Sub

    <Test>
    Public Sub PromotionFailureRestoresOriginalAndDeletesOnlyConfirmedNewCopy()
        Dim fixture As New TransactionFixture(1, True) With {.Mode = "promotion"}
        Assert.Throws(Of FileActionFailedException)(Sub() fixture.RunCopyCase(True))
        Assert.That(fixture.Events, [Is].EqualTo(New String() {"upload", "rename:target:backup", "rename:created:final", "rename:target:final", "delete:created"}))
        Assert.That(fixture.Deleted, [Is].EqualTo(New String() {"created"}))
    End Sub

    <Test>
    Public Sub NewDestinationUsesSameConfirmedResultWithoutReplacementMutations()
        Dim fixture As New TransactionFixture(1, False)
        fixture.RunCopyCase(False)
        Assert.That(fixture.UploadCalls, [Is].EqualTo(1))
        Assert.That(fixture.Events, [Is].EqualTo(New String() {"upload"}))
        Assert.That(fixture.Deleted, [Is].Empty)
    End Sub

    <Test>
    Public Sub DeniedOverwriteNeverAdmitsCopyUpload()
        Dim fixture As New TransactionFixture(1, True)
        Assert.Throws(Of CompuMaster.Dms.Data.FileAlreadyExistsException)(Sub() fixture.RunCopyCase(False))
        Assert.That(fixture.UploadCalls, [Is].Zero)
        Assert.That(fixture.Events, [Is].Empty)
    End Sub

    Private Class FixtureIo
        Inherits Global.CenterDevice.IO.IOClientBase
        Public Sub New()
            MyBase.New(Nothing, "fixture-user")
        End Sub
    End Class

    Private Class TransactionFixture
        Inherits ScopevisioTeamworkDmsProvider
        Friend ReadOnly Events As New List(Of String)
        Friend ReadOnly Deleted As New List(Of String)
        Friend ReadOnly MetadataReads As New List(Of String)
        Friend Mode As String
        Friend UploadCalls As Integer
        Friend UploadCollection As String
        Friend UploadFolder As String
        Private ReadOnly Documents As New Dictionary(Of String, DocumentFullMetadata)(StringComparer.Ordinal)
        Private ReadOnly Parent As Global.CenterDevice.IO.DirectoryInfo
        Private ReadOnly Source As Global.CenterDevice.IO.FileInfo
        Private Const FinalName As String = "target.bin"

        Public Sub New(depth As Integer, existingTarget As Boolean)
            Dim io As New FixtureIo()
            Me.IOClient = io
            Dim parentDirectory As New Global.CenterDevice.IO.DirectoryInfo(io, io.RootDirectory,
                New Global.CenterDevice.Rest.Clients.Collections.Collection With {.Id = "collection", .Name = "collection"})
            For index As Integer = 1 To depth
                parentDirectory = New Global.CenterDevice.IO.DirectoryInfo(io, parentDirectory,
                    New Global.CenterDevice.Rest.Clients.Folders.Folder With {.Id = "folder-" & index, .Name = "folder-" & index, .Collection = "collection"})
            Next
            Parent = parentDirectory
            Dim sourceMetadata = Metadata("source", "source.bin")
            Documents.Add("source", sourceMetadata)
            Source = New Global.CenterDevice.IO.FileInfo(io, Parent, sourceMetadata)
            Dim files As New List(Of Global.CenterDevice.IO.FileInfo) From {Source}
            If existingTarget Then
                Dim targetMetadata = Metadata("target", FinalName)
                Documents.Add("target", targetMetadata)
                files.Add(New Global.CenterDevice.IO.FileInfo(io, Parent, targetMetadata))
            End If
            SeedFiles(files.ToArray())
            GetType(Global.CenterDevice.IO.DirectoryInfo).GetField("getDirectories", BindingFlags.Instance Or BindingFlags.NonPublic).
                SetValue(Parent, New Global.CenterDevice.IO.DirectoryInfo() {})
        End Sub

        Private Function Metadata(id As String, name As String) As DocumentFullMetadata
            Return New DocumentFullMetadata With {.Id = id, .Filename = name,
                .Collections = New SharingInfo With {.Visible = New List(Of String) From {"collection"}},
                .Folders = If(Parent.FolderID Is Nothing, New List(Of String), New List(Of String) From {Parent.FolderID})}
        End Function

        Private Sub SeedFiles(files As Global.CenterDevice.IO.FileInfo())
            GetType(Global.CenterDevice.IO.DirectoryInfo).GetField("getFiles", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Parent, files)
        End Sub

        Friend Sub RunCopyCase(overwrite As Boolean)
            Dim method = GetType(CenterDeviceDmsProviderBase).GetMethod("CopyFileExact", BindingFlags.Instance Or BindingFlags.NonPublic)
            Try
                method.Invoke(Me, New Object() {Source, Parent, FinalName, "source-path", "target-path", CType(overwrite, Boolean?)})
            Catch failure As TargetInvocationException
                ExceptionDispatchInfo.Capture(failure.InnerException).Throw()
            End Try
        End Sub

        Friend Overrides Function UploadCopyDocument(source As Global.CenterDevice.IO.FileInfo, parent As Global.CenterDevice.IO.DirectoryInfo, name As String, collectionId As String) As UploadDocumentResponse
            UploadCalls += 1
            Events.Add("upload")
            UploadCollection = collectionId
            UploadFolder = parent.FolderID
            If Mode = "upload-failure" Then Throw New System.Net.Http.HttpRequestException("Fixed fixture upload failure.")
            Documents("created") = Metadata("created", name)
            Dim response As New UploadDocumentResponse With {.Id = If(Mode = "protected", "source", "created")}
            If Mode = "partial" Then response.UploadActions = New UploadActions With {.AddToFolder = New AddToFolder With {.Failed = New List(Of String) From {"folder"}}}
            'Keep the uploaded document entirely absent from the SDK's name-list cache.
            'The configured API client is Nothing: an unintended transport call fails the test.
            SeedFiles({Source})
            Return response
        End Function

        Friend Overrides Function ReadCopyDocumentMetadata(id As String) As DocumentFullMetadata
            MetadataReads.Add(id)
            If Mode = "metadata-failure" Then Throw New System.IO.FileNotFoundException("Fixed fixture metadata failure.")
            If Mode = "mismatch" Then Return Metadata("wrong", "wrong.bin")
            Return Documents(id)
        End Function

        Friend Overrides Sub RenameCopyParticipant(file As Global.CenterDevice.IO.FileInfo, name As String)
            Events.Add("rename:" & file.ID & ":" & If(name = FinalName, "final", "backup"))
            If Mode = "promotion" AndAlso file.ID = "created" Then Throw New IOException("Fixed promotion failure.")
            Documents(file.ID).Filename = name
        End Sub

        Friend Overrides Sub DeleteCopyParticipant(file As Global.CenterDevice.IO.FileInfo)
            Events.Add("delete:" & file.ID)
            Deleted.Add(file.ID)
            Documents.Remove(file.ID)
        End Sub
    End Class
End Class
