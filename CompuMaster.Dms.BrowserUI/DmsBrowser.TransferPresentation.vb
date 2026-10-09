Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Partial Public Class DmsBrowser
    Friend Function CreateTransferDialog(files As String(), Optional download As Boolean = False, Optional allowRetry As Boolean = True) As UploadProgressDialog
        Return New UploadProgressDialog(files, download, allowRetry, Me.Icon) With {
            .Text = GetTransferWindowTitle(If(download, "DownloadTitle", "UploadTitle"))
        }
    End Function

    Private Function GetTransferWindowTitle(resourceKey As String) As String
        Return TransferWindowTitle.WithTarget(UiStrings.GetText(resourceKey), Me.DmsProvider, Me.DmsProfile)
    End Function
End Class

Friend NotInheritable Class TransferWindowTitle
    Friend Shared Function WithTarget(title As String, provider As BaseDmsProvider, Optional profile As IDmsLoginProfile = Nothing) As String
        If provider Is Nothing Then Return title
        Dim target = provider.Name
        If provider.WebApiUrlCustomization <> BaseDmsProvider.UrlCustomizationType.WebApiUrlNotCustomizable Then
            'Use the authorized WebDAV endpoint for WebDAV, ownCloud and Nextcloud.
            'Other customizable providers can use their common login-profile address.
            Dim address = TryCast(provider, WebDavDmsProvider)?.CustomWebApiUrl
            If String.IsNullOrWhiteSpace(address) Then address = profile?.ServerAddress
            If String.IsNullOrWhiteSpace(address) Then address = provider.WebApiDefaultUrl
            Dim endpoint As Uri = Nothing
            If Uri.TryCreate(address, UriKind.Absolute, endpoint) AndAlso
               (endpoint.Scheme = Uri.UriSchemeHttp OrElse endpoint.Scheme = Uri.UriSchemeHttps) Then
                Dim displayUrl As New UriBuilder(endpoint) With {.UserName = "", .Password = "", .Query = "", .Fragment = ""}
                target = displayUrl.Uri.AbsoluteUri
            End If
        End If
        Return If(String.IsNullOrWhiteSpace(target), title, title & " — " & target)
    End Function
End Class
