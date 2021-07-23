Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Public Class DmsItemSharings

    Public Property DmsItem As DmsResourceItem
    Public Property DmsProvider As BaseDmsProvider

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

    Private Sub DmsItemSharings_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.Text = Me.DmsProvider.Name & " - " & Me.DmsItem.FullName
        Me.LabelCurrentOwner.Text = String.Format(Me.LabelCurrentOwner.Text, Me.DmsItem.ExtendedInfosOwner.DisplayName)
        Me.ListViewInternalSharings.Items.Clear()
        'Add group sharings
        If Me.DmsItem.ExtendedInfosHasGroupSharings Then
            For Each Share As DmsShareForGroup In Me.DmsItem.ExtendedInfosGroupSharings
                Me.AddSharingEntry(Share, "Group", Share.Group.DisplayName, Strings.Join(Share.AllowedActions.ToArray, ", "))
            Next
        End If
        If Me.DmsItem.ExtendedInfosHasHiddenGroupSharings Then
            Me.AddSharingEntry(Nothing, "Group", "{Hidden}", "Unknown")
        End If
        'Add user sharings
        If Me.DmsItem.ExtendedInfosHasUserSharings Then
            For Each Share As DmsShareForUser In Me.DmsItem.ExtendedInfosUserSharings
                Me.AddSharingEntry(Share, "User", Share.User.DisplayName, Strings.Join(Share.AllowedActions.ToArray, ", "))
            Next
        End If
        If Me.DmsItem.ExtendedInfosHasHiddenUserSharings Then
            Me.AddSharingEntry(Nothing, "User", "{Hidden}", "Unknown")
        End If
        'Add link sharings
        Me.ListViewExternalSharings.Items.Clear()
        If Me.DmsItem.ExtendedInfosLinks IsNot Nothing Then
            For Each LinkShare As DmsLink In Me.DmsItem.ExtendedInfosLinks
                Dim Limitations As New List(Of String)
                If Not LinkShare.Password = Nothing Then Limitations.Add("Password")
                If LinkShare.ExpiryDateLocalTime.HasValue Then Limitations.Add(LinkShare.ExpiryDateLocalTime.Value.ToString("yyyy-MM-dd HH:mm:ss"))
                If LinkShare.MaxBytes.HasValue Then Limitations.Add(Tools.ByteSizeToUIDisplayText(LinkShare.MaxBytes.Value))
                If LinkShare.MaxDownloads.HasValue Then Limitations.Add("Downloads: " & LinkShare.MaxDownloads.Value)
                If LinkShare.MaxUploads.HasValue Then Limitations.Add("Uploads: " & LinkShare.MaxUploads.Value)
                Dim DisplayName As String = LinkShare.Name
                If DisplayName = Nothing Then DisplayName = LinkShare.ID
                Me.AddLinkSharingEntry(LinkShare, DisplayName, Strings.Join(LinkShare.AllowedActions.ToArray, ", "), Strings.Join(Limitations.ToArray, ", "))
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

    Public ReadOnly Property AuthorizedGroupIDs As List(Of String)
        Get
            Dim Result As New List(Of String)
            For Each Item As ListViewItem In Me.ListViewInternalSharings.Items
                If CType(Item.Tag, DmsShareBase).GetType = GetType(DmsShareForGroup) Then
                    Result.Add(CType(Item.Tag, DmsShareForGroup).Group.ID)
                End If
            Next
            Return Result
        End Get
    End Property

    Public ReadOnly Property AuthorizedUserIDs As List(Of String)
        Get
            Dim Result As New List(Of String)
            For Each Item As ListViewItem In Me.ListViewInternalSharings.Items
                If CType(Item.Tag, DmsShareBase).GetType = GetType(DmsShareForUser) Then
                    Result.Add(CType(Item.Tag, DmsShareForUser).User.ID)
                End If
            Next
            Return Result
        End Get
    End Property

    Private Sub ToolStripButtonInternalSharingsAddGroup_Click(sender As Object, e As EventArgs) Handles ToolStripButtonInternalSharingsAddGroup.Click
        Try
            Dim HideGroupIDs As List(Of String) = AuthorizedGroupIDs
            Dim StandardSharingSetupForm As DmsStandardShareSetup
            StandardSharingSetupForm = New DmsStandardShareSetup(CType(Nothing, DmsShareForGroup), Me.DmsProvider, HideGroupIDs)
            StandardSharingSetupForm.DmsItem = Me.DmsItem
            If StandardSharingSetupForm.ShowDialog(Me) = DialogResult.OK Then
                Dim CreatedSharing As DmsShareForGroup = CType(StandardSharingSetupForm.DmsUpdatedSharingDetails, DmsShareForGroup)
                Me.DmsItem.ExtendedInfosHasGroupSharings = True
                Me.DmsItem.ExtendedInfosGroupSharings.Add(CreatedSharing)
                DmsItemSharings_Load(Nothing, Nothing)
            End If
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripButtonInternalSharingsAddUser_Click(sender As Object, e As EventArgs) Handles ToolStripButtonInternalSharingsAddUser.Click
        Try
            Dim HideUserIDs As List(Of String) = Me.AuthorizedUserIDs
            If Not HideUserIDs.Contains(Me.DmsProvider.CurrentContextUserID) Then
                HideUserIDs.Add(Me.DmsProvider.CurrentContextUserID)
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
                DmsItemSharings_Load(Nothing, Nothing)
            End If
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripButtonInternalSharingsEdit_Click(sender As Object, e As EventArgs) Handles ToolStripButtonInternalSharingsEdit.Click
        Try
            Dim CurrentSharing As DmsShareBase = Me.CurrentSelectedUserOrGroupSharing
            If CurrentSharing Is Nothing Then Throw New DmsUserErrorMessageException("Keine Benutzer-Freigabe ausgewählt")
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
                    Throw New NotImplementedException("Unknown derived class from DmsShareBase")
            End Select
            DmsItemSharings_Load(Nothing, Nothing)
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripButtonInternalSharingsDelete_Click(sender As Object, e As EventArgs) Handles ToolStripButtonInternalSharingsDelete.Click
        Try
            Dim CurrentSharing As DmsShareBase = Me.CurrentSelectedUserOrGroupSharing
            If CurrentSharing Is Nothing Then Throw New DmsUserErrorMessageException("Keine Benutzer-Freigabe ausgewählt")
            Select Case CurrentSharing.GetType
                Case GetType(DmsShareForGroup)
                    Dim RemoveGroupSharing As DmsShareForGroup = CType(CurrentSharing, DmsShareForGroup)
                    Me.DmsProvider.DeleteSharing(RemoveGroupSharing)
                    Me.ReplaceUpdatedSharingInDmsItem(RemoveGroupSharing.Group.ID, Nothing)
                    Me.DmsItem.ExtendedInfosHasGroupSharings = (Not Me.DmsItem.ExtendedInfosGroupSharings.Count = 0)
                Case GetType(DmsShareForUser)
                    Dim RemoveUserSharing As DmsShareForUser = CType(CurrentSharing, DmsShareForUser)
                    Me.DmsProvider.DeleteSharing(RemoveUserSharing)
                    Me.ReplaceUpdatedSharingInDmsItem(RemoveUserSharing.User.ID, Nothing)
                    Me.DmsItem.ExtendedInfosHasUserSharings = (Not Me.DmsItem.ExtendedInfosUserSharings.Count = 0)
                Case Else
                    Throw New NotImplementedException("Unknown derived class from DmsShareBase")
            End Select
            DmsItemSharings_Load(Nothing, Nothing)
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripButtonExternalSharingsAdd_Click(sender As Object, e As EventArgs) Handles ToolStripButtonExternalSharingsAdd.Click
        Try
            Dim LinkShareForm As New DmsLinkShareSetup
            LinkShareForm.DmsProvider = Me.DmsProvider
            LinkShareForm.DmsLinkDetails = Nothing
            LinkShareForm.DmsItem = Me.DmsItem
            LinkShareForm.DialogMode = DmsLinkShareSetup.DialogModes.CreateLink
            If LinkShareForm.ShowDialog(Me) = DialogResult.OK Then
                LinkShareForm.DmsUpdatedLinkDetails.Refresh()
                Me.DmsItem.ExtendedInfosLinks.Add(LinkShareForm.DmsUpdatedLinkDetails)
                DmsItemSharings_Load(Nothing, Nothing)
            End If
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function CurrentSelectedLink() As DmsLink
        If Me.ListViewExternalSharings.SelectedItems?.Count = 0 Then
            Return Nothing
        Else
            Return CType(Me.ListViewExternalSharings.SelectedItems(0).Tag, DmsLink)
        End If
    End Function

    Private Sub ToolStripButtonExternalSharingsEdit_Click(sender As Object, e As EventArgs) Handles ToolStripButtonExternalSharingsEdit.Click
        Try
            If Me.CurrentSelectedLink Is Nothing Then Throw New DmsUserErrorMessageException("Keine Link-Freigabe ausgewählt")
            Dim LinkShareForm As New DmsLinkShareSetup()
            LinkShareForm.DmsProvider = Me.DmsProvider
            LinkShareForm.DmsLinkDetails = Me.CurrentSelectedLink
            LinkShareForm.DialogMode = DmsLinkShareSetup.DialogModes.UpdateLink
            If LinkShareForm.ShowDialog(Me) = DialogResult.OK Then
                LinkShareForm.DmsUpdatedLinkDetails.Refresh()
                Me.ReplaceUpdatedLinkInDmsItem(LinkShareForm.DmsUpdatedLinkDetails.ID, LinkShareForm.DmsUpdatedLinkDetails)
                DmsItemSharings_Load(Nothing, Nothing)
            End If
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
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
        Throw New InvalidOperationException("Origin DmsLink with ID """ & id & """ not found in DmsResourceItem")
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
        Throw New InvalidOperationException("Origin DmsShareBase item with ID """ & id & """ not found in DmsResourceItem")
    End Sub

    Private Sub ToolStripButtonExternalSharingsDelete_Click(sender As Object, e As EventArgs) Handles ToolStripButtonExternalSharingsDelete.Click
        Try
            If Me.CurrentSelectedLink Is Nothing Then Throw New DmsUserErrorMessageException("Keine Link-Freigabe ausgewählt")
            Dim RemoveLink As DmsLink = Me.CurrentSelectedLink
            If Not RemoveLink.ID = Nothing Then Me.DmsProvider.DeleteLink(RemoveLink)
            Me.ReplaceUpdatedLinkInDmsItem(RemoveLink.ID, Nothing)
            DmsItemSharings_Load(Nothing, Nothing)
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToolStripButtonCopyLinkUrlToClipboard_Click(sender As Object, e As EventArgs) Handles ToolStripButtonCopyLinkUrlToClipboard.Click
        Try
            If Me.CurrentSelectedLink Is Nothing Then Throw New DmsUserErrorMessageException("Keine Link-Freigabe ausgewählt")
            System.Windows.Forms.Clipboard.Clear()
            System.Windows.Forms.Clipboard.SetText(Me.CurrentSelectedLink.WebUrl)
        Catch ex As Data.DmsUserErrorMessageException
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.Message, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch ex As Exception
            System.Windows.Forms.MessageBox.Show(Me, "ERROR: " & ex.ToString, Me.Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
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

End Class