Option Explicit On
Option Strict On

Imports System.Windows.Forms
Imports CompuMaster.Dms.Providers

Partial Public Class DmsBrowser
    Friend Function RootPropertiesDetails(node As TreeNode) As String
        If node Is Nothing OrElse node IsNot RootNode OrElse node.TreeView IsNot TreeViewDmsFolders OrElse HasScopedBrowserRoot Then Return Nothing
        Dim nodeData = TryCast(node.Tag, NodeTagData)
        If nodeData Is Nothing OrElse nodeData.DmsResourceItem IsNot Nothing Then Return Nothing

        Dim details = UiStrings.GetText("RootValue")
        'Use the currently authenticated instance, without discovering other tenants.
        Dim instance = TryCast(DmsProvider, IDmsInstanceProvider)?.CurrentDmsInstance
        If instance IsNot Nothing Then
            details &= Environment.NewLine & Environment.NewLine &
                UiStrings.GetText("PropertiesInstanceName") & instance.DisplayName & Environment.NewLine &
                UiStrings.GetText("PropertiesInstanceId") & instance.ID
        End If
        Return details
    End Function
End Class
