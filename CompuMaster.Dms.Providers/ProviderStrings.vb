Imports System.Globalization
Imports System.Resources

Friend NotInheritable Class ProviderStrings
    Private Shared ReadOnly Resources As New ResourceManager("CompuMaster.Dms.ProviderStrings", GetType(ProviderStrings).Assembly)

    Private Sub New()
    End Sub

    Friend Shared Function GetText(name As String) As String
        Return Resources.GetString(name, CultureInfo.CurrentUICulture)
    End Function

    Friend Shared Function Format(name As String, ParamArray arguments As Object()) As String
        Return String.Format(CultureInfo.CurrentCulture, GetText(name), arguments)
    End Function
End Class
