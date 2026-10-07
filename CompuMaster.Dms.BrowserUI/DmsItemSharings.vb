Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Providers

''' <summary>Displays and edits supported user, group and link shares for a DMS resource.</summary>
Public Class DmsItemSharings

    Private ReadOnly PendingOperation As New UiAsyncOperation(Me)

    ''' <summary>Initializes the resource sharing dialog.</summary>
    Public Sub New()
        InitializeComponent()
        ApplyLocalizedText()
    End Sub

    Private Sub ApplyLocalizedText()
        Me.Text = UiStrings.GetText("SharingsTitle")
        Me.ButtonCancel.Text = UiStrings.GetText("ActionClose")
        Me.GroupBoxInternalSharings.Text = UiStrings.GetText("InternalSharings")
        Me.ColumnHeaderType.Text = UiStrings.GetText("ColumnType")
        Me.ColumnHeaderName.Text = UiStrings.GetText("ColumnName")
        Me.ColumnHeaderAuthorizations.Text = UiStrings.GetText("ColumnAuthorizations")
        Me.ToolStripButtonInternalSharingsAddGroup.Text = UiStrings.GetText("ActionAddGroup")
        Me.ToolStripButtonInternalSharingsAddUser.Text = UiStrings.GetText("ActionAddUser")
        Me.ToolStripButtonInternalSharingsEdit.Text = UiStrings.GetText("ActionEdit")
        Me.ToolStripButtonInternalSharingsDelete.Text = UiStrings.GetText("ActionDelete")
        Me.GroupBoxExternalSharings.Text = UiStrings.GetText("ExternalSharings")
        Me.ColumnHeaderDisplayName.Text = UiStrings.GetText("ColumnName")
        Me.ColumnHeaderAuths.Text = UiStrings.GetText("ColumnAuthorizations")
        Me.ColumnHeaderLimitations.Text = UiStrings.GetText("ColumnLimitations")
        Me.ToolStripButtonExternalSharingsAdd.Text = UiStrings.GetText("ActionAdd")
        Me.ToolStripButtonExternalSharingsEdit.Text = UiStrings.GetText("ActionEdit")
        Me.ToolStripButtonExternalSharingsDelete.Text = UiStrings.GetText("ActionDelete")
        Me.ToolStripButtonCopyLinkUrlToClipboard.Text = UiStrings.GetText("ActionCopyWebLink")
        Me.LabelCurrentOwner.Text = UiStrings.GetText("CurrentOwner")
    End Sub

    ''' <summary>Gets or sets the remote resource whose sharing settings are displayed.</summary>
    Public Property DmsItem As DmsResourceItem
    ''' <summary>Gets or sets the provider associated with this object.</summary>
    Public Property DmsProvider As BaseDmsProvider

    Friend Event SharingsChanged As EventHandler

    Private Sub ButtonCancel_Click(sender As Object, e As EventArgs) Handles ButtonCancel.Click
        Me.Close()
    End Sub

    Private Sub AddSharingEntry(shareInfo As Object, type As String, displayName As String, authorizations As String)
        Dim ItemLine As New ListViewItem
        ItemLine.SubItems.Add(New ListViewItem.ListViewSubItem(ItemLine, type))
        ItemLine.SubItems.Add(New ListViewItem.ListViewSubItem(ItemLine, displayName))
        ItemLine.SubItems.Add(New ListViewItem.ListViewSubItem(ItemLine, authorizations))
        ItemLine.SubItems.RemoveAt(0) 'Remove very first, empty sub item which couldn't be cleared before adding additional sub items
        ItemLine.Tag = shareInfo
        Me.ListViewInternalSharings.Items.Add(ItemLine)
    End Sub

    Private Sub AddLinkSharingEntry(shareInfo As DmsLink, displayName As String, authorizations As String, limitations As String)
        Dim ItemLine As New ListViewItem
        ItemLine.SubItems.Add(New ListViewItem.ListViewSubItem(ItemLine, displayName))
        ItemLine.SubItems.Add(New ListViewItem.ListViewSubItem(ItemLine, authorizations))
        ItemLine.SubItems.Add(New ListViewItem.ListViewSubItem(ItemLine, limitations))
        ItemLine.SubItems.RemoveAt(0) 'Remove very first, empty sub item which couldn't be cleared before adding additional sub items
        ItemLine.Tag = shareInfo
        Me.ListViewExternalSharings.Items.Add(ItemLine)
    End Sub

    Friend Shared Function LocalizedAllowedActions(sharing As DmsShareBase, Optional separator As String = ", ") As String
        Dim actions As New List(Of String)
        For Each action As String In sharing.AllowedActions()
            Select Case action
                Case "View"
                    actions.Add(UiStrings.GetText("PermissionView"))
                Case "Download"
                    actions.Add(UiStrings.GetText("PermissionDownload"))
                Case "Edit"
                    actions.Add(UiStrings.GetText("PermissionEdit"))
                Case "Upload"
                    actions.Add(UiStrings.GetText("PermissionUpload"))
                Case "Delete"
                    actions.Add(UiStrings.GetText("PermissionDelete"))
                Case "Share"
                    actions.Add(UiStrings.GetText("PermissionShare"))
                Case Else
                    actions.Add(action)
            End Select
        Next
        Return String.Join(separator, actions)
    End Function

    Private Async Sub DmsItemSharings_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Try
            Await PendingOperation.RunAsync(Async Function()
                                                If Me.DmsItem.ExtendedInfosLinks IsNot Nothing Then
                                                    For Each Link As DmsLink In Me.DmsItem.ExtendedInfosLinks
                                                        Await Link.RefreshAsync()
                                                    Next
                                                End If
                                                RefreshControls()
                                            End Function)
        Catch ex As Exception
            MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.Message), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
            Me.Close()
        End Try
    End Sub

    Private Sub RefreshControls()
        Me.Text = Me.DmsProvider.Name & " - " & Me.DmsItem.FullName
        Me.LabelCurrentOwner.Text = String.Format(Me.LabelCurrentOwner.Text, Me.DmsItem.ExtendedInfosOwner.DisplayName)
        Me.ListViewInternalSharings.Items.Clear()
        'Add group sharings
        If Me.DmsItem.ExtendedInfosHasGroupSharings Then
            For Each Share As DmsShareForGroup In Me.DmsItem.ExtendedInfosGroupSharings
                Me.AddSharingEntry(Share, UiStrings.GetText("EntityGroup"), Share.Group.DisplayName, LocalizedAllowedActions(Share))
            Next
        End If
        If Me.DmsItem.ExtendedInfosHasHiddenGroupSharings Then
            Me.AddSharingEntry(Nothing, UiStrings.GetText("EntityGroup"), UiStrings.GetText("HiddenValue"), UiStrings.GetText("UnknownValue"))
        End If
        'Add user sharings
        If Me.DmsItem.ExtendedInfosHasUserSharings Then
            For Each Share As DmsShareForUser In Me.DmsItem.ExtendedInfosUserSharings
                Me.AddSharingEntry(Share, UiStrings.GetText("EntityUser"), Share.User.DisplayName, LocalizedAllowedActions(Share))
            Next
        End If
        If Me.DmsItem.ExtendedInfosHasHiddenUserSharings Then
            Me.AddSharingEntry(Nothing, UiStrings.GetText("EntityUser"), UiStrings.GetText("HiddenValue"), UiStrings.GetText("UnknownValue"))
        End If
        'Add link sharings
        Me.ListViewExternalSharings.Items.Clear()
        If Me.DmsItem.ExtendedInfosLinks IsNot Nothing Then
            For Each LinkShare As DmsLink In Me.DmsItem.ExtendedInfosLinks
                Dim Limitations As New List(Of String)
                If Not LinkShare.Password = Nothing Then Limitations.Add(UiStrings.GetText("LimitationPassword"))
                If LinkShare.ExpiryDateLocalTime.HasValue Then Limitations.Add(LinkShare.ExpiryDateLocalTime.Value.ToString("g", Globalization.CultureInfo.CurrentCulture))
                If LinkShare.MaxBytes.HasValue Then Limitations.Add(Tools.ByteSizeToUIDisplayText(LinkShare.MaxBytes.Value))
                If LinkShare.MaxDownloads.HasValue Then Limitations.Add(UiStrings.Format("LimitationDownloads", LinkShare.MaxDownloads.Value))
                If LinkShare.MaxUploads.HasValue Then Limitations.Add(UiStrings.Format("LimitationUploads", LinkShare.MaxUploads.Value))
                Dim DisplayName As String = LinkShare.Name
                If DisplayName = Nothing Then DisplayName = LinkShare.ID
                Me.AddLinkSharingEntry(LinkShare, DisplayName, LocalizedAllowedActions(LinkShare), Strings.Join(Limitations.ToArray, ", "))
            Next
        End If
        Me.ListViewInternalSharings.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize)
        Me.ListViewInternalSharings.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent)
        Me.ListViewExternalSharings.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize)
        Me.ListViewExternalSharings.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent)
        If System.Environment.OSVersion.Platform <= PlatformID.WinCE Then
            'All windows platforms support size -2 (auto-sizing for headers and content cells), see https://stackoverflow.com/questions/14133225/listview-autoresizecolumns-based-on-both-column-content-and-header
            For MyCounter As Integer = 0 To Me.ListViewInternalSharings.Columns.Count - 1
                Me.ListViewInternalSharings.Columns(MyCounter).Width = -2
            Next
            For MyCounter As Integer = 0 To Me.ListViewExternalSharings.Columns.Count - 1
                Me.ListViewExternalSharings.Columns(MyCounter).Width = -2
            Next
        End If
    End Sub

    ''' <summary>Gets the identifiers of groups shown as authorized for the resource.</summary>
    Public ReadOnly Property AuthorizedGroupIDs As List(Of String)
        Get
            Dim Result As New List(Of String)
            For Each Item As ListViewItem In Me.ListViewInternalSharings.Items
                If TypeOf Item.Tag Is DmsShareForGroup Then
                    Result.Add(CType(Item.Tag, DmsShareForGroup).Group.ID)
                End If
            Next
            Return Result
        End Get
    End Property

    ''' <summary>Gets the identifiers of users shown as authorized for the resource.</summary>
    Public ReadOnly Property AuthorizedUserIDs As List(Of String)
        Get
            Dim Result As New List(Of String)
            For Each Item As ListViewItem In Me.ListViewInternalSharings.Items
                If TypeOf Item.Tag Is DmsShareForUser Then
                    Result.Add(CType(Item.Tag, DmsShareForUser).User.ID)
                End If
            Next
            Return Result
        End Get
    End Property

    Private Sub ToolStripButtonInternalSharingsAddGroup_Click(sender As Object, e As EventArgs) Handles ToolStripButtonInternalSharingsAddGroup.Click
        If PendingOperation.IsRunning Then Return
        Try
            Dim HideGroupIDs As List(Of String) = AuthorizedGroupIDs
            Dim StandardSharingSetupForm As DmsStandardShareSetup
            StandardSharingSetupForm = New DmsStandardShareSetup(CType(Nothing, DmsShareForGroup), Me.DmsProvider, HideGroupIDs)
            StandardSharingSetupForm.DmsItem = Me.DmsItem
            If StandardSharingSetupForm.ShowDialog(Me) = DialogResult.OK Then
                Dim CreatedSharing As DmsShareForGroup = CType(StandardSharingSetupForm.DmsUpdatedSharingDetails, DmsShareForGroup)
                Me.DmsItem.ExtendedInfosHasGroupSharings = True
                Me.DmsItem.ExtendedInfosGroupSharings.Add(CreatedSharing)
                RefreshControls()
                RaiseEvent SharingsChanged(Me, EventArgs.Empty)
            End If
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.Message), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.ToString), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Async Sub ToolStripButtonInternalSharingsAddUser_Click(sender As Object, e As EventArgs) Handles ToolStripButtonInternalSharingsAddUser.Click
        If PendingOperation.IsRunning Then Return
        Try
            Dim HideUserIDs As List(Of String) = Me.AuthorizedUserIDs
            Dim CurrentUserID As String = Nothing
            Await PendingOperation.RunAsync(Async Function()
                                                CurrentUserID = Await Me.DmsProvider.GetCurrentContextUserIDAsync()
                                            End Function)
            If Not HideUserIDs.Contains(CurrentUserID) Then
                HideUserIDs.Add(CurrentUserID)
            End If
            If Not Me.DmsItem.ExtendedInfosOwner.ID = Nothing AndAlso Not HideUserIDs.Contains(Me.DmsItem.ExtendedInfosOwner.ID) Then
                HideUserIDs.Add(Me.DmsItem.ExtendedInfosOwner.ID)
            End If
            Dim StandardSharingSetupForm As DmsStandardShareSetup
            StandardSharingSetupForm = New DmsStandardShareSetup(CType(Nothing, DmsShareForUser), Me.DmsProvider, HideUserIDs)
            StandardSharingSetupForm.DmsItem = Me.DmsItem
            If StandardSharingSetupForm.ShowDialog(Me) = DialogResult.OK Then
                Dim CreatedSharing As DmsShareForUser = CType(StandardSharingSetupForm.DmsUpdatedSharingDetails, DmsShareForUser)
                Me.DmsItem.ExtendedInfosHasUserSharings = True
                Me.DmsItem.ExtendedInfosUserSharings.Add(CreatedSharing)
                RefreshControls()
                RaiseEvent SharingsChanged(Me, EventArgs.Empty)
            End If
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.Message), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.ToString), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripButtonInternalSharingsEdit_Click(sender As Object, e As EventArgs) Handles ToolStripButtonInternalSharingsEdit.Click
        If PendingOperation.IsRunning Then Return
        Try
            Dim CurrentSharing As DmsShareBase = Me.CurrentSelectedUserOrGroupSharing
            If CurrentSharing Is Nothing Then Throw New DmsUserErrorMessageException(UiStrings.GetText("UserSharingRequired"))
            Dim StandardSharingSetupForm As DmsStandardShareSetup
            Select Case CurrentSharing.GetType
                Case GetType(DmsShareForGroup)
                    StandardSharingSetupForm = New DmsStandardShareSetup(CType(CurrentSharing, DmsShareForGroup), Me.DmsProvider, Nothing)
                    If StandardSharingSetupForm.ShowDialog(Me) = DialogResult.OK Then
                        Dim UpdatedSharing As DmsShareForGroup = CType(StandardSharingSetupForm.DmsUpdatedSharingDetails, DmsShareForGroup)
                        Me.ReplaceUpdatedSharingInDmsItem(UpdatedSharing.Group.ID, UpdatedSharing)
                    End If
                Case GetType(DmsShareForUser)
                    StandardSharingSetupForm = New DmsStandardShareSetup(CType(CurrentSharing, DmsShareForUser), Me.DmsProvider, Nothing)
                    If StandardSharingSetupForm.ShowDialog(Me) = DialogResult.OK Then
                        Dim UpdatedSharing As DmsShareForUser = CType(StandardSharingSetupForm.DmsUpdatedSharingDetails, DmsShareForUser)
                        Me.ReplaceUpdatedSharingInDmsItem(UpdatedSharing.User.ID, UpdatedSharing)
                    End If
                Case Else
                    Throw New NotImplementedException(UiStrings.GetText("UnknownDerivedClassFromDmsShareBase"))
            End Select
            RefreshControls()
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.Message), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.ToString), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Async Sub ToolStripButtonInternalSharingsDelete_Click(sender As Object, e As EventArgs) Handles ToolStripButtonInternalSharingsDelete.Click
        If PendingOperation.IsRunning Then Return
        Try
            Dim CurrentSharing As DmsShareBase = Me.CurrentSelectedUserOrGroupSharing
            If CurrentSharing Is Nothing Then Throw New DmsUserErrorMessageException(UiStrings.GetText("UserSharingRequired"))
            Await PendingOperation.RunAsync(Function() DeleteSharingForUiAsync(CurrentSharing))
        Catch ex As Exception
            MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.Message), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Async Function DeleteSharingForUiAsync(CurrentSharing As DmsShareBase) As Task
            Select Case CurrentSharing.GetType
                Case GetType(DmsShareForGroup)
                    Dim RemoveGroupSharing As DmsShareForGroup = CType(CurrentSharing, DmsShareForGroup)
                    Await Me.DmsProvider.DeleteSharingAsync(RemoveGroupSharing)
                    Me.ReplaceUpdatedSharingInDmsItem(RemoveGroupSharing.Group.ID, Nothing)
                    Me.DmsItem.ExtendedInfosHasGroupSharings = (Not Me.DmsItem.ExtendedInfosGroupSharings.Count = 0)
                Case GetType(DmsShareForUser)
                    Dim RemoveUserSharing As DmsShareForUser = CType(CurrentSharing, DmsShareForUser)
                    Await Me.DmsProvider.DeleteSharingAsync(RemoveUserSharing)
                    Me.ReplaceUpdatedSharingInDmsItem(RemoveUserSharing.User.ID, Nothing)
                    Me.DmsItem.ExtendedInfosHasUserSharings = (Not Me.DmsItem.ExtendedInfosUserSharings.Count = 0)
                Case Else
                    Throw New NotImplementedException(UiStrings.GetText("UnknownDerivedClassFromDmsShareBase"))
            End Select
            RefreshControls()
            RaiseEvent SharingsChanged(Me, EventArgs.Empty)
    End Function

    Private Async Sub ToolStripButtonExternalSharingsAdd_Click(sender As Object, e As EventArgs) Handles ToolStripButtonExternalSharingsAdd.Click
        If PendingOperation.IsRunning Then Return
        Try
            Dim LinkShareForm As New DmsLinkShareSetup
            LinkShareForm.DmsProvider = Me.DmsProvider
            LinkShareForm.DmsLinkDetails = Nothing
            LinkShareForm.DmsItem = Me.DmsItem
            LinkShareForm.DialogMode = DmsLinkShareSetup.DialogModes.CreateLink
            If LinkShareForm.ShowDialog(Me) = DialogResult.OK Then
                Await PendingOperation.RunAsync(Async Function()
                                                    Await LinkShareForm.DmsUpdatedLinkDetails.RefreshAsync()
                                                    RefreshControls()
                                                    RaiseEvent SharingsChanged(Me, EventArgs.Empty)
                                                End Function)
            End If
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.Message), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.ToString), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function CurrentSelectedLink() As DmsLink
        If Me.ListViewExternalSharings.SelectedItems?.Count = 0 Then
            Return Nothing
        Else
            Return CType(Me.ListViewExternalSharings.SelectedItems(0).Tag, DmsLink)
        End If
    End Function

    Private Async Sub ToolStripButtonExternalSharingsEdit_Click(sender As Object, e As EventArgs) Handles ToolStripButtonExternalSharingsEdit.Click
        If PendingOperation.IsRunning Then Return
        Try
            If Me.CurrentSelectedLink Is Nothing Then Throw New DmsUserErrorMessageException(UiStrings.GetText("LinkSharingRequired"))
            Dim LinkShareForm As New DmsLinkShareSetup()
            LinkShareForm.DmsProvider = Me.DmsProvider
            LinkShareForm.DmsLinkDetails = Me.CurrentSelectedLink
            LinkShareForm.DialogMode = DmsLinkShareSetup.DialogModes.UpdateLink
            If LinkShareForm.ShowDialog(Me) = DialogResult.OK Then
                Await PendingOperation.RunAsync(Async Function()
                                                    Await LinkShareForm.DmsUpdatedLinkDetails.RefreshAsync()
                                                    Me.ReplaceUpdatedLinkInDmsItem(LinkShareForm.DmsUpdatedLinkDetails.ID, LinkShareForm.DmsUpdatedLinkDetails)
                                                    RefreshControls()
                                                    RaiseEvent SharingsChanged(Me, EventArgs.Empty)
                                                End Function)
            End If
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.Message), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.ToString), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ''' <summary>
    ''' Search for a DmsLink with ID as in updatedLinkDetails and replace that list item with updatedLinkDetails (to update a DmsResourceItem partially without need of full data refreshing)
    ''' </summary>
    ''' <param name="updatedLinkDetails"></param>
    Private Sub ReplaceUpdatedLinkInDmsItem(id As String, updatedLinkDetails As DmsLink)
        For MyCounter As Integer = 0 To Me.DmsItem.ExtendedInfosLinks.Count - 1
            If Me.DmsItem.ExtendedInfosLinks(MyCounter).ID = id Then
                If updatedLinkDetails Is Nothing Then
                    Me.DmsItem.ExtendedInfosLinks.RemoveAt(MyCounter)
                Else
                    Me.DmsItem.ExtendedInfosLinks(MyCounter) = updatedLinkDetails
                End If
                Return
            End If
        Next
        Throw New InvalidOperationException(UiStrings.Format("OriginDmsLinkWithIDNotFoundInDmsResourceItem", id))
    End Sub

    ''' <summary>
    ''' Search for a DmsShareBase with ID as in updatedSharingDetails and replace that list item with updatedSharingDetails (to update a DmsResourceItem partially without need of full data refreshing)
    ''' </summary>
    ''' <param name="updatedSharingDetails"></param>
    Private Sub ReplaceUpdatedSharingInDmsItem(id As String, updatedSharingDetails As DmsShareBase)
        For MyCounter As Integer = 0 To Me.DmsItem.ExtendedInfosGroupSharings.Count - 1
            If Me.DmsItem.ExtendedInfosGroupSharings(MyCounter).Group.ID = id Then
                If updatedSharingDetails Is Nothing Then
                    Me.DmsItem.ExtendedInfosGroupSharings.RemoveAt(MyCounter)
                Else
                    Me.DmsItem.ExtendedInfosGroupSharings(MyCounter) = CType(updatedSharingDetails, DmsShareForGroup)
                End If
                Return
            End If
        Next
        For MyCounter As Integer = 0 To Me.DmsItem.ExtendedInfosUserSharings.Count - 1
            If Me.DmsItem.ExtendedInfosUserSharings(MyCounter).User.ID = id Then
                If updatedSharingDetails Is Nothing Then
                    Me.DmsItem.ExtendedInfosUserSharings.RemoveAt(MyCounter)
                Else
                    Me.DmsItem.ExtendedInfosUserSharings(MyCounter) = CType(updatedSharingDetails, DmsShareForUser)
                End If
                Return
            End If
        Next
        Throw New InvalidOperationException(UiStrings.Format("OriginDmsShareBaseItemWithIDNotFoundIn", id))
    End Sub

    Private Async Sub ToolStripButtonExternalSharingsDelete_Click(sender As Object, e As EventArgs) Handles ToolStripButtonExternalSharingsDelete.Click
        If PendingOperation.IsRunning Then Return
        Try
            If Me.CurrentSelectedLink Is Nothing Then Throw New DmsUserErrorMessageException(UiStrings.GetText("LinkSharingRequired"))
            Dim RemoveLink As DmsLink = Me.CurrentSelectedLink
            Await PendingOperation.RunAsync(Async Function()
                                                If Not RemoveLink.ID = Nothing Then Await Me.DmsProvider.DeleteLinkAsync(RemoveLink)
                                                Me.ReplaceUpdatedLinkInDmsItem(RemoveLink.ID, Nothing)
                                                RefreshControls()
                                                RaiseEvent SharingsChanged(Me, EventArgs.Empty)
                                            End Function)
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.Message), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.ToString), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripButtonCopyLinkUrlToClipboard_Click(sender As Object, e As EventArgs) Handles ToolStripButtonCopyLinkUrlToClipboard.Click
        Try
            If Me.CurrentSelectedLink Is Nothing Then Throw New DmsUserErrorMessageException(UiStrings.GetText("LinkSharingRequired"))
            System.Windows.Forms.Clipboard.Clear()
            System.Windows.Forms.Clipboard.SetText(Me.CurrentSelectedLink.WebUrl)
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.Message), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.ToString), Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ListViewExternalSharings_MouseDoubleClick(sender As Object, e As MouseEventArgs) Handles ListViewExternalSharings.MouseDoubleClick
        If Not Me.ListViewExternalSharings.SelectedItems.Count = 0 Then
            Me.ToolStripButtonExternalSharingsEdit_Click(sender, Nothing)
        End If
    End Sub

    Private Sub ListViewInternalSharings_MouseDoubleClick(sender As Object, e As MouseEventArgs) Handles ListViewInternalSharings.MouseDoubleClick
        If Not Me.ListViewInternalSharings.SelectedItems.Count = 0 Then
            Me.ToolStripButtonInternalSharingsEdit_Click(sender, Nothing)
        End If
    End Sub

    Private Function CurrentSelectedUserOrGroupSharing() As DmsShareBase
        If Me.ListViewInternalSharings.SelectedItems?.Count = 0 Then
            Return Nothing
        Else
            Return CType(Me.ListViewInternalSharings.SelectedItems(0).Tag, DmsShareBase)
        End If
    End Function

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
