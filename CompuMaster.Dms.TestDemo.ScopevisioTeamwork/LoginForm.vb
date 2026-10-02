Imports System.ComponentModel
Imports System.Windows.Forms
Imports System.Drawing

Public Class LoginForm

    ''' <summary>Creates the login form with the provider-independent default icon.</summary>
    <Obsolete("Use overload instead")>
    <EditorBrowsable(EditorBrowsableState.Never)>
    Public Sub New()
        Me.New(Nothing)
    End Sub

    ''' <summary>Creates the login form with the specified icon.</summary>
    ''' <param name="formIcon">The window icon, or <see langword="Nothing"/> to use the provider-independent default icon.</param>
    Public Sub New(formIcon As Icon)
        MyBase.New()
        InitializeComponent()
        Me.Icon = If(formIcon, CType((New ComponentResourceManager(GetType(BrowserUI.DmsBrowser))).GetObject("$this.Icon"), Icon))
    End Sub

    Private Sub Form_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Me.Load
        Me.UsernameTextBox.Text = Settings.InputFromBufferFile("username")
        Me.PasswordTextBox.Text = Settings.InputFromBufferFile("password")
        Me.CustomerNoTextBox.Text = Settings.InputFromBufferFile("customer no.")
        Me.StartPathTextBox.Text = If(Settings.InputFromBufferFile("start path") <> Nothing, Settings.InputFromBufferFile("start path"), "/")
        If Settings.IsBufferedByFile("username") OrElse Settings.IsBufferedByFile("password") OrElse Settings.IsBufferedByFile("customer no.") Then
            Me.CheckboxPersistLoginCredentialsToDisk.Checked = True
        Else
            Me.CheckboxPersistLoginCredentialsToDisk.Checked = False
        End If
    End Sub

    Private Sub LoginForm_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If Me.CheckboxPersistLoginCredentialsToDisk.Checked Then
            Settings.PersistInputValue("username", Me.UsernameTextBox.Text)
            Settings.PersistInputValue("password", Me.PasswordTextBox.Text)
            Settings.PersistInputValue("customer no.", Me.CustomerNoTextBox.Text)
            Settings.PersistInputValue("start path", Me.StartPathTextBox.Text)
        Else
            Settings.RemoveBufferFile("username")
            Settings.RemoveBufferFile("password")
            Settings.RemoveBufferFile("customer no.")
            Settings.RemoveBufferFile("start path")
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
            Dim InstanceSelector As New BrowserUI.RemoteInstanceSelector(
                        BrowserUI.RemoteInstanceSelector.StartupInstance.SelectionDialog,
                        AddressOf BrowserUI.RemoteInstanceSelector.ShowDefaultSelectionDialog)
            Dim SelectedProvider As Providers.BaseDmsProvider = InstanceSelector.CreateSelectedProvider(LoginProfile, Me)
            If SelectedProvider Is Nothing Then Return
            Using Browser As New CompuMaster.Dms.BrowserUI.DmsBrowser(
                        SelectedProvider, InstanceSelector.SelectionDialog,
                        "DMS Browser DEMO for Scopevisio Teamwork", Me.Icon,
                        If(Me.StartPathTextBox.Text.StartsWith("/"), Me.StartPathTextBox.Text.Substring(1), Me.StartPathTextBox.Text), "",
                        BrowserUI.DmsBrowser.BrowseModes.FoldersAndFiles,
                        BrowserUI.DmsBrowser.FileOrFolderActions.AllowCopyRenameMoveFiles Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowCreateFolders Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowDeleteFiles Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowDownloadFiles Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowSharings Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowSwitchBrowseMode Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowSwitchDmsInstance Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowUploadFiles,
                        BrowserUI.DmsBrowser.DialogOperationModes.NoResults,
                        "", "", ""
                    )
                Me.Cursor = Cursors.Default
                Me.UseWaitCursor = False
                Me.Refresh()
                Browser.ShowDialog(Me)
            End Using
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
        Finally
            Me.Cursor = Cursors.Default
            Me.UseWaitCursor = False
        End Try
    End Sub

    Private Sub Cancel_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Cancel.Click
        Me.Close()
    End Sub

End Class
