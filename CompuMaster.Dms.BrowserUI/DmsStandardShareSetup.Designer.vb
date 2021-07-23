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
        Me.CheckBoxAllowView.Location = New System.Drawing.Point(6, 19)
        Me.CheckBoxAllowView.Name = "CheckBoxAllowView"
        Me.CheckBoxAllowView.Size = New System.Drawing.Size(70, 17)
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
        Me.GroupBoxAuthorizations.Location = New System.Drawing.Point(12, 66)
        Me.GroupBoxAuthorizations.Name = "GroupBoxAuthorizations"
        Me.GroupBoxAuthorizations.Size = New System.Drawing.Size(509, 70)
        Me.GroupBoxAuthorizations.TabIndex = 10
        Me.GroupBoxAuthorizations.TabStop = False
        Me.GroupBoxAuthorizations.Text = "Berechtigungen"
        '
        'CheckBoxAllowShare
        '
        Me.CheckBoxAllowShare.AutoSize = True
        Me.CheckBoxAllowShare.Location = New System.Drawing.Point(6, 47)
        Me.CheckBoxAllowShare.Name = "CheckBoxAllowShare"
        Me.CheckBoxAllowShare.Size = New System.Drawing.Size(55, 17)
        Me.CheckBoxAllowShare.TabIndex = 16
        Me.CheckBoxAllowShare.Text = "Teilen"
        Me.CheckBoxAllowShare.UseVisualStyleBackColor = True
        '
        'CheckBoxAllowDelete
        '
        Me.CheckBoxAllowDelete.AutoSize = True
        Me.CheckBoxAllowDelete.Location = New System.Drawing.Point(423, 19)
        Me.CheckBoxAllowDelete.Name = "CheckBoxAllowDelete"
        Me.CheckBoxAllowDelete.Size = New System.Drawing.Size(67, 17)
        Me.CheckBoxAllowDelete.TabIndex = 15
        Me.CheckBoxAllowDelete.Text = "Löschen"
        Me.CheckBoxAllowDelete.UseVisualStyleBackColor = True
        '
        'CheckBoxAllowUpload
        '
        Me.CheckBoxAllowUpload.AutoSize = True
        Me.CheckBoxAllowUpload.Location = New System.Drawing.Point(333, 19)
        Me.CheckBoxAllowUpload.Name = "CheckBoxAllowUpload"
        Me.CheckBoxAllowUpload.Size = New System.Drawing.Size(60, 17)
        Me.CheckBoxAllowUpload.TabIndex = 14
        Me.CheckBoxAllowUpload.Text = "Upload"
        Me.CheckBoxAllowUpload.UseVisualStyleBackColor = True
        '
        'CheckBoxAllowDownload
        '
        Me.CheckBoxAllowDownload.AutoSize = True
        Me.CheckBoxAllowDownload.Location = New System.Drawing.Point(227, 19)
        Me.CheckBoxAllowDownload.Name = "CheckBoxAllowDownload"
        Me.CheckBoxAllowDownload.Size = New System.Drawing.Size(74, 17)
        Me.CheckBoxAllowDownload.TabIndex = 13
        Me.CheckBoxAllowDownload.Text = "Download"
        Me.CheckBoxAllowDownload.UseVisualStyleBackColor = True
        '
        'CheckBoxAllowEdit
        '
        Me.CheckBoxAllowEdit.AutoSize = True
        Me.CheckBoxAllowEdit.Location = New System.Drawing.Point(120, 19)
        Me.CheckBoxAllowEdit.Name = "CheckBoxAllowEdit"
        Me.CheckBoxAllowEdit.Size = New System.Drawing.Size(77, 17)
        Me.CheckBoxAllowEdit.TabIndex = 12
        Me.CheckBoxAllowEdit.Text = "Bearbeiten"
        Me.CheckBoxAllowEdit.UseVisualStyleBackColor = True
        '
        'ButtonCancel
        '
        Me.ButtonCancel.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.ButtonCancel.Location = New System.Drawing.Point(365, 143)
        Me.ButtonCancel.Name = "ButtonCancel"
        Me.ButtonCancel.Size = New System.Drawing.Size(75, 23)
        Me.ButtonCancel.TabIndex = 41
        Me.ButtonCancel.Text = "&Abbrechen"
        Me.ButtonCancel.UseVisualStyleBackColor = True
        '
        'ButtonSave
        '
        Me.ButtonSave.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.ButtonSave.Location = New System.Drawing.Point(446, 143)
        Me.ButtonSave.Name = "ButtonSave"
        Me.ButtonSave.Size = New System.Drawing.Size(75, 23)
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
        Me.GroupBoxGeneral.Location = New System.Drawing.Point(12, 12)
        Me.GroupBoxGeneral.Name = "GroupBoxGeneral"
        Me.GroupBoxGeneral.Size = New System.Drawing.Size(509, 48)
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
        Me.ComboBoxUsersOrGroups.Location = New System.Drawing.Point(120, 19)
        Me.ComboBoxUsersOrGroups.Name = "ComboBoxUsersOrGroups"
        Me.ComboBoxUsersOrGroups.Size = New System.Drawing.Size(383, 21)
        Me.ComboBoxUsersOrGroups.TabIndex = 1
        Me.ComboBoxUsersOrGroups.ValueMember = "Key"
        '
        'LabelName
        '
        Me.LabelName.AutoSize = True
        Me.LabelName.Location = New System.Drawing.Point(6, 22)
        Me.LabelName.Name = "LabelName"
        Me.LabelName.Size = New System.Drawing.Size(35, 13)
        Me.LabelName.TabIndex = 31
        Me.LabelName.Text = "Name"
        '
        'DmsStandardShareSetup
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(533, 178)
        Me.Controls.Add(Me.GroupBoxGeneral)
        Me.Controls.Add(Me.ButtonCancel)
        Me.Controls.Add(Me.ButtonSave)
        Me.Controls.Add(Me.GroupBoxAuthorizations)
        Me.Icon = CType(resources.GetObject("$this.Icon"), System.Drawing.Icon)
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
