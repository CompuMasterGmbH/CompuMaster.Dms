Option Strict On

Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class AsyncFileAndDirectoryActionsTest

    <Test>
    Public Async Function CopyMergeKeepsDestinationOnlyFiles() As Task
        Dim provider As RecordingAsyncProvider = CreateProvider()
        Await provider.CopyAsync(provider.Items("source"), "target", True, False, CancellationToken.None)

        Assert.That(provider.CopiedFiles, [Is].EquivalentTo({"source/replaced.txt->target/replaced.txt", "source/new.txt->target/new.txt"}))
        Assert.That(provider.Items.ContainsKey("target/keep.txt"), [Is].True)
        Assert.That(provider.DeletedDirectories, [Is].Empty)
    End Function

    <Test>
    Public Async Function MoveMergeDeletesSourceAfterMovingChildren() As Task
        Dim provider As RecordingAsyncProvider = CreateProvider()
        Await provider.MoveAsync(provider.Items("source"), "target", True, False, CancellationToken.None)

        Assert.That(provider.MovedFiles, [Is].EquivalentTo({"source/replaced.txt->target/replaced.txt", "source/new.txt->target/new.txt"}))
        Assert.That(provider.Items.ContainsKey("target/keep.txt"), [Is].True)
        Assert.That(provider.DeletedDirectories, [Is].EqualTo({"source"}))
    End Function

    <Test>
    Public Sub MoveMergeKeepsSourceDirectoryAfterChildFailure()
        Dim provider As RecordingAsyncProvider = CreateProvider()
        provider.FailMoveSourcePath = "source/new.txt"
        Assert.ThrowsAsync(Of InvalidOperationException)(Async Function() As Task
                                                             Await provider.MoveAsync(provider.Items("source"), "target", True, False, CancellationToken.None)
                                                         End Function)
        Assert.That(provider.DeletedDirectories, [Is].Empty)
        Assert.That(provider.Items.ContainsKey("source"), [Is].True)
    End Sub

    <Test>
    Public Sub CancellationStopsBeforeRemoteRequests()
        Dim provider As RecordingAsyncProvider = CreateProvider()
        Using cancellation As New CancellationTokenSource
            cancellation.Cancel()
            Assert.ThrowsAsync(Of OperationCanceledException)(Async Function() As Task
                                                                   Await provider.CopyAsync(provider.Items("source"), "target", True, False, cancellation.Token)
                                                               End Function)
        End Using
        Assert.That(provider.CopiedFiles, [Is].Empty)
    End Sub

    Private Shared Function CreateProvider() As RecordingAsyncProvider
        Dim provider As New RecordingAsyncProvider
        For Each path As String In {"source", "target"}
            provider.Items.Add(path, CreateItem(path, DmsResourceItem.ItemTypes.Folder))
        Next
        For Each path As String In {"source/replaced.txt", "source/new.txt", "target/replaced.txt", "target/keep.txt"}
            provider.Items.Add(path, CreateItem(path, DmsResourceItem.ItemTypes.File))
        Next
        Return provider
    End Function

    Private Shared Function CreateItem(path As String, itemType As DmsResourceItem.ItemTypes) As DmsResourceItem
        Return New DmsResourceItem With {.FullName = path, .Name = path.Substring(path.LastIndexOf("/"c) + 1), .ItemType = itemType}
    End Function

    Private NotInheritable Class RecordingAsyncProvider
        Inherits NoDmsProvider

        Friend ReadOnly Items As New Dictionary(Of String, DmsResourceItem)(StringComparer.Ordinal)
        Friend ReadOnly CopiedFiles As New List(Of String)
        Friend ReadOnly MovedFiles As New List(Of String)
        Friend ReadOnly DeletedDirectories As New List(Of String)
        Friend Property FailMoveSourcePath As String

        Public Overrides ReadOnly Property SupportsAsynchronousIo As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides Function ListRemoteItemAsync(remotePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            cancellationToken.ThrowIfCancellationRequested()
            Dim item As DmsResourceItem = Nothing
            Items.TryGetValue(remotePath, item)
            Return Task.FromResult(item)
        End Function

        Public Overrides Function ListAllRemoteItemsAsync(remoteFolderPath As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            cancellationToken.ThrowIfCancellationRequested()
            Dim result As New List(Of DmsResourceItem)
            For Each item In Items.Values
                If Me.ParentDirectoryPath(item.FullName) = remoteFolderPath Then result.Add(item)
            Next
            Return Task.FromResult(result)
        End Function

        Public Overrides Sub ResetCachesForRemoteItems(remoteFolderPath As String, searchType As SearchItemType)
        End Sub

        Protected Overrides Function CopyFileItemAsync(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?) As Task
            CopiedFiles.Add(remoteSourcePath & "->" & remoteDestinationPath)
            Items(remoteDestinationPath) = CreateItem(remoteDestinationPath, DmsResourceItem.ItemTypes.File)
            Return Task.CompletedTask
        End Function

        Protected Overrides Function MoveFileItemAsync(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?, cancellationToken As CancellationToken) As Task
            cancellationToken.ThrowIfCancellationRequested()
            If remoteSourcePath = FailMoveSourcePath Then Throw New InvalidOperationException("Simulated move failure.")
            MovedFiles.Add(remoteSourcePath & "->" & remoteDestinationPath)
            Items.Remove(remoteSourcePath)
            Items(remoteDestinationPath) = CreateItem(remoteDestinationPath, DmsResourceItem.ItemTypes.File)
            Return Task.CompletedTask
        End Function

        Public Overrides Function DeleteRemoteItemAsync(remotePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            cancellationToken.ThrowIfCancellationRequested()
            DeletedDirectories.Add(remotePath)
            Items.Remove(remotePath)
            Return Task.CompletedTask
        End Function
    End Class
End Class
