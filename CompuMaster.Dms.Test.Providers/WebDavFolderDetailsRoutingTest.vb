Option Explicit On
Option Strict On

Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class WebDavFolderDetailsRoutingTest
    <Test>
    Public Sub ActualWebDavFolderDetailsUsePathAndCorroborateReturnedIdentity()
        Using handler As New WebDavFolderIdentityMetadataTest.MetadataHandler("<oc:fileid>17</oc:fileid>", 200), client As New HttpClient(handler)
            Dim provider = WebDavFolderIdentityMetadataTest.CreateProvider(client)
            Dim selected = provider.ListRemoteItem("item")
            selected.ExtendedInfosCollisionDetected = True
            handler.ETag = "changed-folder-contents"
            Dim details = Global.CompuMaster.Dms.BrowserUI.DmsResourceDetailsResolver.LoadAsync(provider, selected).GetAwaiter().GetResult()
            Assert.That(details.ExtendedInfosFolderID, [Is].EqualTo("17"))
            Assert.That(details.ProviderSpecificHashOrETag, [Is].Not.EqualTo(selected.ProviderSpecificHashOrETag))
            Assert.That(details.FullName, [Is].EqualTo(selected.FullName))
            Assert.That(details.ExtendedInfosCollisionDetected, [Is].True)
            Assert.That(handler.Requests, [Is].EqualTo(2))
            Assert.That(handler.Depth, [Is].EqualTo("0"))
        End Using
    End Sub

    <TestCase("<oc:fileid>18</oc:fileid>", True)>
    <TestCase("", True)>
    <TestCase("<oc:fileid>17</oc:fileid>", False)>
    Public Sub ChangedMissingOrReplacedFolderIdentityCannotShowAnotherResource(properties As String, folder As Boolean)
        Using handler As New WebDavFolderIdentityMetadataTest.MetadataHandler("<oc:fileid>17</oc:fileid>", 200), client As New HttpClient(handler)
            Dim provider = WebDavFolderIdentityMetadataTest.CreateProvider(client)
            Dim selected = provider.ListRemoteItem("item")
            handler.Properties = properties
            handler.IsFolder = folder
            Assert.Throws(Of InvalidOperationException)(Sub() Global.CompuMaster.Dms.BrowserUI.DmsResourceDetailsResolver.LoadAsync(provider, selected).GetAwaiter().GetResult())
            Assert.That(handler.Requests, [Is].EqualTo(2))
        End Using
    End Sub

    <Test>
    Public Sub ProviderWithIdSearchRetainsSelectedIdForDuplicatePaths()
        Dim provider As New IdentifierProvider
        Dim entry As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .FullName = "collection/duplicate", .ExtendedInfosFolderID = "selected-id", .ExtendedInfosCollisionDetected = True}
        Dim details = Global.CompuMaster.Dms.BrowserUI.DmsResourceDetailsResolver.LoadAsync(provider, entry).GetAwaiter().GetResult()
        Assert.That(provider.SelectedId, [Is].EqualTo("selected-id"))
        Assert.That(provider.PathCalls, [Is].Zero)
        Assert.That(details.FullName, [Is].EqualTo(entry.FullName))
        Assert.That(details.ExtendedInfosCollisionDetected, [Is].True)
    End Sub

    Private Class IdentifierProvider
        Inherits NoDmsProvider
        Friend SelectedId As String
        Friend PathCalls As Integer
        Public Overrides Function FindFolderByIdAsync(id As String, Optional token As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            SelectedId = id
            Return Task.FromResult(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .ExtendedInfosFolderID = id})
        End Function
        Public Overrides Function ListRemoteItemAsync(path As String, Optional token As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            PathCalls += 1
            Throw New InvalidOperationException("Unexpected ambiguous path lookup.")
        End Function
    End Class
End Class
