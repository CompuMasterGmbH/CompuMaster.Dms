Imports System.Text
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture> Public NotInheritable Class ScopevisioTeamworkSettings
    Inherits SettingsBase

    Public Overrides ReadOnly Property AppTitleInBufferFile As String
        Get
            Return "Scopevisio.Teamwork.Test"
        End Get
    End Property

    Public Overrides ReadOnly Property AppTitleInEnvironmentVariable As String
        Get
            Return "ScopevisioTeamwork".ToUpperInvariant
        End Get
    End Property

    <Test, Explicit("Run only to persist login credentials on dev workstation")>
    Public Overrides Sub PersistInputValue()
        Dim username As String = InputLine("username")
        Dim customerno As String = InputLine("customer no.")
        Dim password As String = InputLine("password")

        System.Console.WriteLine(Me.AppTitleInBufferFile & " Environment " & EnvironmentVariable("USERNAME") & "=" & System.Environment.GetEnvironmentVariable(EnvironmentVariable("USERNAME")))
        System.Console.WriteLine("Environment written to disk for future use at local dev workstation:")
        System.Console.WriteLine("- ClientNumber=" & customerno)
        System.Console.WriteLine("- Username=" & username)

        If password <> "" Then
            System.Console.WriteLine("- Password=********************")
        Else
            System.Console.WriteLine("- Password=")
        End If

        ClassicAssert.NotNull(username, "User credentials not found in environment or buffer files (run Sample app for creating buffer files in temp directory!)")
        ClassicAssert.NotNull(customerno, "User credentials not found in environment or buffer files (run Sample app for creating buffer files in temp directory!)")
        ClassicAssert.NotNull(password, "User credentials not found in environment or buffer files (run Sample app for creating buffer files in temp directory!)")
    End Sub

    Public Overrides Function PersitingScriptForRequiredEnvironmentVariables() As String
        Return "@echo off" & vbCrLf &
                "SET " & EnvironmentVariable("USERNAME") & "=xy@abc.login" & vbCrLf &
                "SET " & EnvironmentVariable("CUSTOMERNO") & "=1234567" & vbCrLf &
                "SET " & EnvironmentVariable("PASSWORD") & "=xxxxxxx(encode with leading ^-char )" & vbCrLf &
                "dotnet test --filter ""FullyQualifiedName=" & Me.GetType.FullName & "." & NameOf(PersistInputValue) & """ --framework net8.0"
    End Function
End Class