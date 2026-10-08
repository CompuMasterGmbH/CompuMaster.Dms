Imports System.Globalization
Imports System.Resources
Imports System.Windows.Forms

Friend NotInheritable Class DemoStrings
    Private Shared ReadOnly Resources As New ResourceManager(GetType(DemoStrings).Namespace & ".DemoStrings", GetType(DemoStrings).Assembly)

    Private Sub New()
    End Sub

    Friend Shared Function GetText(name As String) As String
        Return Resources.GetString(name, CultureInfo.CurrentUICulture)
    End Function

    Friend Shared Sub ApplyLoginLabels(form As Form)
        Dim names As New Dictionary(Of String, String) From {
            {"UsernameLabel", "UserName"}, {"PasswordLabel", "Password"},
            {"StartPathLabel", "StartPath"}, {"Label3", "StartPath"},
            {"OK", "SignIn"}, {"Cancel", "Exit"},
            {"CheckboxPersistLoginCredentialsToDisk", "PersistCredentials"}}
        Dim customerLogin = form.Controls.Find("CustomerNoTextBox", True).Length <> 0
        names.Add("Label1", If(customerLogin, "CustomerNumber", "ServerUrl"))
        For Each pair In names
            For Each control As Control In form.Controls.Find(pair.Key, True)
                control.Text = GetText(pair.Value)
                If TypeOf control Is Button Then
                    Dim preferred = control.GetPreferredSize(Drawing.Size.Empty)
                    control.Size = New Drawing.Size(Math.Max(control.Width, preferred.Width), Math.Max(control.Height, preferred.Height))
                End If
            Next
        Next
        Dim exitButton = TryCast(form.Controls.Find("Cancel", True).SingleOrDefault(), Button)
        Dim signInButton = TryCast(form.Controls.Find("OK", True).SingleOrDefault(), Button)
        If exitButton IsNot Nothing AndAlso signInButton IsNot Nothing Then
            Dim rightMargin = Math.Max(12, form.ClientSize.Width - exitButton.Right)
            exitButton.Left = form.ClientSize.Width - rightMargin - exitButton.Width
            signInButton.Left = exitButton.Left - 9 - signInButton.Width
        End If
        Dim arranging As Boolean
        Dim arrange As Action =
            Sub()
                If arranging OrElse form.IsDisposed OrElse form.Disposing Then Return
                arranging = True
                Try
                    ArrangeLoginControls(form, customerLogin, signInButton, exitButton)
                Finally
                    arranging = False
                End Try
            End Sub
        AddHandler form.FontChanged, Sub(sender, e) arrange()
        AddHandler form.SizeChanged, Sub(sender, e) arrange()
        AddHandler form.Shown, Sub(sender, e) arrange()
        AddHandler form.Layout, Sub(sender, e) arrange()
        arrange()
    End Sub

    Private Shared Sub ArrangeLoginControls(form As Form, customerLogin As Boolean, signInButton As Button, exitButton As Button)
        For Each button In {signInButton, exitButton}
            Dim preferred = button.GetPreferredSize(Drawing.Size.Empty)
            button.Size = New Drawing.Size(Math.Max(button.Width, preferred.Width), Math.Max(button.Height, preferred.Height))
        Next
        exitButton.Left = form.ClientSize.Width - exitButton.Width - 12
        signInButton.Left = exitButton.Left - signInButton.Width - 9
        Dim pairs As String(,) = {{"Label1", If(customerLogin, "CustomerNoTextBox", "ServerAddress")}, {"UsernameLabel", "UsernameTextBox"}, {"PasswordLabel", "PasswordTextBox"}, {If(customerLogin, "Label3", "StartPathLabel"), "StartPathTextBox"}}
        Dim nextTop As Integer = 7
        For row As Integer = 0 To pairs.GetLength(0) - 1
            Dim label = DirectCast(form.Controls.Find(pairs(row, 0), False).Single(), Label)
            Dim input = form.Controls.Find(pairs(row, 1), False).Single()
            label.AutoSize = True
            label.Size = TextRenderer.MeasureText(label.Text, label.Font)
            label.Top = nextTop
            input.Top = label.Bottom + 4
            nextTop = input.Bottom + 8
        Next
        Dim persist = DirectCast(form.Controls.Find("CheckboxPersistLoginCredentialsToDisk", False).Single(), CheckBox)
        persist.AutoSize = True
        persist.Size = persist.GetPreferredSize(Drawing.Size.Empty)
        Dim persistTop = Math.Max(nextTop, persist.Top)
        Dim buttonTop = Math.Max(persistTop, signInButton.Top)
        If signInButton IsNot Nothing AndAlso persist.Right + 10 > signInButton.Left Then
            persistTop = nextTop
            buttonTop = persistTop + persist.Height + 10
        End If
        form.ClientSize = New Drawing.Size(Math.Max(form.ClientSize.Width, persist.Right + 14), Math.Max(form.ClientSize.Height, buttonTop + signInButton.Height + 12))
        persist.Top = persistTop
        signInButton.Top = buttonTop
        exitButton.Top = buttonTop
    End Sub
End Class
