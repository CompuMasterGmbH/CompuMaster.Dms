Option Explicit On
Option Strict On

Imports System.Data
Imports System.Windows.Forms
Imports System.Drawing
Imports CompuMaster.VisualBasicCompatibility.Information
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Partial Friend Class UITools

    Private Shared Function ApplicationTitle() As String
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
    Public Shared Function InputBox(prompt As String, Optional title As String = "", Optional defaultResponse As String = "", Optional xPos As Integer = -1, Optional yPos As Integer = -1) As String
        Dim localInputText As String = defaultResponse
        If title = "" Then title = ApplicationTitle()

        If InputQuery(title, prompt, localInputText) Then
            Return localInputText
        Else
            Return ""
        End If
    End Function

    ''' <summary>Show an input dialog while distinguishing Cancel from an empty answer.</summary>
    Public Shared Function TryInputBox(prompt As String, title As String, defaultResponse As String, ByRef response As String) As Boolean
        response = defaultResponse
        Return InputQuery(title, prompt, response)
    End Function

    Private Shared Function InputQuery(ByVal caption As String, ByVal prompt As String, ByRef value As String) As Boolean
        Dim input As TextBox = Nothing
        Using form = CreateInputDialog(caption, prompt, value, input)
            If form.ShowDialog() <> DialogResult.OK Then Return False
            value = input.Text
            Return True
        End Using
    End Function

    Friend Shared Function CreateInputDialog(caption As String, prompt As String, value As String, ByRef input As TextBox) As Form
        Dim form As New Form()
        form.AutoScaleMode = AutoScaleMode.Font
        form.Font = SystemFonts.IconTitleFont
        form.FormBorderStyle = FormBorderStyle.FixedDialog
        form.MinimizeBox = False
        form.MaximizeBox = False
        form.Text = caption
        form.ClientSize = New Size(440, 160)
        form.StartPosition = FormStartPosition.CenterScreen
        Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(12), .ColumnCount = 1, .RowCount = 3}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        For row As Integer = 0 To 2
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next
        Dim lblPrompt As New Label With {.Name = "Prompt", .AutoSize = True, .MaximumSize = New Size(410, 0), .Text = prompt, .Margin = New Padding(0, 0, 0, 8)}
        input = New TextBox With {.Name = "Input", .Dock = DockStyle.Top, .Text = value, .Margin = New Padding(0, 0, 0, 12)}
        input.SelectAll()
        Dim buttons As New FlowLayoutPanel With {.AutoSize = True, .Anchor = AnchorStyles.Right, .FlowDirection = FlowDirection.LeftToRight}
        Dim bbOk As System.Windows.Forms.Button = New System.Windows.Forms.Button With {
            .Name = "Accept",
            .AutoSize = True,
            .Text = UiStrings.GetText("ActionOkay"),
            .DialogResult = DialogResult.OK
        }
        form.AcceptButton = bbOk
        Dim bbCancel As New System.Windows.Forms.Button()
        bbCancel.Name = "Cancel"
        bbCancel.AutoSize = True
        bbCancel.Text = UiStrings.GetText("ActionCancel")
        bbCancel.DialogResult = DialogResult.Cancel
        form.CancelButton = bbCancel
        buttons.Controls.Add(bbOk)
        buttons.Controls.Add(bbCancel)
        layout.Controls.Add(lblPrompt)
        layout.Controls.Add(input)
        layout.Controls.Add(buttons)
        form.Controls.Add(layout)
        form.ClientSize = New Size(440, Math.Max(160, lblPrompt.GetPreferredSize(New Size(410, 0)).Height + input.PreferredHeight + buttons.GetPreferredSize(Size.Empty).Height + 50))
        Dim entry = input
        LocalizedLayout.Bind(form,
            Sub()
                lblPrompt.MaximumSize = New Size(Math.Max(1, form.ClientSize.Width - layout.Padding.Horizontal - lblPrompt.Margin.Horizontal), 0)
                Dim spacing = layout.Padding.Vertical + lblPrompt.Margin.Vertical + entry.Margin.Vertical + buttons.Margin.Vertical
                form.ClientSize = New Size(form.ClientSize.Width, Math.Max(160, lblPrompt.GetPreferredSize(lblPrompt.MaximumSize).Height + entry.PreferredHeight + buttons.GetPreferredSize(Size.Empty).Height + spacing))
            End Sub)
        Return form
    End Function

    Private Shared Function MulDiv(ByVal nNumber As Single, ByVal nNumerator As Single, ByVal nDenominator As Integer) As Integer
        Return CInt(Math.Round(nNumber * nNumerator / nDenominator))
    End Function

    Private Shared Function MulDiv(ByVal nNumber As Integer, ByVal nNumerator As Single, ByVal nDenominator As Integer) As Integer
        Return CInt(Math.Round(CSng(nNumber) * nNumerator / nDenominator))
    End Function

    Private Shared Function MulDiv(ByVal nNumber As Integer, ByVal nNumerator As Integer, ByVal nDenominator As Integer) As Integer
        Return CInt(Math.Round(CSng(nNumber) * nNumerator / nDenominator))
    End Function

End Class
