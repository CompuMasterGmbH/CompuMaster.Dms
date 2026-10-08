Option Explicit On
Option Strict On

Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

'Uses the actual provider listing APIs and SDK/HTTP boundaries without remote servers.
<TestFixture>
<Apartment(ApartmentState.STA)>
Public Class ProviderUiContextTest

    <TestCase("Scopevisio", 0), TestCase("Scopevisio", 1), TestCase("Scopevisio", 2), TestCase("Scopevisio", 3), TestCase("Scopevisio", 4), TestCase("Scopevisio", 5), TestCase("Scopevisio", 6)>
    <TestCase("CenterDevice", 0), TestCase("CenterDevice", 1), TestCase("CenterDevice", 2), TestCase("CenterDevice", 3), TestCase("CenterDevice", 4), TestCase("CenterDevice", 5), TestCase("CenterDevice", 6)>
    Public Sub NativeDirectoryEntriesKeepEveryExistingSharingIcon(kind As String, sharingMode As Integer)
        Using boundary As New ListingBoundary(kind, sharingMode),
              browser As New ControlledStartupBrowser(boundary.Provider),
              dispatcher As New UiTestDispatcher
            boundary.Completion.SetResult(True)
            dispatcher.Finish(browser.LoadTreeAsync())
            Dim node = browser.TreeViewDmsFolders.Nodes(0).Nodes(0)
            Assert.That(node.ImageIndex, [Is].EqualTo(If(sharingMode = 0, 1, 4)), "Private entries keep their ordinary icon; download/upload links and visible/hidden user/group shares keep the shared icon.")
            Assert.That(node.SelectedImageIndex, [Is].EqualTo(node.ImageIndex))
        End Using
    End Sub

    Public Shared Iterator Function ProviderOutcomes() As IEnumerable(Of TestCaseData)
        For Each kind In {"Scopevisio", "CenterDevice", "WebDAV", "ownCloud", "Nextcloud"}
            For outcome As Integer = 0 To 2
                For Each listFiles In {False, True}
                    Yield New TestCaseData(kind, outcome, listFiles)
                Next
            Next
        Next
    End Function

    <TestCaseSource(NameOf(ProviderOutcomes))>
    Public Sub DelayedProviderListingCompletesAndRestoresTheModalBrowser(kind As String, outcome As Integer, listFiles As Boolean)
        Dim previousContext = SynchronizationContext.Current
        Dim previousAutoInstall = WindowsFormsSynchronizationContext.AutoInstall
        SynchronizationContext.SetSynchronizationContext(Nothing)
        WindowsFormsSynchronizationContext.AutoInstall = True
        Try
            Using boundary As New ListingBoundary(kind, listFiles:=listFiles),
                  owner As New Form With {.ShowInTaskbar = False, .Opacity = 0},
                  browser As New ControlledStartupBrowser(boundary.Provider) With {.ShowInTaskbar = False, .Opacity = 0},
                  watchdog As New System.Windows.Forms.Timer With {.Interval = 25},
                  cancellation As New CancellationTokenSource
                owner.Show()
                Dim operation As Task = Nothing
                Dim released As Boolean
                Dim restored As Boolean
                Dim wasDisabled As Boolean
                Dim deadline = DateTime.UtcNow.AddSeconds(5)
                browser.StartListing =
                    Sub()
                        operation = browser.RunTransferAsync(Async Function()
                                                                 Dim listing As Task(Of List(Of DmsResourceItem))
                                                                 If listFiles Then
                                                                     listing = boundary.Provider.ListFileEntriesAsync("Collection", cancellation.Token)
                                                                 Else
                                                                     listing = boundary.Provider.ListDirectoryEntriesAsync("/", cancellation.Token)
                                                                 End If
                                                                 Dim items = Await listing
                                                                 Assert.That(items.Count, [Is].EqualTo(1))
                                                             End Function)
                        wasDisabled = browser.Enabled AndAlso Not browser.SplitContainer.Enabled AndAlso browser.UseWaitCursor
                    End Sub
                AddHandler watchdog.Tick,
                    Sub()
                        If operation Is Nothing AndAlso browser.Visible Then
                            browser.StartListing()
                        ElseIf boundary.Entered AndAlso Not released Then
                            released = True
                            Select Case outcome
                                Case 0 : boundary.Completion.TrySetResult(True)
                                Case 1 : boundary.Completion.TrySetException(New InvalidOperationException("Listing fixture failed."))
                                Case 2 : cancellation.Cancel()
                            End Select
                        ElseIf operation IsNot Nothing AndAlso operation.IsCompleted Then
                            restored = browser.Enabled AndAlso browser.SplitContainer.Enabled AndAlso Not browser.UseWaitCursor
                            browser.Close()
                        ElseIf DateTime.UtcNow >= deadline Then
                            cancellation.Cancel()
                            browser.Dispose()
                        End If
                    End Sub
                watchdog.Start()
                browser.ShowDialog(owner)
                watchdog.Stop()
                Assert.That(wasDisabled, [Is].True, "The modal window must stay active with a wait cursor while its content is locked.")
                Assert.That(released, [Is].True, "The actual provider must reach its SDK/HTTP boundary.")
                Assert.That(operation, [Is].Not.Null)
                Assert.That(operation.IsCompleted, [Is].True, "Provider continuations must complete on a real WinForms message loop.")
                Assert.That(restored, [Is].True, "Success, failure and cancellation must restore the modal browser.")
                If outcome = 0 Then
                    operation.GetAwaiter().GetResult()
                ElseIf outcome = 1 Then
                    Assert.That(operation.IsFaulted, [Is].True)
                    'Observe the failure without opening a message box from the test.
                    Dim failure = operation.Exception
                    Assert.That(failure, [Is].Not.Null)
                Else
                    Assert.That(operation.IsCanceled, [Is].True)
                End If
                owner.Close()
            End Using
        Finally
            SynchronizationContext.SetSynchronizationContext(previousContext)
            WindowsFormsSynchronizationContext.AutoInstall = previousAutoInstall
        End Try
    End Sub

    Private Class ControlledStartupBrowser
        Inherits Global.CompuMaster.Dms.BrowserUI.DmsBrowser
        Public Sub New(provider As BaseDmsProvider)
            MyBase.New(provider)
        End Sub
        Protected Overrides Sub OnLoad(e As EventArgs)
            'The test owns the listing so failed/canceled cases cannot open a modal error UI.
        End Sub
        Protected Overrides Sub OnShown(e As EventArgs)
            'The test timer starts the listing after the modal message loop is active.
        End Sub
        Public StartListing As Action
    End Class

    Private Class ListingBoundary
        Implements IDisposable
        Public ReadOnly Completion As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Public Entered As Boolean
        Public ReadOnly Provider As BaseDmsProvider
        Private ReadOnly Transport As HttpClient

        Public ReadOnly SharingMode As Integer
        Public ReadOnly ListFiles As Boolean
        Public Sub New(kind As String, Optional sharingMode As Integer = 0, Optional listFiles As Boolean = False)
            Me.SharingMode = sharingMode
            Me.ListFiles = listFiles
            Select Case kind
                Case "Scopevisio" : Provider = New ScopevisioFixtureProvider(New ListingIo(Me), Me)
                Case "CenterDevice" : Provider = New CenterDeviceFixtureProvider(New ListingIo(Me), Me)
                Case Else
                    Dim segment = If(kind = "WebDAV", "", If(kind = "ownCloud", "remote.php/webdav/", "remote.php/dav/files/fixture/"))
                    Dim url = "https://startup.fixture.invalid/" & segment
                    Dim webdav As New WebDavDmsProvider With {.CustomWebApiUrl = url}
                    Transport = New HttpClient(New ListingHandler(Me, url))
                    GetType(WebDavDmsProvider).GetField("WebDavClient", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(webdav, New Global.WebDav.WebDavClient(Transport))
                    Provider = webdav
            End Select
        End Sub
        Public Async Function WaitAsync(token As CancellationToken) As Task
            Entered = True
            Await Completion.Task.WaitAsync(token).ConfigureAwait(False)
        End Function
        Public Sub Dispose() Implements IDisposable.Dispose
            Transport?.Dispose()
        End Sub
        Public Function UploadLinks() As Global.CenterDevice.Rest.Clients.Link.UploadLinks
            Dim links As New List(Of Global.CenterDevice.Rest.Clients.Link.UploadLink)
            If SharingMode = 2 Then
                Dim link As New Global.CenterDevice.Rest.Clients.Link.UploadLink With {.Id = "fixture-upload", .Collection = "fixture-collection"}
                GetType(Global.CenterDevice.Rest.Clients.Link.UploadLink).GetField("UploadLinkBaseUrl", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(link, "https://startup.fixture.invalid/upload/")
                links.Add(link)
            End If
            Return New Global.CenterDevice.Rest.Clients.Link.UploadLinks With {.UploadLinksList = links}
        End Function
    End Class

    Private Class ListingHandler
        Inherits HttpMessageHandler
        Private ReadOnly Boundary As ListingBoundary
        Private ReadOnly Url As String
        Public Sub New(boundary As ListingBoundary, url As String)
            Me.Boundary = boundary
            Me.Url = url
        End Sub
        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, token As CancellationToken) As Task(Of HttpResponseMessage)
            Await Boundary.WaitAsync(token).ConfigureAwait(False)
            Dim parentUrl = If(Boundary.ListFiles, Url & "Collection/", Url)
            Dim child = If(Boundary.ListFiles, "File.txt", "Folder/")
            Dim childType = If(Boundary.ListFiles, "", "<d:collection/>")
            Dim xml = "<d:multistatus xmlns:d=""DAV:""><d:response><d:href>" & parentUrl & "</d:href><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response><d:response><d:href>" & parentUrl & child & "</d:href><d:propstat><d:prop><d:resourcetype>" & childType & "</d:resourcetype></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response></d:multistatus>"
            Return New HttpResponseMessage(CType(207, HttpStatusCode)) With {.Content = New StringContent(xml, System.Text.Encoding.UTF8, "application/xml")}
        End Function
    End Class

    Private Class ListingIo
        Inherits Global.CenterDevice.IO.IOClientBase
        Private ReadOnly Boundary As ListingBoundary
        Public Sub New(boundary As ListingBoundary)
            MyBase.New(Nothing, "fixture-user")
            Me.Boundary = boundary
        End Sub
        Protected Overrides Async Function LookupCollectionsAsync(token As CancellationToken) As Task(Of List(Of Global.CenterDevice.Rest.Clients.Collections.Collection))
            Await Boundary.WaitAsync(token).ConfigureAwait(False)
            Dim collection As New Global.CenterDevice.Rest.Clients.Collections.Collection With {.Id = "fixture-collection", .Name = "Collection"}
            Select Case Boundary.SharingMode
                Case 1 : collection.Link = "fixture-download"
                Case 3 : collection.Users = New Global.CenterDevice.Rest.Clients.Common.Sharings With {.Visible = New List(Of String) From {"fixture-user"}}
                Case 4 : collection.Groups = New Global.CenterDevice.Rest.Clients.Common.Sharings With {.Visible = New List(Of String) From {"fixture-group"}}
                Case 5 : collection.Users = New Global.CenterDevice.Rest.Clients.Common.Sharings With {.NotVisibleCount = 1}
                Case 6 : collection.Groups = New Global.CenterDevice.Rest.Clients.Common.Sharings With {.NotVisibleCount = 1}
            End Select
            Return New List(Of Global.CenterDevice.Rest.Clients.Collections.Collection) From {
                collection
            }
        End Function
        Protected Overrides Async Function LookupChildDocumentsAsync(collectionId As String, parentId As String, token As CancellationToken) As Task(Of List(Of Global.CenterDevice.Rest.Clients.Documents.Metadata.DocumentFullMetadata))
            Await Boundary.WaitAsync(token).ConfigureAwait(False)
            Return New List(Of Global.CenterDevice.Rest.Clients.Documents.Metadata.DocumentFullMetadata) From {
                New Global.CenterDevice.Rest.Clients.Documents.Metadata.DocumentFullMetadata With {.Id = "fixture-file", .Filename = "File.txt", .Size = 128}
            }
        End Function
    End Class

    Private Class ScopevisioFixtureProvider
        Inherits ScopevisioTeamworkDmsProvider
        Private ReadOnly Boundary As ListingBoundary
        Public Sub New(io As Global.CenterDevice.IO.IOClientBase, boundary As ListingBoundary)
            Me.IOClient = io
            Me.Boundary = boundary
        End Sub
        Protected Overrides Function LoadNativeUploadLinksAsync(token As CancellationToken) As Task(Of Global.CenterDevice.Rest.Clients.Link.UploadLinks)
            Return Task.FromResult(Boundary.UploadLinks())
        End Function
    End Class

    Private Class CenterDeviceFixtureProvider
        Inherits CenterDeviceDmsProvider
        Private ReadOnly Boundary As ListingBoundary
        Public Sub New(io As Global.CenterDevice.IO.IOClientBase, boundary As ListingBoundary)
            Me.IOClient = io
            Me.Boundary = boundary
        End Sub
        Protected Overrides Function LoadNativeUploadLinksAsync(token As CancellationToken) As Task(Of Global.CenterDevice.Rest.Clients.Link.UploadLinks)
            Return Task.FromResult(Boundary.UploadLinks())
        End Function
    End Class
End Class
