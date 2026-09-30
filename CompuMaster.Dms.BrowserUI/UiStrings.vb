Imports System.Globalization
Imports System.Reflection
Imports System.Resources

Friend NotInheritable Class UiStrings

    Private Shared ReadOnly Resources As New ResourceManager("CompuMaster.Dms.BrowserUI.UiStrings", GetType(UiStrings).Assembly)

    Private Sub New()
    End Sub

    Friend Shared Function GetText(name As String) As String
        Return Resources.GetString(name, CultureInfo.CurrentUICulture)
    End Function

    Friend Shared Function Format(name As String, ParamArray arguments As Object()) As String
        Return String.Format(CultureInfo.CurrentCulture, GetText(name), arguments)
    End Function

End Class
