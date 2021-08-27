Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Public Class DmsLinkShareSetup

    Public Property DmsLinkDetails As DmsLink
    Public Property DmsProvider As Providers.BaseDmsProvider
    ''' <summary>
    ''' DmsResourceItem required for creation of links
    ''' </summary>
    ''' <returns></returns>
    Public Property DmsItem As DmsResourceItem

    Private _DmsUpdatedLinkDetails As DmsLink = Nothing
    Public ReadOnly Property DmsUpdatedLinkDetails As DmsLink
        Get
            Return Me._DmsUpdatedLinkDetails
        End Get
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

    Private Sub DmsLinkShare_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        If Me._DialogMode = Nothing Then
            If Me.DmsLinkDetails Is Nothing Then
                Me.DialogMode = DialogModes.CreateLink
            Else
                Me.DialogMode = DialogModes.UpdateLink
            End If
        End If
        Select Case Me.DmsProvider.GetType.Name
            Case "ScopevisioTeamworkDmsProvider"
                If Me._DialogMode = DialogModes.CreateLink Then
                    'Create link mode: enable supported auths
                    Me.CheckBoxAllowView.Enabled = True
                    Me.CheckBoxAllowDownload.Enabled = True
                    Me.CheckBoxAllowEdit.Enabled = False
                    Me.CheckBoxAllowUpload.Enabled = True
                    Me.CheckBoxAllowDelete.Enabled = False
                    Me.CheckBoxAllowShare.Enabled = False
                    Me.TextBoxNumberOfUploads.Text = "5000"
                Else
                    'Update link mode: view and upload links can't be exchanged and must remain
                    Me.CheckBoxAllowView.Enabled = False
                    Me.CheckBoxAllowDownload.Enabled = Not Me.DmsLinkDetails.AllowUpload
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
                Me.CheckBoxAllowDelete.Checked = False
                Me.CheckBoxAllowDownload.Checked = False
                Me.CheckBoxAllowEdit.Checked = False
                Me.CheckBoxAllowUpload.Checked = False
                Me.CheckBoxAllowShare.Checked = False
                Me.CheckBoxExpiryDate.Checked = False
                Me.CheckBoxPassword.Checked = False
                Me.CheckBoxMaxBytes.Checked = False
                Me.CheckBoxMaxDownloads.Checked = False
                Me.CheckBoxMaxUploads.Checked = False
                Me.TextBoxID.Enabled = False
                Me.TextBoxWebUrl.Enabled = False
                Me.TextBoxDownloadUrl.Enabled = False
                Me.TextBoxNumberOfBytes.Enabled = False
                Me.TextBoxNumberOfDownloads.Enabled = False
                Me.TextBoxNumberOfUploads.Enabled = False
            Case DialogModes.UpdateLink
                Me.LoadDataIntoControls()
            Case Else
                Throw New NullReferenceException(NameOf(Me.DialogMode))
        End Select
        Me.SwitchControlsBasedOnCheckboxesForAllowedActions()
    End Sub

    Private Sub SwitchControlsBasedOnCheckboxesForAllowedActions()
        Select Case Me.DmsProvider.GetType.Name
            Case "ScopevisioTeamworkDmsProvider"
                Me.TextBoxName.Enabled = Me.CheckBoxAllowUpload.Checked
                Me.CheckBoxMaxUploads.Enabled = Me.CheckBoxAllowUpload.Checked
                Me.TextBoxMaxUploads.Enabled = Me.CheckBoxAllowUpload.Checked
                Me.CheckBoxMaxDownloads.Enabled = Me.CheckBoxAllowDownload.Checked
                Me.TextBoxMaxDownloads.Enabled = Me.CheckBoxAllowDownload.Checked
                Me.CheckBoxMaxViews.Enabled = False
                Me.TextBoxMaxViews.Enabled = False
                Me.TextBoxDownloadUrl.Enabled = Me.CheckBoxAllowDownload.Checked AndAlso Me._DialogMode = DialogModes.UpdateLink
            Case Else
                Throw New NotImplementedException("DmsProvider implementation required for " & Me.DmsProvider.GetType.Name)
        End Select
    End Sub

    Private Sub LoadDataIntoControls()
        Me.TextBoxID.Text = Me.DmsLinkDetails.ID
        Me.TextBoxName.Text = Me.DmsLinkDetails.Name
        Me.TextBoxDownloadUrl.Text = Me.DmsLinkDetails.DownloadUrl
        Me.TextBoxWebUrl.Text = Me.DmsLinkDetails.WebUrl
        Me.CheckBoxExpiryDate.Checked = Me.DmsLinkDetails.ExpiryDateLocalTime.HasValue
        If Me.DmsLinkDetails.ExpiryDateLocalTime.HasValue Then
            Me.DateTimePickerExpiryDate.Value = Me.DmsLinkDetails.ExpiryDateLocalTime.Value
        End If
        Me.CheckBoxPassword.Checked = Me.DmsLinkDetails.Password <> Nothing
        Me.TextBoxPassword.Text = Me.DmsLinkDetails.Password
        Me.CheckBoxAllowDelete.Checked = Me.DmsLinkDetails.AllowDelete
        Me.CheckBoxAllowDownload.Checked = Me.DmsLinkDetails.AllowDownload
        Me.CheckBoxAllowEdit.Checked = Me.DmsLinkDetails.AllowEdit
        Me.CheckBoxAllowUpload.Checked = Me.DmsLinkDetails.AllowUpload
        Me.CheckBoxAllowView.Checked = Me.DmsLinkDetails.AllowView
        Me.CheckBoxAllowShare.Checked = Me.DmsLinkDetails.AllowShare
        Me.CheckBoxMaxBytes.Checked = Me.DmsLinkDetails.MaxBytes.HasValue
        Me.TextBoxMaxBytes.Text = Me.DmsLinkDetails.MaxBytes?.ToString
        Me.CheckBoxMaxDownloads.Checked = Me.DmsLinkDetails.MaxDownloads.HasValue
        Me.TextBoxMaxDownloads.Text = Me.DmsLinkDetails.MaxDownloads?.ToString
        Me.CheckBoxMaxUploads.Checked = Me.DmsLinkDetails.MaxUploads.HasValue
        Me.TextBoxMaxUploads.Text = Me.DmsLinkDetails.MaxUploads?.ToString
        Me.TextBoxNumberOfBytes.Text = Me.DmsLinkDetails.UploadedBytes?.ToString
        Me.TextBoxNumberOfDownloads.Text = Me.DmsLinkDetails.DownloadsCount?.ToString
        Me.TextBoxNumberOfUploads.Text = Me.DmsLinkDetails.UploadsCount?.ToString
        Me.TextBoxNumberOfViews.Text = Me.DmsLinkDetails.ViewsCount?.ToString
    End Sub

    Private Sub SaveControlDataIntoDmsLink()
        Dim Result As DmsLink
        If Me._DialogMode = DialogModes.CreateLink Then
            Result = New DmsLink(Nothing, Nothing, Me.DmsProvider, Nothing)
        Else
            Result = CType(Me.DmsLinkDetails.Clone(), DmsLink)
        End If
        'always keeps as it is: Me.DmsLinkDetails.ID
        Result.Name = Me.TextBoxName.Text
        Result.DownloadUrl = Me.TextBoxDownloadUrl.Text
        Result.WebUrl = Me.TextBoxWebUrl.Text
        If Me.CheckBoxExpiryDate.Checked AndAlso Not Me.DateTimePickerExpiryDate.Value = Nothing Then
            Result.ExpiryDateLocalTime = Me.DateTimePickerExpiryDate.Value
        Else
            Result.ExpiryDateLocalTime = Nothing
        End If
        Result.Password = Me.TextBoxPassword.Text
        Result.AllowDelete = Me.CheckBoxAllowDelete.Checked
        Result.AllowDownload = Me.CheckBoxAllowDownload.Checked
        Result.AllowEdit = Me.CheckBoxAllowEdit.Checked
        Result.AllowUpload = Me.CheckBoxAllowUpload.Checked
        Result.AllowView = Me.CheckBoxAllowView.Checked
        Result.AllowShare = Me.CheckBoxAllowShare.Checked
        If Me.CheckBoxMaxBytes.Checked AndAlso Not Me.TextBoxMaxBytes.Text = Nothing Then
            Result.MaxBytes = Long.Parse(Me.TextBoxMaxBytes.Text)
        Else
            Result.MaxBytes = Nothing
        End If
        If Me.CheckBoxMaxDownloads.Checked AndAlso Not Me.TextBoxMaxDownloads.Text = Nothing Then
            Result.MaxDownloads = Integer.Parse(Me.TextBoxMaxDownloads.Text)
        Else
            Result.MaxDownloads = Nothing
        End If
        If Me.CheckBoxMaxUploads.Checked AndAlso Not Me.TextBoxMaxUploads.Text = Nothing Then
            Result.MaxUploads = Integer.Parse(Me.TextBoxMaxUploads.Text)
        Else
            Result.MaxUploads = Nothing
        End If
        Me._DmsUpdatedLinkDetails = Result
    End Sub

    Private Sub CheckBoxExpiryDate_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxExpiryDate.CheckedChanged
        Me.DateTimePickerExpiryDate.Enabled = Me.CheckBoxExpiryDate.Checked
    End Sub

    Private Sub CheckBoxPassword_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxPassword.CheckedChanged
        Me.TextBoxPassword.Enabled = Me.CheckBoxPassword.Checked
    End Sub

    Private Sub CheckBoxMaxDownloads_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxMaxDownloads.CheckedChanged
        Me.TextBoxMaxDownloads.Enabled = Me.CheckBoxMaxDownloads.Checked
    End Sub

    Private Sub CheckBoxMaxUploads_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxMaxUploads.CheckedChanged
        Me.TextBoxMaxUploads.Enabled = Me.CheckBoxMaxUploads.Checked
    End Sub

    Private Sub CheckBoxMaxBytes_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxMaxBytes.CheckedChanged
        Me.TextBoxMaxBytes.Enabled = Me.CheckBoxMaxBytes.Checked
    End Sub

    Private Sub CheckBoxAllowDownload_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxAllowDownload.CheckedChanged
        If Me.CheckBoxAllowDownload.Checked Then
            Me.CheckBoxAllowView.Checked = True 'Download requires View auths
        End If
    End Sub

    Private Sub CheckBoxAllowView_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxAllowView.CheckedChanged
        If Me.CheckBoxAllowView.Checked = False Then
            Me.CheckBoxAllowDownload.Checked = False 'Download requires View auths
        End If
    End Sub

    Private Sub ButtonSave_Click(sender As Object, e As EventArgs) Handles ButtonSave.Click
        Try
            Select Case Me.DmsProvider.GetType.Name
                Case "ScopevisioTeamworkDmsProvider"
                    If Me.CheckBoxAllowDownload.Checked And Not Me.CheckBoxAllowView.Checked Then
                        Throw New DmsUserInputInvalidException("Required authorization setup for View if Download selected")
                    End If
                    If Not (Me.CheckBoxAllowView.Checked Xor Me.CheckBoxAllowUpload.Checked) Then
                        Throw New DmsUserInputInvalidException("Required authorization setup for either View or Upload")
                    End If
                    If Me.CheckBoxAllowUpload.Checked AndAlso Me.TextBoxName.Text = Nothing Then
                        Throw New DmsUserInputMissingException("Name required for upload links")
                    End If
                Case Else
                    Throw New NotImplementedException("DmsProvider implementation required for " & Me.DmsProvider.GetType.Name)
            End Select
            Me.SaveControlDataIntoDmsLink()
            If Me._DialogMode = DialogModes.CreateLink Then
                Me.DmsUpdatedLinkDetails.ID = Me.DmsProvider.CreateLink(Me.DmsItem, Me.DmsUpdatedLinkDetails).ID
            Else
                Me.DmsProvider.UpdateLink(Me.DmsUpdatedLinkDetails)
            End If
            Me.DialogResult = DialogResult.OK
            Me.Close()
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