Imports System.Windows.Forms

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class DmsStandardShareSetup
    Inherits System.Windows.Forms.Form

    'Das Formular überschreibt den Löschvorgang, um die Komponentenliste zu bereinigen.
    <System.Diagnostics.DebuggerNonUserCode()>
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
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(DmsStandardShareSetup))
        Me.CheckBoxAllowView = New System.Windows.Forms.CheckBox()
        Me.GroupBoxAuthorizations = New System.Windows.Forms.GroupBox()
        Me.CheckBoxAllowShare = New System.Windows.Forms.CheckBox()
        Me.CheckBoxAllowDelete = New System.Windows.Forms.CheckBox()
        Me.CheckBoxAllowUpload = New System.Windows.Forms.CheckBox()
        Me.CheckBoxAllowDownload = New System.Windows.Forms.CheckBox()
        Me.CheckBoxAllowEdit = New System.Windows.Forms.CheckBox()
        Me.ButtonCancel = New System.Windows.Forms.Button()
        Me.ButtonSave = New System.Windows.Forms.Button()
        Me.GroupBoxGeneral = New System.Windows.Forms.GroupBox()
        Me.ComboBoxUsersOrGroups = New System.Windows.Forms.ComboBox()
        Me.LabelName = New System.Windows.Forms.Label()
        Me.GroupBoxAuthorizations.SuspendLayout()
        Me.GroupBoxGeneral.SuspendLayout()
        Me.SuspendLayout()
        '
        'CheckBoxAllowView
        '
        Me.CheckBoxAllowView.AutoSize = True
        Me.CheckBoxAllowView.Location = New System.Drawing.Point(7, 22)
        Me.CheckBoxAllowView.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.CheckBoxAllowView.Name = "CheckBoxAllowView"
        Me.CheckBoxAllowView.Size = New System.Drawing.Size(75, 19)
        Me.CheckBoxAllowView.TabIndex = 11
        Me.CheckBoxAllowView.Text = "Anzeigen"
        Me.CheckBoxAllowView.UseVisualStyleBackColor = True
        '
        'GroupBoxAuthorizations
        '
        Me.GroupBoxAuthorizations.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.GroupBoxAuthorizations.Controls.Add(Me.CheckBoxAllowShare)
        Me.GroupBoxAuthorizations.Controls.Add(Me.CheckBoxAllowDelete)
        Me.GroupBoxAuthorizations.Controls.Add(Me.CheckBoxAllowUpload)
        Me.GroupBoxAuthorizations.Controls.Add(Me.CheckBoxAllowDownload)
        Me.GroupBoxAuthorizations.Controls.Add(Me.CheckBoxAllowEdit)
        Me.GroupBoxAuthorizations.Controls.Add(Me.CheckBoxAllowView)
        Me.GroupBoxAuthorizations.Location = New System.Drawing.Point(14, 76)
        Me.GroupBoxAuthorizations.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.GroupBoxAuthorizations.Name = "GroupBoxAuthorizations"
        Me.GroupBoxAuthorizations.Padding = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.GroupBoxAuthorizations.Size = New System.Drawing.Size(594, 81)
        Me.GroupBoxAuthorizations.TabIndex = 10
        Me.GroupBoxAuthorizations.TabStop = False
        Me.GroupBoxAuthorizations.Text = "Berechtigungen"
        '
        'CheckBoxAllowShare
        '
        Me.CheckBoxAllowShare.AutoSize = True
        Me.CheckBoxAllowShare.Location = New System.Drawing.Point(7, 54)
        Me.CheckBoxAllowShare.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.CheckBoxAllowShare.Name = "CheckBoxAllowShare"
        Me.CheckBoxAllowShare.Size = New System.Drawing.Size(56, 19)
        Me.CheckBoxAllowShare.TabIndex = 16
        Me.CheckBoxAllowShare.Text = "Teilen"
        Me.CheckBoxAllowShare.UseVisualStyleBackColor = True
        '
        'CheckBoxAllowDelete
        '
        Me.CheckBoxAllowDelete.AutoSize = True
        Me.CheckBoxAllowDelete.Location = New System.Drawing.Point(493, 22)
        Me.CheckBoxAllowDelete.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.CheckBoxAllowDelete.Name = "CheckBoxAllowDelete"
        Me.CheckBoxAllowDelete.Size = New System.Drawing.Size(70, 19)
        Me.CheckBoxAllowDelete.TabIndex = 15
        Me.CheckBoxAllowDelete.Text = "Löschen"
        Me.CheckBoxAllowDelete.UseVisualStyleBackColor = True
        '
        'CheckBoxAllowUpload
        '
        Me.CheckBoxAllowUpload.AutoSize = True
        Me.CheckBoxAllowUpload.Location = New System.Drawing.Point(388, 22)
        Me.CheckBoxAllowUpload.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.CheckBoxAllowUpload.Name = "CheckBoxAllowUpload"
        Me.CheckBoxAllowUpload.Size = New System.Drawing.Size(64, 19)
        Me.CheckBoxAllowUpload.TabIndex = 14
        Me.CheckBoxAllowUpload.Text = "Upload"
        Me.CheckBoxAllowUpload.UseVisualStyleBackColor = True
        '
        'CheckBoxAllowDownload
        '
        Me.CheckBoxAllowDownload.AutoSize = True
        Me.CheckBoxAllowDownload.Location = New System.Drawing.Point(265, 22)
        Me.CheckBoxAllowDownload.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.CheckBoxAllowDownload.Name = "CheckBoxAllowDownload"
        Me.CheckBoxAllowDownload.Size = New System.Drawing.Size(80, 19)
        Me.CheckBoxAllowDownload.TabIndex = 13
        Me.CheckBoxAllowDownload.Text = "Download"
        Me.CheckBoxAllowDownload.UseVisualStyleBackColor = True
        '
        'CheckBoxAllowEdit
        '
        Me.CheckBoxAllowEdit.AutoSize = True
        Me.CheckBoxAllowEdit.Location = New System.Drawing.Point(140, 22)
        Me.CheckBoxAllowEdit.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.CheckBoxAllowEdit.Name = "CheckBoxAllowEdit"
        Me.CheckBoxAllowEdit.Size = New System.Drawing.Size(82, 19)
        Me.CheckBoxAllowEdit.TabIndex = 12
        Me.CheckBoxAllowEdit.Text = "Bearbeiten"
        Me.CheckBoxAllowEdit.UseVisualStyleBackColor = True
        '
        'ButtonCancel
        '
        Me.ButtonCancel.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.ButtonCancel.Location = New System.Drawing.Point(426, 165)
        Me.ButtonCancel.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.ButtonCancel.Name = "ButtonCancel"
        Me.ButtonCancel.Size = New System.Drawing.Size(88, 27)
        Me.ButtonCancel.TabIndex = 41
        Me.ButtonCancel.Text = "&Abbrechen"
        Me.ButtonCancel.UseVisualStyleBackColor = True
        '
        'ButtonSave
        '
        Me.ButtonSave.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.ButtonSave.Location = New System.Drawing.Point(520, 165)
        Me.ButtonSave.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.ButtonSave.Name = "ButtonSave"
        Me.ButtonSave.Size = New System.Drawing.Size(88, 27)
        Me.ButtonSave.TabIndex = 40
        Me.ButtonSave.Text = "&Speichern"
        Me.ButtonSave.UseVisualStyleBackColor = True
        '
        'GroupBoxGeneral
        '
        Me.GroupBoxGeneral.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.GroupBoxGeneral.Controls.Add(Me.ComboBoxUsersOrGroups)
        Me.GroupBoxGeneral.Controls.Add(Me.LabelName)
        Me.GroupBoxGeneral.Location = New System.Drawing.Point(14, 14)
        Me.GroupBoxGeneral.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.GroupBoxGeneral.Name = "GroupBoxGeneral"
        Me.GroupBoxGeneral.Padding = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.GroupBoxGeneral.Size = New System.Drawing.Size(594, 55)
        Me.GroupBoxGeneral.TabIndex = 0
        Me.GroupBoxGeneral.TabStop = False
        Me.GroupBoxGeneral.Text = "Allgemeine Einstellungen"
        '
        'ComboBoxUsersOrGroups
        '
        Me.ComboBoxUsersOrGroups.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.ComboBoxUsersOrGroups.DisplayMember = "Value"
        Me.ComboBoxUsersOrGroups.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.ComboBoxUsersOrGroups.FormattingEnabled = True
        Me.ComboBoxUsersOrGroups.Location = New System.Drawing.Point(140, 22)
        Me.ComboBoxUsersOrGroups.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.ComboBoxUsersOrGroups.Name = "ComboBoxUsersOrGroups"
        Me.ComboBoxUsersOrGroups.Size = New System.Drawing.Size(446, 23)
        Me.ComboBoxUsersOrGroups.TabIndex = 1
        Me.ComboBoxUsersOrGroups.ValueMember = "Key"
        '
        'LabelName
        '
        Me.LabelName.AutoSize = True
        Me.LabelName.Location = New System.Drawing.Point(7, 25)
        Me.LabelName.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.LabelName.Name = "LabelName"
        Me.LabelName.Size = New System.Drawing.Size(39, 15)
        Me.LabelName.TabIndex = 31
        Me.LabelName.Text = "Name"
        '
        'DmsStandardShareSetup
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.CancelButton = Me.ButtonCancel
        Me.ClientSize = New System.Drawing.Size(622, 205)
        Me.Controls.Add(Me.GroupBoxGeneral)
        Me.Controls.Add(Me.ButtonCancel)
        Me.Controls.Add(Me.ButtonSave)
        Me.Controls.Add(Me.GroupBoxAuthorizations)
        Me.Icon = CType(resources.GetObject("$this.Icon"), System.Drawing.Icon)
        Me.Margin = New System.Windows.Forms.Padding(4, 3, 4, 3)
        Me.Name = "DmsStandardShareSetup"
        Me.Text = "DMS Freigabe Einstellungen - {0}"
        Me.GroupBoxAuthorizations.ResumeLayout(False)
        Me.GroupBoxAuthorizations.PerformLayout()
        Me.GroupBoxGeneral.ResumeLayout(False)
        Me.GroupBoxGeneral.PerformLayout()
        Me.ResumeLayout(False)

    End Sub

    Friend WithEvents CheckBoxAllowView As CheckBox
    Friend WithEvents GroupBoxAuthorizations As GroupBox
    Friend WithEvents CheckBoxAllowDelete As CheckBox
    Friend WithEvents CheckBoxAllowUpload As CheckBox
    Friend WithEvents CheckBoxAllowDownload As CheckBox
    Friend WithEvents CheckBoxAllowEdit As CheckBox
    Friend WithEvents ButtonCancel As Button
    Friend WithEvents ButtonSave As Button
    Friend WithEvents GroupBoxGeneral As GroupBox
    Friend WithEvents LabelName As Label
    Friend WithEvents CheckBoxAllowShare As CheckBox
    Friend WithEvents ComboBoxUsersOrGroups As ComboBox
End Class
