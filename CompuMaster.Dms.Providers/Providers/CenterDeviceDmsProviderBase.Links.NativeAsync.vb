Option Explicit On
Option Strict On

Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest.Clients.Link
Imports CenterDevice.Rest.Exceptions
Imports CompuMaster.Dms.Data

Namespace Providers
    Partial Public MustInherit Class CenterDeviceDmsProviderBase
        ''' <summary>Retrieves download-link metadata using native asynchronous I/O.</summary>
        ''' <param name="id">The selected link identifier.</param>
        ''' <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        ''' <returns>The download-link metadata.</returns>
        Protected Overridable Function LoadNativeDownloadLinkAsync(id As String, cancellationToken As CancellationToken) As Task(Of CenterDevice.Rest.Clients.Link.Link)
            Return Me.IOClient.GetLinkAsync(id, cancellationToken)
        End Function

        ''' <summary>Retrieves upload-link metadata using native asynchronous I/O.</summary>
        ''' <param name="id">The selected link identifier.</param>
        ''' <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        ''' <returns>The upload-link metadata.</returns>
        Protected Overridable Function LoadNativeUploadLinkAsync(id As String, cancellationToken As CancellationToken) As Task(Of UploadLink)
            Return Me.IOClient.GetUploadLinkAsync(id, cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function RefreshLinkAsync(shareInfo As DmsLink, Optional cancellationToken As CancellationToken = Nothing) As Task
            If shareInfo Is Nothing Then Throw New ArgumentNullException(NameOf(shareInfo))
            cancellationToken.ThrowIfCancellationRequested()
            If String.IsNullOrEmpty(shareInfo.ID) OrElse shareInfo.FillLinkDetails Is Nothing Then Return
            If shareInfo.FillLinkDetails.Equals(New DmsLink.FillLinkDetailsFromId(AddressOf DelegatedFillUploadLinkDetails)) Then
                Dim metadata = Await Me.LoadNativeUploadLinkAsync(shareInfo.ID, cancellationToken).ConfigureAwait(False)
                If metadata Is Nothing Then Throw New InvalidOperationException(ProviderStrings.GetText("TheUploadLinkResponseContainsNoMetadata"))
                cancellationToken.ThrowIfCancellationRequested()
                shareInfo.Initialize(metadata.Password, DateTimeUtcToLocalTime(metadata.ExpiryDate), Nothing, metadata.MaxDocuments, metadata.MaxBytes,
                                     Nothing, Nothing, metadata.UploadsMade, metadata.UploadedBytes, metadata.Web, Nothing, Nothing,
                                     False, False, False, True, False, False)
                shareInfo.Name = metadata.Name
            ElseIf shareInfo.FillLinkDetails.Equals(New DmsLink.FillLinkDetailsFromId(AddressOf DelegatedFillLinkDetails)) Then
                Dim metadata = Await Me.LoadNativeDownloadLinkAsync(shareInfo.ID, cancellationToken).ConfigureAwait(False)
                If metadata?.AccessControl Is Nothing Then Throw New InvalidOperationException(ProviderStrings.GetText("TheDownloadLinkResponseContainsNoAccessControl"))
                cancellationToken.ThrowIfCancellationRequested()
                shareInfo.Initialize(metadata.AccessControl.Password, DateTimeUtcToLocalTime(metadata.AccessControl.ExpiryDate), metadata.AccessControl.MaxDownloads, Nothing, Nothing,
                                     metadata.Views, metadata.Downloads, Nothing, Nothing, metadata.Web, metadata.Download, metadata.Rest,
                                     True, Not metadata.AccessControl.ViewOnly, False, False, False, False)
            Else
                'An external derived provider may supply an arbitrary synchronous callback.
                Await MyBase.RefreshLinkAsync(shareInfo, cancellationToken).ConfigureAwait(False)
            End If
        End Function

        ''' <summary>Prepares built-in lazy link details before native resource snapshots are returned.</summary>
        ''' <param name="items">The converted resources whose built-in link models require metadata.</param>
        ''' <param name="cancellationToken">Cancels metadata admission and active HTTP I/O.</param>
        ''' <returns>A task representing preparation of uninitialized built-in links.</returns>
        ''' <remarks>Initialized settings and external derived-provider callbacks are preserved without refresh.</remarks>
        Protected Async Function PrepareNativeLinkSnapshotsAsync(items As IList(Of DmsResourceItem), cancellationToken As CancellationToken) As Task
            If items Is Nothing Then Throw New ArgumentNullException(NameOf(items))
            cancellationToken.ThrowIfCancellationRequested()
            For Each item In items
                If item?.ExtendedInfosLinks Is Nothing Then Continue For
                For Each link In item.ExtendedInfosLinks
                    cancellationToken.ThrowIfCancellationRequested()
                    If link Is Nothing OrElse link.DetailsInitialized OrElse link.FillLinkDetails Is Nothing Then Continue For
                    If link.FillLinkDetails.Equals(New DmsLink.FillLinkDetailsFromId(AddressOf DelegatedFillLinkDetails)) OrElse
                        link.FillLinkDetails.Equals(New DmsLink.FillLinkDetailsFromId(AddressOf DelegatedFillUploadLinkDetails)) Then
                        Await Me.RefreshLinkAsync(link, cancellationToken).ConfigureAwait(False)
                    End If
                Next
            Next
            cancellationToken.ThrowIfCancellationRequested()
        End Function

        Private Async Function ValidateNativeLinkSettingsAsync(shareInfo As DmsLink, requireIdentifier As Boolean, cancellationToken As CancellationToken) As Task
            If shareInfo Is Nothing Then Throw New ArgumentNullException(NameOf(shareInfo))
            cancellationToken.ThrowIfCancellationRequested()
            If requireIdentifier AndAlso String.IsNullOrEmpty(shareInfo.ID) Then Throw New InvalidOperationException(ProviderStrings.GetText("TheLinkOperationRequiresAnIdentifier"))
            If Not shareInfo.DetailsInitialized Then Await Me.RefreshLinkAsync(shareInfo, cancellationToken).ConfigureAwait(False)
            If shareInfo.AllowEdit Then Throw New NotSupportedException(ProviderStrings.GetText("AllowEditNotSupportedByProvider"))
            If shareInfo.AllowDelete Then Throw New NotSupportedException(ProviderStrings.GetText("AllowDeleteNotSupportedByProvider"))
            If shareInfo.AllowShare Then Throw New NotSupportedException(ProviderStrings.GetText("AllowShareNotSupportedByProvider"))
            If Not (shareInfo.AllowView Xor shareInfo.AllowUpload) Then Throw New ArgumentException(ProviderStrings.GetText("EitherAllowViewOrAllowUploadMustBeSet"), NameOf(shareInfo))
        End Function

        Private Shared Function NativeLinkAccessControl(shareInfo As DmsLink) As LinkAccessControl
            Return New LinkAccessControl With {.ViewOnly = Not shareInfo.AllowDownload, .Password = shareInfo.Password,
                .ExpiryDate = DateTimeLocalToUtcTime(shareInfo.ExpiryDateLocalTime), .MaxDownloads = ConvertNarrowingToNullableInt32(shareInfo.MaxDownloads)}
        End Function

        ''' <summary>Creates a download link for a selected resource using native I/O.</summary>
        ''' <param name="resource">The selected collection, folder, or file.</param>
        ''' <param name="accessControl">The view/download permissions and limits.</param>
        ''' <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        ''' <returns>The created link identifier and URLs.</returns>
        Protected Overridable Function CreateNativeDownloadLinkAsync(resource As DmsResourceItem, accessControl As LinkAccessControl, cancellationToken As CancellationToken) As Task(Of LinkCreationResponse)
            Select Case resource.ItemType
                Case DmsResourceItem.ItemTypes.Collection : Return Me.IOClient.ApiClient.Links.CreateCollectionLinkAsync(Me.IOClient.CurrentAuthenticationContextUserID, resource.ExtendedInfosCollectionID, accessControl, cancellationToken)
                Case DmsResourceItem.ItemTypes.Folder : Return Me.IOClient.ApiClient.Links.CreateFolderLinkAsync(Me.IOClient.CurrentAuthenticationContextUserID, resource.ExtendedInfosFolderID, accessControl, cancellationToken)
                Case DmsResourceItem.ItemTypes.File : Return Me.IOClient.ApiClient.Links.CreateDocumentLinkAsync(Me.IOClient.CurrentAuthenticationContextUserID, resource.ExtendedInfosFileID, accessControl, cancellationToken)
                Case Else : Throw New NotSupportedException(ProviderStrings.GetText("SharingForRootDirectoryNotSupported"))
            End Select
        End Function

        ''' <summary>Creates a collection upload link using native I/O, including an optional byte limit.</summary>
        ''' <param name="resource">The selected collection.</param>
        ''' <param name="settings">The initialized upload-link settings.</param>
        ''' <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        ''' <returns>The created upload-link identifier and URL.</returns>
        Protected Overridable Function CreateNativeUploadLinkAsync(resource As DmsResourceItem, settings As DmsLink, cancellationToken As CancellationToken) As Task(Of UploadLinkCreationResponse)
            If settings.MaxBytes.HasValue Then
                Return CenterDeviceUploadLinkBytesClient.Create(Me.IOClient.ApiClient).CreateCollectionLinkAsync(Me.IOClient.CurrentAuthenticationContextUserID, resource.ExtendedInfosCollectionID,
                    Tools.NotNullOrEmptyStringValue(settings.Name), DateTimeLocalToUtcTime(settings.ExpiryDateLocalTime), ConvertNarrowingToNullableInt32(settings.MaxUploads), settings.MaxBytes.Value, Tools.NotNullOrEmptyStringValue(settings.Password), cancellationToken)
            End If
            Return Me.IOClient.ApiClient.UploadLinks.CreateCollectionLinkAsync(Me.IOClient.CurrentAuthenticationContextUserID, resource.ExtendedInfosCollectionID, Tools.NotNullOrEmptyStringValue(settings.Name), Nothing,
                DateTimeLocalToUtcTime(settings.ExpiryDateLocalTime), ConvertNarrowingToNullableInt32(settings.MaxUploads), Tools.NotNullOrEmptyStringValue(settings.Password), Nothing, cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function CreateLinkAsync(dmsResource As DmsResourceItem, shareInfo As DmsLink, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsLink)
            If dmsResource Is Nothing Then Throw New ArgumentNullException(NameOf(dmsResource))
            Await Me.ValidateNativeLinkSettingsAsync(shareInfo, False, cancellationToken).ConfigureAwait(False)
            Dim id As String
            Select Case dmsResource.ItemType
                Case DmsResourceItem.ItemTypes.Collection : id = dmsResource.ExtendedInfosCollectionID
                Case DmsResourceItem.ItemTypes.Folder : id = dmsResource.ExtendedInfosFolderID
                Case DmsResourceItem.ItemTypes.File : id = dmsResource.ExtendedInfosFileID
                Case Else : Throw New NotSupportedException(ProviderStrings.GetText("SharingForRootDirectoryNotSupported"))
            End Select
            If String.IsNullOrEmpty(id) Then Throw New InvalidOperationException(ProviderStrings.GetText("LinkCreationRequiresTheSelectedResourceIdentifier"))
            If shareInfo.AllowUpload Then
                If dmsResource.ItemType <> DmsResourceItem.ItemTypes.Collection Then Throw New NotSupportedException(ProviderStrings.GetText("UploadLinksSupportedOnlyWithCollections"))
                ValidateUploadLinkMaxBytes(shareInfo.MaxBytes)
            End If
            Try
                Dim result As DmsLink
                If shareInfo.AllowUpload Then
                    Dim created = Await Me.CreateNativeUploadLinkAsync(dmsResource, shareInfo, cancellationToken).ConfigureAwait(False)
                    If String.IsNullOrEmpty(created?.Id) Then Throw New InvalidOperationException(ProviderStrings.GetText("UploadLinkCreationReturnedNoIdentifier"))
                    result = New DmsLink(dmsResource, created.Id, Me, AddressOf DelegatedFillUploadLinkDetails)
                    result.Initialize(shareInfo.Password, shareInfo.ExpiryDateLocalTime, Nothing, ConvertNarrowingToNullableInt32(shareInfo.MaxUploads), shareInfo.MaxBytes,
                                      Nothing, Nothing, 0, 0, created.Web, Nothing, Nothing, False, False, False, True, False, False)
                    result.Name = shareInfo.Name
                Else
                    Dim accessControl = NativeLinkAccessControl(shareInfo)
                    Dim created = Await Me.CreateNativeDownloadLinkAsync(dmsResource, accessControl, cancellationToken).ConfigureAwait(False)
                    If String.IsNullOrEmpty(created?.Id) Then Throw New InvalidOperationException(ProviderStrings.GetText("DownloadLinkCreationReturnedNoIdentifier"))
                    result = New DmsLink(dmsResource, created.Id, Me, AddressOf DelegatedFillLinkDetails)
                    result.Initialize(accessControl.Password, DateTimeUtcToLocalTime(accessControl.ExpiryDate), accessControl.MaxDownloads, Nothing, Nothing,
                                      0, 0, Nothing, Nothing, created.Web, created.Download, Nothing, True, Not accessControl.ViewOnly, False, False, False, False)
                End If
                If dmsResource.ExtendedInfosLinks Is Nothing Then dmsResource.ExtendedInfosLinks = New List(Of DmsLink)()
                dmsResource.ExtendedInfosLinks.Add(result)
                Return result
            Catch ex As ForbiddenException
                Throw New DmsUserErrorMessageException(ProviderStrings.Format("Forbidden", ex.ErrorResponse.Message), ex)
            Catch ex As BadRequestException
                Dim message = If(ex.ErrorResponse.Data IsNot Nothing AndAlso ex.ErrorResponse.Data.ContainsKey("explanation"), CStr(ex.ErrorResponse.Data("explanation")), ex.ErrorResponse.Message)
                Throw New DmsUserErrorMessageException(message, ex)
            Finally
                Me.InvalidateNativeLinkCaches()
            End Try
        End Function

        Private Sub InvalidateNativeLinkCaches()
            Me._AllUploadLinks = Nothing
            Me.IOClient.RootDirectory.ResetDirectoriesCache()
            Me.IOClient.RootDirectory.ResetFilesCache()
        End Sub

        ''' <summary>Updates download-link permissions and limits using native I/O.</summary>
        ''' <param name="id">The selected link identifier.</param>
        ''' <param name="accessControl">The updated access-control settings.</param>
        ''' <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        ''' <returns>A task representing the update.</returns>
        Protected Overridable Function UpdateNativeDownloadLinkAsync(id As String, accessControl As LinkAccessControl, cancellationToken As CancellationToken) As Task
            Return Me.IOClient.ApiClient.Link.UpdateLinkAsync(Me.IOClient.CurrentAuthenticationContextUserID, id, accessControl, cancellationToken)
        End Function

        ''' <summary>Updates an upload link using native I/O, including an optional byte limit.</summary>
        ''' <param name="settings">The initialized upload-link settings carrying the selected identifier.</param>
        ''' <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        ''' <returns>A task representing the update.</returns>
        Protected Overridable Function UpdateNativeUploadLinkAsync(settings As DmsLink, cancellationToken As CancellationToken) As Task
            If settings.MaxBytes.HasValue Then
                Return CenterDeviceUploadLinkBytesClient.Create(Me.IOClient.ApiClient).UpdateLinkAsync(Me.IOClient.CurrentAuthenticationContextUserID, settings.ID, Tools.NotNullOrEmptyStringValue(settings.Name),
                    DateTimeLocalToUtcTime(settings.ExpiryDateLocalTime), ConvertNarrowingToNullableInt32(settings.MaxUploads), settings.MaxBytes.Value, Tools.NotNullOrEmptyStringValue(settings.Password), cancellationToken)
            End If
            Return Me.IOClient.ApiClient.UploadLink.UpdateLinkAsync(Me.IOClient.CurrentAuthenticationContextUserID, settings.ID, Nothing, Tools.NotNullOrEmptyStringValue(settings.Name), Nothing,
                DateTimeLocalToUtcTime(settings.ExpiryDateLocalTime), ConvertNarrowingToNullableInt32(settings.MaxUploads), Tools.NotNullOrEmptyStringValue(settings.Password), Nothing, cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function UpdateLinkAsync(shareInfo As DmsLink, Optional cancellationToken As CancellationToken = Nothing) As Task
            Await Me.ValidateNativeLinkSettingsAsync(shareInfo, True, cancellationToken).ConfigureAwait(False)
            If shareInfo.AllowUpload Then ValidateUploadLinkMaxBytes(shareInfo.MaxBytes)
            Try
                If shareInfo.AllowUpload Then
                    Await Me.UpdateNativeUploadLinkAsync(shareInfo, cancellationToken).ConfigureAwait(False)
                Else
                    Await Me.UpdateNativeDownloadLinkAsync(shareInfo.ID, NativeLinkAccessControl(shareInfo), cancellationToken).ConfigureAwait(False)
                End If
            Catch ex As ForbiddenException
                Throw New DmsUserErrorMessageException(ex.ErrorResponse.Message, ex)
            Finally
                Me.InvalidateNativeLinkCaches()
            End Try
        End Function

        ''' <summary>Deletes a download link by its selected identifier using native I/O.</summary>
        ''' <param name="id">The selected link identifier.</param>
        ''' <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        ''' <returns>A task representing deletion.</returns>
        Protected Overridable Function DeleteNativeDownloadLinkAsync(id As String, cancellationToken As CancellationToken) As Task
            Return Me.IOClient.ApiClient.Link.DeleteLinkAsync(Me.IOClient.CurrentAuthenticationContextUserID, id, cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function DeleteLinkAsync(shareInfo As DmsLink, Optional cancellationToken As CancellationToken = Nothing) As Task
            Await Me.ValidateNativeLinkSettingsAsync(shareInfo, True, cancellationToken).ConfigureAwait(False)
            Try
                If shareInfo.AllowUpload Then
                    Await Me.DeleteNativeUploadLinkAsync(shareInfo.ID, cancellationToken).ConfigureAwait(False)
                Else
                    Await Me.DeleteNativeDownloadLinkAsync(shareInfo.ID, cancellationToken).ConfigureAwait(False)
                End If
            Finally
                Me.InvalidateNativeLinkCaches()
            End Try
        End Function
    End Class
End Namespace
