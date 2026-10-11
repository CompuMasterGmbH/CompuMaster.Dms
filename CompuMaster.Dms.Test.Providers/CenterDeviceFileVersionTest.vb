Imports System.IO
Imports CenterDevice.Rest.Clients.Documents.Metadata
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Category("TestLevel1")>
Public Class CenterDeviceFileVersionTest
    <Test>
    Public Sub NativeBackendUsesAuthenticationContextWithoutResolvingOwnerIdentity()
        Dim io As New ContextIo
        Dim backend As New CenterDeviceFileVersionBackend(io)
        Assert.That(backend.AuthenticationContext, [Is].SameAs(io))
        Assert.That(backend.UserID, [Is].EqualTo("authentication-context-id"))
    End Sub
    Private Shared Function FileSnapshot() As DmsResourceItem
        Return New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .Name = "report.txt", .FullName = "/collection/report.txt", .ExtendedInfosFileID = "document-id"}
    End Function

    <Test>
    Public Sub HistoryRetainsOrderNumbersUnknownMetadataAndVisibility()
        Dim backend As New Backend
        backend.History.VersionDetails(0).Uploader = "not-visible"
        backend.History.VersionDetails(0).VersionDate = Nothing
        backend.History.VersionDetails(0).Size = Nothing
        Dim uploaded As New DateTime(2026, 10, 12, 0, 12, 0, DateTimeKind.Utc)
        backend.History.VersionDetails(1).VersionDate = uploaded
        Dim provider As New VersionProvider(backend)
        Dim history = provider.ListFileVersions(FileSnapshot())
        Assert.That(history.Select(Function(version) version.VersionID).ToArray(), [Is].EqualTo({"5", "2", "3"}))
        Assert.That(history(0).FileID, [Is].EqualTo("document-id"))
        Assert.That(history(0).ContentLength, [Is].Null)
        Assert.That(history(0).LastModificationUtc, [Is].Null)
        Assert.That(history(0).AuthorID, [Is].Null)
        Assert.That(history(1).ContentLength, [Is].EqualTo(0))
        Assert.That(history(1).AuthorID, [Is].EqualTo("uploader-id"))
        Assert.That(history(1).VersionDateUtc, [Is].EqualTo(uploaded))
        Assert.That(history(1).LastModificationUtc, [Is].Null)
        Assert.That(history(1).AuthorDisplayName, [Is].Null, "Uploader IDs and document author metadata must not be mistaken for an authenticated display name.")
    End Sub

    <TestCase("missing-details")>
    <TestCase("wrong-file")>
    <TestCase("invalid-number")>
    <TestCase("duplicate-number")>
    Public Sub InvalidOrOmittedHistoryIsAnErrorRatherThanAnEmptyList(kind As String)
        Dim backend As New Backend
        Select Case kind
            Case "missing-details" : backend.History.VersionDetails = Nothing
            Case "wrong-file" : backend.History.Id = "other-id"
            Case "invalid-number" : backend.History.VersionDetails(0).Version = Nothing
            Case "duplicate-number" : backend.History.VersionDetails(0).Version = 2
        End Select
        Dim provider As New VersionProvider(backend)
        Dim errorValue = Assert.Throws(Of FileActionFailedException)(Sub() provider.ListFileVersions(FileSnapshot()))
        Assert.That(errorValue.InnerException, [Is].InstanceOf(Of InvalidDataException)())
    End Sub

    <Test>
    Public Sub ExplicitlyEmptyHistoryRemainsEmpty()
        Dim backend As New Backend
        backend.History.VersionDetails.Clear()
        Dim provider As New VersionProvider(backend)
        Assert.That(provider.ListFileVersions(FileSnapshot()), [Is].Empty)
    End Sub

    <TestCase("foreign-provider")>
    <TestCase("reauthorized")>
    <TestCase("changed-user")>
    <TestCase("forged")>
    Public Sub ForeignAndStaleSnapshotsFailBeforeAnyMutation(kind As String)
        Dim backend As New Backend
        Dim provider As New VersionProvider(backend)
        Dim version = provider.ListFileVersions(FileSnapshot())(0)
        Select Case kind
            Case "foreign-provider" : provider = New VersionProvider(backend)
            Case "reauthorized" : backend.Context = New Object
            Case "changed-user" : backend.ContextUser = "other-account"
            Case "forged" : version = New DmsFileVersion(FileSnapshot(), version.VersionID)
        End Select
        Dim reads = backend.Reads
        Assert.Throws(Of ArgumentException)(Sub() provider.RestoreFileVersion(version))
        Assert.Throws(Of ArgumentException)(Sub() provider.RemoveFileVersion(version))
        Assert.That(backend.Writes, [Is].Zero)
        Assert.That(backend.Reads, [Is].EqualTo(reads))
    End Sub

    <TestCase(False)>
    <TestCase(True)>
    Public Sub SelectedMutationRetainsDocumentIdentityAndVersionNumber(restore As Boolean)
        Dim backend As New Backend
        Dim provider As New VersionProvider(backend)
        Dim version = provider.ListFileVersions(FileSnapshot())(1)
        If restore Then provider.RestoreFileVersion(version) Else provider.RemoveFileVersion(version)
        Assert.That(backend.LastFile, [Is].EqualTo("document-id"))
        Assert.That(backend.LastVersion, [Is].EqualTo(2))
        Assert.That(backend.Writes, [Is].EqualTo(1))
    End Sub

    <TestCase(False)>
    <TestCase(True)>
    Public Sub RemovedVersionsFailBeforeWrites(restore As Boolean)
        Dim backend As New Backend
        Dim provider As New VersionProvider(backend)
        Dim version = provider.ListFileVersions(FileSnapshot())(1)
        backend.History.VersionDetails.RemoveAt(1)
        Assert.Throws(Of FileActionFailedException)(Sub()
                                                       If restore Then provider.RestoreFileVersion(version) Else provider.RemoveFileVersion(version)
                                                   End Sub)
        Assert.That(backend.Writes, [Is].Zero)
    End Sub

    <Test>
    Public Sub LastVisibleVersionCannotBeRemoved()
        Dim backend As New Backend
        backend.History.VersionDetails.RemoveRange(1, 2)
        Dim provider As New VersionProvider(backend)
        Dim version = provider.ListFileVersions(FileSnapshot())(0)
        Assert.Throws(Of FileActionFailedException)(Sub() provider.RemoveFileVersion(version))
        Assert.That(backend.Writes, [Is].Zero)
    End Sub

    <TestCase(False)>
    <TestCase(True)>
    Public Sub AmbiguousWriteFailureRetainsOriginalCauseAndIsNotRetried(restore As Boolean)
        Dim backend As New Backend With {.WriteFailure = New IOException("connection lost after request")}
        Dim provider As New VersionProvider(backend)
        Dim version = provider.ListFileVersions(FileSnapshot())(0)
        Dim errorValue = Assert.Throws(Of FileActionFailedException)(Sub()
                                                                        If restore Then provider.RestoreFileVersion(version) Else provider.RemoveFileVersion(version)
                                                                    End Sub)
        Assert.That(errorValue.InnerException, [Is].SameAs(backend.WriteFailure))
        Assert.That(backend.Writes, [Is].EqualTo(1))
    End Sub

    <TestCase(False)>
    <TestCase(True)>
    Public Sub HistoricalDownloadLeavesCallerStreamOpenAndPreservesCause(fail As Boolean)
        Dim backend As New Backend
        Dim provider As New VersionProvider(backend)
        Dim version = provider.ListFileVersions(FileSnapshot())(1)
        Using destination As New MemoryStream
            If fail Then
                backend.WriteFailure = New IOException("body interrupted")
                Dim errorValue = Assert.Throws(Of FileActionFailedException)(Sub() provider.DownloadFileVersion(version, destination))
                Assert.That(errorValue.InnerException, [Is].SameAs(backend.WriteFailure))
            Else
                provider.DownloadFileVersion(version, destination)
            End If
            Assert.That(destination.CanWrite, [Is].True)
            Assert.That(destination.ToArray(), [Is].EqualTo({CByte(42)}))
            Assert.That(backend.LastVersion, [Is].EqualTo(2))
        End Using
    End Sub

    Private Class VersionProvider
        Inherits ScopevisioTeamworkDmsProvider
        Private ReadOnly Backend As ICenterDeviceFileVersionBackend
        Friend Sub New(backend As ICenterDeviceFileVersionBackend)
            Me.Backend = backend
        End Sub
        Friend Overrides Function CreateFileVersionBackend() As ICenterDeviceFileVersionBackend
            Return Backend
        End Function
    End Class

    Private Class ContextIo
        Inherits Global.CenterDevice.IO.IOClientBase
        Friend Sub New()
            MyBase.New(Nothing, "authentication-context-id")
        End Sub
    End Class

    Private Class Backend
        Implements ICenterDeviceFileVersionBackend
        Friend Context As Object = New Object
        Friend ContextUser As String = "authentication-context-id"
        Friend History As New DocumentVersionHistoryMetadata With {.Id = "document-id", .Version = 5,
            .VersionDetails = New List(Of DocumentVersionMetadata) From {
                New DocumentVersionMetadata With {.Version = 5, .Size = 20, .Uploader = "uploader-id"},
                New DocumentVersionMetadata With {.Version = 2, .Size = 0, .Uploader = "uploader-id", .Author = "document author"},
                New DocumentVersionMetadata With {.Version = 3, .Size = 40}}}
        Friend Reads As Integer
        Friend Writes As Integer
        Friend LastFile As String
        Friend LastVersion As Long
        Friend WriteFailure As Exception
        Public ReadOnly Property AuthenticationContext As Object Implements ICenterDeviceFileVersionBackend.AuthenticationContext
            Get
                Return Context
            End Get
        End Property
        Public ReadOnly Property UserID As String Implements ICenterDeviceFileVersionBackend.UserID
            Get
                Return ContextUser
            End Get
        End Property
        Public Function ReadHistory(fileID As String) As DocumentVersionHistoryMetadata Implements ICenterDeviceFileVersionBackend.ReadHistory
            Reads += 1
            Return History
        End Function
        Public Sub Download(fileID As String, version As Long, destination As Stream) Implements ICenterDeviceFileVersionBackend.Download
            LastFile = fileID
            LastVersion = version
            destination.WriteByte(42)
            If WriteFailure IsNot Nothing Then Throw WriteFailure
        End Sub
        Public Sub Restore(fileID As String, version As Long) Implements ICenterDeviceFileVersionBackend.Restore
            Write(fileID, version)
        End Sub
        Public Sub Remove(fileID As String, version As Long) Implements ICenterDeviceFileVersionBackend.Remove
            Write(fileID, version)
        End Sub
        Private Sub Write(fileID As String, version As Long)
            LastFile = fileID
            LastVersion = version
            Writes += 1
            If WriteFailure IsNot Nothing Then Throw WriteFailure
        End Sub
    End Class
End Class
