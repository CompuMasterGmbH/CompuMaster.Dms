Option Explicit On
Option Strict On

Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class WebDavResourceDetailsTest
    <TestCase(DmsResourceItem.ItemTypes.File)>
    <TestCase(DmsResourceItem.ItemTypes.Folder)>
    Public Async Function IdentityMetadataUsesPathLookupWithTheSelectedContext(kind As DmsResourceItem.ItemTypes) As Task
        Dim entry As New DmsResourceItem With {.ItemType = kind, .FullName = "/selected/item", .Folder = "/selected",
            .ExtendedInfosFileID = "file-id", .ExtendedInfosFolderID = "folder-id", .ExtendedInfosAssignedFolderID = "parent-id"}
        Dim provider As New PathOnlyProvider With {.Result = New DmsResourceItem With {.ItemType = kind,
            .ExtendedInfosFileID = "file-id", .ExtendedInfosFolderID = "folder-id"}}
        Dim details = Await Global.CompuMaster.Dms.BrowserUI.DmsResourceDetailsResolver.LoadAsync(provider, entry)
        Assert.That(details.FullName, [Is].EqualTo(entry.FullName))
        Assert.That(details.Folder, [Is].EqualTo(entry.Folder))
        Assert.That(details.ExtendedInfosAssignedFolderID, [Is].EqualTo("parent-id"))
        Assert.That(provider.SelectedPath, [Is].EqualTo(entry.FullName))
        Assert.That(provider.IdentifierCalls, [Is].Zero)
    End Function

    <TestCase("different", DmsResourceItem.ItemTypes.File)>
    <TestCase(Nothing, DmsResourceItem.ItemTypes.File)>
    <TestCase("selected", DmsResourceItem.ItemTypes.Folder)>
    Public Sub ReplacedFileMissingIdentityOrChangedTypeIsRejected(returnedId As String, returnedKind As DmsResourceItem.ItemTypes)
        Dim entry As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .FullName = "/selected/item", .ExtendedInfosFileID = "selected"}
        Dim provider As New PathOnlyProvider With {.Result = New DmsResourceItem With {.ItemType = returnedKind, .ExtendedInfosFileID = returnedId}}
        Assert.ThrowsAsync(Of InvalidOperationException)(Async Function() Await Global.CompuMaster.Dms.BrowserUI.DmsResourceDetailsResolver.LoadAsync(provider, entry))
        Assert.That(provider.IdentifierCalls, [Is].Zero)
    End Sub

    <Test>
    Public Async Function FileWithoutAnIdentifierStillLoadsByPath() As Task
        Dim entry As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .FullName = "/selected/item"}
        Dim provider As New PathOnlyProvider With {.Result = New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File}}
        Assert.That(Await Global.CompuMaster.Dms.BrowserUI.DmsResourceDetailsResolver.LoadAsync(provider, entry), [Is].SameAs(provider.Result))
        Assert.That(provider.SelectedPath, [Is].EqualTo(entry.FullName))
        Assert.That(provider.IdentifierCalls, [Is].Zero)
    End Function

    <Test>
    Public Sub MissingFileStillReportsFileNotFound()
        Dim provider As New PathOnlyProvider
        Dim entry As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .FullName = "/selected/item", .ExtendedInfosFileID = "selected"}
        Assert.ThrowsAsync(Of CompuMaster.Dms.Data.FileNotFoundException)(Async Function() Await Global.CompuMaster.Dms.BrowserUI.DmsResourceDetailsResolver.LoadAsync(provider, entry))
    End Sub

    Private Class PathOnlyProvider
        Inherits NoDmsProvider
        Public Result As DmsResourceItem
        Public SelectedPath As String
        Public IdentifierCalls As Integer
        Public Overrides ReadOnly Property DmsProviderID As DmsProviders
            Get
                Return DmsProviders.WebDAV
            End Get
        End Property
        Public Overrides Function ListRemoteItemAsync(path As String, Optional token As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            SelectedPath = path
            Return Task.FromResult(Result)
        End Function
        Public Overrides Function FindFileByIdAsync(id As String, Optional token As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            IdentifierCalls += 1
            Throw New NotImplementedException("No WebDAV identifier lookup API.")
        End Function
        Public Overrides Function FindFolderByIdAsync(id As String, Optional token As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            IdentifierCalls += 1
            Throw New NotImplementedException("No WebDAV identifier lookup API.")
        End Function
    End Class
End Class
