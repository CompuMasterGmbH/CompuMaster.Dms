Imports System.Windows.Forms

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class DmsLinkShareSetup
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
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(DmsLinkShareSetup))
        Me.CheckBoxAllowView = New System.Windows.Forms.CheckBox()
        Me.GroupBoxAuthorizations = New System.Windows.Forms.GroupBox()
        Me.CheckBoxAllowShare = New System.Windows.Forms.CheckBox()
        Me.CheckBoxAllowDelete = New System.Windows.Forms.CheckBox()
        Me.CheckBoxAllowUpload = New System.Windows.Forms.CheckBox()
        Me.CheckBoxAllowDownload = New System.Windows.Forms.CheckBox()
        Me.CheckBoxAllowEdit = New System.Windows.Forms.CheckBox()
        Me.ButtonCancel = New System.Windows.Forms.Button()
        Me.ButtonSave = New System.Windows.Forms.Button()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.TextBoxID = New System.Windows.Forms.TextBox()
        Me.GroupBoxGeneral = New System.Windows.Forms.GroupBox()
        Me.TextBoxName = New System.Windows.Forms.TextBox()
        Me.Label12 = New System.Windows.Forms.Label()
        Me.CheckBoxPassword = New System.Windows.Forms.CheckBox()
        Me.CheckBoxExpiryDate = New System.Windows.Forms.CheckBox()
        Me.DateTimePickerExpiryDate = New System.Windows.Forms.DateTimePicker()
        Me.TextBoxPassword = New System.Windows.Forms.TextBox()
        Me.Label8 = New System.Windows.Forms.Label()
        Me.Label7 = New System.Windows.Forms.Label()
        Me.TextBoxDownloadUrl = New System.Windows.Forms.TextBox()
        Me.Label3 = New System.Windows.Forms.Label()
        Me.TextBoxWebUrl = New System.Windows.Forms.TextBox()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.GroupBoxExtended = New System.Windows.Forms.GroupBox()
        Me.CheckBoxMaxViews = New System.Windows.Forms.CheckBox()
        Me.TextBoxMaxViews = New System.Windows.Forms.TextBox()
        Me.Label14 = New System.Windows.Forms.Label()
        Me.CheckBoxMaxBytes = New System.Windows.Forms.CheckBox()
        Me.CheckBoxMaxUploads = New System.Windows.Forms.CheckBox()
        Me.CheckBoxMaxDownloads = New System.Windows.Forms.CheckBox()
        Me.TextBoxMaxBytes = New System.Windows.Forms.TextBox()
        Me.Label6 = New System.Windows.Forms.Label()
        Me.TextBoxMaxUploads = New System.Windows.Forms.TextBox()
        Me.Label5 = New System.Windows.Forms.Label()
        Me.TextBoxMaxDownloads = New System.Windows.Forms.TextBox()
        Me.Label4 = New System.Windows.Forms.Label()
        Me.GroupBox1 = New System.Windows.Forms.GroupBox()
        Me.TextBoxNumberOfViews = New System.Windows.Forms.TextBox()
        Me.Label13 = New System.Windows.Forms.Label()
        Me.TextBoxNumberOfBytes = New System.Windows.Forms.TextBox()
        Me.Label9 = New System.Windows.Forms.Label()
        Me.TextBoxNumberOfUploads = New System.Windows.Forms.TextBox()
        Me.Label10 = New System.Windows.Forms.Label()
        Me.TextBoxNumberOfDownloads = New System.Windows.Forms.TextBox()
        Me.Label11 = New System.Windows.Forms.Label()
        Me.GroupBoxAuthorizations.SuspendLayout()
        Me.GroupBoxGeneral.SuspendLayout()
        Me.GroupBoxExtended.SuspendLayout()
        Me.GroupBox1.SuspendLayout()
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
        Me.GroupBoxAuthorizations.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.GroupBoxAuthorizations.Controls.Add(Me.CheckBoxAllowShare)
        Me.GroupBoxAuthorizations.Controls.Add(Me.CheckBoxAllowDelete)
        Me.GroupBoxAuthorizations.Controls.Add(Me.CheckBoxAllowUpload)
        Me.GroupBoxAuthorizations.Controls.Add(Me.CheckBoxAllowDownload)
        Me.GroupBoxAuthorizations.Controls.Add(Me.CheckBoxAllowEdit)
        Me.GroupBoxAuthorizations.Controls.Add(Me.CheckBoxAllowView)
        Me.GroupBoxAuthorizations.Location = New System.Drawing.Point(448, 12)
        Me.GroupBoxAuthorizations.Name = "GroupBoxAuthorizations"
        Me.GroupBoxAuthorizations.Size = New System.Drawing.Size(108, 156)
        Me.GroupBoxAuthorizations.TabIndex = 10
        Me.GroupBoxAuthorizations.TabStop = False
        Me.GroupBoxAuthorizations.Text = "Berechtigungen"
        '
        'CheckBoxAllowShare
        '
        Me.CheckBoxAllowShare.AutoSize = True
        Me.CheckBoxAllowShare.Location = New System.Drawing.Point(6, 134)
        Me.CheckBoxAllowShare.Name = "CheckBoxAllowShare"
        Me.CheckBoxAllowShare.Size = New System.Drawing.Size(55, 17)
        Me.CheckBoxAllowShare.TabIndex = 16
        Me.CheckBoxAllowShare.Text = "Teilen"
        Me.CheckBoxAllowShare.UseVisualStyleBackColor = True
        '
        'CheckBoxAllowDelete
        '
        Me.CheckBoxAllowDelete.AutoSize = True
        Me.CheckBoxAllowDelete.Location = New System.Drawing.Point(6, 111)
        Me.CheckBoxAllowDelete.Name = "CheckBoxAllowDelete"
        Me.CheckBoxAllowDelete.Size = New System.Drawing.Size(67, 17)
        Me.CheckBoxAllowDelete.TabIndex = 15
        Me.CheckBoxAllowDelete.Text = "Löschen"
        Me.CheckBoxAllowDelete.UseVisualStyleBackColor = True
        '
        'CheckBoxAllowUpload
        '
        Me.CheckBoxAllowUpload.AutoSize = True
        Me.CheckBoxAllowUpload.Location = New System.Drawing.Point(6, 88)
        Me.CheckBoxAllowUpload.Name = "CheckBoxAllowUpload"
        Me.CheckBoxAllowUpload.Size = New System.Drawing.Size(60, 17)
        Me.CheckBoxAllowUpload.TabIndex = 14
        Me.CheckBoxAllowUpload.Text = "Upload"
        Me.CheckBoxAllowUpload.UseVisualStyleBackColor = True
        '
        'CheckBoxAllowDownload
        '
        Me.CheckBoxAllowDownload.AutoSize = True
        Me.CheckBoxAllowDownload.Location = New System.Drawing.Point(6, 65)
        Me.CheckBoxAllowDownload.Name = "CheckBoxAllowDownload"
        Me.CheckBoxAllowDownload.Size = New System.Drawing.Size(74, 17)
        Me.CheckBoxAllowDownload.TabIndex = 13
        Me.CheckBoxAllowDownload.Text = "Download"
        Me.CheckBoxAllowDownload.UseVisualStyleBackColor = True
        '
        'CheckBoxAllowEdit
        '
        Me.CheckBoxAllowEdit.AutoSize = True
        Me.CheckBoxAllowEdit.Location = New System.Drawing.Point(6, 42)
        Me.CheckBoxAllowEdit.Name = "CheckBoxAllowEdit"
        Me.CheckBoxAllowEdit.Size = New System.Drawing.Size(77, 17)
        Me.CheckBoxAllowEdit.TabIndex = 12
        Me.CheckBoxAllowEdit.Text = "Bearbeiten"
        Me.CheckBoxAllowEdit.UseVisualStyleBackColor = True
        '
        'ButtonCancel
        '
        Me.ButtonCancel.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.ButtonCancel.Location = New System.Drawing.Point(400, 328)
        Me.ButtonCancel.Name = "ButtonCancel"
        Me.ButtonCancel.Size = New System.Drawing.Size(75, 23)
        Me.ButtonCancel.TabIndex = 41
        Me.ButtonCancel.Text = "&Abbrechen"
        Me.ButtonCancel.UseVisualStyleBackColor = True
        '
        'ButtonSave
        '
        Me.ButtonSave.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.ButtonSave.Location = New System.Drawing.Point(481, 328)
        Me.ButtonSave.Name = "ButtonSave"
        Me.ButtonSave.Size = New System.Drawing.Size(75, 23)
        Me.ButtonSave.TabIndex = 40
        Me.ButtonSave.Text = "&Speichern"
        Me.ButtonSave.UseVisualStyleBackColor = True
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(6, 22)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(18, 13)
        Me.Label1.TabIndex = 21
        Me.Label1.Text = "ID"
        '
        'TextBoxID
        '
        Me.TextBoxID.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.TextBoxID.Location = New System.Drawing.Point(101, 19)
        Me.TextBoxID.Name = "TextBoxID"
        Me.TextBoxID.ReadOnly = True
        Me.TextBoxID.Size = New System.Drawing.Size(323, 20)
        Me.TextBoxID.TabIndex = 1
        '
        'GroupBoxGeneral
        '
        Me.GroupBoxGeneral.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.GroupBoxGeneral.Controls.Add(Me.TextBoxName)
        Me.GroupBoxGeneral.Controls.Add(Me.Label12)
        Me.GroupBoxGeneral.Controls.Add(Me.CheckBoxPassword)
        Me.GroupBoxGeneral.Controls.Add(Me.CheckBoxExpiryDate)
        Me.GroupBoxGeneral.Controls.Add(Me.DateTimePickerExpiryDate)
        Me.GroupBoxGeneral.Controls.Add(Me.TextBoxPassword)
        Me.GroupBoxGeneral.Controls.Add(Me.Label8)
        Me.GroupBoxGeneral.Controls.Add(Me.Label7)
        Me.GroupBoxGeneral.Controls.Add(Me.TextBoxDownloadUrl)
        Me.GroupBoxGeneral.Controls.Add(Me.Label3)
        Me.GroupBoxGeneral.Controls.Add(Me.TextBoxWebUrl)
        Me.GroupBoxGeneral.Controls.Add(Me.Label2)
        Me.GroupBoxGeneral.Controls.Add(Me.TextBoxID)
        Me.GroupBoxGeneral.Controls.Add(Me.Label1)
        Me.GroupBoxGeneral.Location = New System.Drawing.Point(12, 12)
        Me.GroupBoxGeneral.Name = "GroupBoxGeneral"
        Me.GroupBoxGeneral.Size = New System.Drawing.Size(430, 177)
        Me.GroupBoxGeneral.TabIndex = 0
        Me.GroupBoxGeneral.TabStop = False
        Me.GroupBoxGeneral.Text = "Allgemeine Einstellungen"
        '
        'TextBoxName
        '
        Me.TextBoxName.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.TextBoxName.Location = New System.Drawing.Point(101, 45)
        Me.TextBoxName.Name = "TextBoxName"
        Me.TextBoxName.Size = New System.Drawing.Size(323, 20)
        Me.TextBoxName.TabIndex = 30
        '
        'Label12
        '
        Me.Label12.AutoSize = True
        Me.Label12.Location = New System.Drawing.Point(6, 48)
        Me.Label12.Name = "Label12"
        Me.Label12.Size = New System.Drawing.Size(35, 13)
        Me.Label12.TabIndex = 31
        Me.Label12.Text = "Name"
        '
        'CheckBoxPassword
        '
        Me.CheckBoxPassword.AutoSize = True
        Me.CheckBoxPassword.Location = New System.Drawing.Point(101, 152)
        Me.CheckBoxPassword.Name = "CheckBoxPassword"
        Me.CheckBoxPassword.Size = New System.Drawing.Size(15, 14)
        Me.CheckBoxPassword.TabIndex = 6
        Me.CheckBoxPassword.UseVisualStyleBackColor = True
        '
        'CheckBoxExpiryDate
        '
        Me.CheckBoxExpiryDate.AutoSize = True
        Me.CheckBoxExpiryDate.Location = New System.Drawing.Point(101, 126)
        Me.CheckBoxExpiryDate.Name = "CheckBoxExpiryDate"
        Me.CheckBoxExpiryDate.Size = New System.Drawing.Size(15, 14)
        Me.CheckBoxExpiryDate.TabIndex = 4
        Me.CheckBoxExpiryDate.UseVisualStyleBackColor = True
        '
        'DateTimePickerExpiryDate
        '
        Me.DateTimePickerExpiryDate.Location = New System.Drawing.Point(122, 123)
        Me.DateTimePickerExpiryDate.Name = "DateTimePickerExpiryDate"
        Me.DateTimePickerExpiryDate.Size = New System.Drawing.Size(200, 20)
        Me.DateTimePickerExpiryDate.TabIndex = 5
        '
        'TextBoxPassword
        '
        Me.TextBoxPassword.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.TextBoxPassword.Location = New System.Drawing.Point(122, 149)
        Me.TextBoxPassword.Name = "TextBoxPassword"
        Me.TextBoxPassword.Size = New System.Drawing.Size(302, 20)
        Me.TextBoxPassword.TabIndex = 7
        '
        'Label8
        '
        Me.Label8.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.Label8.AutoSize = True
        Me.Label8.Location = New System.Drawing.Point(6, 152)
        Me.Label8.Name = "Label8"
        Me.Label8.Size = New System.Drawing.Size(50, 13)
        Me.Label8.TabIndex = 29
        Me.Label8.Text = "Passwort"
        '
        'Label7
        '
        Me.Label7.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.Label7.AutoSize = True
        Me.Label7.Location = New System.Drawing.Point(6, 126)
        Me.Label7.Name = "Label7"
        Me.Label7.Size = New System.Drawing.Size(71, 13)
        Me.Label7.TabIndex = 27
        Me.Label7.Text = "Ablauf-Datum"
        '
        'TextBoxDownloadUrl
        '
        Me.TextBoxDownloadUrl.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.TextBoxDownloadUrl.Location = New System.Drawing.Point(101, 97)
        Me.TextBoxDownloadUrl.Name = "TextBoxDownloadUrl"
        Me.TextBoxDownloadUrl.ReadOnly = True
        Me.TextBoxDownloadUrl.Size = New System.Drawing.Size(323, 20)
        Me.TextBoxDownloadUrl.TabIndex = 3
        '
        'Label3
        '
        Me.Label3.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.Label3.AutoSize = True
        Me.Label3.Location = New System.Drawing.Point(6, 100)
        Me.Label3.Name = "Label3"
        Me.Label3.Size = New System.Drawing.Size(78, 13)
        Me.Label3.TabIndex = 25
        Me.Label3.Text = "Download-Link"
        '
        'TextBoxWebUrl
        '
        Me.TextBoxWebUrl.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.TextBoxWebUrl.Location = New System.Drawing.Point(101, 71)
        Me.TextBoxWebUrl.Name = "TextBoxWebUrl"
        Me.TextBoxWebUrl.ReadOnly = True
        Me.TextBoxWebUrl.Size = New System.Drawing.Size(323, 20)
        Me.TextBoxWebUrl.TabIndex = 2
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Location = New System.Drawing.Point(6, 74)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(53, 13)
        Me.Label2.TabIndex = 23
        Me.Label2.Text = "Web-Link"
        '
        'GroupBoxExtended
        '
        Me.GroupBoxExtended.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.GroupBoxExtended.Controls.Add(Me.CheckBoxMaxViews)
        Me.GroupBoxExtended.Controls.Add(Me.TextBoxMaxViews)
        Me.GroupBoxExtended.Controls.Add(Me.Label14)
        Me.GroupBoxExtended.Controls.Add(Me.CheckBoxMaxBytes)
        Me.GroupBoxExtended.Controls.Add(Me.CheckBoxMaxUploads)
        Me.GroupBoxExtended.Controls.Add(Me.CheckBoxMaxDownloads)
        Me.GroupBoxExtended.Controls.Add(Me.TextBoxMaxBytes)
        Me.GroupBoxExtended.Controls.Add(Me.Label6)
        Me.GroupBoxExtended.Controls.Add(Me.TextBoxMaxUploads)
        Me.GroupBoxExtended.Controls.Add(Me.Label5)
        Me.GroupBoxExtended.Controls.Add(Me.TextBoxMaxDownloads)
        Me.GroupBoxExtended.Controls.Add(Me.Label4)
        Me.GroupBoxExtended.Location = New System.Drawing.Point(12, 195)
        Me.GroupBoxExtended.Name = "GroupBoxExtended"
        Me.GroupBoxExtended.Size = New System.Drawing.Size(267, 127)
        Me.GroupBoxExtended.TabIndex = 20
        Me.GroupBoxExtended.TabStop = False
        Me.GroupBoxExtended.Text = "Limits"
        '
        'CheckBoxMaxViews
        '
        Me.CheckBoxMaxViews.AutoSize = True
        Me.CheckBoxMaxViews.Location = New System.Drawing.Point(101, 22)
        Me.CheckBoxMaxViews.Name = "CheckBoxMaxViews"
        Me.CheckBoxMaxViews.Size = New System.Drawing.Size(15, 14)
        Me.CheckBoxMaxViews.TabIndex = 21
        Me.CheckBoxMaxViews.UseVisualStyleBackColor = True
        '
        'TextBoxMaxViews
        '
        Me.TextBoxMaxViews.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.TextBoxMaxViews.Location = New System.Drawing.Point(122, 19)
        Me.TextBoxMaxViews.Name = "TextBoxMaxViews"
        Me.TextBoxMaxViews.Size = New System.Drawing.Size(139, 20)
        Me.TextBoxMaxViews.TabIndex = 22
        '
        'Label14
        '
        Me.Label14.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.Label14.AutoSize = True
        Me.Label14.Location = New System.Drawing.Point(6, 22)
        Me.Label14.Name = "Label14"
        Me.Label14.Size = New System.Drawing.Size(61, 13)
        Me.Label14.TabIndex = 32
        Me.Label14.Text = "Max. Views"
        '
        'CheckBoxMaxBytes
        '
        Me.CheckBoxMaxBytes.AutoSize = True
        Me.CheckBoxMaxBytes.Location = New System.Drawing.Point(101, 100)
        Me.CheckBoxMaxBytes.Name = "CheckBoxMaxBytes"
        Me.CheckBoxMaxBytes.Size = New System.Drawing.Size(15, 14)
        Me.CheckBoxMaxBytes.TabIndex = 27
        Me.CheckBoxMaxBytes.UseVisualStyleBackColor = True
        '
        'CheckBoxMaxUploads
        '
        Me.CheckBoxMaxUploads.AutoSize = True
        Me.CheckBoxMaxUploads.Location = New System.Drawing.Point(101, 73)
        Me.CheckBoxMaxUploads.Name = "CheckBoxMaxUploads"
        Me.CheckBoxMaxUploads.Size = New System.Drawing.Size(15, 14)
        Me.CheckBoxMaxUploads.TabIndex = 25
        Me.CheckBoxMaxUploads.UseVisualStyleBackColor = True
        '
        'CheckBoxMaxDownloads
        '
        Me.CheckBoxMaxDownloads.AutoSize = True
        Me.CheckBoxMaxDownloads.Location = New System.Drawing.Point(101, 48)
        Me.CheckBoxMaxDownloads.Name = "CheckBoxMaxDownloads"
        Me.CheckBoxMaxDownloads.Size = New System.Drawing.Size(15, 14)
        Me.CheckBoxMaxDownloads.TabIndex = 23
        Me.CheckBoxMaxDownloads.UseVisualStyleBackColor = True
        '
        'TextBoxMaxBytes
        '
        Me.TextBoxMaxBytes.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.TextBoxMaxBytes.Location = New System.Drawing.Point(122, 97)
        Me.TextBoxMaxBytes.Name = "TextBoxMaxBytes"
        Me.TextBoxMaxBytes.Size = New System.Drawing.Size(139, 20)
        Me.TextBoxMaxBytes.TabIndex = 28
        '
        'Label6
        '
        Me.Label6.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.Label6.AutoSize = True
        Me.Label6.Location = New System.Drawing.Point(6, 100)
        Me.Label6.Name = "Label6"
        Me.Label6.Size = New System.Drawing.Size(59, 13)
        Me.Label6.TabIndex = 29
        Me.Label6.Text = "Max. Bytes"
        '
        'TextBoxMaxUploads
        '
        Me.TextBoxMaxUploads.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.TextBoxMaxUploads.Location = New System.Drawing.Point(122, 71)
        Me.TextBoxMaxUploads.Name = "TextBoxMaxUploads"
        Me.TextBoxMaxUploads.Size = New System.Drawing.Size(139, 20)
        Me.TextBoxMaxUploads.TabIndex = 26
        '
        'Label5
        '
        Me.Label5.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.Label5.AutoSize = True
        Me.Label5.Location = New System.Drawing.Point(6, 74)
        Me.Label5.Name = "Label5"
        Me.Label5.Size = New System.Drawing.Size(72, 13)
        Me.Label5.TabIndex = 27
        Me.Label5.Text = "Max. Uploads"
        '
        'TextBoxMaxDownloads
        '
        Me.TextBoxMaxDownloads.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.TextBoxMaxDownloads.Location = New System.Drawing.Point(122, 45)
        Me.TextBoxMaxDownloads.Name = "TextBoxMaxDownloads"
        Me.TextBoxMaxDownloads.Size = New System.Drawing.Size(139, 20)
        Me.TextBoxMaxDownloads.TabIndex = 24
        '
        'Label4
        '
        Me.Label4.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.Label4.AutoSize = True
        Me.Label4.Location = New System.Drawing.Point(6, 48)
        Me.Label4.Name = "Label4"
        Me.Label4.Size = New System.Drawing.Size(86, 13)
        Me.Label4.TabIndex = 25
        Me.Label4.Text = "Max. Downloads"
        '
        'GroupBox1
        '
        Me.GroupBox1.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.GroupBox1.Controls.Add(Me.TextBoxNumberOfViews)
        Me.GroupBox1.Controls.Add(Me.Label13)
        Me.GroupBox1.Controls.Add(Me.TextBoxNumberOfBytes)
        Me.GroupBox1.Controls.Add(Me.Label9)
        Me.GroupBox1.Controls.Add(Me.TextBoxNumberOfUploads)
        Me.GroupBox1.Controls.Add(Me.Label10)
        Me.GroupBox1.Controls.Add(Me.TextBoxNumberOfDownloads)
        Me.GroupBox1.Controls.Add(Me.Label11)
        Me.GroupBox1.Location = New System.Drawing.Point(289, 195)
        Me.GroupBox1.Name = "GroupBox1"
        Me.GroupBox1.Size = New System.Drawing.Size(267, 127)
        Me.GroupBox1.TabIndex = 30
        Me.GroupBox1.TabStop = False
        Me.GroupBox1.Text = "Statistiken"
        '
        'TextBoxNumberOfViews
        '
        Me.TextBoxNumberOfViews.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.TextBoxNumberOfViews.Location = New System.Drawing.Point(101, 19)
        Me.TextBoxNumberOfViews.Name = "TextBoxNumberOfViews"
        Me.TextBoxNumberOfViews.ReadOnly = True
        Me.TextBoxNumberOfViews.Size = New System.Drawing.Size(160, 20)
        Me.TextBoxNumberOfViews.TabIndex = 31
        '
        'Label13
        '
        Me.Label13.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.Label13.AutoSize = True
        Me.Label13.Location = New System.Drawing.Point(6, 22)
        Me.Label13.Name = "Label13"
        Me.Label13.Size = New System.Drawing.Size(70, 13)
        Me.Label13.TabIndex = 34
        Me.Label13.Text = "Anzahl Views"
        '
        'TextBoxNumberOfBytes
        '
        Me.TextBoxNumberOfBytes.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.TextBoxNumberOfBytes.Location = New System.Drawing.Point(101, 97)
        Me.TextBoxNumberOfBytes.Name = "TextBoxNumberOfBytes"
        Me.TextBoxNumberOfBytes.ReadOnly = True
        Me.TextBoxNumberOfBytes.Size = New System.Drawing.Size(160, 20)
        Me.TextBoxNumberOfBytes.TabIndex = 34
        '
        'Label9
        '
        Me.Label9.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.Label9.AutoSize = True
        Me.Label9.Location = New System.Drawing.Point(6, 100)
        Me.Label9.Name = "Label9"
        Me.Label9.Size = New System.Drawing.Size(68, 13)
        Me.Label9.TabIndex = 29
        Me.Label9.Text = "Anzahl Bytes"
        '
        'TextBoxNumberOfUploads
        '
        Me.TextBoxNumberOfUploads.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.TextBoxNumberOfUploads.Location = New System.Drawing.Point(101, 71)
        Me.TextBoxNumberOfUploads.Name = "TextBoxNumberOfUploads"
        Me.TextBoxNumberOfUploads.ReadOnly = True
        Me.TextBoxNumberOfUploads.Size = New System.Drawing.Size(160, 20)
        Me.TextBoxNumberOfUploads.TabIndex = 33
        '
        'Label10
        '
        Me.Label10.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.Label10.AutoSize = True
        Me.Label10.Location = New System.Drawing.Point(6, 74)
        Me.Label10.Name = "Label10"
        Me.Label10.Size = New System.Drawing.Size(81, 13)
        Me.Label10.TabIndex = 27
        Me.Label10.Text = "Anzahl Uploads"
        '
        'TextBoxNumberOfDownloads
        '
        Me.TextBoxNumberOfDownloads.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.TextBoxNumberOfDownloads.Location = New System.Drawing.Point(101, 45)
        Me.TextBoxNumberOfDownloads.Name = "TextBoxNumberOfDownloads"
        Me.TextBoxNumberOfDownloads.ReadOnly = True
        Me.TextBoxNumberOfDownloads.Size = New System.Drawing.Size(160, 20)
        Me.TextBoxNumberOfDownloads.TabIndex = 32
        '
        'Label11
        '
        Me.Label11.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.Label11.AutoSize = True
        Me.Label11.Location = New System.Drawing.Point(6, 48)
        Me.Label11.Name = "Label11"
        Me.Label11.Size = New System.Drawing.Size(95, 13)
        Me.Label11.TabIndex = 25
        Me.Label11.Text = "Anzahl Downloads"
        '
        'DmsLinkShareSetup
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(568, 363)
        Me.Controls.Add(Me.GroupBox1)
        Me.Controls.Add(Me.GroupBoxExtended)
        Me.Controls.Add(Me.GroupBoxGeneral)
        Me.Controls.Add(Me.ButtonCancel)
        Me.Controls.Add(Me.ButtonSave)
        Me.Controls.Add(Me.GroupBoxAuthorizations)
        Me.Icon = CType(resources.GetObject("$this.Icon"), System.Drawing.Icon)
        Me.Name = "DmsLinkShareSetup"
        Me.Text = "DMS Externe Links Einstellungen"
        Me.GroupBoxAuthorizations.ResumeLayout(False)
        Me.GroupBoxAuthorizations.PerformLayout()
        Me.GroupBoxGeneral.ResumeLayout(False)
        Me.GroupBoxGeneral.PerformLayout()
        Me.GroupBoxExtended.ResumeLayout(False)
        Me.GroupBoxExtended.PerformLayout()
        Me.GroupBox1.ResumeLayout(False)
        Me.GroupBox1.PerformLayout()
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
    Friend WithEvents Label1 As Label
    Friend WithEvents TextBoxID As TextBox
    Friend WithEvents GroupBoxGeneral As GroupBox
    Friend WithEvents TextBoxDownloadUrl As TextBox
    Friend WithEvents Label3 As Label
    Friend WithEvents TextBoxWebUrl As TextBox
    Friend WithEvents Label2 As Label
    Friend WithEvents GroupBoxExtended As GroupBox
    Friend WithEvents TextBoxMaxBytes As TextBox
    Friend WithEvents Label6 As Label
    Friend WithEvents TextBoxMaxUploads As TextBox
    Friend WithEvents Label5 As Label
    Friend WithEvents TextBoxMaxDownloads As TextBox
    Friend WithEvents Label4 As Label
    Friend WithEvents DateTimePickerExpiryDate As DateTimePicker
    Friend WithEvents TextBoxPassword As TextBox
    Friend WithEvents Label8 As Label
    Friend WithEvents Label7 As Label
    Friend WithEvents GroupBox1 As GroupBox
    Friend WithEvents TextBoxNumberOfBytes As TextBox
    Friend WithEvents Label9 As Label
    Friend WithEvents TextBoxNumberOfUploads As TextBox
    Friend WithEvents Label10 As Label
    Friend WithEvents TextBoxNumberOfDownloads As TextBox
    Friend WithEvents Label11 As Label
    Friend WithEvents CheckBoxPassword As CheckBox
    Friend WithEvents CheckBoxExpiryDate As CheckBox
    Friend WithEvents CheckBoxMaxBytes As CheckBox
    Friend WithEvents CheckBoxMaxUploads As CheckBox
    Friend WithEvents CheckBoxMaxDownloads As CheckBox
    Friend WithEvents TextBoxName As TextBox
    Friend WithEvents Label12 As Label
    Friend WithEvents CheckBoxAllowShare As CheckBox
    Friend WithEvents CheckBoxMaxViews As CheckBox
    Friend WithEvents TextBoxMaxViews As TextBox
    Friend WithEvents Label14 As Label
    Friend WithEvents TextBoxNumberOfViews As TextBox
    Friend WithEvents Label13 As Label
End Class
