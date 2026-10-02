Option Explicit On
Option Strict On

Imports System.Windows.Forms
Imports System.ComponentModel
Imports CompuMaster.Dms.Data

Friend NotInheritable Class DmsInstanceSelectionDialog
    Inherits Form

    Private ReadOnly InstancesList As New ListBox()

    <Obsolete("Use overload instead")>
    <EditorBrowsable(EditorBrowsableState.Never)>
    Friend Sub New(instances As IReadOnlyList(Of DmsInstanceInfo))
        Me.New(instances, Nothing)
    End Sub

    Friend Sub New(instances As IReadOnlyList(Of DmsInstanceInfo), formIcon As Drawing.Icon)
        Me.Text = UiStrings.GetText("DmsInstanceSelectionTitle")
        Me.Icon = If(formIcon, CType((New System.ComponentModel.ComponentResourceManager(GetType(DmsBrowser))).GetObject("$this.Icon"), Drawing.Icon))
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.StartPosition = FormStartPosition.CenterParent
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.ShowInTaskbar = False
        Me.ClientSize = New Drawing.Size(420, 300)

        Dim Prompt As New Label With {
            .Text = UiStrings.GetText("DmsInstanceSelectionPrompt"),
            .Location = New Drawing.Point(12, 12),
            .Size = New Drawing.Size(396, 24)
        }
        Me.InstancesList.DisplayMember = NameOf(DmsInstanceInfo.DisplayName)
        Me.InstancesList.Location = New Drawing.Point(12, 42)
        Me.InstancesList.Size = New Drawing.Size(396, 210)
        Me.InstancesList.Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        For Each Instance As DmsInstanceInfo In instances.OrderBy(Function(item) item.DisplayName, StringComparer.CurrentCultureIgnoreCase).ThenBy(Function(item) item.ID, StringComparer.Ordinal)
            Me.InstancesList.Items.Add(Instance)
            If Instance.IsSelected Then Me.InstancesList.SelectedItem = Instance
        Next
        If Me.InstancesList.SelectedIndex < 0 AndAlso Me.InstancesList.Items.Count > 0 Then Me.InstancesList.SelectedIndex = 0

        Dim OkayButton As New Button With {
            .Text = UiStrings.GetText("ActionOkay"),
            .DialogResult = DialogResult.OK,
            .Location = New Drawing.Point(232, 263),
            .Size = New Drawing.Size(85, 25),
            .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right
        }
        Dim CancelButton As New Button With {
            .Text = UiStrings.GetText("ActionCancel"),
            .DialogResult = DialogResult.Cancel,
            .Location = New Drawing.Point(323, 263),
            .Size = New Drawing.Size(85, 25),
            .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right
        }
        Me.AcceptButton = OkayButton
        Me.CancelButton = CancelButton
        Me.Controls.Add(Prompt)
        Me.Controls.Add(Me.InstancesList)
        Me.Controls.Add(OkayButton)
        Me.Controls.Add(CancelButton)
    End Sub

    Friend ReadOnly Property SelectedInstance As DmsInstanceInfo
        Get
            Return TryCast(Me.InstancesList.SelectedItem, DmsInstanceInfo)
        End Get
    End Property
End Class
