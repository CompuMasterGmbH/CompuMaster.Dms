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
        Me.New(formIcon, Nothing)
    End Sub

    ''' <summary>Creates the login form with the specified icon and artwork.</summary>
    ''' <param name="formIcon">The window icon, or <see langword="Nothing"/> to use the default icon.</param>
    ''' <param name="loginImage">The artwork copied for this form, or <see langword="Nothing"/> to use the common document-management illustration.</param>
    ''' <remarks>The caller retains ownership of the supplied image and may dispose it after construction.</remarks>
    Public Sub New(formIcon As Icon, loginImage As Image)
        MyBase.New()
        InitializeComponent()
        Me.LoginImage = loginImage
        DemoStrings.ApplyLoginLabels(Me)
        Me.Icon = If(formIcon, CType((New ComponentResourceManager(GetType(Global.CompuMaster.Dms.BrowserUI.DmsBrowser))).GetObject("$this.Icon"), Icon))
    End Sub

    ''' <summary>Gets or sets the artwork displayed beside the login fields.</summary>
    ''' <value>The form-owned display image. Assigning <see langword="Nothing"/> restores the common default.</value>
    ''' <remarks>The setter copies the supplied image; the caller retains ownership of its original.
    ''' The getter returns a borrowed form-owned image that must not be disposed by the caller.</remarks>
    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public Property LoginImage As Image
        Get
            Return Me.LogoPictureBox.Image
        End Get
        Set(value As Image)
            Dim replacement = DemoLoginArtwork.CreateImage(value)
            Dim previous = Me.LogoPictureBox.Image
            Me.LogoPictureBox.Image = replacement
            previous?.Dispose()
        End Set
    End Property

    Private Sub Form_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Me.Load
        Me.Text = Settings.DemoTitle
        Me.UsernameTextBox.Text = Settings.Username()
        Me.PasswordTextBox.Text = Settings.Password()
        Me.ServerAddress.Text = Settings.ServerUrl()
        Me.StartPathTextBox.Text = If(Settings.InputFromBufferFile("start path"), "/")
        Me.CheckboxPersistLoginCredentialsToDisk.Checked = Settings.HasPersistedCredentials()
    End Sub

    Private Sub LoginForm_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If Me.CheckboxPersistLoginCredentialsToDisk.Checked Then
            Settings.PersistCredentials(Me.UsernameTextBox.Text, Me.PasswordTextBox.Text, Me.ServerAddress.Text)
            Settings.PersistInputValue("start path", Me.StartPathTextBox.Text)
        Else
            Settings.RemovePersistedCredentials()
            Settings.RemoveBufferFile("start path")
        End If
    End Sub

    Private Sub OK_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles OK.Click
        Try
            Me.UseWaitCursor = True
            Me.Cursor = Cursors.WaitCursor
            Me.Refresh()
            Dim b As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(
            New DmsLoginProfile() With {
                    .DmsProvider = Providers.BaseDmsProvider.DmsProviders.WebDAV,
                    .BaseUrl = Settings.ResolveServerUrl(Me.ServerAddress.Text, Me.UsernameTextBox.Text),
                    .Username = Me.UsernameTextBox.Text,
                    .Password = Me.PasswordTextBox.Text
                },
                Settings.DemoTitle, Me.Icon,
                If(Me.StartPathTextBox.Text.StartsWith("/"), Me.StartPathTextBox.Text.Substring(1), Me.StartPathTextBox.Text), "",
                Global.CompuMaster.Dms.BrowserUI.DmsBrowser.BrowseModes.FoldersAndFiles,
                Global.CompuMaster.Dms.BrowserUI.DmsBrowser.FileOrFolderActions.AllowCopyRenameMoveFiles Or Global.CompuMaster.Dms.BrowserUI.DmsBrowser.FileOrFolderActions.AllowCreateFolders Or Global.CompuMaster.Dms.BrowserUI.DmsBrowser.FileOrFolderActions.AllowDeleteFiles Or Global.CompuMaster.Dms.BrowserUI.DmsBrowser.FileOrFolderActions.AllowDownloadFiles Or Global.CompuMaster.Dms.BrowserUI.DmsBrowser.FileOrFolderActions.AllowSharings Or Global.CompuMaster.Dms.BrowserUI.DmsBrowser.FileOrFolderActions.AllowSwitchBrowseMode Or Global.CompuMaster.Dms.BrowserUI.DmsBrowser.FileOrFolderActions.AllowSwitchDmsInstance Or Global.CompuMaster.Dms.BrowserUI.DmsBrowser.FileOrFolderActions.AllowUploadFiles,
                Global.CompuMaster.Dms.BrowserUI.DmsBrowser.DialogOperationModes.NoResults,
                "", "", ""
            )
            Me.Cursor = Cursors.Default
            Me.UseWaitCursor = False
            Me.Refresh()
            b.ShowDialog(Me)
        Catch ex As CompuMaster.Dms.Data.DirectoryNotFoundException
            Me.Cursor = Cursors.Default
            Me.UseWaitCursor = False
            Me.Refresh()
            System.Windows.Forms.MessageBox.Show(Me, ex.Message, DemoStrings.GetText("OpenDmsFailed"), MessageBoxButtons.OK, MessageBoxIcon.Error)
#Disable Warning CA1031 ' Do not catch general exception types
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, ex.ToString, DemoStrings.GetText("OpenDmsFailed"), MessageBoxButtons.OK, MessageBoxIcon.Error)
#Enable Warning CA1031 ' Do not catch general exception types
            Me.Cursor = Cursors.Default
            Me.UseWaitCursor = False
        End Try
    End Sub

    Private Sub Cancel_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Cancel.Click
        Me.Close()
    End Sub

End Class
