Public NotInheritable Class Settings

    Private Const AppTitle As String = "Scopevisio.Teamwork.Test"
    Private Const EnvironmentPrefix As String = "SCOPEVISIOTEAMWORK"

    Friend Shared Function Storage() As DemoInputBuffer
        Return New DemoInputBuffer(System.IO.Path.GetTempPath(), AppTitle, EnvironmentPrefix, AddressOf System.Environment.GetEnvironmentVariable)
    End Function

    Friend Shared Sub PersistLoginInputs(username As String, password As String, customer As String, startPath As String,
                                        organisation As String, expectedTenant As String, invalidOrganisationMessage As String)
        ScopevisioDemoSelection.PersistInputs(Storage(), username, password, customer, startPath, organisation, expectedTenant, invalidOrganisationMessage)
    End Sub

    Public Shared Sub PersistInputValue(ByVal fieldName As String, ByVal value As String)
        Storage().Persist(fieldName, value)
    End Sub

    Public Shared Function InputFromBufferFile(ByVal fieldName As String) As String
        Return Storage().Read(fieldName)
    End Function

    Public Shared Function IsBufferedByFile(fieldName As String) As Boolean
        Return Storage().Exists(fieldName)
    End Function

    Public Shared Sub RemoveBufferFile(fieldName As String)
        Storage().Remove(fieldName)
    End Sub

End Class
