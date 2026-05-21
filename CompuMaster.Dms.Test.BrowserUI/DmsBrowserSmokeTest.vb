Imports System.Reflection
Imports System.Threading
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture>
<Apartment(ApartmentState.STA)>
Public Class DmsBrowserSmokeTest

    <Test>
    Public Sub ConstructorInitializesFileIconImageListWithoutSerializedImageStream()
        Using browser As New DmsBrowser()
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

    Private Shared Function GetImageListFileIcons(browser As DmsBrowser) As ImageList
        Dim fieldInfo As FieldInfo = GetType(DmsBrowser).GetField("_ImageListFileIcons", BindingFlags.Instance Or BindingFlags.NonPublic)
        ClassicAssert.IsNotNull(fieldInfo)
        Return CType(fieldInfo.GetValue(browser), ImageList)
    End Function

End Class
