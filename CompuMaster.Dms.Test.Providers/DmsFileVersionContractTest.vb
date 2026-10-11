Imports System.IO
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class DmsFileVersionContractTest
    <TestCase(0), TestCase(1), TestCase(2), TestCase(3)>
    Public Sub ProvidersAdvertiseOnlyTheirImplementedWorkflows(kind As Integer)
        Dim provider As BaseDmsProvider
        Select Case kind
            Case 0 : provider = New NoDmsProvider()
            Case 1 : provider = New ManualUrlDmsProvider()
            Case 2 : provider = New WebDavDmsProvider()
            Case Else : provider = New ScopevisioTeamworkDmsProvider()
        End Select
#If CENTERDEVICE_FILE_VERSIONS Then
        If kind = 3 Then
            Assert.That(provider.SupportsFileVersionHistory, [Is].True)
            Assert.That(provider.SupportsFileVersionDownload, [Is].True)
            Assert.That(provider.SupportsFileVersionRestore, [Is].True)
            Assert.That(provider.SupportsFileVersionRemoval, [Is].True)
            Return
        End If
#End If
        Assert.That(provider.SupportsFileVersionHistory, [Is].False)
        Assert.That(provider.SupportsFileVersionDownload, [Is].False)
        Assert.That(provider.SupportsFileVersionRestore, [Is].False)
        Assert.That(provider.SupportsFileVersionRemoval, [Is].False)
        Assert.Throws(Of NotSupportedException)(Sub() provider.ListFileVersions(Nothing))
        Assert.Throws(Of NotSupportedException)(Sub() provider.RestoreFileVersion(Nothing))
        Assert.Throws(Of NotSupportedException)(Sub() provider.RemoveFileVersion(Nothing))
        Using output As New MemoryStream()
            Assert.Throws(Of NotSupportedException)(Sub() provider.DownloadFileVersion(Nothing, output))
            Assert.That(output.CanWrite, [Is].True)
        End Using
    End Sub

    <Test>
    Public Sub ImmutableVersionRetainsFileIdentityAndUnknownVersusZeroMetadata()
        Dim file As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .FullName = "folder/name", .Name = "name", .ExtendedInfosFileID = "opaque!file"}
        Dim unknown As New DmsFileVersion(file, "opaque-revision")
        Dim empty As New DmsFileVersion(file, "second-revision", New DateTime(2026, 10, 11, 8, 0, 0, DateTimeKind.Utc), 0)
        file.FullName = "changed"
        file.ExtendedInfosFileID = "replacement"
        Assert.That(unknown.FileFullName, [Is].EqualTo("folder/name"))
        Assert.That(unknown.FileID, [Is].EqualTo("opaque!file"))
        Assert.That(unknown.VersionID, [Is].EqualTo("opaque-revision"))
        Assert.That(unknown.ContentLength, [Is].Null)
        Assert.That(unknown.LastModificationUtc, [Is].Null)
        Assert.That(unknown.AuthorID, [Is].Null)
        Assert.That(empty.ContentLength, [Is].Zero)
        Assert.That(empty.LastModificationUtc.Value.Kind, [Is].EqualTo(DateTimeKind.Utc))
    End Sub

    <TestCase(DmsResourceItem.ItemTypes.Folder), TestCase(DmsResourceItem.ItemTypes.Collection), TestCase(DmsResourceItem.ItemTypes.Root)>
    Public Sub VersionSnapshotsRequireFiles(kind As DmsResourceItem.ItemTypes)
        Assert.Throws(Of ArgumentException)(Sub() CreateVersion(New DmsResourceItem With {.ItemType = kind}, "revision"))
    End Sub
    Private Sub CreateVersion(file As DmsResourceItem, id As String)
        Dim result = New DmsFileVersion(file, id)
    End Sub
End Class
