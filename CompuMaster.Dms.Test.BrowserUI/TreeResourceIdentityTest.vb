Imports System.Reflection
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports NUnit.Framework

<TestFixture>
Public Class TreeResourceIdentityTest
    <TestCase(Nothing, Nothing), TestCase("a", Nothing), TestCase(Nothing, "b")>
    Public Sub DifferentFoldersInOneCollectionDoNotMatchWhenAStableFolderIdIsUnavailable(previousId As String, currentId As String)
        Dim previous = Folder("Collection/A", previousId)
        Dim current = Folder("Collection/B", currentId)
        Assert.That(SameResource(previous, current), [Is].False)
    End Sub

    <TestCase(Nothing, Nothing), TestCase("a", Nothing), TestCase(Nothing, "a")>
    Public Sub TheSameFolderPathRemainsIdentifiableWhenFolderMetadataIsIncomplete(previousId As String, currentId As String)
        Assert.That(SameResource(Folder("Collection/A", previousId), Folder("Collection/A", currentId)), [Is].True)
    End Sub

    <Test>
    Public Sub CollectionIdentityStillSurvivesRenaming()
        Dim previous As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Collection, .FullName = "Old", .ExtendedInfosCollectionID = "collection"}
        Dim current As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Collection, .FullName = "Renamed", .ExtendedInfosCollectionID = "collection"}
        Assert.That(SameResource(previous, current), [Is].True)
    End Sub

    <Test>
    Public Sub StableFolderIdentityStillSurvivesRenaming()
        Assert.That(SameResource(Folder("Collection/A", "a"), Folder("Collection/Renamed", "a")), [Is].True)
    End Sub

    Private Shared Function Folder(path As String, folderId As String) As DmsResourceItem
        Return New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .FullName = path,
            .ExtendedInfosFolderID = folderId, .ExtendedInfosCollectionID = "collection"}
    End Function

    Private Shared Function SameResource(previous As DmsResourceItem, current As DmsResourceItem) As Boolean
        'Exercise the production identity rule without constructing controls or opening windows.
        Dim method = GetType(DmsBrowser).GetMethod("SameTreeResource", BindingFlags.Static Or BindingFlags.NonPublic)
        Assert.That(method, [Is].Not.Null)
        Return CBool(method.Invoke(Nothing, New Object() {previous, current}))
    End Function
End Class
