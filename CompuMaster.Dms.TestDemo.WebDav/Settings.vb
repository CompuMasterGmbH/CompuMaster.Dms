Public NotInheritable Class Settings

#If OWNCLOUD_CLASSIC_DEMO Then
    Private Const AppTitle As String = "OwnCloudWebDav.Test"
    Private Const EnvironmentPrefix As String = "CMOWNCLOUD"
    Public Const DemoTitle As String = "DMS Browser DEMO for ownCloud Classic"
#ElseIf NEXTCLOUD_DEMO Then
    Private Const AppTitle As String = "NextcloudWebDav.Test"
    Private Const EnvironmentPrefix As String = "CMNEXTCLOUD"
    Public Const DemoTitle As String = "DMS Browser DEMO for Nextcloud"
#Else
    Private Const AppTitle As String = "WebDav.Test"
    Private Const EnvironmentPrefix As String = "WEBDAV"
    Public Const DemoTitle As String = "DMS Browser DEMO for generic WebDAV"
#End If

    Private Const UsernameField As String = "username"
    Private Const PasswordField As String = "password"
    Private Const ServerUrlField As String = "server url"

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

        Dim EnvVarName As String = "TEST_" & EnvironmentPrefix & "_" & fieldName.Replace(" ", "").Replace(".", "").ToUpperInvariant()
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

    Public Shared Function Username() As String
        Return InputFromBufferFile(UsernameField)
    End Function

    Public Shared Function Password() As String
        Return InputFromBufferFile(PasswordField)
    End Function

    Public Shared Function ServerUrl() As String
        Return InputFromBufferFile(ServerUrlField)
    End Function

    Public Shared Function HasPersistedCredentials() As Boolean
        Return IsBufferedByFile(UsernameField) OrElse IsBufferedByFile(PasswordField) OrElse IsBufferedByFile(ServerUrlField)
    End Function

    Public Shared Sub PersistCredentials(username As String, password As String, serverUrl As String)
        PersistInputValue(UsernameField, username)
        PersistInputValue(PasswordField, password)
        PersistInputValue(ServerUrlField, serverUrl)
    End Sub

    Public Shared Sub RemovePersistedCredentials()
        RemoveBufferFile(UsernameField)
        RemoveBufferFile(PasswordField)
        RemoveBufferFile(ServerUrlField)
    End Sub

    Public Shared Function ResolveServerUrl(serverUrl As String, username As String) As String
#If NEXTCLOUD_DEMO Then
        Dim ParsedServerUrl As Uri = Nothing
        If Not Uri.TryCreate(serverUrl, UriKind.Absolute, ParsedServerUrl) OrElse
           (ParsedServerUrl.Scheme <> Uri.UriSchemeHttp AndAlso ParsedServerUrl.Scheme <> Uri.UriSchemeHttps) Then
            Throw New InvalidOperationException(DemoStrings.GetText("InvalidNextcloudServerUrl"))
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
#Else
        Return serverUrl
#End If
    End Function

End Class
