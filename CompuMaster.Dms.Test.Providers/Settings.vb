Imports System.Text
Imports NUnit.Framework

<TestFixture> Public NotInheritable Class Settings

    Private Const AppTitle As String = "Scopevisio.Teamwork.Test"

    <Test, Explicit("Run only to persist login credentials on dev workstation")>
    Public Sub PersistInputValue()
        Dim username As String = Settings.InputLine("username")
        Dim customerno As String = Settings.InputLine("customer no.")
        Dim password As String = Settings.InputLine("password")

        System.Console.WriteLine("Environment TEST_USERNAME=" & System.Environment.GetEnvironmentVariable("TEST_USERNAME"))
        System.Console.WriteLine("Environment written to disk for future use at local dev workstation:")
        System.Console.WriteLine("- ClientNumber=" & customerno)
        System.Console.WriteLine("- Username=" & username)

        If password <> "" Then
            System.Console.WriteLine("- Password=********************")
        Else
            System.Console.WriteLine("- Password=")
        End If

        Assert.NotNull(username, "User credentials not found in environment or buffer files (run Sample app for creating buffer files in temp directory!)")
        Assert.NotNull(customerno, "User credentials not found in environment or buffer files (run Sample app for creating buffer files in temp directory!)")
        Assert.NotNull(password, "User credentials not found in environment or buffer files (run Sample app for creating buffer files in temp directory!)")

    End Sub

    Public Shared Function InputLine(ByVal fieldName As String) As String
        Dim BufferFile As String = BufferFilePath(fieldName)
        Dim EnvVarName As String = "TEST_" & fieldName.Replace(" ", "").Replace(".", "").ToUpperInvariant()

        If Not String.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable(EnvVarName)) Then

            If System.Environment.MachineName.StartsWith("WKS") Then
                PersistInputValue(fieldName, System.Environment.GetEnvironmentVariable(EnvVarName))
            End If

            Return System.Environment.GetEnvironmentVariable(EnvVarName)
        End If

        Dim DefaultValue As String

        If System.IO.File.Exists(BufferFile) Then
            DefaultValue = System.IO.File.ReadAllText(BufferFile)
        Else
            DefaultValue = ""
        End If

        If Not String.IsNullOrWhiteSpace(DefaultValue) Then Return DefaultValue
        Throw New InvalidOperationException("Missing persisted input for field """ & fieldName & """, use environment variable " & EnvVarName & " or write to disk by code with method PersistInputValue()" & vbCrLf &
                                            "Ex. run following customized batch to create local temp-files-cache for credentials (works on WKSxxxx workstations only):" & vbCrLf &
                                            "@echo off" & vbCrLf &
                                            "SET TEST_USERNAME=xy@abc.login" & vbCrLf &
                                            "SET TEST_CUSTOMERNO=1234567" & vbCrLf &
                                            "SET TEST_PASSWORD=xxxxxxx(encode with leading ^-char )" & vbCrLf &
                                            "dotnet test --filter ""FullyQualifiedName=" & GetType(Settings).FullName & "." & NameOf(PersistInputValue) & """ --framework net5.0")
    End Function

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

    Private Shared Sub PersistInputValue(ByVal fieldName As String, ByVal value As String)
        System.IO.File.WriteAllText(BufferFilePath(fieldName), value)
    End Sub

    Private Shared Function InputFromBufferFile(ByVal fieldName As String) As String
        Dim BufferFile As String = BufferFilePath(fieldName)

        If System.IO.File.Exists(BufferFile) Then
            Return System.IO.File.ReadAllText(BufferFile)
        Else
            Return Nothing
        End If
    End Function

    Private Shared Sub InputToBufferFile(ByVal fieldName As String, ByVal value As String)
        System.IO.File.WriteAllText(BufferFilePath(fieldName), value)
    End Sub

End Class