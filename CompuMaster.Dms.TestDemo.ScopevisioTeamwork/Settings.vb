Public NotInheritable Class Settings

    Private Const AppTitle As String = "SampleScopevisioTeamwork"

    Private Shared Function BufferFilePath(ByVal fieldName As String) As String
        Dim HashedFieldName As String

        Using md5 As System.Security.Cryptography.MD5 = System.Security.Cryptography.MD5.Create()
            Dim inputBytes As Byte() = System.Text.Encoding.ASCII.GetBytes(fieldName)
            Dim hashBytes As Byte() = md5.ComputeHash(inputBytes)
            Dim sb As New System.Text.StringBuilder()

            For i As Integer = 0 To hashBytes.Length - 1
                sb.Append(hashBytes(i).ToString("X2"))
            Next

            HashedFieldName = sb.ToString()
        End Using

        Return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "~Buffer." & AppTitle & "." & HashedFieldName & ".tmp")
    End Function

    Public Shared Sub PersistInputValue(ByVal fieldName As String, ByVal value As String)
        System.IO.File.WriteAllText(BufferFilePath(fieldName), value)
    End Sub

    Public Shared Function InputFromBufferFile(ByVal fieldName As String) As String
        Dim BufferFile As String = BufferFilePath(fieldName)

        Dim EnvVarName As String = "TEST_" & fieldName.Replace(" ", "").Replace(".", "").ToUpperInvariant()
        If Not String.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable(EnvVarName)) Then
            Return System.Environment.GetEnvironmentVariable(EnvVarName)
        End If

        If System.IO.File.Exists(BufferFile) Then
            Return System.IO.File.ReadAllText(BufferFile)
        Else
            Return Nothing
        End If
    End Function

    Public Shared Function IsBufferedByFile(fieldName As String) As Boolean
        Return System.IO.File.Exists(BufferFilePath(fieldName))
    End Function

    Public Shared Sub RemoveBufferFile(fieldName As String)
        Dim BufferFile As String = BufferFilePath(fieldName)
        If System.IO.File.Exists(BufferFile) Then
            System.IO.File.Delete(BufferFile)
        End If
    End Sub

End Class
