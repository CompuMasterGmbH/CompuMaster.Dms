Imports System.ComponentModel
Imports System.Windows.Forms
Imports System.Drawing

Public Class LoginForm
    Private ReadOnly InputBuffer As DemoInputBuffer

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
        Me.New(formIcon, loginImage, Settings.Storage())
    End Sub

    'The isolated GUI harness can inject private buffers and a fake environment before any Load/Closing event.
    Friend Sub New(formIcon As Icon, loginImage As Image, inputBuffer As DemoInputBuffer)
        MyBase.New()
        If inputBuffer Is Nothing Then Throw New ArgumentNullException(NameOf(inputBuffer))
        Me.InputBuffer = inputBuffer
        InitializeComponent()
        Me.LoginImage = loginImage
        DemoStrings.ApplyLoginLabels(Me)
        Me.Icon = If(formIcon, CType((New ComponentResourceManager(GetType(BrowserUI.DmsBrowser))).GetObject("$this.Icon"), Icon))
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
        Me.UsernameTextBox.Text = InputBuffer.Read("username")
        Me.PasswordTextBox.Text = InputBuffer.Read("password")
        Me.CustomerNoTextBox.Text = InputBuffer.Read("customer no.")
        Me.OrganisationIdTextBox.Text = InputBuffer.Read(ScopevisioDemoSelection.OrganisationField)
        Me.ExpectedTeamworkTenantIdTextBox.Text = InputBuffer.Read(ScopevisioDemoSelection.TenantField)
        Dim startPath = InputBuffer.Read("start path")
        Me.StartPathTextBox.Text = If(startPath <> Nothing, startPath, "/")
        If InputBuffer.Exists("username") OrElse InputBuffer.Exists("password") OrElse InputBuffer.Exists("customer no.") OrElse
           InputBuffer.Exists(ScopevisioDemoSelection.OrganisationField) OrElse InputBuffer.Exists(ScopevisioDemoSelection.TenantField) Then
            Me.CheckboxPersistLoginCredentialsToDisk.Checked = True
        Else
            Me.CheckboxPersistLoginCredentialsToDisk.Checked = False
        End If
    End Sub

    Private Sub LoginForm_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If Me.CheckboxPersistLoginCredentialsToDisk.Checked Then
            Try
                ScopevisioDemoSelection.PersistInputs(InputBuffer, Me.UsernameTextBox.Text, Me.PasswordTextBox.Text, Me.CustomerNoTextBox.Text, Me.StartPathTextBox.Text,
                    Me.OrganisationIdTextBox.Text, Me.ExpectedTeamworkTenantIdTextBox.Text, DemoStrings.GetText("InvalidOrganisationId"))
            Catch ex As ArgumentException
                e.Cancel = True
                MessageBox.Show(Me, ex.Message, DemoStrings.GetText("OpenDmsFailed"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End Try
        Else
            InputBuffer.Remove("username")
            InputBuffer.Remove("password")
            InputBuffer.Remove("customer no.")
            InputBuffer.Remove("start path")
            InputBuffer.Remove(ScopevisioDemoSelection.OrganisationField)
            InputBuffer.Remove(ScopevisioDemoSelection.TenantField)
        End If
    End Sub

    Private Sub OK_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles OK.Click
        Try
            Me.UseWaitCursor = True
            Me.Cursor = Cursors.WaitCursor
            Me.Refresh()
            Dim expectedTenant = Me.ExpectedTeamworkTenantIdTextBox.Text
            Dim LoginProfile = ScopevisioDemoSelection.CreateInteractiveProfile(Me.UsernameTextBox.Text, Me.PasswordTextBox.Text, Me.CustomerNoTextBox.Text,
                Me.OrganisationIdTextBox.Text, expectedTenant, DemoStrings.GetText("InvalidOrganisationId"))
            Dim InstanceSelector As New BrowserUI.RemoteInstanceSelector(
                        BrowserUI.RemoteInstanceSelector.StartupInstance.SelectionDialog,
                        AddressOf BrowserUI.RemoteInstanceSelector.ShowDefaultSelectionDialog)
            Dim SelectedProvider = ScopevisioDemoSelection.SelectInteractiveProvider(LoginProfile, expectedTenant,
                Function(credentials)
                    Dim direct As New Providers.ScopevisioTeamworkDmsProvider()
                    direct.Authorize(credentials)
                    Return direct
                End Function,
                Function(profile) InstanceSelector.CreateSelectedProvider(profile, Me),
                DemoStrings.GetText("SelectedTeamworkTenantMismatch"))
            If SelectedProvider Is Nothing Then Return
            Dim allowedActions = BrowserUI.DmsBrowser.FileOrFolderActions.AllowCopyRenameMoveFiles Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowCreateFolders Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowDeleteFiles Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowDownloadFiles Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowSharings Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowSwitchBrowseMode Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowSwitchDmsInstance Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowUploadFiles Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowViewFileVersionHistory Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowRestoreFileVersions Or BrowserUI.DmsBrowser.FileOrFolderActions.AllowRemoveFileVersions
            If Not ScopevisioDemoSelection.AllowsInstanceSwitch(LoginProfile, expectedTenant) Then allowedActions = allowedActions And Not BrowserUI.DmsBrowser.FileOrFolderActions.AllowSwitchDmsInstance
            Using Browser As New CompuMaster.Dms.BrowserUI.DmsBrowser(
                        SelectedProvider, InstanceSelector.SelectionDialog,
                        "DMS Browser DEMO for Scopevisio Teamwork", Me.Icon,
                        If(Me.StartPathTextBox.Text.StartsWith("/"), Me.StartPathTextBox.Text.Substring(1), Me.StartPathTextBox.Text), "",
                        BrowserUI.DmsBrowser.BrowseModes.FoldersAndFiles,
                        allowedActions,
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
            System.Windows.Forms.MessageBox.Show(Me, ex.Message, DemoStrings.GetText("OpenDmsFailed"), MessageBoxButtons.OK, MessageBoxIcon.Error)
#Disable Warning CA1031 ' Do not catch general exception types
        Catch ex As Exception
            Me.Cursor = Cursors.Default
            Me.UseWaitCursor = False
            Me.Refresh()
            System.Windows.Forms.MessageBox.Show(Me, ex.ToString, DemoStrings.GetText("OpenDmsFailed"), MessageBoxButtons.OK, MessageBoxIcon.Error)
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
