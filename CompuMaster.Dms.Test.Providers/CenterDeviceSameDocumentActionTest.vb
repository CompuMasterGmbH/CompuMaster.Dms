Option Explicit On
Option Strict On

Imports System.Reflection
Imports CenterDevice.Rest.Clients.Collections
Imports CenterDevice.Rest.Clients.Documents.Metadata
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class CenterDeviceSameDocumentActionTest
    <TestCase(False), TestCase(True)>
    Public Sub SynchronousReplacementRejectsAnotherReferenceToTheSourceBeforeAnyRequest(moving As Boolean)
        Dim provider As New CachedProvider()
        Assert.Throws(Of ArgumentException)(Sub() provider.InvokeCore(moving))
        Assert.That(provider.SourceFile.ID, [Is].EqualTo("shared-document"))
        Assert.That(provider.TargetFile.ID, [Is].EqualTo("shared-document"))
        Assert.That(provider.SourceFile.FileName, [Is].EqualTo("document.txt"))
        Assert.That(provider.TargetFile.FileName, [Is].EqualTo("document.txt"))
    End Sub

    Private Class CachedProvider
        Inherits ScopevisioTeamworkDmsProvider
        Public ReadOnly SourceFile As Global.CenterDevice.IO.FileInfo
        Public ReadOnly TargetFile As Global.CenterDevice.IO.FileInfo
        Public Sub New()
            'Cached SDK models model two collection references to one document. No API client exists;
            'entering download, rename, move or delete would fail instead of reaching a server.
            Dim io As New FixtureIo()
            Me.IOClient = io
            Dim source As New Global.CenterDevice.IO.DirectoryInfo(io, io.RootDirectory, New Collection With {.Id = "source", .Name = "source"})
            Dim target As New Global.CenterDevice.IO.DirectoryInfo(io, io.RootDirectory, New Collection With {.Id = "target", .Name = "target"})
            SourceFile = New Global.CenterDevice.IO.FileInfo(io, source, New DocumentFullMetadata With {.Id = "shared-document", .Filename = "document.txt"})
            TargetFile = New Global.CenterDevice.IO.FileInfo(io, target, New DocumentFullMetadata With {.Id = "shared-document", .Filename = "document.txt"})
            SetCache(io.RootDirectory, "getDirectories", New Global.CenterDevice.IO.DirectoryInfo() {source, target})
            SetCache(source, "getFiles", New Global.CenterDevice.IO.FileInfo() {SourceFile})
            SetCache(target, "getFiles", New Global.CenterDevice.IO.FileInfo() {TargetFile})
            SetCache(source, "getDirectories", New Global.CenterDevice.IO.DirectoryInfo() {})
            SetCache(target, "getDirectories", New Global.CenterDevice.IO.DirectoryInfo() {})
        End Sub
        Public Sub InvokeCore(moving As Boolean)
            Dim source As New DmsResourceItem With {.FullName = "source/document.txt", .Name = "document.txt", .ItemType = DmsResourceItem.ItemTypes.File}
            If moving Then
                Me.MoveItem(source, "target/document.txt", True)
            Else
                Me.CopyItem(source, "target/document.txt", True)
            End If
        End Sub
        Private Shared Sub SetCache(directory As Global.CenterDevice.IO.DirectoryInfo, name As String, value As Object)
            Dim cacheField = GetType(Global.CenterDevice.IO.DirectoryInfo).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic)
            Assert.That(cacheField, [Is].Not.Null, "The SDK fixture cache layout changed.")
            cacheField.SetValue(directory, value)
        End Sub
    End Class

    Private Class FixtureIo
        Inherits Global.CenterDevice.IO.IOClientBase
        Public Sub New()
            MyBase.New(Nothing, "fixture-user")
        End Sub
    End Class
End Class
