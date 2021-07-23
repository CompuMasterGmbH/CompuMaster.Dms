Option Explicit On
Option Strict On

Imports System.Data
Imports System.Windows.Forms
Imports System.Drawing
Imports CompuMaster.VisualBasicCompatibility.Information
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Partial Friend Module Tools

    Private Function ApplicationTitle() As String
        Try
            Dim entryAssembly = System.Reflection.Assembly.GetEntryAssembly()
            Dim Result As String = (CType(entryAssembly.GetCustomAttributes(GetType(System.Reflection.AssemblyTitleAttribute), False)(0), System.Reflection.AssemblyTitleAttribute)).Title
            If String.IsNullOrWhiteSpace(Result) Then
                Result = entryAssembly.GetName().Name
            End If
            Return Result
        Catch
            Return "DMS Browser"
        End Try
    End Function

    ''' <summary>
    ''' Show an input box dialog
    ''' </summary>
    ''' <param name="prompt"></param>
    ''' <param name="title"></param>
    ''' <param name="defaultResponse"></param>
    ''' <param name="xPos">Ignored value</param>
    ''' <param name="yPos">Ignored value</param>
    ''' <returns></returns>
    Public Function InputBox(prompt As String, Optional title As String = "", Optional defaultResponse As String = "", Optional xPos As Integer = -1, Optional yPos As Integer = -1) As String
        Dim localInputText As String = defaultResponse
        If title = "" Then title = ApplicationTitle()

        If InputQuery(title, prompt, localInputText) Then
            Return localInputText
        Else
            Return ""
        End If
    End Function

    Private Function InputQuery(ByVal caption As String, ByVal prompt As String, ByRef value As String) As Boolean
        Dim form As Form
        form = New Form()
        form.AutoScaleMode = AutoScaleMode.Font
        form.Font = SystemFonts.IconTitleFont
        Dim dialogUnits As SizeF
        dialogUnits = form.AutoScaleDimensions
        form.FormBorderStyle = FormBorderStyle.FixedDialog
        form.MinimizeBox = False
        form.MaximizeBox = False
        form.Text = caption
        form.ClientSize = New Size(MulDiv(180, dialogUnits.Width, 4), MulDiv(63, dialogUnits.Height, 8))
        form.StartPosition = FormStartPosition.CenterScreen
        Dim lblPrompt As System.Windows.Forms.Label
        lblPrompt = New System.Windows.Forms.Label()
        lblPrompt.Parent = form
        lblPrompt.AutoSize = True
        lblPrompt.Left = MulDiv(8, dialogUnits.Width, 4)
        lblPrompt.Top = MulDiv(8, dialogUnits.Height, 8)
        lblPrompt.Text = prompt
        Dim edInput As System.Windows.Forms.TextBox
        edInput = New System.Windows.Forms.TextBox()
        edInput.Parent = form
        edInput.Left = lblPrompt.Left
        edInput.Top = MulDiv(19, dialogUnits.Height, 8)
        edInput.Width = MulDiv(164, dialogUnits.Width, 4)
        edInput.Text = value
        edInput.SelectAll()
        Dim buttonTop As Integer = MulDiv(41, dialogUnits.Height, 8)
        Dim buttonSize As Size = New Size(MulDiv(50, CInt(dialogUnits.Width), 4), MulDiv(14, CInt(dialogUnits.Height), 8))
        Dim bbOk As System.Windows.Forms.Button = New System.Windows.Forms.Button With {
            .Parent = form,
            .Text = "OK",
            .DialogResult = DialogResult.OK
        }
        form.AcceptButton = bbOk
        bbOk.Location = New Point(MulDiv(38, dialogUnits.Width, 4), buttonTop)
        bbOk.Size = buttonSize
        Dim bbCancel As New System.Windows.Forms.Button()
        bbCancel.Parent = form
        bbCancel.Text = "Cancel"
        bbCancel.DialogResult = DialogResult.Cancel
        form.CancelButton = bbCancel
        bbCancel.Location = New Point(MulDiv(92, dialogUnits.Width, 4), buttonTop)
        bbCancel.Size = buttonSize

        If form.ShowDialog() = DialogResult.OK Then
            value = edInput.Text
            Return True
        Else
            Return False
        End If
    End Function

    Private Function MulDiv(ByVal nNumber As Single, ByVal nNumerator As Single, ByVal nDenominator As Integer) As Integer
        Return CInt(Math.Round(nNumber * nNumerator / nDenominator))
    End Function

    Private Function MulDiv(ByVal nNumber As Integer, ByVal nNumerator As Single, ByVal nDenominator As Integer) As Integer
        Return CInt(Math.Round(CSng(nNumber) * nNumerator / nDenominator))
    End Function

    Private Function MulDiv(ByVal nNumber As Integer, ByVal nNumerator As Integer, ByVal nDenominator As Integer) As Integer
        Return CInt(Math.Round(CSng(nNumber) * nNumerator / nDenominator))
    End Function

End Module