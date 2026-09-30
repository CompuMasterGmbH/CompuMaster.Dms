Imports System.Threading
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture>
<Apartment(ApartmentState.STA)>
Public Class DmsBrowserTreeNodeTest

    <TestCase(DmsResourceItem.ItemTypes.Collection, False, 1)>
    <TestCase(DmsResourceItem.ItemTypes.Collection, True, 4)>
    <TestCase(DmsResourceItem.ItemTypes.Folder, False, 2)>
    <TestCase(DmsResourceItem.ItemTypes.Folder, True, 5)>
    Public Sub CreatedDirectoryNodeUsesItsTypeIconForNormalAndSelectedStates(itemType As DmsResourceItem.ItemTypes, isShared As Boolean, expectedImageIndex As Integer)
        Dim directory As New DmsResourceItem With {
            .ItemType = itemType,
            .Name = "New directory",
            .ExtendedInfosHasUserSharings = isShared
        }

        Dim node As TreeNode = Global.CompuMaster.Dms.BrowserUI.DmsBrowser.CreateDirectoryTreeNode(directory)

        ClassicAssert.AreEqual(expectedImageIndex, node.ImageIndex)
        ClassicAssert.AreEqual(expectedImageIndex, node.SelectedImageIndex)
    End Sub

    <Test>
    Public Sub SwitchingSelectionPreservesCollectionAndFolderIcons()
        Dim collectionNode As TreeNode = Global.CompuMaster.Dms.BrowserUI.DmsBrowser.CreateDirectoryTreeNode(
            New DmsResourceItem With {
                .ItemType = DmsResourceItem.ItemTypes.Collection,
                .Name = "Collection"
            })
        Dim folderNode As TreeNode = Global.CompuMaster.Dms.BrowserUI.DmsBrowser.CreateDirectoryTreeNode(
            New DmsResourceItem With {
                .ItemType = DmsResourceItem.ItemTypes.Folder,
                .Name = "Folder"
            })

        Using treeView As New TreeView()
            treeView.Nodes.Add(collectionNode)
            treeView.Nodes.Add(folderNode)

            treeView.SelectedNode = collectionNode
            treeView.SelectedNode = folderNode

            ClassicAssert.AreEqual(1, collectionNode.ImageIndex)
            ClassicAssert.AreEqual(1, collectionNode.SelectedImageIndex)
            ClassicAssert.AreEqual(2, folderNode.ImageIndex)
            ClassicAssert.AreEqual(2, folderNode.SelectedImageIndex)
        End Using
    End Sub

End Class
