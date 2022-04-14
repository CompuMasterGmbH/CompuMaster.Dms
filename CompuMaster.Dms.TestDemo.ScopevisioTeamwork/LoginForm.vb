Imports System.ComponentModel
Imports System.Windows.Forms

Public Class LoginForm

    Private Sub Form_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Me.Load
        Me.UsernameTextBox.Text = Settings.InputFromBufferFile("Username")
        Me.PasswordTextBox.Text = Settings.InputFromBufferFile("Password")
        Me.CustomerNoTextBox.Text = Settings.InputFromBufferFile("Customer")
    End Sub

    Private Sub LoginForm_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        Settings.PersistInputValue("Username", Me.UsernameTextBox.Text)
        Settings.PersistInputValue("Password", Me.PasswordTextBox.Text)
        Settings.PersistInputValue("Customer", Me.CustomerNoTextBox.Text)
    End Sub

    Private Sub OK_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles OK.Click
        Try
            Me.UseWaitCursor = True
            Me.Cursor = Cursors.WaitCursor
            Me.Refresh()
            Dim LoginProfile As New DmsLoginProfile() With {
                            .DmsProvider = Providers.BaseDmsProvider.DmsProviders.Scopevisio,
                            .CustomerInstance = Me.CustomerNoTextBox.Text,
                            .Username = Me.UsernameTextBox.Text,
                            .Password = Me.PasswordTextBox.Text
                        }
            Dim Browser As New CompuMaster.Dms.BrowserUI.DmsBrowser(
                        LoginProfile,
                        "DMS Browser DEMO for Scopevisio Teamwork", Me.Icon,
                        "", "",
                        BrowserUI.DmsBrowser.BrowseModes.FoldersAndFiles,
                        BrowserUI.DmsBrowser.FileOrFolderActions.AllowCopyRenameMoveFiles Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowCreateFolders Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowDeleteFiles Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowDownloadFiles Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowSharings Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowSwitchBrowseMode Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowUploadFiles,
                        BrowserUI.DmsBrowser.DialogOperationModes.NoResults,
                        "", "", ""
                    )
            Me.Cursor = Cursors.Default
            Me.UseWaitCursor = False
            Me.Refresh()
            Browser.ShowDialog(Me)
#Disable Warning CA1031 ' Do not catch general exception types
        Catch ex As Exception
            Me.Cursor = Cursors.Default
            Me.UseWaitCursor = False
            Me.Refresh()
            System.Windows.Forms.MessageBox.Show(Me, ex.ToString, Nothing, MessageBoxButtons.OK, MessageBoxIcon.Error)
#Enable Warning CA1031 ' Do not catch general exception types
        End Try
    End Sub

    Private Sub Cancel_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Cancel.Click
        Me.Close()
    End Sub

End Class
