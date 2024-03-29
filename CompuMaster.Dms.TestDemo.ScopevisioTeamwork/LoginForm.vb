Imports System.ComponentModel
Imports System.Windows.Forms

Public Class LoginForm

    Private Sub Form_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Me.Load
        Me.UsernameTextBox.Text = Settings.InputFromBufferFile("Username")
        Me.PasswordTextBox.Text = Settings.InputFromBufferFile("Password")
        Me.CustomerNoTextBox.Text = Settings.InputFromBufferFile("Customer")
        Me.StartPathTextBox.Text = If(Settings.InputFromBufferFile("StartPath") <> Nothing, Settings.InputFromBufferFile("StartPath"), "/")
        If Settings.IsBufferedByFile("Username") OrElse Settings.IsBufferedByFile("Password") OrElse Settings.IsBufferedByFile("ServerAddress") Then
            Me.CheckboxPersistLoginCredentialsToDisk.Checked = True
        Else
            Me.CheckboxPersistLoginCredentialsToDisk.Checked = False
        End If
    End Sub

    Private Sub LoginForm_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If Me.CheckboxPersistLoginCredentialsToDisk.Checked Then
            Settings.PersistInputValue("Username", Me.UsernameTextBox.Text)
            Settings.PersistInputValue("Password", Me.PasswordTextBox.Text)
            Settings.PersistInputValue("Customer", Me.CustomerNoTextBox.Text)
            Settings.PersistInputValue("StartPath", Me.StartPathTextBox.Text)
        Else
            Settings.RemoveBufferFile("Username")
        Settings.RemoveBufferFile("Password")
        Settings.RemoveBufferFile("ServerAddress")
        End If
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
                        If(Me.StartPathTextBox.Text.StartsWith("/"), Me.StartPathTextBox.Text.Substring(1), Me.StartPathTextBox.Text), "",
                        BrowserUI.DmsBrowser.BrowseModes.FoldersAndFiles,
                        BrowserUI.DmsBrowser.FileOrFolderActions.AllowCopyRenameMoveFiles Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowCreateFolders Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowDeleteFiles Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowDownloadFiles Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowSharings Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowSwitchBrowseMode Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowUploadFiles,
                        BrowserUI.DmsBrowser.DialogOperationModes.NoResults,
                        "", "", ""
                    )
            Me.Cursor = Cursors.Default
            Me.UseWaitCursor = False
            Me.Refresh()
            Browser.ShowDialog(Me)
        Catch ex As CompuMaster.Dms.Data.DirectoryNotFoundException
            Me.Cursor = Cursors.Default
            Me.UseWaitCursor = False
            Me.Refresh()
            System.Windows.Forms.MessageBox.Show(Me, ex.Message, Nothing, MessageBoxButtons.OK, MessageBoxIcon.Error)
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
