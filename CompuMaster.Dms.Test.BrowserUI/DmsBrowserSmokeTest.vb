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
            InitializeFileIconsWrapper(browser)
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
            ClassicAssert.IsFalse(AreImagesEqual(imageList.Images(6), imageList.Images(7)))
        End Using
    End Sub

    <Test>
    Public Sub SharedExtensionIconIsAddedThroughWrapperPath()
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser()
            Dim wrapper As Object = InitializeFileIconsWrapper(browser)
            Dim imageList As ImageList = GetImageListFileIcons(browser)
            Dim initialImageCount As Integer = imageList.Images.Count

            Dim methodInfo As MethodInfo = wrapper.GetType().GetMethod("GetSIImageListIndexForFileExtension", BindingFlags.Instance Or BindingFlags.Public)
            ClassicAssert.IsNotNull(methodInfo)

            Dim sharedTextIconIndex As Integer = CInt(methodInfo.Invoke(wrapper, New Object() {".txt", True}))

            ClassicAssert.GreaterOrEqual(sharedTextIconIndex, 0)
            ClassicAssert.Greater(imageList.Images.Count, initialImageCount)
        End Using
    End Sub

    <Test>
    Public Sub ConstructorConfiguresDpiAwareFileAndTreeIconSizes()
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser()
            Dim fileImageList As ImageList = GetControlField(Of ImageList)(browser, "_ImageListFileIcons")
            Dim treeImageList As ImageList = GetControlField(Of ImageList)(browser, "_ImageListTreeIcons")
            Dim fileList As ListView = GetControlField(Of ListView)(browser, "_ListViewDmsFiles")
            Dim folderTree As TreeView = GetControlField(Of TreeView)(browser, "_TreeViewDmsFolders")
            Dim expectedFileIconSize As Integer = ScaleLogicalPixels(24, browser.DeviceDpi)
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
            Dim fileIcons As Object = GetFileIcons(browser)

            Dim iconIndex As Integer = GetFileIconIndex(fileIcons, ".txt", True)
            Dim repeatedIconIndex As Integer = GetFileIconIndex(fileIcons, ".txt", True)

            ClassicAssert.GreaterOrEqual(iconIndex, 8)
            ClassicAssert.AreEqual(iconIndex, repeatedIconIndex)
            ClassicAssert.AreEqual(imageList.ImageSize, imageList.Images(iconIndex).Size)
            ClassicAssert.AreEqual(imageList.ImageSize, imageList.Images(7).Size)
        End Using
    End Sub

    <Test>
    Public Sub DpiReconfigurationRebuildsIconCacheAndExistingItemIndices()
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser()
            Dim imageList As ImageList = GetImageListFileIcons(browser)
            Dim fileList As ListView = GetControlField(Of ListView)(browser, "_ListViewDmsFiles")
            Dim originalFileIcons As Object = GetFileIcons(browser)
            Dim originalIconIndex As Integer = GetFileIconIndex(originalFileIcons, ".txt", True)
            Dim file As New Global.CompuMaster.Dms.Data.DmsResourceItem With {
                .Name = "document.txt",
                .ExtendedInfosIsShared = True
            }
            Dim item As New ListViewItem(file.Name, originalIconIndex) With {.Tag = file}
            fileList.Items.Add(item)

            ConfigureIconImageListsForDpi(browser, 144)

            Dim reconfiguredFileIcons As Object = GetFileIcons(browser)
            Dim reconfiguredIconIndex As Integer = GetFileIconIndex(reconfiguredFileIcons, ".txt", True)
            ClassicAssert.AreEqual(New Drawing.Size(36, 36), imageList.ImageSize)
            ClassicAssert.AreNotSame(originalFileIcons, reconfiguredFileIcons)
            ClassicAssert.AreEqual(reconfiguredIconIndex, item.ImageIndex)
            ClassicAssert.AreEqual(imageList.ImageSize, imageList.Images(item.ImageIndex).Size)
        End Using
    End Sub

    <Test>
    Public Sub InstanceSelectionIsOptIn()
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser()
            ClassicAssert.IsFalse(browser.EnableDmsInstanceSelection)
        End Using
    End Sub

    <Test>
    Public Sub InstanceDialogShowsNamesAndSelectsCurrentInstance()
        Dim instances As Global.CompuMaster.Dms.Data.DmsInstanceInfo() = {
            New Global.CompuMaster.Dms.Data.DmsInstanceInfo("tenant-one", "First tenant", False),
            New Global.CompuMaster.Dms.Data.DmsInstanceInfo("tenant-two", "Second tenant", True)
        }

        Using picker As New Global.CompuMaster.Dms.BrowserUI.DmsInstanceSelectionDialog(instances)
            Dim list As ListBox = Nothing
            For Each control As Control In picker.Controls
                list = TryCast(control, ListBox)
                If list IsNot Nothing Then Exit For
            Next

            ClassicAssert.IsNotNull(list)
            ClassicAssert.AreEqual("First tenant", list.GetItemText(list.Items(0)))
            ClassicAssert.AreEqual("Second tenant", list.GetItemText(list.Items(1)))
            ClassicAssert.AreEqual("tenant-two", picker.SelectedInstance.ID)
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

    Private Shared Function GetFileIcons(browser As Global.CompuMaster.Dms.BrowserUI.DmsBrowser) As Object
        Dim fileIconsProperty As PropertyInfo = GetType(Global.CompuMaster.Dms.BrowserUI.DmsBrowser).GetProperty("FileIcons", BindingFlags.Instance Or BindingFlags.NonPublic)
        ClassicAssert.IsNotNull(fileIconsProperty)
        Return fileIconsProperty.GetValue(browser)
    End Function

    Private Shared Function GetFileIconIndex(fileIcons As Object, extension As String, isShared As Boolean) As Integer
        Dim getIconIndexMethod As MethodInfo = fileIcons.GetType().GetMethod("GetSIImageListIndexForFileExtension", BindingFlags.Instance Or BindingFlags.Public)
        ClassicAssert.IsNotNull(getIconIndexMethod)
        Return CInt(getIconIndexMethod.Invoke(fileIcons, New Object() {extension, isShared}))
    End Function

    Private Shared Sub ConfigureIconImageListsForDpi(browser As Global.CompuMaster.Dms.BrowserUI.DmsBrowser, deviceDpi As Integer)
        Dim configureMethod As MethodInfo = GetType(Global.CompuMaster.Dms.BrowserUI.DmsBrowser).GetMethod("ConfigureIconImageListsForDpi", BindingFlags.Instance Or BindingFlags.NonPublic)
        ClassicAssert.IsNotNull(configureMethod)
        configureMethod.Invoke(browser, New Object() {deviceDpi})
    End Sub

    Private Shared Function ScaleLogicalPixels(logicalPixels As Integer, deviceDpi As Integer) As Integer
        Return Math.Min(MaximumImageListDimension, CInt(Math.Round(CDbl(logicalPixels) * CDbl(deviceDpi) / DefaultDpi, MidpointRounding.AwayFromZero)))
    End Function

    Private Shared Function InitializeFileIconsWrapper(browser As Global.CompuMaster.Dms.BrowserUI.DmsBrowser) As Object
        Dim propertyInfo As PropertyInfo = GetType(Global.CompuMaster.Dms.BrowserUI.DmsBrowser).GetProperty("FileIcons", BindingFlags.Instance Or BindingFlags.NonPublic)
        ClassicAssert.IsNotNull(propertyInfo)
        Return propertyInfo.GetValue(browser)
    End Function

    Private Shared Function AreImagesEqual(first As Drawing.Image, second As Drawing.Image) As Boolean
        Using firstBitmap As New Drawing.Bitmap(first), secondBitmap As New Drawing.Bitmap(second)
            If firstBitmap.Width <> secondBitmap.Width OrElse firstBitmap.Height <> secondBitmap.Height Then Return False
            For x As Integer = 0 To firstBitmap.Width - 1
                For y As Integer = 0 To firstBitmap.Height - 1
                    If firstBitmap.GetPixel(x, y) <> secondBitmap.GetPixel(x, y) Then Return False
                Next
            Next
            Return True
        End Using
    End Function

End Class
