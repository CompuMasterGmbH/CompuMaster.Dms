Imports System.Reflection
Imports System.Threading
Imports System.Windows.Forms
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture>
<Apartment(ApartmentState.STA)>
Public Class DmsBrowserSmokeTest

    Private Const DefaultDpi As Integer = 96
    Private Const MaximumImageListDimension As Integer = 256

    <Test>
    Public Sub ConstructorInitializesFileIconImageListWithoutSerializedImageStream()
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser()
            Dim imageList As ImageList = GetImageListFileIcons(browser)

            ClassicAssert.AreEqual(8, imageList.Images.Count)
            ClassicAssert.AreEqual("iconfinder_Home-ui-ux-mobile-web_4960719.png", imageList.Images.Keys(0))
            ClassicAssert.AreEqual("iconfinder_bookmark-ui-ux-mobile-web_4960727.png", imageList.Images.Keys(1))
            ClassicAssert.AreEqual("iconfinder_Folder-ui-ux-mobile-web_4960713.png", imageList.Images.Keys(2))
            ClassicAssert.AreEqual("iconfinder_Home-ui-ux-mobile-web_4960719 - Shared.png", imageList.Images.Keys(3))
            ClassicAssert.AreEqual("iconfinder_bookmark-ui-ux-mobile-web_4960727 - Shared.png", imageList.Images.Keys(4))
            ClassicAssert.AreEqual("iconfinder_Folder-ui-ux-mobile-web_4960713 - Shared.png", imageList.Images.Keys(5))
            ClassicAssert.AreEqual("iconfinder_Document-ui-ux-mobile-web-office-microsoftofficeico_4960706.png", imageList.Images.Keys(6))
            ClassicAssert.AreEqual("iconfinder_Document-ui-ux-mobile-web-office-microsoftofficeico_4960706 - Shared.png", imageList.Images.Keys(7))
        End Using
    End Sub

    <Test>
    Public Sub ConstructorConfiguresDpiAwareFileAndTreeIconSizes()
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser()
            Dim fileImageList As ImageList = GetControlField(Of ImageList)(browser, "_ImageListFileIcons")
            Dim treeImageList As ImageList = GetControlField(Of ImageList)(browser, "_ImageListTreeIcons")
            Dim fileList As ListView = GetControlField(Of ListView)(browser, "_ListViewDmsFiles")
            Dim folderTree As TreeView = GetControlField(Of TreeView)(browser, "_TreeViewDmsFolders")
            Dim expectedFileIconSize As Integer = ScaleLogicalPixels(32, browser.DeviceDpi)
            Dim expectedTreeIconSize As Integer = ScaleLogicalPixels(24, browser.DeviceDpi)

            ClassicAssert.AreEqual(New Drawing.Size(expectedFileIconSize, expectedFileIconSize), fileImageList.ImageSize)
            ClassicAssert.AreEqual(New Drawing.Size(expectedTreeIconSize, expectedTreeIconSize), treeImageList.ImageSize)
            ClassicAssert.AreSame(fileImageList, fileList.SmallImageList)
            ClassicAssert.AreSame(fileImageList, fileList.LargeImageList)
            ClassicAssert.AreSame(treeImageList, folderTree.ImageList)
            ClassicAssert.AreNotSame(fileImageList, treeImageList)
            ClassicAssert.GreaterOrEqual(folderTree.ItemHeight, expectedTreeIconSize)
        End Using
    End Sub

    <Test>
    Public Sub SharedFileExtensionIconUsesConfiguredImageSize()
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser()
            Dim imageList As ImageList = GetImageListFileIcons(browser)
            Dim fileIconsProperty As PropertyInfo = GetType(Global.CompuMaster.Dms.BrowserUI.DmsBrowser).GetProperty("FileIcons", BindingFlags.Instance Or BindingFlags.NonPublic)
            ClassicAssert.IsNotNull(fileIconsProperty)
            Dim fileIcons As Object = fileIconsProperty.GetValue(browser)
            Dim getIconIndexMethod As MethodInfo = fileIcons.GetType().GetMethod("GetSIImageListIndexForFileExtension", BindingFlags.Instance Or BindingFlags.Public)
            ClassicAssert.IsNotNull(getIconIndexMethod)

            Dim iconIndex As Integer = CInt(getIconIndexMethod.Invoke(fileIcons, New Object() {".txt", True}))
            Dim repeatedIconIndex As Integer = CInt(getIconIndexMethod.Invoke(fileIcons, New Object() {".txt", True}))

            ClassicAssert.GreaterOrEqual(iconIndex, 8)
            ClassicAssert.AreEqual(iconIndex, repeatedIconIndex)
            ClassicAssert.AreEqual(imageList.ImageSize, imageList.Images(iconIndex).Size)
            ClassicAssert.AreEqual(imageList.ImageSize, imageList.Images(7).Size)
        End Using
    End Sub

    Private Shared Function GetImageListFileIcons(browser As Global.CompuMaster.Dms.BrowserUI.DmsBrowser) As ImageList
        Return GetControlField(Of ImageList)(browser, "_ImageListFileIcons")
    End Function

    Private Shared Function GetControlField(Of T)(browser As Global.CompuMaster.Dms.BrowserUI.DmsBrowser, fieldName As String) As T
        Dim fieldInfo As FieldInfo = GetType(Global.CompuMaster.Dms.BrowserUI.DmsBrowser).GetField(fieldName, BindingFlags.Instance Or BindingFlags.NonPublic)
        ClassicAssert.IsNotNull(fieldInfo)
        Return CType(fieldInfo.GetValue(browser), T)
    End Function

    Private Shared Function ScaleLogicalPixels(logicalPixels As Integer, deviceDpi As Integer) As Integer
        Return Math.Min(MaximumImageListDimension, CInt(Math.Round(CDbl(logicalPixels) * CDbl(deviceDpi) / DefaultDpi, MidpointRounding.AwayFromZero)))
    End Function

End Class
