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
    End Sub
End Class
