Imports System.Windows.Forms

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class DmsItemSharings
    Inherits System.Windows.Forms.Form

    'Das Formular überschreibt den Löschvorgang, um die Komponentenliste zu bereinigen.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Wird vom Windows Form-Designer benötigt.
    Private components As System.ComponentModel.IContainer

    'Hinweis: Die folgende Prozedur ist für den Windows Form-Designer erforderlich.
    'Das Bearbeiten ist mit dem Windows Form-Designer möglich.  
    'Das Bearbeiten mit dem Code-Editor ist nicht möglich.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(DmsItemSharings))
        Me.ButtonCancel = New System.Windows.Forms.Button()
        Me.GroupBoxInternalSharings = New System.Windows.Forms.GroupBox()
        Me.ListViewInternalSharings = New System.Windows.Forms.ListView()
        Me.ColumnHeaderType = CType(New System.Windows.Forms.ColumnHeader(), System.Windows.Forms.ColumnHeader)
        Me.ColumnHeaderName = CType(New System.Windows.Forms.ColumnHeader(), System.Windows.Forms.ColumnHeader)
        Me.ColumnHeaderAuthorizations = CType(New System.Windows.Forms.ColumnHeader(), System.Windows.Forms.ColumnHeader)
        Me.ToolStripInternalSharings = New System.Windows.Forms.ToolStrip()
        Me.ToolStripButtonInternalSharingsAddGroup = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripButtonInternalSharingsAddUser = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripButtonInternalSharingsEdit = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripButtonInternalSharingsDelete = New System.Windows.Forms.ToolStripButton()
        Me.GroupBoxExternalSharings = New System.Windows.Forms.GroupBox()
        Me.ListViewExternalSharings = New System.Windows.Forms.ListView()
        Me.ColumnHeaderDisplayName = CType(New System.Windows.Forms.ColumnHeader(), System.Windows.Forms.ColumnHeader)
        Me.ColumnHeaderAuths = CType(New System.Windows.Forms.ColumnHeader(), System.Windows.Forms.ColumnHeader)
        Me.ColumnHeaderLimitations = CType(New System.Windows.Forms.ColumnHeader(), System.Windows.Forms.ColumnHeader)
        Me.ToolStripExternalSharings = New System.Windows.Forms.ToolStrip()
        Me.ToolStripButtonExternalSharingsAdd = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripButtonExternalSharingsEdit = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripButtonExternalSharingsDelete = New System.Windows.Forms.ToolStripButton()
        Me.ToolStripButtonCopyLinkUrlToClipboard = New System.Windows.Forms.ToolStripButton()
        Me.LabelCurrentOwner = New System.Windows.Forms.Label()
        Me.GroupBoxInternalSharings.SuspendLayout()
        Me.ToolStripInternalSharings.SuspendLayout()
        Me.GroupBoxExternalSharings.SuspendLayout()
        Me.ToolStripExternalSharings.SuspendLayout()
        Me.SuspendLayout()
        '
        'ButtonCancel
        '
        Me.ButtonCancel.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.ButtonCancel.Location = New System.Drawing.Point(479, 296)
        Me.ButtonCancel.Name = "ButtonCancel"
        Me.ButtonCancel.Size = New System.Drawing.Size(75, 23)
        Me.ButtonCancel.TabIndex = 30
        Me.ButtonCancel.Text = "&Schließen"
        Me.ButtonCancel.UseVisualStyleBackColor = True
        '
        'GroupBoxInternalSharings
        '
        Me.GroupBoxInternalSharings.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.GroupBoxInternalSharings.Controls.Add(Me.ListViewInternalSharings)
        Me.GroupBoxInternalSharings.Controls.Add(Me.ToolStripInternalSharings)
        Me.GroupBoxInternalSharings.Location = New System.Drawing.Point(12, 12)
        Me.GroupBoxInternalSharings.Name = "GroupBoxInternalSharings"
        Me.GroupBoxInternalSharings.Size = New System.Drawing.Size(542, 143)
        Me.GroupBoxInternalSharings.TabIndex = 10
        Me.GroupBoxInternalSharings.TabStop = False
        Me.GroupBoxInternalSharings.Text = "Freigaben an interne Benutzer/Gruppen"
        '
        'ListViewInternalSharings
        '
        Me.ListViewInternalSharings.Columns.AddRange(New System.Windows.Forms.ColumnHeader() {Me.ColumnHeaderType, Me.ColumnHeaderName, Me.ColumnHeaderAuthorizations})
        Me.ListViewInternalSharings.Dock = System.Windows.Forms.DockStyle.Fill
        Me.ListViewInternalSharings.FullRowSelect = True
        Me.ListViewInternalSharings.HideSelection = False
        Me.ListViewInternalSharings.Location = New System.Drawing.Point(3, 42)
        Me.ListViewInternalSharings.MultiSelect = False
        Me.ListViewInternalSharings.Name = "ListViewInternalSharings"
        Me.ListViewInternalSharings.ShowItemToolTips = True
        Me.ListViewInternalSharings.Size = New System.Drawing.Size(536, 98)
        Me.ListViewInternalSharings.TabIndex = 12
        Me.ListViewInternalSharings.UseCompatibleStateImageBehavior = False
        Me.ListViewInternalSharings.View = System.Windows.Forms.View.Details
        '
        'ColumnHeaderType
        '
        Me.ColumnHeaderType.Text = "Typ"
        '
        'ColumnHeaderName
        '
        Me.ColumnHeaderName.Text = "Name"
        Me.ColumnHeaderName.Width = 100
        '
        'ColumnHeaderAuthorizations
        '
        Me.ColumnHeaderAuthorizations.Text = "Berechtigungen"
        Me.ColumnHeaderAuthorizations.Width = 100
        '
        'ToolStripInternalSharings
        '
        Me.ToolStripInternalSharings.ImageScalingSize = New System.Drawing.Size(19, 19)
        Me.ToolStripInternalSharings.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.ToolStripButtonInternalSharingsAddGroup, Me.ToolStripButtonInternalSharingsAddUser, Me.ToolStripButtonInternalSharingsEdit, Me.ToolStripButtonInternalSharingsDelete})
        Me.ToolStripInternalSharings.Location = New System.Drawing.Point(3, 16)
        Me.ToolStripInternalSharings.Name = "ToolStripInternalSharings"
        Me.ToolStripInternalSharings.Padding = New System.Windows.Forms.Padding(0, 0, 2, 0)
        Me.ToolStripInternalSharings.Size = New System.Drawing.Size(536, 26)
        Me.ToolStripInternalSharings.TabIndex = 11
        '
        'ToolStripButtonInternalSharingsAddGroup
        '
        Me.ToolStripButtonInternalSharingsAddGroup.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Group_ui_ux_mobile_web_4960717
        Me.ToolStripButtonInternalSharingsAddGroup.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonInternalSharingsAddGroup.Name = "ToolStripButtonInternalSharingsAddGroup"
        Me.ToolStripButtonInternalSharingsAddGroup.Size = New System.Drawing.Size(132, 23)
        Me.ToolStripButtonInternalSharingsAddGroup.Text = "&Gruppe hinzufügen"
        '
        'ToolStripButtonInternalSharingsAddUser
        '
        Me.ToolStripButtonInternalSharingsAddUser.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_User_ui_ux_mobile_web_4960753
        Me.ToolStripButtonInternalSharingsAddUser.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonInternalSharingsAddUser.Name = "ToolStripButtonInternalSharingsAddUser"
        Me.ToolStripButtonInternalSharingsAddUser.Size = New System.Drawing.Size(139, 23)
        Me.ToolStripButtonInternalSharingsAddUser.Text = "Benutzer hin&zufügen"
        '
        'ToolStripButtonInternalSharingsEdit
        '
        Me.ToolStripButtonInternalSharingsEdit.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Rename_ui_ux_mobile_web_4960746
        Me.ToolStripButtonInternalSharingsEdit.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonInternalSharingsEdit.Name = "ToolStripButtonInternalSharingsEdit"
        Me.ToolStripButtonInternalSharingsEdit.Size = New System.Drawing.Size(86, 23)
        Me.ToolStripButtonInternalSharingsEdit.Text = "&Bearbeiten"
        '
        'ToolStripButtonInternalSharingsDelete
        '
        Me.ToolStripButtonInternalSharingsDelete.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Trash_ui_ux_mobile_web_4960750
        Me.ToolStripButtonInternalSharingsDelete.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonInternalSharingsDelete.Name = "ToolStripButtonInternalSharingsDelete"
        Me.ToolStripButtonInternalSharingsDelete.Size = New System.Drawing.Size(74, 23)
        Me.ToolStripButtonInternalSharingsDelete.Text = "&Löschen"
        '
        'GroupBoxExternalSharings
        '
        Me.GroupBoxExternalSharings.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.GroupBoxExternalSharings.Controls.Add(Me.ListViewExternalSharings)
        Me.GroupBoxExternalSharings.Controls.Add(Me.ToolStripExternalSharings)
        Me.GroupBoxExternalSharings.Location = New System.Drawing.Point(12, 161)
        Me.GroupBoxExternalSharings.Name = "GroupBoxExternalSharings"
        Me.GroupBoxExternalSharings.Size = New System.Drawing.Size(542, 129)
        Me.GroupBoxExternalSharings.TabIndex = 20
        Me.GroupBoxExternalSharings.TabStop = False
        Me.GroupBoxExternalSharings.Text = "Freigaben an externe Benutzer via Link"
        '
        'ListViewExternalSharings
        '
        Me.ListViewExternalSharings.Columns.AddRange(New System.Windows.Forms.ColumnHeader() {Me.ColumnHeaderDisplayName, Me.ColumnHeaderAuths, Me.ColumnHeaderLimitations})
        Me.ListViewExternalSharings.Dock = System.Windows.Forms.DockStyle.Fill
        Me.ListViewExternalSharings.FullRowSelect = True
        Me.ListViewExternalSharings.HideSelection = False
        Me.ListViewExternalSharings.Location = New System.Drawing.Point(3, 42)
        Me.ListViewExternalSharings.MultiSelect = False
        Me.ListViewExternalSharings.Name = "ListViewExternalSharings"
        Me.ListViewExternalSharings.ShowItemToolTips = True
        Me.ListViewExternalSharings.Size = New System.Drawing.Size(536, 84)
        Me.ListViewExternalSharings.TabIndex = 22
        Me.ListViewExternalSharings.UseCompatibleStateImageBehavior = False
        Me.ListViewExternalSharings.View = System.Windows.Forms.View.Details
        '
        'ColumnHeaderDisplayName
        '
        Me.ColumnHeaderDisplayName.Text = "Name"
        Me.ColumnHeaderDisplayName.Width = 100
        '
        'ColumnHeaderAuths
        '
        Me.ColumnHeaderAuths.Text = "Berechtigungen"
        Me.ColumnHeaderAuths.Width = 100
        '
        'ColumnHeaderLimitations
        '
        Me.ColumnHeaderLimitations.Text = "Limitations"
        Me.ColumnHeaderLimitations.Width = 100
        '
        'ToolStripExternalSharings
        '
        Me.ToolStripExternalSharings.ImageScalingSize = New System.Drawing.Size(19, 19)
        Me.ToolStripExternalSharings.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.ToolStripButtonExternalSharingsAdd, Me.ToolStripButtonExternalSharingsEdit, Me.ToolStripButtonExternalSharingsDelete, Me.ToolStripButtonCopyLinkUrlToClipboard})
        Me.ToolStripExternalSharings.Location = New System.Drawing.Point(3, 16)
        Me.ToolStripExternalSharings.Name = "ToolStripExternalSharings"
        Me.ToolStripExternalSharings.Padding = New System.Windows.Forms.Padding(0, 0, 2, 0)
        Me.ToolStripExternalSharings.Size = New System.Drawing.Size(536, 26)
        Me.ToolStripExternalSharings.TabIndex = 21
        '
        'ToolStripButtonExternalSharingsAdd
        '
        Me.ToolStripButtonExternalSharingsAdd.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_weather_ui_ux_mobile_web_4960756
        Me.ToolStripButtonExternalSharingsAdd.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonExternalSharingsAdd.Name = "ToolStripButtonExternalSharingsAdd"
        Me.ToolStripButtonExternalSharingsAdd.Size = New System.Drawing.Size(92, 23)
        Me.ToolStripButtonExternalSharingsAdd.Text = "&Hinzufügen"
        '
        'ToolStripButtonExternalSharingsEdit
        '
        Me.ToolStripButtonExternalSharingsEdit.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Rename_ui_ux_mobile_web_4960746
        Me.ToolStripButtonExternalSharingsEdit.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonExternalSharingsEdit.Name = "ToolStripButtonExternalSharingsEdit"
        Me.ToolStripButtonExternalSharingsEdit.Size = New System.Drawing.Size(86, 23)
        Me.ToolStripButtonExternalSharingsEdit.Text = "&Bearbeiten"
        '
        'ToolStripButtonExternalSharingsDelete
        '
        Me.ToolStripButtonExternalSharingsDelete.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_Trash_ui_ux_mobile_web_4960750
        Me.ToolStripButtonExternalSharingsDelete.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonExternalSharingsDelete.Name = "ToolStripButtonExternalSharingsDelete"
        Me.ToolStripButtonExternalSharingsDelete.Size = New System.Drawing.Size(74, 23)
        Me.ToolStripButtonExternalSharingsDelete.Text = "&Löschen"
        '
        'ToolStripButtonCopyLinkUrlToClipboard
        '
        Me.ToolStripButtonCopyLinkUrlToClipboard.Image = Global.CompuMaster.Dms.BrowserUI.My.Resources.Resources.iconfinder_weather_ui_ux_mobile_web_4960756
        Me.ToolStripButtonCopyLinkUrlToClipboard.ImageTransparentColor = System.Drawing.Color.Magenta
        Me.ToolStripButtonCopyLinkUrlToClipboard.Name = "ToolStripButtonCopyLinkUrlToClipboard"
        Me.ToolStripButtonCopyLinkUrlToClipboard.Size = New System.Drawing.Size(130, 23)
        Me.ToolStripButtonCopyLinkUrlToClipboard.Text = "Web-Link kopieren"
        '
        'LabelCurrentOwner
        '
        Me.LabelCurrentOwner.AutoSize = True
        Me.LabelCurrentOwner.Location = New System.Drawing.Point(12, 301)
        Me.LabelCurrentOwner.Name = "LabelCurrentOwner"
        Me.LabelCurrentOwner.Size = New System.Drawing.Size(124, 13)
        Me.LabelCurrentOwner.TabIndex = 31
        Me.LabelCurrentOwner.Text = "Aktueller Eigentümer: {0}"
        '
        'DmsItemSharings
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(566, 331)
        Me.Controls.Add(Me.LabelCurrentOwner)
        Me.Controls.Add(Me.GroupBoxExternalSharings)
        Me.Controls.Add(Me.GroupBoxInternalSharings)
        Me.Controls.Add(Me.ButtonCancel)
        Me.Icon = CType(resources.GetObject("$this.Icon"), System.Drawing.Icon)
        Me.Name = "DmsItemSharings"
        Me.Text = "DMS Freigabe-Einstellungen"
        Me.GroupBoxInternalSharings.ResumeLayout(False)
        Me.GroupBoxInternalSharings.PerformLayout()
        Me.ToolStripInternalSharings.ResumeLayout(False)
        Me.ToolStripInternalSharings.PerformLayout()
        Me.GroupBoxExternalSharings.ResumeLayout(False)
        Me.GroupBoxExternalSharings.PerformLayout()
        Me.ToolStripExternalSharings.ResumeLayout(False)
        Me.ToolStripExternalSharings.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents ButtonCancel As Button
    Friend WithEvents GroupBoxInternalSharings As GroupBox
    Friend WithEvents GroupBoxExternalSharings As GroupBox
    Friend WithEvents ToolStripInternalSharings As ToolStrip
    Friend WithEvents ToolStripButtonInternalSharingsAddUser As ToolStripButton
    Friend WithEvents ToolStripButtonInternalSharingsEdit As ToolStripButton
    Friend WithEvents ToolStripButtonInternalSharingsDelete As ToolStripButton
    Friend WithEvents ListViewInternalSharings As ListView
    Friend WithEvents ColumnHeaderType As ColumnHeader
    Friend WithEvents ColumnHeaderName As ColumnHeader
    Friend WithEvents ColumnHeaderAuthorizations As ColumnHeader
    Friend WithEvents ListViewExternalSharings As ListView
    Friend WithEvents ColumnHeaderLimitations As ColumnHeader
    Friend WithEvents ColumnHeaderDisplayName As ColumnHeader
    Friend WithEvents ColumnHeaderAuths As ColumnHeader
    Friend WithEvents ToolStripExternalSharings As ToolStrip
    Friend WithEvents ToolStripButtonExternalSharingsAdd As ToolStripButton
    Friend WithEvents ToolStripButtonExternalSharingsEdit As ToolStripButton
    Friend WithEvents ToolStripButtonExternalSharingsDelete As ToolStripButton
    Friend WithEvents ToolStripButtonInternalSharingsAddGroup As ToolStripButton
    Friend WithEvents ToolStripButtonCopyLinkUrlToClipboard As ToolStripButton
    Friend WithEvents LabelCurrentOwner As Label
End Class
