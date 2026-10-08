Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Providers

''' <summary>Edits permissions granted to an internal DMS user or group.</summary>
Public Class DmsStandardShareSetup

    Private ReadOnly PendingOperation As New UiAsyncOperation(Me)

    ''' <summary>Initializes the internal sharing dialog.</summary>
    <Obsolete("Use overloaded constructor")>
    Public Sub New()
        InitializeComponent()
        ApplyLocalizedText()
    End Sub

    ''' <summary>Initializes the internal sharing dialog.</summary>
    ''' <param name="userSharing">The existing user share, or Nothing when preparing a new share.</param>
    ''' <param name="dmsProvider">The authorized provider used for the operation.</param>
    ''' <param name="hideIDs">The identifiers omitted from the list of addable users or groups.</param>
    Public Sub New(userSharing As DmsShareForUser, dmsProvider As Providers.BaseDmsProvider, hideIDs As List(Of String))
        InitializeComponent()
        ApplyLocalizedText()
        Me.DialogObjectMode = DialogObjectModes.UserSharing
        Me.DmsSharingDetails = userSharing
        If userSharing IsNot Nothing Then
            Me.DmsItem = userSharing.ParentDmsResourceItem
        End If
        Me.DmsProvider = dmsProvider
        Me.HideIDs = hideIDs
    End Sub

    ''' <summary>Initializes the internal sharing dialog.</summary>
    ''' <param name="groupSharing">The existing group share, or Nothing when preparing a new share.</param>
    ''' <param name="dmsProvider">The authorized provider used for the operation.</param>
    ''' <param name="hideIDs">The identifiers omitted from the list of addable users or groups.</param>
    Public Sub New(groupSharing As DmsShareForGroup, dmsProvider As Providers.BaseDmsProvider, hideIDs As List(Of String))
        InitializeComponent()
        ApplyLocalizedText()
        Me.DialogObjectMode = DialogObjectModes.GroupSharing
        Me.DmsSharingDetails = groupSharing
        If groupSharing IsNot Nothing Then
            Me.DmsItem = groupSharing.ParentDmsResourceItem
        End If
        Me.DmsProvider = dmsProvider
        Me.HideIDs = hideIDs
    End Sub

    Private Sub ApplyLocalizedText()
        Me.Text = UiStrings.GetText("ShareSetupTitle")
        Me.CheckBoxAllowView.Text = UiStrings.GetText("PermissionView")
        Me.CheckBoxAllowShare.Text = UiStrings.GetText("PermissionShare")
        Me.CheckBoxAllowDelete.Text = UiStrings.GetText("PermissionDelete")
        Me.CheckBoxAllowUpload.Text = UiStrings.GetText("PermissionUpload")
        Me.CheckBoxAllowDownload.Text = UiStrings.GetText("PermissionDownload")
        Me.CheckBoxAllowEdit.Text = UiStrings.GetText("PermissionEdit")
        Me.GroupBoxAuthorizations.Text = UiStrings.GetText("Permissions")
        Me.ButtonCancel.Text = UiStrings.GetText("ActionCancel")
        Me.ButtonSave.Text = UiStrings.GetText("ActionSave")
        Me.GroupBoxGeneral.Text = UiStrings.GetText("GeneralSettings")
        Me.LabelName.Text = UiStrings.GetText("LabelName")
        LocalizedLayout.FitButtons(Me)
        LocalizedLayout.FitFieldColumns(Me.GroupBoxGeneral)
        LocalizedLayout.Bind(Me, AddressOf ArrangeLocalizedControls)
    End Sub

    Private Sub ArrangeLocalizedControls()
        LocalizedLayout.FitButtons(Me)
        LocalizedLayout.FitRows(Me.GroupBoxGeneral)
        LocalizedLayout.FitRows(Me.GroupBoxAuthorizations, True)
        Dim minimumWidth = Math.Max(622, Math.Max(LocalizedLayout.MinimumFieldWidth(Me.GroupBoxGeneral), LocalizedLayout.MinimumPermissionWidth(Me.GroupBoxAuthorizations)) + 28)
        Me.ClientSize = New Drawing.Size(Math.Max(Me.ClientSize.Width, minimumWidth), Me.ClientSize.Height)
        Me.GroupBoxGeneral.Width = Me.ClientSize.Width - 28
        Me.GroupBoxAuthorizations.Width = Me.ClientSize.Width - 28
        LocalizedLayout.FitFieldColumns(Me.GroupBoxGeneral)
        Me.GroupBoxAuthorizations.Top = Me.GroupBoxGeneral.Bottom + 8
        Dim buttonsTop = Me.GroupBoxAuthorizations.Bottom + 8
        Me.ClientSize = New Drawing.Size(Me.ClientSize.Width, Math.Max(Me.ClientSize.Height, buttonsTop + Me.ButtonSave.Height + 12))
        Me.ButtonSave.Location = New Drawing.Point(Me.ClientSize.Width - Me.ButtonSave.Width - 14, buttonsTop)
        Me.ButtonCancel.Location = New Drawing.Point(Me.ButtonSave.Left - Me.ButtonCancel.Width - 8, buttonsTop)
        Me.MinimumSize = New Drawing.Size(minimumWidth + Me.Width - Me.ClientSize.Width, buttonsTop + Me.ButtonSave.Height + 12 + Me.Height - Me.ClientSize.Height)
    End Sub

    ''' <summary>Gets or sets the original sharing settings to edit.</summary>
    Public Property DmsSharingDetails As DmsShareBase
    ''' <summary>Gets or sets the provider associated with this object.</summary>
    Public Property DmsProvider As Providers.BaseDmsProvider
    ''' <summary>
    ''' DmsResourceItem required for creation of links
    ''' </summary>
    Public Property DmsItem As DmsResourceItem
    ''' <summary>
    ''' IDs which shall be hidden in list of addable users/groups
    ''' </summary>
    Public Property HideIDs As List(Of String)

    Private _DmsUpdatedSharingDetails As DmsShareBase = Nothing
    ''' <summary>Gets the updated share returned after a successful save, or Nothing before saving.</summary>
    Public ReadOnly Property DmsUpdatedSharingDetails As DmsShareBase
        Get
            Return Me._DmsUpdatedSharingDetails
        End Get
    End Property

    ''' <summary>Identifies whether the sharing dialog targets a user or group.</summary>
    Public Enum DialogObjectModes As Byte
        ''' <summary>Edits permissions for a group.</summary>
        GroupSharing = 1
        ''' <summary>Edits permissions for a user.</summary>
        UserSharing = 2
    End Enum

    Private _DialogObjectMode As DialogObjectModes
    ''' <summary>Sets whether the dialog edits a user or group share.</summary>
    Public WriteOnly Property DialogObjectMode As DialogObjectModes
        Set(value As DialogObjectModes)
            Select Case value
                Case DialogObjectModes.GroupSharing
                    Me.LabelName.Text = UiStrings.GetText("LabelGroupName")
                Case DialogObjectModes.UserSharing
                    Me.LabelName.Text = UiStrings.GetText("LabelUserName")
                Case Else
                    Throw New ArgumentOutOfRangeException(NameOf(value))
            End Select
            _DialogObjectMode = value
            LocalizedLayout.FitFieldColumns(Me.GroupBoxGeneral)
        End Set
    End Property

    ''' <summary>Specifies whether the sharing dialog creates or updates a share.</summary>
    Public Enum DialogModes As Byte
        ''' <summary>Creates a new share or link.</summary>
        CreateLink = 1
        ''' <summary>Updates an existing share or link.</summary>
        UpdateLink = 2
    End Enum

    Private _DialogMode As DialogModes
    ''' <summary>Sets whether the dialog creates or updates sharing settings.</summary>
    Public WriteOnly Property DialogMode As DialogModes
        Set(value As DialogModes)
            Select Case value
                Case DialogModes.CreateLink
                Case DialogModes.UpdateLink
                Case Else
                    Throw New ArgumentOutOfRangeException(NameOf(value))
            End Select
            _DialogMode = value
        End Set
    End Property

    Private Async Sub DmsStandardShare_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Try
            Await PendingOperation.RunAsync(Function() LoadControlsAsync())
        Catch ex As Exception
            MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.Message), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
            Me.Close()
        End Try
    End Sub

    Private Async Function LoadControlsAsync() As Task
        Me.Text = String.Format(Me.Text, DmsItem.FullName)
        If Me._DialogMode = Nothing Then
            If Me.DmsSharingDetails Is Nothing Then
                Me.DialogMode = DialogModes.CreateLink
            Else
                Me.DialogMode = DialogModes.UpdateLink
            End If
        End If
        Select Case Me.DmsProvider.GetType.Name
            Case "ScopevisioTeamworkDmsProvider"
                If Me._DialogMode = DialogModes.CreateLink Then
                    'Create link mode: enable supported auths
                    Me.CheckBoxAllowView.Enabled = False
                    Me.CheckBoxAllowDownload.Enabled = False
                    Me.CheckBoxAllowEdit.Enabled = False
                    Me.CheckBoxAllowUpload.Enabled = False
                    Me.CheckBoxAllowDelete.Enabled = False
                    Me.CheckBoxAllowShare.Enabled = False
                Else
                    'Update link mode: view and upload links can't be exchanged and must remain
                    Me.CheckBoxAllowView.Enabled = False
                    Me.CheckBoxAllowDownload.Enabled = False
                    Me.CheckBoxAllowEdit.Enabled = False
                    Me.CheckBoxAllowUpload.Enabled = False
                    Me.CheckBoxAllowDelete.Enabled = False
                    Me.CheckBoxAllowShare.Enabled = False
                End If
            Case "WebDavDmsProvider"
                Me.CheckBoxAllowView.Enabled = False
                Me.CheckBoxAllowDownload.Enabled = False
                Me.CheckBoxAllowEdit.Enabled = True
                Me.CheckBoxAllowUpload.Enabled = True
                Me.CheckBoxAllowDelete.Enabled = True
                Me.CheckBoxAllowShare.Enabled = True
            Case Else
                Throw New NotImplementedException(UiStrings.Format("DmsProviderImplementationRequiredFor", Me.DmsProvider.GetType.Name))
        End Select
        Select Case _DialogMode
            Case DialogModes.CreateLink
                Me.CheckBoxAllowView.Checked = True
                Me.CheckBoxAllowDelete.Checked = True
                Me.CheckBoxAllowDownload.Checked = True
                Me.CheckBoxAllowEdit.Checked = True
                Me.CheckBoxAllowUpload.Checked = True
                Me.CheckBoxAllowShare.Checked = True
                Me.ComboBoxUsersOrGroups.Enabled = True
                Await Me.LoadUserOrGroupListAsync()
            Case DialogModes.UpdateLink
                Me.ComboBoxUsersOrGroups.Enabled = False
                Me.LoadDataIntoControls()
            Case Else
                Throw New NullReferenceException(NameOf(Me.DialogMode))
        End Select
        Me.SwitchControlsBasedOnCheckboxesForAllowedActions()
    End Function

    Private Sub SwitchControlsBasedOnCheckboxesForAllowedActions()
        Select Case Me.DmsProvider.GetType.Name
            Case "ScopevisioTeamworkDmsProvider", "WebDavDmsProvider"
            Case Else
                Throw New NotImplementedException(UiStrings.Format("DmsProviderImplementationRequiredFor", Me.DmsProvider.GetType.Name))
        End Select
    End Sub

    Private Async Function LoadUserOrGroupListAsync() As Task
        Select Case Me._DialogObjectMode
            Case DialogObjectModes.GroupSharing
                ComboBoxUsersOrGroups.Items.Clear()
                For Each Group As DmsGroup In Await Me.DmsProvider.GetAllGroupsAsync()
                    If Me.HideIDs Is Nothing OrElse Me.HideIDs.Contains(Group.ID) = False Then
                        Dim NewItem As New KeyValuePair(Of String, String)(Group.ID, Group.DisplayName)
                        ComboBoxUsersOrGroups.Items.Add(NewItem)
                    End If
                Next
            Case DialogObjectModes.UserSharing
                ComboBoxUsersOrGroups.Items.Clear()
                For Each User As DmsUser In Await Me.DmsProvider.GetAllUsersAsync()
                    If Me.HideIDs Is Nothing OrElse Me.HideIDs.Contains(User.ID) = False Then
                        Dim NewItem As New KeyValuePair(Of String, String)(User.ID, User.DisplayName)
                        ComboBoxUsersOrGroups.Items.Add(NewItem)
                    End If
                Next
            Case Else
                Throw New NotImplementedException(UiStrings.GetText("DialogObjectModeNotImplementedForLoading"))
        End Select
    End Function

    Private Sub LoadDataIntoControls()
        Select Case Me._DialogObjectMode
            Case DialogObjectModes.GroupSharing
                Dim GroupSharing As DmsShareForGroup = CType(Me.DmsSharingDetails, DmsShareForGroup)
                Dim Group As New KeyValuePair(Of String, String)(GroupSharing.Group.ID, GroupSharing.Group.DisplayName)
                ComboBoxUsersOrGroups.Items.Clear()
                ComboBoxUsersOrGroups.Items.Add(Group)
                ComboBoxUsersOrGroups.SelectedIndex = 0
                ComboBoxUsersOrGroups.Enabled = False
            Case DialogObjectModes.UserSharing
                Dim UserSharing As DmsShareForUser = CType(Me.DmsSharingDetails, DmsShareForUser)
                Dim User As New KeyValuePair(Of String, String)(UserSharing.User.ID, UserSharing.User.DisplayName)
                ComboBoxUsersOrGroups.Items.Clear()
                ComboBoxUsersOrGroups.Items.Add(User)
                ComboBoxUsersOrGroups.SelectedIndex = 0
                ComboBoxUsersOrGroups.Enabled = False
            Case Else
                Throw New NotImplementedException(UiStrings.GetText("DialogObjectModeNotImplementedForLoading"))
        End Select
        Me.CheckBoxAllowDelete.Checked = Me.DmsSharingDetails.AllowDelete
        Me.CheckBoxAllowDownload.Checked = Me.DmsSharingDetails.AllowDownload
        Me.CheckBoxAllowEdit.Checked = Me.DmsSharingDetails.AllowEdit
        Me.CheckBoxAllowUpload.Checked = Me.DmsSharingDetails.AllowUpload
        Me.CheckBoxAllowView.Checked = Me.DmsSharingDetails.AllowView
        Me.CheckBoxAllowShare.Checked = Me.DmsSharingDetails.AllowShare
    End Sub

    Private Sub SaveControlDataIntoDmsLink()
        If Me.ComboBoxUsersOrGroups.SelectedIndex < 0 Then
            Throw New Data.DmsUserInputMissingException(UiStrings.GetText("AuthorizationObjectRequired"))
        End If
        Dim Result As DmsShareBase
        Dim SelectedId As String = CType(Me.ComboBoxUsersOrGroups.SelectedItem, KeyValuePair(Of String, String)).Key
        Dim SelectedDisplayName As String = CType(Me.ComboBoxUsersOrGroups.SelectedItem, KeyValuePair(Of String, String)).Value
        If Me._DialogMode = DialogModes.CreateLink Then
            Select Case Me._DialogObjectMode
                Case DialogObjectModes.GroupSharing
                    Result = New DmsShareForGroup(Me.DmsItem, New DmsGroup() With {.ID = SelectedId, .Name = SelectedDisplayName}, False, False, False, False, False, False)
                Case DialogObjectModes.UserSharing
                    Result = New DmsShareForUser(Me.DmsItem, New DmsUser() With {.ID = SelectedId, .DisplayName = SelectedDisplayName}, False, False, False, False, False, False)
                Case Else
                    Throw New NotImplementedException(UiStrings.GetText("DialogObjectModeNotImplementedForSavingSharing"))
            End Select
        Else
            Select Case Me._DialogObjectMode
                Case DialogObjectModes.GroupSharing
                    Result = CType(CType(Me.DmsSharingDetails, DmsShareForGroup).Clone(), DmsShareForGroup)
                Case DialogObjectModes.UserSharing
                    Result = CType(CType(Me.DmsSharingDetails, DmsShareForUser).Clone(), DmsShareForUser)
                Case Else
                    Throw New NotImplementedException(UiStrings.GetText("DialogObjectModeNotImplementedForSavingSharing"))
            End Select
        End If
        'always keeps as it is: Me.DmsLinkDetails.ID
        Result.AllowDelete = Me.CheckBoxAllowDelete.Checked
        Result.AllowDownload = Me.CheckBoxAllowDownload.Checked
        Result.AllowEdit = Me.CheckBoxAllowEdit.Checked
        Result.AllowUpload = Me.CheckBoxAllowUpload.Checked
        Result.AllowView = Me.CheckBoxAllowView.Checked
        Result.AllowShare = Me.CheckBoxAllowShare.Checked
        Me._DmsUpdatedSharingDetails = Result
    End Sub

    Private Sub CheckBoxAllowDownload_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxAllowDownload.CheckedChanged
        If Me.CheckBoxAllowDownload.Checked Then
            Me.CheckBoxAllowView.Checked = True 'Download requires View auths
        End If
    End Sub

    Private Sub CheckBoxAllowView_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxAllowView.CheckedChanged
        If Me.CheckBoxAllowView.Checked = False Then
            Me.CheckBoxAllowDownload.Checked = False 'Download requires View auths
            Me.CheckBoxAllowEdit.Checked = False 'requires View auths
            Me.CheckBoxAllowDelete.Checked = False 'requires View auths
            Me.CheckBoxAllowShare.Checked = False 'requires View auths
        End If
    End Sub

    Private Async Sub ButtonSave_Click(sender As Object, e As EventArgs) Handles ButtonSave.Click
        If PendingOperation.IsRunning Then Return
        Try
            Select Case Me.DmsProvider.GetType.Name
                Case "ScopevisioTeamworkDmsProvider"
                    If Me.CheckBoxAllowDownload.Checked And Not Me.CheckBoxAllowView.Checked Then
                        Throw New DmsUserInputInvalidException(UiStrings.GetText("ViewRequiredForDownload"))
                    End If
                Case "WebDavDmsProvider"
                    If Me.CheckBoxAllowView.Checked <> Me.CheckBoxAllowDownload.Checked Then
                        Throw New DmsUserInputInvalidException(UiStrings.GetText("OCSRequiresViewAndDownloadPermissionsToBe"))
                    End If
                    If Not (Me.CheckBoxAllowView.Checked OrElse Me.CheckBoxAllowEdit.Checked OrElse Me.CheckBoxAllowUpload.Checked OrElse Me.CheckBoxAllowDelete.Checked OrElse Me.CheckBoxAllowShare.Checked) Then
                        Throw New DmsUserInputMissingException(UiStrings.GetText("AtLeastOneAuthorizationIsRequired"))
                    End If
                Case Else
                    Throw New NotImplementedException(UiStrings.Format("DmsProviderImplementationRequiredFor", Me.DmsProvider.GetType.Name))
            End Select
            Me.SaveControlDataIntoDmsLink()
            Await PendingOperation.RunAsync(Function() SaveSharingAsync(), Sub()
                                                                              Me.DialogResult = DialogResult.OK
                                                                              Me.Close()
                                                                          End Sub)
        Catch ex As Exception
            MessageBox.Show(Me, UiStrings.Format("ErrorMessage", If(TypeOf ex Is DmsUserErrorMessageException OrElse TypeOf ex Is NotSupportedException, ex.Message, ex.ToString())), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Async Function SaveSharingAsync() As Task
            If Me._DialogMode = DialogModes.CreateLink Then
                Select Case Me._DialogObjectMode
                    Case DialogObjectModes.GroupSharing
                        Await Me.DmsProvider.CreateSharingAsync(Me.DmsItem, CType(Me.DmsUpdatedSharingDetails, DmsShareForGroup))
                    Case DialogObjectModes.UserSharing
                        Await Me.DmsProvider.CreateSharingAsync(Me.DmsItem, CType(Me.DmsUpdatedSharingDetails, DmsShareForUser))
                    Case Else
                        Throw New NotImplementedException(UiStrings.GetText("DialogObjectModeNotImplementedForCreatingSharing"))
                End Select
            Else
                Select Case Me.DmsProvider.GetType.Name
                    Case "ScopevisioTeamworkDmsProvider"
                        'nothing to do since form controls are all disabled, user could not change anything, so no update command required
                    Case Else
                        Select Case Me._DialogObjectMode
                            Case DialogObjectModes.GroupSharing
                                Await Me.DmsProvider.UpdateSharingAsync(CType(Me.DmsUpdatedSharingDetails, DmsShareForGroup))
                            Case DialogObjectModes.UserSharing
                                Await Me.DmsProvider.UpdateSharingAsync(CType(Me.DmsUpdatedSharingDetails, DmsShareForUser))
                            Case Else
                                Throw New NotImplementedException(UiStrings.GetText("DialogObjectModeNotImplementedForUpdatingSharing"))
                        End Select
                End Select
            End If
    End Function

    Private Sub ButtonCancel_Click(sender As Object, e As EventArgs) Handles ButtonCancel.Click
        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub

    Private Sub CheckBoxesAllowedActions_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxAllowUpload.CheckedChanged, CheckBoxAllowView.CheckedChanged, CheckBoxAllowEdit.CheckedChanged, CheckBoxAllowDelete.CheckedChanged, CheckBoxAllowShare.CheckedChanged, CheckBoxAllowDownload.CheckedChanged
        Try
            SwitchControlsBasedOnCheckboxesForAllowedActions()
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.ToString), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ''' <inheritdoc/>
    ''' <summary>
    ''' Handle ESC key to cancel dialog
    ''' </summary>
    ''' <param name="keyData">The pressed key and modifier flags.</param>
    ''' <returns>True when the dialog handles the key; otherwise the inherited result.</returns>
    Protected Overrides Function ProcessDialogKey(keyData As Keys) As Boolean
        If Form.ModifierKeys = Keys.None AndAlso keyData = Keys.Escape Then
            Me.Close()
            Return True
        End If
        Return MyBase.ProcessDialogKey(keyData)
    End Function

End Class
