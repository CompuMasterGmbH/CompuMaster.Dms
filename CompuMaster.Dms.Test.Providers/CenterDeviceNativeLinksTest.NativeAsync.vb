Option Explicit On
Option Strict On

Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest.Clients.Collections
Imports CenterDevice.Rest.Clients.Link
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class CenterDeviceNativeLinksTest
    <TestCase(DmsResourceItem.ItemTypes.Collection), TestCase(DmsResourceItem.ItemTypes.Folder), TestCase(DmsResourceItem.ItemTypes.File)>
    Public Async Function DownloadLinkCreationRetainsIdentityLimitsAndInitializedUrls(kind As DmsResourceItem.ItemTypes) As Task
        Dim provider As New LinkProvider()
        Dim item = SelectedItem(kind)
        Dim settings = CreateSettings(item, provider, False)
        settings.MaxDownloads = Long.MaxValue
        Using cancellation As New CancellationTokenSource()
            Dim result = Await provider.CreateLinkAsync(item, settings, cancellation.Token)
            Assert.That(provider.Selected, [Is].SameAs(item))
            Assert.That(provider.RequestToken, [Is].EqualTo(cancellation.Token))
            Assert.That(provider.AccessControl.MaxDownloads, [Is].EqualTo(Integer.MaxValue))
            Assert.That(provider.AccessControl.ViewOnly, [Is].False)
            Assert.That(result.WebUrl, [Is].EqualTo("https://fixture.invalid/view"))
            Assert.That(result.DownloadUrl, [Is].EqualTo("https://fixture.invalid/download"))
            Assert.That(result.MaxDownloads, [Is].EqualTo(Integer.MaxValue))
            Assert.That(result.AllowDownload, [Is].True)
            Assert.That(item.ExtendedInfosLinks, [Is].EqualTo(New DmsLink() {result}))
            Assert.That(provider.Events, [Is].EqualTo(New String() {"create-download:selected-id"}))
        End Using
    End Function

    <Test>
    Public Async Function UploadLinkCreationRetainsThe64BitByteLimitAndFactoryMetadata() As Task
        Dim provider As New LinkProvider()
        Dim item = SelectedItem(DmsResourceItem.ItemTypes.Collection)
        Dim settings = CreateSettings(item, provider, True)
        Dim result = Await provider.CreateLinkAsync(item, settings)
        Assert.That(provider.LastSettings, [Is].SameAs(settings))
        Assert.That(result.MaxBytes, [Is].EqualTo(3L * 1073741824L))
        Assert.That(result.MaxUploads, [Is].EqualTo(7))
        Assert.That(result.Name, [Is].EqualTo("upload name"))
        Assert.That(result.WebUrl, [Is].EqualTo("https://fixture.invalid/upload"))
        Assert.That(result.AllowUpload, [Is].True)
        Assert.That(result.AllowDownload, [Is].False)
        Assert.That(provider.Events, [Is].EqualTo(New String() {"create-upload:selected-id"}))
    End Function

    <TestCase(DmsResourceItem.ItemTypes.Folder), TestCase(DmsResourceItem.ItemTypes.File), TestCase(DmsResourceItem.ItemTypes.Root)>
    Public Sub UploadLinksRejectUnsupportedResourceKindsBeforeDispatch(kind As DmsResourceItem.ItemTypes)
        Dim provider As New LinkProvider()
        Dim item = SelectedItem(kind)
        Assert.ThrowsAsync(Of NotSupportedException)(CType(Async Function()
                                                               Await provider.CreateLinkAsync(item, CreateSettings(item, provider, True))
                                                           End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].Empty)
    End Sub

    <Test>
    Public Sub DownloadLinksRequireTheSelectedIdentifier()
        Dim provider As New LinkProvider()
        Dim item = SelectedItem(DmsResourceItem.ItemTypes.File)
        item.ExtendedInfosFileID = Nothing
        Assert.ThrowsAsync(Of InvalidOperationException)(CType(Async Function()
                                                                   Await provider.CreateLinkAsync(item, CreateSettings(item, provider, False))
                                                               End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].Empty)
    End Sub

    <TestCase(False), TestCase(True)>
    Public Async Function NativeRefreshInitializesDetailsWithoutInvokingTheSynchronousCallback(upload As Boolean) As Task
        Dim provider As New LinkProvider()
        Dim item = SelectedItem(DmsResourceItem.ItemTypes.Collection)
        Dim link = ExistingLink(item, provider, upload)
        Await link.RefreshAsync()
        Assert.That(link.DetailsInitialized, [Is].True)
        Assert.That(link.Password, [Is].EqualTo("server password"))
        If upload Then
            Assert.That(link.MaxBytes, [Is].EqualTo(5L * 1073741824L))
            Assert.That(link.UploadedBytes, [Is].EqualTo(4L * 1073741824L))
            Assert.That(link.UploadsCount, [Is].EqualTo(3))
            Assert.That(link.WebUrl, [Is].EqualTo("https://fixture.invalid/upload/link-id"))
        Else
            Assert.That(link.DownloadsCount, [Is].EqualTo(11))
            Assert.That(link.ViewsCount, [Is].EqualTo(13))
            Assert.That(link.AllowDownload, [Is].False)
        End If
        Assert.That(provider.Events.Count, [Is].EqualTo(1), "Property reads must not initiate a second refresh.")
    End Function

    <TestCase(False), TestCase(True)>
    Public Async Function UpdatePreservesAlreadyEditedSettingsAndSelectedLinkId(upload As Boolean) As Task
        Dim provider As New LinkProvider()
        Dim item = SelectedItem(DmsResourceItem.ItemTypes.Collection)
        Dim link = CreateSettings(item, provider, upload)
        link.ID = "edited-link-id"
        link.Password = "edited password"
        link.MaxDownloads = 17
        Await provider.UpdateLinkAsync(link)
        Assert.That(provider.Events, [Is].EqualTo(New String() {If(upload, "update-upload:", "update-download:") & "edited-link-id"}))
        If upload Then
            Assert.That(provider.LastSettings.Password, [Is].EqualTo("edited password"))
        Else
            Assert.That(provider.AccessControl.Password, [Is].EqualTo("edited password"))
            Assert.That(provider.AccessControl.MaxDownloads, [Is].EqualTo(17))
        End If
    End Function

    <TestCase(False), TestCase(True)>
    Public Async Function DeleteUninitializedLinkResolvesItsKindNativelyBeforeDispatch(upload As Boolean) As Task
        Dim provider As New LinkProvider()
        Dim link = ExistingLink(SelectedItem(DmsResourceItem.ItemTypes.Collection), provider, upload)
        Await provider.DeleteLinkAsync(link)
        Assert.That(provider.Events, [Is].EqualTo(New String() {If(upload, "load-upload:", "load-download:") & "link-id", If(upload, "delete-upload:", "delete-download:") & "link-id"}))
    End Function

    <TestCase(False), TestCase(True)>
    Public Sub InvalidUploadLimitsAreRejectedBeforeCreateOrUpdate(update As Boolean)
        Dim provider As New LinkProvider()
        Dim item = SelectedItem(DmsResourceItem.ItemTypes.Collection)
        Dim link = CreateSettings(item, provider, True)
        link.MaxBytes = 1
        link.ID = "link-id"
        Assert.ThrowsAsync(Of ArgumentOutOfRangeException)(CType(Async Function()
                                                                   If update Then
                                                                       Await provider.UpdateLinkAsync(link)
                                                                   Else
                                                                       Await provider.CreateLinkAsync(item, link)
                                                                   End If
                                                               End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].Empty)
    End Sub

    <Test>
    Public Async Function FailedCreationInvalidatesCachesAndDoesNotPublishAFabricatedLink() As Task
        Dim provider As New LinkProvider With {.Fail = True}
        Dim item = SelectedItem(DmsResourceItem.ItemTypes.Collection)
        Await provider.ListAllCollectionNamesAsync("/")
        Assert.ThrowsAsync(Of IOException)(CType(Async Function()
                                                    Await provider.CreateLinkAsync(item, CreateSettings(item, provider, False))
                                                End Function, Func(Of Task)))
        Await provider.ListAllCollectionNamesAsync("/")
        Assert.That(provider.Io.CollectionCalls, [Is].EqualTo(2))
        Assert.That(item.ExtendedInfosLinks, [Is].Null.Or.Empty)
        Assert.That(provider.Events.Count, [Is].EqualTo(1))
    End Function

    <Test>
    Public Async Function ActiveLinkMutationCancellationInvalidatesCachesWithoutReplay() As Task
        Dim provider As New LinkProvider With {.Block = True}
        Dim item = SelectedItem(DmsResourceItem.ItemTypes.Collection)
        Dim link = CreateSettings(item, provider, False)
        link.ID = "link-id"
        Await provider.ListAllCollectionNamesAsync("/")
        Using cancellation As New CancellationTokenSource()
            Dim operation = provider.UpdateLinkAsync(link, cancellation.Token)
            Await provider.Entered.Task
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                       Await operation
                                                                   End Function, Func(Of Task)))
        End Using
        Await provider.ListAllCollectionNamesAsync("/")
        Assert.That(provider.Io.CollectionCalls, [Is].EqualTo(2))
        Assert.That(provider.Events.Count, [Is].EqualTo(1))
    End Function

    <Test>
    Public Async Function RefreshCancellationPreservesExistingDetails() As Task
        Dim provider As New LinkProvider With {.Block = True}
        Dim link = ExistingLink(SelectedItem(DmsResourceItem.ItemTypes.Collection), provider, False)
        link.Initialize("old password", Nothing, 5, Nothing, Nothing, 0, 0, Nothing, Nothing, "old url", Nothing, Nothing, True, True, False, False, False, False)
        Using cancellation As New CancellationTokenSource()
            Dim operation = link.RefreshAsync(cancellation.Token)
            Await provider.Entered.Task
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                       Await operation
                                                                   End Function, Func(Of Task)))
        End Using
        Assert.That(link.Password, [Is].EqualTo("old password"))
        Assert.That(link.WebUrl, [Is].EqualTo("old url"))
    End Function

    <Test>
    Public Async Function ExternalLinkCallbacksRetainTheCompatibleFallback() As Task
        Dim provider As New LinkProvider()
        Dim calls As Integer
        Dim link As New DmsLink(SelectedItem(DmsResourceItem.ItemTypes.Collection), "external-id", provider,
            Sub(owner, id, target)
                calls += 1
                target.WebUrl = "external url"
            End Sub)
        Await link.RefreshAsync()
        Assert.That(link.WebUrl, [Is].EqualTo("external url"))
        Assert.That(calls, [Is].EqualTo(1))
        Assert.That(provider.Events, [Is].Empty)
    End Function

    <TestCase(False), TestCase(True)>
    Public Async Function ResourceSnapshotPreparesBuiltinLinksBeforePropertyReads(upload As Boolean) As Task
        Dim provider As New LinkProvider()
        Dim item = SelectedItem(DmsResourceItem.ItemTypes.Collection)
        Dim link = ExistingLink(item, provider, upload)
        item.ExtendedInfosLinks = New List(Of DmsLink) From {link}
        Await provider.PrepareLinksAsync(item, CancellationToken.None)
        Assert.That(link.DetailsInitialized, [Is].True)
        Assert.That(link.Password, [Is].EqualTo("server password"))
        Assert.That(link.ParentDmsResourceItem, [Is].SameAs(item))
        Assert.That(link.ID, [Is].EqualTo("link-id"))
        Assert.That(link.AllowUpload, [Is].EqualTo(upload))
        Assert.That(provider.Events, [Is].EqualTo(New String() {If(upload, "load-upload:", "load-download:") & "link-id"}))
        Await provider.PrepareLinksAsync(item, CancellationToken.None)
        Assert.That(provider.Events.Count, [Is].EqualTo(1), "Prepared snapshots must not trigger another detail request.")
    End Function

    <Test>
    Public Async Function ResourceSnapshotPreservesInitializedEditsAndExternalCallbacks() As Task
        Dim provider As New LinkProvider()
        Dim item = SelectedItem(DmsResourceItem.ItemTypes.Collection)
        Dim edited = ExistingLink(item, provider, False)
        edited.Initialize("edited", Nothing, 99, Nothing, Nothing, 1, 2, Nothing, Nothing, "edited URL", Nothing, Nothing, True, True, False, False, False, False)
        Dim calls As Integer
        Dim external As New DmsLink(item, "external", provider, Sub(owner, id, target) calls += 1)
        item.ExtendedInfosLinks = New List(Of DmsLink) From {edited, external}
        Await provider.PrepareLinksAsync(item, CancellationToken.None)
        Assert.That(edited.Password, [Is].EqualTo("edited"))
        Assert.That(edited.MaxDownloads, [Is].EqualTo(99))
        Assert.That(edited.WebUrl, [Is].EqualTo("edited URL"))
        Assert.That(external.DetailsInitialized, [Is].False)
        Assert.That(external.FillLinkDetails, [Is].Not.Null)
        Assert.That(calls, [Is].Zero)
        Assert.That(provider.Events, [Is].Empty)
    End Function

    <Test>
    Public Sub ResourceSnapshotFailureRetainsOriginalCauseAndLeavesFailedLinkUninitialized()
        Dim provider As New LinkProvider With {.Fail = True}
        Dim item = SelectedItem(DmsResourceItem.ItemTypes.File)
        Dim link = ExistingLink(item, provider, False)
        item.ExtendedInfosLinks = New List(Of DmsLink) From {link}
        Dim failure = Assert.ThrowsAsync(Of IOException)(CType(Async Function()
                                                                  Await provider.PrepareLinksAsync(item, CancellationToken.None)
                                                              End Function, Func(Of Task)))
        Assert.That(failure.Message, [Is].EqualTo("Uncertain link operation."))
        Assert.That(link.DetailsInitialized, [Is].False)
        Assert.That(provider.Events.Count, [Is].EqualTo(1))
    End Sub

    <TestCase(False), TestCase(True)>
    Public Async Function ResourceSnapshotCancellationDoesNotInitializeOrReplayDetails(active As Boolean) As Task
        Dim provider As New LinkProvider With {.Block = active}
        Dim item = SelectedItem(DmsResourceItem.ItemTypes.File)
        Dim link = ExistingLink(item, provider, False)
        item.ExtendedInfosLinks = New List(Of DmsLink) From {link}
        Using cancellation As New CancellationTokenSource()
            If Not active Then cancellation.Cancel()
            Dim operation = provider.PrepareLinksAsync(item, cancellation.Token)
            If active Then
                Await provider.Entered.Task
                cancellation.Cancel()
            End If
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                     Await operation
                                                                 End Function, Func(Of Task)))
            Assert.That(link.DetailsInitialized, [Is].False)
            Assert.That(provider.Events.Count, [Is].EqualTo(If(active, 1, 0)))
        End Using
    End Function

    Private Shared Function SelectedItem(kind As DmsResourceItem.ItemTypes) As DmsResourceItem
        Return New DmsResourceItem With {.ItemType = kind, .FullName = "duplicate", .ExtendedInfosCollectionID = "selected-id", .ExtendedInfosFolderID = "selected-id", .ExtendedInfosFileID = "selected-id", .ExtendedInfosCollisionDetected = True}
    End Function

    Private Shared Function CreateSettings(item As DmsResourceItem, provider As LinkProvider, upload As Boolean) As DmsLink
        Dim result As New DmsLink(item, provider)
        result.Initialize("password", Nothing, 5, 7, If(upload, CType(3L * 1073741824L, Long?), Nothing), 0, 0, 0, 0, Nothing, Nothing, Nothing, Not upload, Not upload, False, upload, False, False)
        result.Name = "upload name"
        Return result
    End Function

    Private Shared Function ExistingLink(item As DmsResourceItem, provider As LinkProvider, upload As Boolean) As DmsLink
        If upload Then Return New DmsLink(item, "link-id", provider, AddressOf CenterDeviceDmsProviderBase.DelegatedFillUploadLinkDetails)
        Return New DmsLink(item, "link-id", provider, AddressOf CenterDeviceDmsProviderBase.DelegatedFillLinkDetails)
    End Function

    Private Class LinkProvider
        Inherits ScopevisioTeamworkDmsProvider
        Public ReadOnly Io As New FixtureIo()
        Public ReadOnly Events As New List(Of String)()
        Public Selected As DmsResourceItem
        Public RequestToken As CancellationToken
        Public LastSettings As DmsLink
        Public AccessControl As LinkAccessControl
        Public Fail As Boolean
        Public Block As Boolean
        Public ReadOnly Entered As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Public Sub New()
            Me.IOClient = Io
        End Sub
        Public Function PrepareLinksAsync(item As DmsResourceItem, cancellationToken As CancellationToken) As Task
            Return Me.PrepareNativeLinkSnapshotsAsync(New List(Of DmsResourceItem) From {item}, cancellationToken)
        End Function
        Private Async Function RecordAsync(operation As String, id As String, cancellationToken As CancellationToken) As Task
            Events.Add(operation & ":" & id)
            RequestToken = cancellationToken
            Entered.TrySetResult(True)
            If Block Then Await Task.Delay(Timeout.Infinite, cancellationToken)
            If Fail Then Throw New IOException("Uncertain link operation.")
        End Function
        Protected Overrides Async Function CreateNativeDownloadLinkAsync(resource As DmsResourceItem, accessControl As LinkAccessControl, cancellationToken As CancellationToken) As Task(Of LinkCreationResponse)
            Selected = resource
            Me.AccessControl = accessControl
            Await RecordAsync("create-download", "selected-id", cancellationToken)
            Return New LinkCreationResponse With {.Id = "link-id", .Web = "https://fixture.invalid/view", .Download = "https://fixture.invalid/download"}
        End Function
        Protected Overrides Async Function CreateNativeUploadLinkAsync(resource As DmsResourceItem, settings As DmsLink, cancellationToken As CancellationToken) As Task(Of UploadLinkCreationResponse)
            Selected = resource
            LastSettings = settings
            Await RecordAsync("create-upload", "selected-id", cancellationToken)
            Return New UploadLinkCreationResponse With {.Id = "link-id", .Web = "https://fixture.invalid/upload"}
        End Function
        Protected Overrides Async Function LoadNativeDownloadLinkAsync(id As String, cancellationToken As CancellationToken) As Task(Of CenterDevice.Rest.Clients.Link.Link)
            Await RecordAsync("load-download", id, cancellationToken)
            Return New CenterDevice.Rest.Clients.Link.Link With {.Id = id, .Web = "https://fixture.invalid/view", .Download = "https://fixture.invalid/download", .Rest = "https://fixture.invalid/rest", .Views = 13, .Downloads = 11,
                .AccessControl = New LinkAccessControl With {.Password = "server password", .ViewOnly = True, .MaxDownloads = 17}}
        End Function
        Protected Overrides Async Function LoadNativeUploadLinkAsync(id As String, cancellationToken As CancellationToken) As Task(Of UploadLink)
            Await RecordAsync("load-upload", id, cancellationToken)
            Dim result As New UploadLink With {.Id = id, .Password = "server password", .Name = "server upload name", .MaxDocuments = 19, .MaxBytes = 5L * 1073741824L, .UploadedBytes = 4L * 1073741824L, .UploadsMade = 3}
            GetType(UploadLink).GetField("UploadLinkBaseUrl", Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Instance).SetValue(result, "https://fixture.invalid/upload/")
            Return result
        End Function
        Protected Overrides Function UpdateNativeDownloadLinkAsync(id As String, accessControl As LinkAccessControl, cancellationToken As CancellationToken) As Task
            Me.AccessControl = accessControl
            Return RecordAsync("update-download", id, cancellationToken)
        End Function
        Protected Overrides Function UpdateNativeUploadLinkAsync(settings As DmsLink, cancellationToken As CancellationToken) As Task
            LastSettings = settings
            Return RecordAsync("update-upload", settings.ID, cancellationToken)
        End Function
        Protected Overrides Function DeleteNativeDownloadLinkAsync(id As String, cancellationToken As CancellationToken) As Task
            Return RecordAsync("delete-download", id, cancellationToken)
        End Function
        Protected Overrides Function DeleteNativeUploadLinkAsync(linkId As String, cancellationToken As CancellationToken) As Task
            Return RecordAsync("delete-upload", linkId, cancellationToken)
        End Function
    End Class

    Private Class FixtureIo
        Inherits Global.CenterDevice.IO.IOClientBase
        Public CollectionCalls As Integer
        Public Sub New()
            MyBase.New(Nothing, "fixture-user")
        End Sub
        Protected Overrides Function LookupCollectionsAsync(cancellationToken As CancellationToken) As Task(Of List(Of Collection))
            CollectionCalls += 1
            Return Task.FromResult(New List(Of Collection) From {New Collection With {.Id = "selected-id", .Name = "duplicate"}})
        End Function
    End Class
End Class
