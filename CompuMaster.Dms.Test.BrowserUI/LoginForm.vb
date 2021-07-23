Imports System.Windows.Forms

Public Class LoginForm

    Private Sub OK_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles OK.Click
        Dim b As New CompuMaster.Dms.BrowserUI.DmsBrowser(
            New DmsLoginProfile() With {
                    .DmsProvider = Providers.BaseDmsProvider.DmsProviders.Scopevisio,
                    .CustomerInstance = Me.CustomerNoTextBox.Text,
                    .Username = Me.UsernameTextBox.Text,
                    .Password = Me.PasswordTextBox.Text
                },
                "DMS Browser DEMO for Scopevisio Teamwork", Me.Icon,
                "", "",
                BrowserUI.DmsBrowser.BrowseModes.FoldersAndFiles,
                BrowserUI.DmsBrowser.FileOrFolderActions.AllowCopyRenameMoveFiles Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowCreateFolders Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowDeleteFiles Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowDownloadFiles Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowSharings Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowSwitchBrowseMode Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowUploadFiles,
                BrowserUI.DmsBrowser.DialogOperationModes.NoResults,
                "", "", ""
            )
        b.ShowDialog(Me)
    End Sub

    Private Sub Cancel_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Cancel.Click
        Me.Close()
    End Sub

End Class
