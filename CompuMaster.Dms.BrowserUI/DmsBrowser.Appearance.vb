Imports System.Windows.Forms

Partial Public Class DmsBrowser
    Private _LightPalette, _DarkPalette As BrowserPalette
    ''' <summary>Gets or sets the optional palette for light appearance.</summary>
    Public Property LightPalette As BrowserPalette
        Get
            Return _LightPalette
        End Get
        Set(value As BrowserPalette)
            _LightPalette = value
            BrowserAppearance.SetPalettes(Me, _LightPalette, _DarkPalette)
        End Set
    End Property
    ''' <summary>Gets or sets the optional palette for dark appearance.</summary>
    Public Property DarkPalette As BrowserPalette
        Get
            Return _DarkPalette
        End Get
        Set(value As BrowserPalette)
            _DarkPalette = value
            BrowserAppearance.SetPalettes(Me, _LightPalette, _DarkPalette)
        End Set
    End Property

    Private Sub ShowPropertiesDialog(details As String, title As String)
        Using dialog As New Form With {.Text = title, .Icon = Me.Icon, .Width = 760, .Height = 600, .StartPosition = FormStartPosition.CenterParent}
            Dim content As New TextBox With {.Name = "PropertiesDetails", .Multiline = True, .ReadOnly = True, .ScrollBars = ScrollBars.Both, .Dock = DockStyle.Fill, .Text = details}
            Dim close As New Button With {.Text = UiStrings.GetText("ActionClose"), .DialogResult = DialogResult.OK, .Dock = DockStyle.Bottom, .AutoSize = True}
            dialog.Controls.Add(content) : dialog.Controls.Add(close)
            dialog.AcceptButton = close : dialog.CancelButton = close
            BrowserAppearance.Bind(dialog)
            dialog.ShowDialog(Me)
        End Using
    End Sub
End Class
