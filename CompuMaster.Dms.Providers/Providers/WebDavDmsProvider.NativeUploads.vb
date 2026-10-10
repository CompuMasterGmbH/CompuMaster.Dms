Imports System.IO
Imports System.Threading
Imports CompuMaster.Ocs.Core

Namespace Providers
    Partial Public Class WebDavDmsProvider
        Private NativeUploadFamily As OcsServerFamily
        Private NativeUploadNamespace As Uri
        Private NativeUploadSupport As Boolean?

        Friend Sub ConfigureNativeUploadsForTesting(transport As Global.WebDav.IWebDavClient, baseUrl As String, family As OcsServerFamily)
            Me.WebDavClient = transport
            Me.CustomWebApiUrl = baseUrl
            Me.NativeUploadFamily = family
            Me.NativeUploadNamespace = Nothing
            Me.NativeUploadSupport = Nothing
        End Sub

        Friend Shared Function GetNativeUploadNamespace(baseUrl As String) As Uri
            Dim address As Uri = Nothing
            If Not Uri.TryCreate(baseUrl, UriKind.Absolute, address) OrElse
                (address.Scheme <> "https" AndAlso address.Scheme <> "http") OrElse
                address.UserInfo.Length <> 0 OrElse address.Query.Length <> 0 OrElse address.Fragment.Length <> 0 Then Return Nothing
            'Use the protocol user ID from the authenticated DAV path, never a display/login name.
            Dim match = System.Text.RegularExpressions.Regex.Match(address.AbsolutePath, "^(.*/(?:remote\.php/)?dav/)files/([^/]+)(?:/|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            If Not match.Success Then Return Nothing
            Return New Uri(address.GetLeftPart(UriPartial.Authority) & match.Groups(1).Value & "uploads/" & match.Groups(2).Value & "/")
        End Function

        Private Async Function UploadFileCoreAsync(remoteFilePath As String, input As Stream, modificationTime As DateTime?, cancellationToken As CancellationToken) As Task
            cancellationToken.ThrowIfCancellationRequested()
            Dim remaining As Long? = Nothing
            If input.CanSeek Then remaining = input.Length - input.Position
            If remaining.HasValue AndAlso remaining.Value >= 10L * 1024 * 1024 AndAlso
                (Me.NativeUploadFamily = OcsServerFamily.Nextcloud OrElse Me.NativeUploadFamily = OcsServerFamily.OwnCloudServer) Then
                Dim uploadRoot = GetNativeUploadNamespace(Me.CustomWebApiUrl)
                If uploadRoot IsNot Nothing Then
                    If uploadRoot <> Me.NativeUploadNamespace Then
                        Me.NativeUploadNamespace = uploadRoot
                        Me.NativeUploadSupport = Nothing
                    End If
                    Dim chunks As New DavChunkUploadClient(Me.WebDavClient, uploadRoot)
                    If Not Me.NativeUploadSupport.HasValue Then Me.NativeUploadSupport = Await chunks.IsSupportedAsync(cancellationToken).ConfigureAwait(False)
                    If Me.NativeUploadSupport.Value Then
                        Await chunks.UploadAsync(New Uri(Me.CustomWebApiUrl & remoteFilePath), input, remaining.Value, modificationTime, cancellationToken).ConfigureAwait(False)
                        Return
                    End If
                End If
            End If
            'Generic/unsupported namespaces retain standard PUT. Never replay a failed native write.
            Dim parameters As New Global.WebDav.PutFileParameters With {.CancellationToken = cancellationToken}
            If modificationTime.HasValue AndAlso (Me.NativeUploadFamily = OcsServerFamily.Nextcloud OrElse Me.NativeUploadFamily = OcsServerFamily.OwnCloudServer) Then
                parameters.Headers = New KeyValuePair(Of String, String)() {
                    New KeyValuePair(Of String, String)("X-OC-Mtime", New DateTimeOffset(modificationTime.Value.ToUniversalTime()).ToUnixTimeSeconds().ToString(Globalization.CultureInfo.InvariantCulture))}
            End If
            Dim request = Me.WebDavClient.PutFile(Me.CustomWebApiUrl & remoteFilePath, input, parameters)
            Await request.ConfigureAwait(False)
            Await CheckTaskResultForErrorsAsync(request.Result, Nothing, remoteFilePath, ProviderStrings.GetText("UploadFailed"), ExceptionTypeForItemType.File, cancellationToken).ConfigureAwait(False)
        End Function
    End Class
End Namespace
