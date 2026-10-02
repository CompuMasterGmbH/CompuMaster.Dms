Option Strict On

Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class BaseDmsProviderAsyncFallbackTest

    <Test>
    Public Async Function FallbackSerializesProviderInstancesAndCancelsQueuedCalls() As Task
        Dim state As New SharedState()
        Dim first As New BlockingProvider(state)
        Dim second As New BlockingProvider(state)
        Dim firstRequest As Task(Of DmsResourceItem) = first.ListRemoteItemAsync("first")

        Try
            Assert.That(state.FirstEntered.Wait(TimeSpan.FromSeconds(5)), [Is].True)
            Using cancellation As New CancellationTokenSource()
                Dim canceledRequest As Task(Of DmsResourceItem) = second.ListRemoteItemAsync("canceled", cancellation.Token)
                cancellation.Cancel()
                Assert.ThrowsAsync(Of OperationCanceledException)(Async Function() As Task
                                                                       Await canceledRequest
                                                                   End Function)
            End Using

            Dim secondRequest As Task = second.RunProtectedOperationAsync("second")
            Assert.That(state.StartedCount, [Is].EqualTo(1))
            Assert.That(secondRequest.IsCompleted, [Is].False)
            state.ReleaseFirst.Set()
            Await Task.WhenAll(firstRequest, secondRequest)
            Assert.That(state.StartedCount, [Is].EqualTo(2))
            Assert.That(state.MaximumConcurrency, [Is].EqualTo(1))
        Finally
            state.ReleaseFirst.Set()
        End Try
    End Function

    <Test>
    Public Async Function ResourceOverloadsPassTheSelectedItemToTheProvider() As Task
        Dim provider As New IdentityProvider()
        Dim item As New DmsResourceItem With {
            .FullName = "same-name.txt",
            .Name = "same-name.txt",
            .ItemType = DmsResourceItem.ItemTypes.File,
            .ExtendedInfosFileID = "selected-id"
        }

        Await provider.DownloadFileAsync(item, "local.txt")
        Await provider.DeleteRemoteItemAsync(item)

        Assert.That(provider.DownloadedItem, [Is].SameAs(item))
        Assert.That(provider.DeletedItem, [Is].SameAs(item))
    End Function

    <Test>
    Public Async Function CancellationDoesNotAbandonAnActiveSynchronousRequest() As Task
        Dim state As New SharedState()
        Dim provider As New BlockingProvider(state)
        Using cancellation As New CancellationTokenSource()
            Dim request As Task(Of DmsResourceItem) = provider.ListRemoteItemAsync("active", cancellation.Token)
            Try
                Assert.That(state.FirstEntered.Wait(TimeSpan.FromSeconds(5)), [Is].True)
                cancellation.Cancel()
                Assert.That(request.IsCompleted, [Is].False)
                state.ReleaseFirst.Set()
                Assert.That((Await request).FullName, [Is].EqualTo("active"))
            Finally
                state.ReleaseFirst.Set()
            End Try
        End Using
    End Function

    <Test>
    Public Async Function NativeAsyncOverloadsComposeWithoutCallingSynchronousIo() As Task
        Dim provider As New RecordingNativeProvider()
        Await provider.CreateFolderAsync("parent/child", True)
        Await provider.UploadFileAsync("parent/child/file.txt", New Byte() {1, 2}, False)

        Assert.That(provider.CreatedFolders, [Is].EqualTo({"parent", "parent/child"}))
        Assert.That(provider.UploadedPath, [Is].EqualTo("parent/child/file.txt"))
        Assert.That(provider.UploadedLength, [Is].EqualTo(2))
    End Function

    Private NotInheritable Class SharedState
        Friend ReadOnly FirstEntered As New ManualResetEventSlim(False)
        Friend ReadOnly ReleaseFirst As New ManualResetEventSlim(False)
        Friend StartedCount As Integer
        Friend ActiveCount As Integer
        Friend MaximumConcurrency As Integer = 1
    End Class

    Private NotInheritable Class BlockingProvider
        Inherits NoDmsProvider

        Private ReadOnly state As SharedState

        Friend Sub New(state As SharedState)
            Me.state = state
        End Sub

        Friend Function RunProtectedOperationAsync(remotePath As String) As Task
            Return Me.RunSynchronousFallbackAsync(Sub() Me.ListRemoteItem(remotePath), CancellationToken.None)
        End Function

        Public Overrides Function ListRemoteItem(remotePath As String) As DmsResourceItem
            Dim active = Interlocked.Increment(state.ActiveCount)
            Try
                If active > 1 Then Interlocked.Exchange(state.MaximumConcurrency, active)
                Dim started = Interlocked.Increment(state.StartedCount)
                If started = 1 Then
                    state.FirstEntered.Set()
                    If Not state.ReleaseFirst.Wait(TimeSpan.FromSeconds(10)) Then Throw New TimeoutException("The first fallback request was not released.")
                End If
                Return New DmsResourceItem With {.FullName = remotePath, .Name = remotePath, .ItemType = DmsResourceItem.ItemTypes.File}
            Finally
                Interlocked.Decrement(state.ActiveCount)
            End Try
        End Function
    End Class

    Private NotInheritable Class IdentityProvider
        Inherits NoDmsProvider

        Friend DownloadedItem As DmsResourceItem
        Friend DeletedItem As DmsResourceItem

        Public Overrides Sub DownloadFile(remoteFile As DmsResourceItem, localFilePath As String)
            DownloadedItem = remoteFile
        End Sub

        Public Overrides Sub DeleteRemoteItem(remoteItem As DmsResourceItem)
            DeletedItem = remoteItem
        End Sub
    End Class

    Private NotInheritable Class RecordingNativeProvider
        Inherits NoDmsProvider

        Friend ReadOnly CreatedFolders As New List(Of String)
        Friend UploadedPath As String
        Friend UploadedLength As Long

        Public Overrides ReadOnly Property SupportsAsynchronousIo As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides Function ListRemoteItemAsync(remotePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            cancellationToken.ThrowIfCancellationRequested()
            If remotePath = "" OrElse CreatedFolders.Contains(remotePath) Then
                Return Task.FromResult(New DmsResourceItem With {.FullName = remotePath, .ItemType = DmsResourceItem.ItemTypes.Folder})
            End If
            Return Task.FromResult(CType(Nothing, DmsResourceItem))
        End Function

        Public Overrides Function CreateFolderAsync(remoteDirectoryPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            cancellationToken.ThrowIfCancellationRequested()
            CreatedFolders.Add(remoteDirectoryPath)
            Return Task.CompletedTask
        End Function

        Public Overrides Function UploadFileAsync(remoteFilePath As String, binaryData As Func(Of System.IO.Stream), Optional cancellationToken As CancellationToken = Nothing) As Task
            cancellationToken.ThrowIfCancellationRequested()
            UploadedPath = remoteFilePath
            Using input = binaryData()
                UploadedLength = input.Length
            End Using
            Return Task.CompletedTask
        End Function
    End Class
End Class
