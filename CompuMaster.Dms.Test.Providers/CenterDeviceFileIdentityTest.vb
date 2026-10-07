Option Explicit On
Option Strict On

Imports System.Reflection
Imports CenterDevice.Rest.Clients.Documents
Imports CenterDevice.Rest.Clients.Documents.Metadata
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class CenterDeviceFileIdentityTest
    <TestCase(False), TestCase(True)>
    Public Sub ConversionWithoutAnAssignedCollectionKeepsFileIdentity(useRootParent As Boolean)
        Dim io As New FixtureIo()
        Dim provider As New ScopevisioTeamworkDmsProvider()
        Dim document As New DocumentFullMetadata With {
            .Id = "document-id", .Filename = "fixture.bin", .Size = 5L * 1024L * 1024L * 1024L,
            .Owner = "owner-id", .Link = "link-id",
            .Collections = New SharingInfo With {.Visible = New List(Of String) From {"collection-a", "collection-b"}},
            .Folders = New List(Of String) From {"folder-a", "folder-b"}
        }
        Dim file As New Global.CenterDevice.IO.FileInfo(io, If(useRootParent, io.RootDirectory, Nothing), document)
        Dim signature = If(useRootParent, New Type() {GetType(Global.CenterDevice.IO.FileInfo), GetType(Boolean)}, New Type() {GetType(Global.CenterDevice.IO.FileInfo)})
        Dim arguments = If(useRootParent, New Object() {file, False}, New Object() {file})
        Dim converter = GetType(CenterDeviceDmsProviderBase).GetMethod("CreateDmsResourceItem", BindingFlags.Instance Or BindingFlags.NonPublic, Nothing, signature, Nothing)
        Dim item = CType(converter.Invoke(provider, arguments), DmsResourceItem)
        Assert.That(item.ExtendedInfosFileID, [Is].EqualTo("document-id"))
        Assert.That(item.ContentLength, [Is].EqualTo(document.Size))
        Assert.That(item.Name, [Is].EqualTo("fixture.bin"))
        Assert.That(item.FullName, [Is].EqualTo(If(useRootParent, "/fixture.bin", "fixture.bin")))
        Assert.That(item.Collection, [Is].Empty)
        Assert.That(item.Folder, [Is].Empty)
        Assert.That(item.ExtendedInfosAssignedCollectionID, [Is].Null)
        Assert.That(item.ExtendedInfosAssignedFolderID, [Is].Null)
        Assert.That(item.ExtendedInfosReferencedFromCollectionIDs, [Is].EqualTo(document.Collections.Visible))
        Assert.That(item.ExtendedInfosReferencedFromFolderIDs, [Is].EqualTo(document.Folders))
        Assert.That(item.ExtendedInfosOwner.ID, [Is].EqualTo("owner-id"))
        Assert.That(item.ExtendedInfosLinks(0).ID, [Is].EqualTo("link-id"))
    End Sub

    Private Class FixtureIo
        Inherits Global.CenterDevice.IO.IOClientBase
        Public Sub New()
            MyBase.New(Nothing, "fixture-user")
        End Sub
    End Class
End Class
