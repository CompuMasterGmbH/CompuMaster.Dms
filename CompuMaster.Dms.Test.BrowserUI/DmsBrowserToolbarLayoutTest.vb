Imports System.Drawing
Imports System.Reflection
Imports System.Threading
Imports System.Windows.Forms
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture>
<Apartment(ApartmentState.STA)>
Public Class DmsBrowserToolbarLayoutTest

    <Test>
    Public Sub FileToolbarHostUsesVisibleToolStripsToDetermineItsHeight()
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser()
            browser.ShowInTaskbar = False
            browser.Opacity = 0
            browser.Show()

            Dim splitContainer As SplitContainer = GetFieldValue(Of SplitContainer)(browser, "SplitContainer")
            Dim toolbarHost As FlowLayoutPanel = GetFieldValue(Of FlowLayoutPanel)(browser, "FlowLayoutPanel1")
            Dim fileList As ListView = GetFieldValue(Of ListView)(browser, "ListViewDmsFiles")
            Dim fileActionsToolbar As ToolStrip = GetFieldValue(Of ToolStrip)(browser, "ToolStripFileActions")
            Dim fileSharingToolbar As ToolStrip = GetFieldValue(Of ToolStrip)(browser, "ToolStripFileShareActions")
            Dim folderSharingToolbar As ToolStrip = GetFieldValue(Of ToolStrip)(browser, "ToolStripFolderShareActions")
            Dim propertiesToolbar As ToolStrip = GetFieldValue(Of ToolStrip)(browser, "ToolStripProperties")

            browser.ClientSize = New Size(1500, 700)
            Dim widthWithoutSharingToolbars As Integer = fileActionsToolbar.Width + fileActionsToolbar.Margin.Horizontal + propertiesToolbar.Width + propertiesToolbar.Margin.Horizontal
            Dim widthWithSharingToolbars As Integer = widthWithoutSharingToolbars + fileSharingToolbar.Width + fileSharingToolbar.Margin.Horizontal + folderSharingToolbar.Width + folderSharingToolbar.Margin.Horizontal
            Dim targetFilePanelWidth As Integer = (widthWithoutSharingToolbars + widthWithSharingToolbars) \ 2
            splitContainer.SplitterDistance = splitContainer.ClientSize.Width - splitContainer.SplitterWidth - targetFilePanelWidth
            PerformFilePanelLayout(browser, splitContainer, toolbarHost)
            Dim heightWithSharingToolbars As Integer = toolbarHost.Height

            fileSharingToolbar.Visible = False
            folderSharingToolbar.Visible = False
            PerformFilePanelLayout(browser, splitContainer, toolbarHost)

            ClassicAssert.IsTrue(toolbarHost.AutoSize)
            ClassicAssert.AreEqual(AutoSizeMode.GrowAndShrink, toolbarHost.AutoSizeMode)
            ClassicAssert.AreEqual(DockStyle.Top, toolbarHost.Dock)
            ClassicAssert.AreEqual(DockStyle.Fill, fileList.Dock)
            ClassicAssert.Less(toolbarHost.Height, heightWithSharingToolbars)
            ClassicAssert.AreEqual(toolbarHost.Bottom, fileList.Top)
        End Using
    End Sub

    <Test>
    Public Sub FileToolbarLayoutRemainsAdjacentToFileListWhenDialogIsResized()
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser()
            browser.ShowInTaskbar = False
            browser.Opacity = 0
            browser.Show()

            Dim splitContainer As SplitContainer = GetFieldValue(Of SplitContainer)(browser, "SplitContainer")
            Dim toolbarHost As FlowLayoutPanel = GetFieldValue(Of FlowLayoutPanel)(browser, "FlowLayoutPanel1")
            Dim fileList As ListView = GetFieldValue(Of ListView)(browser, "ListViewDmsFiles")

            browser.ClientSize = New Size(800, 500)
            PerformFilePanelLayout(browser, splitContainer, toolbarHost)
            Dim narrowToolbarHeight As Integer = toolbarHost.Height
            ClassicAssert.AreEqual(toolbarHost.Bottom, fileList.Top)

            browser.ClientSize = New Size(1400, 700)
            PerformFilePanelLayout(browser, splitContainer, toolbarHost)

            ClassicAssert.Less(toolbarHost.Height, narrowToolbarHeight)
            ClassicAssert.AreEqual(toolbarHost.Bottom, fileList.Top)
            ClassicAssert.AreEqual(splitContainer.Panel2.ClientSize.Width, toolbarHost.Width)
        End Using
    End Sub

    <Test>
    Public Sub FileToolbarsUseDpiScaledIconsFromHighResolutionSources()
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser()
            Dim toolbarNames As String() = {
                "ToolStripFileActions",
                "ToolStripFileShareActions",
                "ToolStripFolderShareActions",
                "ToolStripProperties"
            }

            For Each toolbarName As String In toolbarNames
                Dim toolbar As ToolStrip = GetFieldValue(Of ToolStrip)(browser, toolbarName)
                ClassicAssert.AreEqual(New Size(16, 16), toolbar.ImageScalingSize, toolbarName)

                For Each item As ToolStripItem In toolbar.Items
                    If item.Image IsNot Nothing Then
                        ClassicAssert.GreaterOrEqual(item.Image.Width, 64, item.Name)
                        ClassicAssert.GreaterOrEqual(item.Image.Height, 64, item.Name)
                    End If
                Next
            Next

            browser.UpdateFileToolbarIconSize(120)
            AssertToolbarIconSize(browser, toolbarNames, New Size(20, 20))

            browser.UpdateFileToolbarIconSize(144)
            AssertToolbarIconSize(browser, toolbarNames, New Size(24, 24))
        End Using
    End Sub

    Private Shared Sub AssertToolbarIconSize(browser As Global.CompuMaster.Dms.BrowserUI.DmsBrowser, toolbarNames As IEnumerable(Of String), expectedSize As Size)
        For Each toolbarName As String In toolbarNames
            Dim toolbar As ToolStrip = GetFieldValue(Of ToolStrip)(browser, toolbarName)
            ClassicAssert.AreEqual(expectedSize, toolbar.ImageScalingSize, toolbarName)
        Next
    End Sub

    Private Shared Sub PerformFilePanelLayout(browser As Form, splitContainer As SplitContainer, toolbarHost As FlowLayoutPanel)
        browser.PerformLayout()
        splitContainer.PerformLayout()
        splitContainer.Panel2.PerformLayout()
        toolbarHost.PerformLayout()
        splitContainer.Panel2.PerformLayout()
    End Sub

    Private Shared Function GetFieldValue(Of T)(browser As Global.CompuMaster.Dms.BrowserUI.DmsBrowser, fieldName As String) As T
        Dim fieldInfo As FieldInfo = GetType(Global.CompuMaster.Dms.BrowserUI.DmsBrowser).GetField(fieldName, BindingFlags.Instance Or BindingFlags.NonPublic)
        If fieldInfo Is Nothing Then
            fieldInfo = GetType(Global.CompuMaster.Dms.BrowserUI.DmsBrowser).GetField("_" & fieldName, BindingFlags.Instance Or BindingFlags.NonPublic)
        End If
        ClassicAssert.IsNotNull(fieldInfo)
        Return DirectCast(fieldInfo.GetValue(browser), T)
    End Function

End Class
