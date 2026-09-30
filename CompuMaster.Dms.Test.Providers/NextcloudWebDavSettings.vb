Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture> Public NotInheritable Class NextcloudWebDavSettings
    Inherits SettingsBase

    Public Overrides ReadOnly Property AppTitleInBufferFile As String
        Get
            Return "NextcloudWebDav.Test"
        End Get
    End Property

    Public Overrides ReadOnly Property AppTitleInEnvironmentVariable As String
        Get
            Return "CMNEXTCLOUD"
        End Get
    End Property

    Friend Overrides Function NormalizeServerUrl(serverUrl As String, username As String) As String
        Dim ParsedServerUrl As Uri = Nothing
        If Not Uri.TryCreate(serverUrl, UriKind.Absolute, ParsedServerUrl) OrElse
           (ParsedServerUrl.Scheme <> Uri.UriSchemeHttp AndAlso ParsedServerUrl.Scheme <> Uri.UriSchemeHttps) Then
            Throw New InvalidOperationException("The Nextcloud server URL must be an absolute HTTP or HTTPS URL.")
        End If

        Dim ServerUrlBuilder As New UriBuilder(ParsedServerUrl) With {
            .Query = String.Empty,
            .Fragment = String.Empty
        }
        Dim ServerPath As String = ServerUrlBuilder.Path.TrimEnd("/"c)

        If ServerPath.IndexOf("/remote.php/dav/files/", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
           ServerPath.EndsWith("/remote.php/webdav", StringComparison.OrdinalIgnoreCase) Then
            Return serverUrl
        End If

        If ServerPath.EndsWith("/remote.php/dav", StringComparison.OrdinalIgnoreCase) Then
            ServerPath &= "/files/" & Uri.EscapeDataString(username)
        Else
            ServerPath &= "/remote.php/dav/files/" & Uri.EscapeDataString(username)
        End If

        ServerUrlBuilder.Path = ServerPath & "/"
        Return ServerUrlBuilder.Uri.AbsoluteUri
    End Function

    <TestCase("https://cloud.example.com/", "test-user", "https://cloud.example.com/remote.php/dav/files/test-user/")>
    <TestCase("https://cloud.example.com/nextcloud", "test-user", "https://cloud.example.com/nextcloud/remote.php/dav/files/test-user/")>
    <TestCase("https://cloud.example.com/remote.php/dav", "user@example.com", "https://cloud.example.com/remote.php/dav/files/user%40example.com/")>
    <TestCase("https://cloud.example.com/remote.php/dav/files/test-user/", "test-user", "https://cloud.example.com/remote.php/dav/files/test-user/")>
    <TestCase("https://cloud.example.com/remote.php/webdav/", "test-user", "https://cloud.example.com/remote.php/webdav/")>
    Public Sub NormalizesNextcloudWebDavUrl(serverUrl As String, username As String, expectedUrl As String)
        ClassicAssert.AreEqual(expectedUrl, NormalizeServerUrl(serverUrl, username))
    End Sub

    <Test, Explicit("Run only to persist login credentials on dev workstation")>
    Public Overrides Sub PersistInputValue()
        Dim username As String = InputLine("username")
        Dim serverurl As String = InputLine("server url")
        Dim password As String = InputLine("password")

        System.Console.WriteLine(Me.AppTitleInBufferFile & " Environment " & EnvironmentVariable("USERNAME") & "=" & System.Environment.GetEnvironmentVariable(EnvironmentVariable("USERNAME")))
        System.Console.WriteLine("Environment written to disk for future use at local dev workstation:")
        System.Console.WriteLine("- ServerUrl=" & serverurl)
        System.Console.WriteLine("- Username=" & username)

        If password <> "" Then
            System.Console.WriteLine("- Password=********************")
        Else
            System.Console.WriteLine("- Password=")
        End If

        ClassicAssert.NotNull(serverurl, "User credentials not found in environment or buffer files")
        ClassicAssert.NotNull(username, "User credentials not found in environment or buffer files")
        ClassicAssert.NotNull(password, "User credentials not found in environment or buffer files")
    End Sub

    Public Overrides Function PersitingScriptForRequiredEnvironmentVariables() As String
        Return "@echo off" & vbCrLf &
                "SET " & EnvironmentVariable("SERVERURL") & "=https://cloud.server.url/" & vbCrLf &
                "SET " & EnvironmentVariable("USERNAME") & "=xy@abc.login" & vbCrLf &
                "SET " & EnvironmentVariable("PASSWORD") & "=xxxxxxx(encode with leading ^-char )" & vbCrLf &
                "dotnet test --filter ""FullyQualifiedName=" & Me.GetType.FullName & "." & NameOf(PersistInputValue) & """ --framework net8.0"
    End Function
End Class
