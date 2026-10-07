Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Providers

''' <summary>Edits the supported permissions and limits of an external sharing link.</summary>
Public Class DmsLinkShareSetup

    Private ReadOnly PendingOperation As New UiAsyncOperation(Me)

    ''' <summary>Initializes the external link dialog.</summary>
    Public Sub New()
        InitializeComponent()
        ApplyLocalizedText()
    End Sub

    Private Sub ApplyLocalizedText()
        Me.Text = UiStrings.GetText("LinkSetupTitle")
        Me.CheckBoxAllowView.Text = UiStrings.GetText("PermissionView")
        Me.CheckBoxAllowShare.Text = UiStrings.GetText("PermissionShare")
        Me.CheckBoxAllowDelete.Text = UiStrings.GetText("PermissionDelete")
        Me.CheckBoxAllowUpload.Text = UiStrings.GetText("PermissionUpload")
        Me.CheckBoxAllowDownload.Text = UiStrings.GetText("PermissionDownload")
        Me.CheckBoxAllowEdit.Text = UiStrings.GetText("PermissionEdit")
        Me.GroupBoxAuthorizations.Text = UiStrings.GetText("Permissions")
        Me.ButtonCancel.Text = UiStrings.GetText("ActionCancel")
        Me.ButtonSave.Text = UiStrings.GetText("ActionSave")
        Me.Label1.Text = UiStrings.GetText("LabelId")
        Me.GroupBoxGeneral.Text = UiStrings.GetText("GeneralSettings")
        Me.Label12.Text = UiStrings.GetText("LabelName")
        Me.Label8.Text = UiStrings.GetText("LabelPassword")
        Me.Label7.Text = UiStrings.GetText("LabelExpiryDate")
        Me.Label3.Text = UiStrings.GetText("LabelDownloadLink")
        Me.Label2.Text = UiStrings.GetText("LabelWebLink")
        Me.GroupBoxExtended.Text = UiStrings.GetText("Limits")
        Me.Label14.Text = UiStrings.GetText("LabelMaxViews")
        Me.Label6.Text = UiStrings.GetText("LabelMaxBytes")
        Me.Label5.Text = UiStrings.GetText("LabelMaxUploads")
        Me.Label4.Text = UiStrings.GetText("LabelMaxDownloads")
        Me.GroupBox1.Text = UiStrings.GetText("Statistics")
        Me.Label13.Text = UiStrings.GetText("LabelNumberViews")
        Me.Label9.Text = UiStrings.GetText("LabelNumberBytes")
        Me.Label10.Text = UiStrings.GetText("LabelNumberUploads")
        Me.Label11.Text = UiStrings.GetText("LabelNumberDownloads")
    End Sub

    ''' <summary>Gets or sets the original link settings to edit.</summary>
    Public Property DmsLinkDetails As DmsLink
    ''' <summary>Gets or sets the provider associated with this object.</summary>
    Public Property DmsProvider As Providers.BaseDmsProvider
    ''' <summary>
    ''' DmsResourceItem required for creation of links
    ''' </summary>
    Public Property DmsItem As DmsResourceItem

    Private _DmsUpdatedLinkDetails As DmsLink = Nothing
    Private _LoadingControls As Boolean
    ''' <summary>Gets the updated link returned after a successful save, or Nothing before saving.</summary>
    Public ReadOnly Property DmsUpdatedLinkDetails As DmsLink
        Get
            Return Me._DmsUpdatedLinkDetails
        End Get
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

    Private Async Sub DmsLinkShare_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Try
            If Me.DmsLinkDetails IsNot Nothing Then
                Await PendingOperation.RunAsync(Function() Me.DmsLinkDetails.RefreshAsync())
            End If
            LoadControls()
        Catch ex As Exception
            MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.Message), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
            Me.Close()
        End Try
    End Sub

    Private Sub LoadControls()
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
                    Me.CheckBoxAllowUpload.Enabled = Me.DmsItem IsNot Nothing AndAlso Me.DmsItem.ItemType = DmsResourceItem.ItemTypes.Collection
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
            Case "WebDavDmsProvider"
                Dim SharingItem As DmsResourceItem = If(Me._DialogMode = DialogModes.CreateLink, Me.DmsItem, Me.DmsLinkDetails?.ParentDmsResourceItem)
                Me.CheckBoxAllowView.Enabled = False
                Me.CheckBoxAllowDownload.Enabled = False
                Me.CheckBoxAllowEdit.Enabled = True
                Me.CheckBoxAllowUpload.Enabled = SharingItem IsNot Nothing AndAlso SharingItem.ItemType = DmsResourceItem.ItemTypes.Folder
                Me.CheckBoxAllowDelete.Enabled = True
                Me.CheckBoxAllowShare.Enabled = True
            Case Else
                Throw New NotImplementedException(UiStrings.Format("DmsProviderImplementationRequiredFor", Me.DmsProvider.GetType.Name))
        End Select
        Select Case _DialogMode
            Case DialogModes.CreateLink
                Me.CheckBoxAllowView.Checked = True
                Me.CheckBoxAllowDelete.Checked = False
                Me.CheckBoxAllowDownload.Checked = False
                Me.CheckBoxAllowEdit.Checked = False
                Me.CheckBoxAllowUpload.Checked = False
                Me.CheckBoxAllowShare.Checked = False
                If Me.DmsProvider.GetType.Name = "WebDavDmsProvider" Then
                    Me.CheckBoxAllowDownload.Checked = True
                End If
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
                Me._LoadingControls = True
                Try
                    Me.LoadDataIntoControls()
                Finally
                    Me._LoadingControls = False
                End Try
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
                Me.CheckBoxMaxBytes.Enabled = Me.CheckBoxAllowUpload.Checked
                Me.TextBoxMaxBytes.Enabled = Me.CheckBoxAllowUpload.Checked
                Me.CheckBoxMaxViews.Enabled = False
                Me.TextBoxMaxViews.Enabled = False
                Me.TextBoxDownloadUrl.Enabled = Me.CheckBoxAllowDownload.Checked AndAlso Me._DialogMode = DialogModes.UpdateLink
            Case "WebDavDmsProvider"
                Me.TextBoxName.Enabled = True
                Me.CheckBoxMaxBytes.Enabled = False
                Me.TextBoxMaxBytes.Enabled = False
                Me.CheckBoxMaxUploads.Enabled = False
                Me.TextBoxMaxUploads.Enabled = False
                Me.CheckBoxMaxDownloads.Enabled = False
                Me.TextBoxMaxDownloads.Enabled = False
                Me.CheckBoxMaxViews.Enabled = False
                Me.TextBoxMaxViews.Enabled = False
                Me.TextBoxDownloadUrl.Enabled = False
            Case Else
                Throw New NotImplementedException(UiStrings.Format("DmsProviderImplementationRequiredFor", Me.DmsProvider.GetType.Name))
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
        Result.Password = If(Me.CheckBoxPassword.Checked, Me.TextBoxPassword.Text, Nothing)
        Result.AllowDelete = Me.CheckBoxAllowDelete.Checked
        Result.AllowDownload = Me.CheckBoxAllowDownload.Checked
        Result.AllowEdit = Me.CheckBoxAllowEdit.Checked
        Result.AllowUpload = Me.CheckBoxAllowUpload.Checked
        Result.AllowView = Me.CheckBoxAllowView.Checked
        Result.AllowShare = Me.CheckBoxAllowShare.Checked
        If Me.CheckBoxAllowUpload.Checked AndAlso Me.CheckBoxMaxBytes.Checked AndAlso Not Me.TextBoxMaxBytes.Text = Nothing Then
            Result.MaxBytes = Long.Parse(Me.TextBoxMaxBytes.Text)
        Else
            Result.MaxBytes = Nothing
        End If
        If Me.CheckBoxAllowDownload.Checked AndAlso Me.CheckBoxMaxDownloads.Checked AndAlso Not Me.TextBoxMaxDownloads.Text = Nothing Then
            Result.MaxDownloads = Integer.Parse(Me.TextBoxMaxDownloads.Text)
        Else
            Result.MaxDownloads = Nothing
        End If
        If Me.CheckBoxAllowUpload.Checked AndAlso Me.CheckBoxMaxUploads.Checked AndAlso Not Me.TextBoxMaxUploads.Text = Nothing Then
            Result.MaxUploads = Integer.Parse(Me.TextBoxMaxUploads.Text)
        Else
            Result.MaxUploads = Nothing
        End If
        Me._DmsUpdatedLinkDetails = Result
    End Sub

    ''' <summary>
    ''' Creates a link and reconciles provider-side model updates with the dialog model.
    ''' </summary>
    Friend Shared Function CreateLinkAndSynchronizeDmsItem(dmsProvider As BaseDmsProvider, dmsItem As DmsResourceItem, requestedLink As DmsLink) As DmsLink
        If dmsProvider Is Nothing Then Throw New ArgumentNullException(NameOf(dmsProvider))
        If dmsItem Is Nothing Then Throw New ArgumentNullException(NameOf(dmsItem))
        If requestedLink Is Nothing Then Throw New ArgumentNullException(NameOf(requestedLink))

        Dim CreatedLink As DmsLink = dmsProvider.CreateLink(dmsItem, requestedLink)
        Return SynchronizeCreatedLink(dmsItem, CreatedLink)
    End Function

    Friend Shared Async Function CreateLinkAndSynchronizeDmsItemAsync(dmsProvider As BaseDmsProvider, dmsItem As DmsResourceItem, requestedLink As DmsLink) As Task(Of DmsLink)
        If dmsProvider Is Nothing Then Throw New ArgumentNullException(NameOf(dmsProvider))
        If dmsItem Is Nothing Then Throw New ArgumentNullException(NameOf(dmsItem))
        If requestedLink Is Nothing Then Throw New ArgumentNullException(NameOf(requestedLink))
        Dim CreatedLink As DmsLink = Await dmsProvider.CreateLinkAsync(dmsItem, requestedLink)
        Return SynchronizeCreatedLink(dmsItem, CreatedLink)
    End Function

    Private Shared Function SynchronizeCreatedLink(dmsItem As DmsResourceItem, CreatedLink As DmsLink) As DmsLink
        If CreatedLink Is Nothing Then Throw New InvalidOperationException(UiStrings.GetText("TheDMSProviderReturnedNoCreatedLink"))
        If String.IsNullOrEmpty(CreatedLink.ID) Then Throw New InvalidOperationException(UiStrings.GetText("TheDMSProviderReturnedACreatedLinkWithout"))

        If dmsItem.ExtendedInfosLinks Is Nothing Then
            dmsItem.ExtendedInfosLinks = New List(Of DmsLink)
        End If

        Dim MatchingLinkIndex As Integer = -1
        For MyCounter As Integer = 0 To dmsItem.ExtendedInfosLinks.Count - 1
            If String.Equals(dmsItem.ExtendedInfosLinks(MyCounter).ID, CreatedLink.ID, StringComparison.Ordinal) Then
                MatchingLinkIndex = MyCounter
                Exit For
            End If
        Next

        If MatchingLinkIndex < 0 Then
            dmsItem.ExtendedInfosLinks.Add(CreatedLink)
            MatchingLinkIndex = dmsItem.ExtendedInfosLinks.Count - 1
        Else
            dmsItem.ExtendedInfosLinks(MatchingLinkIndex) = CreatedLink
        End If

        For MyCounter As Integer = dmsItem.ExtendedInfosLinks.Count - 1 To 0 Step -1
            If MyCounter <> MatchingLinkIndex AndAlso String.Equals(dmsItem.ExtendedInfosLinks(MyCounter).ID, CreatedLink.ID, StringComparison.Ordinal) Then
                dmsItem.ExtendedInfosLinks.RemoveAt(MyCounter)
            End If
        Next

        Return CreatedLink
    End Function

    Private Sub CheckBoxExpiryDate_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxExpiryDate.CheckedChanged
        Me.DateTimePickerExpiryDate.Enabled = Me.CheckBoxExpiryDate.Checked
    End Sub

    Private Sub CheckBoxPassword_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxPassword.CheckedChanged
        Me.TextBoxPassword.Enabled = Me.CheckBoxPassword.Checked
    End Sub

    Private Sub CheckBoxMaxDownloads_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxMaxDownloads.CheckedChanged
        Me.TextBoxMaxDownloads.Enabled = Me.CheckBoxAllowDownload.Checked AndAlso Me.CheckBoxMaxDownloads.Checked
    End Sub

    Private Sub CheckBoxMaxUploads_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxMaxUploads.CheckedChanged
        Me.TextBoxMaxUploads.Enabled = Me.CheckBoxAllowUpload.Checked AndAlso Me.CheckBoxMaxUploads.Checked
    End Sub

    Private Sub CheckBoxMaxBytes_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxMaxBytes.CheckedChanged
        Me.TextBoxMaxBytes.Enabled = Me.CheckBoxAllowUpload.Checked AndAlso Me.CheckBoxMaxBytes.Checked
    End Sub

    Private Sub DateTimePickerExpiryDate_ValueChanged(sender As Object, e As EventArgs) Handles DateTimePickerExpiryDate.ValueChanged
        If Not Me._LoadingControls AndAlso Me.DateTimePickerExpiryDate.Enabled Then
            Me.CheckBoxExpiryDate.Checked = True
        End If
    End Sub

    Private Sub OptionalValue_TextChanged(sender As Object, e As EventArgs) Handles TextBoxPassword.TextChanged, TextBoxMaxViews.TextChanged, TextBoxMaxDownloads.TextChanged, TextBoxMaxUploads.TextChanged, TextBoxMaxBytes.TextChanged
        Dim Input As TextBox = DirectCast(sender, TextBox)
        If Me._LoadingControls OrElse Not Input.Enabled OrElse Input.TextLength = 0 Then Return

        If Input Is Me.TextBoxPassword Then
            Me.CheckBoxPassword.Checked = True
        ElseIf Input Is Me.TextBoxMaxViews Then
            Me.CheckBoxMaxViews.Checked = True
        ElseIf Input Is Me.TextBoxMaxDownloads Then
            Me.CheckBoxMaxDownloads.Checked = True
        ElseIf Input Is Me.TextBoxMaxUploads Then
            Me.CheckBoxMaxUploads.Checked = True
        ElseIf Input Is Me.TextBoxMaxBytes Then
            Me.CheckBoxMaxBytes.Checked = True
        End If
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

    Private Async Sub ButtonSave_Click(sender As Object, e As EventArgs) Handles ButtonSave.Click
        If PendingOperation.IsRunning Then Return
        Try
            Select Case Me.DmsProvider.GetType.Name
                Case "ScopevisioTeamworkDmsProvider"
                    If Me.CheckBoxAllowDownload.Checked And Not Me.CheckBoxAllowView.Checked Then
                        Throw New DmsUserInputInvalidException(UiStrings.GetText("ViewRequiredForDownload"))
                    End If
                    If Not (Me.CheckBoxAllowView.Checked Xor Me.CheckBoxAllowUpload.Checked) Then
                        Throw New DmsUserInputInvalidException(UiStrings.GetText("ViewOrUploadRequired"))
                    End If
                    If Me.CheckBoxAllowUpload.Checked AndAlso Me.TextBoxName.Text = Nothing Then
                        Throw New DmsUserInputMissingException(UiStrings.GetText("UploadLinkNameRequired"))
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
            Await PendingOperation.RunAsync(Async Function()
                                                If Me._DialogMode = DialogModes.CreateLink Then
                                                    Me._DmsUpdatedLinkDetails = Await CreateLinkAndSynchronizeDmsItemAsync(Me.DmsProvider, Me.DmsItem, Me.DmsUpdatedLinkDetails)
                                                Else
                                                    Await Me.DmsProvider.UpdateLinkAsync(Me.DmsUpdatedLinkDetails)
                                                End If
                                            End Function, Sub()
                                                              Me.DialogResult = DialogResult.OK
                                                              Me.Close()
                                                          End Sub)
        Catch ex As Data.DmsUserInputInvalidException
            System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.Message), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Data.DmsUserInputMissingException
            System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.Message), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.Message), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.ToString), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
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
