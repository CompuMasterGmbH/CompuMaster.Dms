Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Public Class DmsStandardShareSetup

    <Obsolete("Use overloaded constructor")>
    Public Sub New()
        InitializeComponent()
    End Sub

    Public Sub New(userSharing As DmsShareForUser, dmsProvider As Providers.BaseDmsProvider, hideIDs As List(Of String))
        InitializeComponent()
        Me.DialogObjectMode = DialogObjectModes.UserSharing
        Me.DmsSharingDetails = userSharing
        If userSharing IsNot Nothing Then
            Me.DmsItem = userSharing.ParentDmsResourceItem
        End If
        Me.DmsProvider = dmsProvider
        Me.HideIDs = hideIDs
    End Sub

    Public Sub New(groupSharing As DmsShareForGroup, dmsProvider As Providers.BaseDmsProvider, hideIDs As List(Of String))
        InitializeComponent()
        Me.DialogObjectMode = DialogObjectModes.GroupSharing
        Me.DmsSharingDetails = groupSharing
        If groupSharing IsNot Nothing Then
            Me.DmsItem = groupSharing.ParentDmsResourceItem
        End If
        Me.DmsProvider = dmsProvider
        Me.HideIDs = hideIDs
    End Sub

    Public Property DmsSharingDetails As DmsShareBase
    Public Property DmsProvider As Providers.BaseDmsProvider
    ''' <summary>
    ''' DmsResourceItem required for creation of links
    ''' </summary>
    ''' <returns></returns>
    Public Property DmsItem As DmsResourceItem
    ''' <summary>
    ''' IDs which shall be hidden in list of addable users/groups
    ''' </summary>
    ''' <returns></returns>
    Public Property HideIDs As List(Of String)

    Private _DmsUpdatedSharingDetails As DmsShareBase = Nothing
    Public ReadOnly Property DmsUpdatedSharingDetails As DmsShareBase
        Get
            Return Me._DmsUpdatedSharingDetails
        End Get
    End Property

    Public Enum DialogObjectModes As Byte
        GroupSharing = 1
        UserSharing = 2
    End Enum

    Private _DialogObjectMode As DialogObjectModes
    Public WriteOnly Property DialogObjectMode As DialogObjectModes
        Set(value As DialogObjectModes)
            Select Case value
                Case DialogObjectModes.GroupSharing
                    Me.LabelName.Text = "Name der Gruppe"
                Case DialogObjectModes.UserSharing
                    Me.LabelName.Text = "Name des Benutzers"
                Case Else
                    Throw New ArgumentOutOfRangeException(NameOf(value))
            End Select
            _DialogObjectMode = value
        End Set
    End Property

    Public Enum DialogModes As Byte
        CreateLink = 1
        UpdateLink = 2
    End Enum

    Private _DialogMode As DialogModes
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

    Private Sub DmsStandardShare_Load(sender As Object, e As EventArgs) Handles MyBase.Load
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
            Case Else
                Throw New NotImplementedException("DmsProvider implementation required for " & Me.DmsProvider.GetType.Name)
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
                Me.LoadUserOrGroupList()
            Case DialogModes.UpdateLink
                Me.ComboBoxUsersOrGroups.Enabled = False
                Me.LoadDataIntoControls()
            Case Else
                Throw New NullReferenceException(NameOf(Me.DialogMode))
        End Select
        Me.SwitchControlsBasedOnCheckboxesForAllowedActions()
    End Sub

    Private Sub SwitchControlsBasedOnCheckboxesForAllowedActions()
        Select Case Me.DmsProvider.GetType.Name
            Case "ScopevisioTeamworkDmsProvider"
            Case Else
                Throw New NotImplementedException("DmsProvider implementation required for " & Me.DmsProvider.GetType.Name)
        End Select
    End Sub

    Private Sub LoadUserOrGroupList()
        Select Case Me._DialogObjectMode
            Case DialogObjectModes.GroupSharing
                ComboBoxUsersOrGroups.Items.Clear()
                For Each Group As DmsGroup In Me.DmsProvider.GetAllGroups
                    If Me.HideIDs.Contains(Group.ID) = False Then
                        Dim NewItem As New KeyValuePair(Of String, String)(Group.ID, Group.DisplayName)
                        ComboBoxUsersOrGroups.Items.Add(NewItem)
                    End If
                Next
            Case DialogObjectModes.UserSharing
                ComboBoxUsersOrGroups.Items.Clear()
                For Each User As DmsUser In Me.DmsProvider.GetAllUsers
                    If Me.HideIDs.Contains(User.ID) = False Then
                        Dim NewItem As New KeyValuePair(Of String, String)(User.ID, User.DisplayName)
                        ComboBoxUsersOrGroups.Items.Add(NewItem)
                    End If
                Next
            Case Else
                Throw New NotImplementedException("DialogObjectMode not implemented for loading")
        End Select
    End Sub

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
                Throw New NotImplementedException("DialogObjectMode not implemented for loading")
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
            Throw New Data.DmsUserInputMissingException("Kein Berechtigungs-Objekt ausgewählt")
        End If
        Dim Result As DmsShareBase
        Dim SelectedId As String = CType(Me.ComboBoxUsersOrGroups.SelectedItem, KeyValuePair(Of String, String)).Key
        Dim SelectedDisplayName As String = CType(Me.ComboBoxUsersOrGroups.SelectedItem, KeyValuePair(Of String, String)).Value
        If Me._DialogMode = DialogModes.CreateLink Then
            Select Case Me._DialogObjectMode
                Case DialogObjectModes.GroupSharing
                    Result = New DmsShareForGroup(Me.DmsItem, New DmsGroup() With {.ID = SelectedId, .Name = SelectedDisplayName}, False, False, False, False, False, False)
                Case DialogObjectModes.UserSharing
                    Result = New DmsShareForUser(Me.DmsItem, New DmsUser() With {.ID = SelectedId, .Name = SelectedDisplayName}, False, False, False, False, False, False)
                Case Else
                    Throw New NotImplementedException("DialogObjectMode not implemented for saving sharing")
            End Select
        Else
            Select Case Me._DialogObjectMode
                Case DialogObjectModes.GroupSharing
                    Result = CType(CType(Me.DmsSharingDetails, DmsShareForGroup).Clone(), DmsShareForGroup)
                Case DialogObjectModes.UserSharing
                    Result = CType(CType(Me.DmsSharingDetails, DmsShareForUser).Clone(), DmsShareForUser)
                Case Else
                    Throw New NotImplementedException("DialogObjectMode not implemented for saving sharing")
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

    Private Sub ButtonSave_Click(sender As Object, e As EventArgs) Handles ButtonSave.Click
        Try
            Select Case Me.DmsProvider.GetType.Name
                Case "ScopevisioTeamworkDmsProvider"
                    If Me.CheckBoxAllowDownload.Checked And Not Me.CheckBoxAllowView.Checked Then
                        Throw New DmsUserInputInvalidException("Required authorization setup for View if Download selected")
                    End If
                Case Else
                    Throw New NotImplementedException("DmsProvider implementation required for " & Me.DmsProvider.GetType.Name)
            End Select
            Me.SaveControlDataIntoDmsLink()
            If Me._DialogMode = DialogModes.CreateLink Then
                Select Case Me._DialogObjectMode
                    Case DialogObjectModes.GroupSharing
                        Me.DmsProvider.CreateSharing(Me.DmsItem, CType(Me.DmsUpdatedSharingDetails, DmsShareForGroup))
                    Case DialogObjectModes.UserSharing
                        Me.DmsProvider.CreateSharing(Me.DmsItem, CType(Me.DmsUpdatedSharingDetails, DmsShareForUser))
                    Case Else
                        Throw New NotImplementedException("DialogObjectMode not implemented for creating sharing")
                End Select
            Else
                Select Case Me.DmsProvider.GetType.Name
                    Case "ScopevisioTeamworkDmsProvider"
                        'nothing to do since form controls are all disabled, user could not change anything, so no update command required
                    Case Else
                        Select Case Me._DialogObjectMode
                            Case DialogObjectModes.GroupSharing
                                Me.DmsProvider.UpdateSharing(CType(Me.DmsUpdatedSharingDetails, DmsShareForGroup))
                            Case DialogObjectModes.UserSharing
                                Me.DmsProvider.UpdateSharing(CType(Me.DmsUpdatedSharingDetails, DmsShareForUser))
                            Case Else
                                Throw New NotImplementedException("DialogObjectMode not implemented for updating sharing")
                        End Select
                End Select
            End If
            Me.DialogResult = DialogResult.OK
            Me.Close()
        Catch ex As NotSupportedException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Data.DmsUserInputInvalidException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Data.DmsUserInputMissingException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ButtonCancel_Click(sender As Object, e As EventArgs) Handles ButtonCancel.Click
        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub

    Private Sub CheckBoxesAllowedActions_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxAllowUpload.CheckedChanged, CheckBoxAllowView.CheckedChanged, CheckBoxAllowEdit.CheckedChanged, CheckBoxAllowDelete.CheckedChanged, CheckBoxAllowShare.CheckedChanged, CheckBoxAllowDownload.CheckedChanged
        Try
            SwitchControlsBasedOnCheckboxesForAllowedActions()
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

End Class