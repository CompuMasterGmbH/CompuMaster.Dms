Option Explicit On
Option Strict On

Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest.Clients.Collections
Imports CenterDevice.Rest.Clients.Documents.Metadata
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class CenterDeviceNativeTransferTest
    <TestCase(False, "bytes"), TestCase(True, "bytes"), TestCase(False, "factory"), TestCase(True, "factory"), TestCase(False, "file"), TestCase(True, "file"), TestCase(False, "progress"), TestCase(True, "progress")>
    Public Async Function UploadPreservesVersionSelectionAndInvalidatesTheCache(existing As Boolean, input As String) As Task
        Dim provider As New TransferProvider(existing)
        Dim localPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        Dim payload = New Byte() {1, 2, 3, 4}
        Dim source As MemoryStream = Nothing
        Try
            Using cancellation As New CancellationTokenSource()
                Select Case input
                    Case "bytes"
                        Await provider.UploadFileAsync("collection/fixture.bin", payload, cancellation.Token)
                    Case "factory"
                        Await provider.UploadFileAsync("collection/fixture.bin", Function()
                                                                                    source = New MemoryStream(payload, False)
                                                                                    Return source
                                                                                End Function, cancellation.Token)
                        Assert.That(source.CanRead, [Is].False, "The upload client owns the stream.")
                    Case Else
                        File.WriteAllBytes(localPath, payload)
                        Dim modified As New DateTime(2024, 2, 3, 4, 5, 6, DateTimeKind.Utc)
                        File.SetLastWriteTimeUtc(localPath, modified)
                        If input = "progress" Then
                            Dim progress As New DownloadCollector
                            Await provider.UploadFileWithProgressAsync("collection/fixture.bin", localPath, progress, cancellation.Token)
                            Assert.That(progress.Values.Last().Phase, [Is].EqualTo(DmsTransferPhase.Completed))
                        Else
                            Await provider.UploadFileAsync("collection/fixture.bin", localPath, cancellation.Token)
                        End If
                        Assert.That(provider.ModificationTime, [Is].EqualTo(modified))
                End Select
                If input = "factory" OrElse input = "bytes" Then Assert.That(provider.ModificationTime, [Is].Null, "Stream uploads retain the existing date default and hook.")
                Assert.That(provider.UploadedBytes, [Is].EqualTo(payload))
                Assert.That(provider.VersionId, [Is].EqualTo(If(existing, "selected-id", Nothing)))
                Assert.That(provider.UploadName, [Is].EqualTo("fixture.bin"))
                Assert.That(provider.RequestToken, [Is].EqualTo(cancellation.Token))
                Assert.That(provider.Io.FileCalls, [Is].EqualTo(1))
                Await provider.ListAllFileNamesAsync("collection")
                Assert.That(provider.Io.FileCalls, [Is].EqualTo(2), "Completed upload invalidates the selected parent cache.")
            End Using
        Finally
            If File.Exists(localPath) Then File.Delete(localPath)
        End Try
    End Function

    <TestCase(False, False), TestCase(True, False), TestCase(False, True), TestCase(True, True)>
    Public Async Function DownloadStreamsToDiskAndPreservesSelectedIdentity(useId As Boolean, withProgress As Boolean) As Task
        Dim provider As New TransferProvider(False)
        Dim localPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        Dim timestamp = New DateTime(2025, 1, 2, 3, 4, 6, DateTimeKind.Local)
        Try
            Using cancellation As New CancellationTokenSource()
                If withProgress Then
                    Dim item As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .FullName = "collection/fixture.bin", .ExtendedInfosFileID = If(useId, "second-id", Nothing), .LastModificationOnLocalTime = timestamp}
                    Dim progress As New DownloadCollector()
                    Await provider.DownloadFileWithProgressAsync(item, localPath, progress, cancellation.Token)
                    Assert.That(provider.RequestId, [Is].EqualTo(If(useId, "second-id", Nothing)))
                    Assert.That(provider.RequestPath, [Is].EqualTo(If(useId, Nothing, "collection/fixture.bin")))
                    Assert.That(progress.Values.Last().BytesTransferred, [Is].EqualTo(200000))
                    Assert.That(progress.Values.Last().Phase, [Is].EqualTo(DmsTransferPhase.Completed))
                ElseIf useId Then
                    Dim item As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .FullName = "collection/duplicate", .ExtendedInfosFileID = "second-id", .ExtendedInfosCollisionDetected = True, .LastModificationOnLocalTime = timestamp}
                    Await provider.DownloadFileAsync(item, localPath, cancellation.Token)
                    Assert.That(provider.RequestId, [Is].EqualTo("second-id"))
                    Assert.That(provider.RequestPath, [Is].Null)
                Else
                    Await provider.DownloadFileAsync("collection/fixture.bin", localPath, timestamp, cancellation.Token)
                    Assert.That(provider.RequestPath, [Is].EqualTo("collection/fixture.bin"))
                    Assert.That(provider.RequestId, [Is].Null)
                End If
                Assert.That(New FileInfo(localPath).Length, [Is].EqualTo(200000))
                Assert.That(File.GetLastWriteTime(localPath), [Is].EqualTo(timestamp).Within(TimeSpan.FromSeconds(1)))
                Assert.That(provider.DownloadStream.Disposed, [Is].True)
                Assert.That(provider.DownloadStream.LargestRead, [Is].LessThanOrEqualTo(131072))
                Assert.That(provider.RequestToken, [Is].EqualTo(cancellation.Token))
            End Using
        Finally
            If File.Exists(localPath) Then File.Delete(localPath)
        End Try
    End Function

    <TestCase(False), TestCase(True)>
    Public Async Function UncertainUploadFailureInvalidatesCacheWithoutReplaying(cancel As Boolean) As Task
        Dim provider As New TransferProvider(True) With {.FailUpload = Not cancel, .BlockUpload = cancel}
        Dim source As MemoryStream = Nothing
        Using cancellation As New CancellationTokenSource()
            Dim operation = provider.UploadFileAsync("collection/fixture.bin", Function()
                                                                                  source = New MemoryStream(New Byte() {4, 5, 6})
                                                                                  Return source
                                                                              End Function, cancellation.Token)
            If cancel Then
                Await provider.UploadEntered.Task
                cancellation.Cancel()
                Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                         Await operation
                                                                     End Function, Func(Of Task)))
            Else
                Assert.ThrowsAsync(Of IOException)(CType(Async Function()
                                                            Await operation
                                                        End Function, Func(Of Task)))
            End If
            Assert.That(source.CanRead, [Is].False)
            Assert.That(provider.UploadCalls, [Is].EqualTo(1))
            Await provider.ListAllFileNamesAsync("collection")
            Assert.That(provider.Io.FileCalls, [Is].EqualTo(2))
        End Using
    End Function

    <TestCase(False, False), TestCase(True, False), TestCase(False, True), TestCase(True, True)>
    Public Async Function DownloadFailureOrCancellationDisposesTheStream(cancel As Boolean, withProgress As Boolean) As Task
        Dim provider As New TransferProvider(False) With {.FailDownload = Not cancel, .BlockDownload = cancel}
        Dim localPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        Try
            Using cancellation As New CancellationTokenSource()
                Dim progress As New DownloadCollector()
                Dim operation = If(withProgress, provider.DownloadFileWithProgressAsync(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .FullName = "collection/fixture.bin", .LastModificationOnLocalTime = New DateTime(2001, 1, 1)}, localPath, progress, cancellation.Token), provider.DownloadFileAsync("collection/fixture.bin", localPath, New DateTime(2001, 1, 1), cancellation.Token))
                If cancel Then
                    Await provider.DownloadStream.Entered.Task
                    cancellation.Cancel()
                    Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                             Await operation
                                                                         End Function, Func(Of Task)))
                Else
                    Assert.ThrowsAsync(Of IOException)(CType(Async Function()
                                                                Await operation
                                                            End Function, Func(Of Task)))
                End If
                Assert.That(provider.DownloadStream.Disposed, [Is].True)
                Assert.That(progress.Values.Any(Function(value) value.Phase = DmsTransferPhase.Completed), [Is].False)
                If File.Exists(localPath) Then Assert.That(File.GetLastWriteTime(localPath).Year, [Is].Not.EqualTo(2001))
            End Using
        Finally
            If File.Exists(localPath) Then File.Delete(localPath)
        End Try
    End Function

    <Test>
    Public Sub PreCanceledTransferDoesNotOpenAStreamOrStartALookup()
        Dim provider As New TransferProvider(False)
        Dim factoryCalls As Integer
        Using cancellation As New CancellationTokenSource()
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                     Await provider.UploadFileAsync("collection/fixture.bin", Function()
                                                                                                                                  factoryCalls += 1
                                                                                                                                  Return New MemoryStream()
                                                                                                                              End Function, cancellation.Token)
                                                                 End Function, Func(Of Task)))
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                     Await provider.DownloadFileAsync("collection/fixture.bin", "unused", Nothing, cancellation.Token)
                                                                 End Function, Func(Of Task)))
            Assert.That(factoryCalls, [Is].Zero)
            Assert.That(provider.Io.FileCalls, [Is].Zero)
            Assert.That(provider.RequestPath, [Is].Null)
        End Using
    End Sub

    Private Class TransferProvider
        Inherits ScopevisioTeamworkDmsProvider
        Public ReadOnly Io As FixtureIo
        Public VersionId As String
        Public UploadName As String
        Public UploadedBytes As Byte()
        Public ModificationTime As DateTime?
        Public RequestId As String
        Public RequestPath As String
        Public RequestToken As CancellationToken
        Public UploadCalls As Integer
        Public FailUpload As Boolean
        Public BlockUpload As Boolean
        Public FailDownload As Boolean
        Public BlockDownload As Boolean
        Public DownloadStream As FixtureStream
        Public ReadOnly UploadEntered As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Public Sub New(existing As Boolean)
            Io = New FixtureIo(existing)
            Me.IOClient = Io
        End Sub
        Protected Overrides Async Function UploadNativeFileAsync(parent As Global.CenterDevice.IO.DirectoryInfo, existingFile As Global.CenterDevice.IO.FileInfo, fileName As String, binaryData As Func(Of Stream), cancellationToken As CancellationToken) As Task
            UploadCalls += 1
            VersionId = existingFile?.ID
            UploadName = fileName
            RequestToken = cancellationToken
            Using source = binaryData(), target As New MemoryStream()
                UploadEntered.TrySetResult(True)
                If BlockUpload Then Await Task.Delay(Timeout.Infinite, cancellationToken)
                If FailUpload Then Throw New IOException("Uncertain upload result.")
                Await source.CopyToAsync(target, 81920, cancellationToken)
                UploadedBytes = target.ToArray()
            End Using
        End Function
        Protected Overrides Function UploadNativeFileAsync(parent As Global.CenterDevice.IO.DirectoryInfo, existingFile As Global.CenterDevice.IO.FileInfo, fileName As String, binaryData As Func(Of Stream), modificationTimeUtc As DateTime?, cancellationToken As CancellationToken) As Task
            ModificationTime = modificationTimeUtc
            Return MyBase.UploadNativeFileAsync(parent, existingFile, fileName, binaryData, modificationTimeUtc, cancellationToken)
        End Function
        Private Function CreateDownloadFile(token As CancellationToken) As Global.CenterDevice.IO.FileInfo
            RequestToken = token
            DownloadStream = New FixtureStream With {.Block = BlockDownload, .Fail = FailDownload}
            Return New DownloadFile(Io, DownloadStream)
        End Function
        Protected Overrides Function GetNativeDownloadFileAsync(remoteFilePath As String, cancellationToken As CancellationToken) As Task(Of Global.CenterDevice.IO.FileInfo)
            RequestPath = remoteFilePath
            Return Task.FromResult(CreateDownloadFile(cancellationToken))
        End Function
        Protected Overrides Function GetNativeDownloadFileByIdAsync(id As String, cancellationToken As CancellationToken) As Task(Of Global.CenterDevice.IO.FileInfo)
            RequestId = id
            Return Task.FromResult(CreateDownloadFile(cancellationToken))
        End Function
    End Class

    Private Class FixtureIo
        Inherits Global.CenterDevice.IO.IOClientBase
        Private ReadOnly Existing As Boolean
        Public FileCalls As Integer
        Public Sub New(existing As Boolean)
            MyBase.New(Nothing, "fixture-user")
            Me.Existing = existing
        End Sub
        Protected Overrides Function LookupCollectionsAsync(cancellationToken As CancellationToken) As Task(Of List(Of Collection))
            Return Task.FromResult(New List(Of Collection) From {New Collection With {.Id = "collection-id", .Name = "collection"}})
        End Function
        Protected Overrides Function LookupChildDocumentsAsync(collectionId As String, parentId As String, cancellationToken As CancellationToken) As Task(Of List(Of DocumentFullMetadata))
            FileCalls += 1
            Return Task.FromResult(If(Existing, New List(Of DocumentFullMetadata) From {New DocumentFullMetadata With {.Id = "selected-id", .Filename = "fixture.bin"}}, New List(Of DocumentFullMetadata)()))
        End Function
    End Class

    Private Class DownloadFile
        Inherits Global.CenterDevice.IO.FileInfo
        Private ReadOnly Source As Stream
        Public Sub New(io As FixtureIo, source As Stream)
            MyBase.New(io, Nothing, New DocumentFullMetadata With {.Id = "download-id", .Filename = "fixture.bin", .Size = 200000, .DocumentDate = New DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)})
            Me.Source = source
        End Sub
        Public Overrides Function DownloadAsync(Optional version As Long = 0, Optional cancellationToken As CancellationToken = Nothing) As Task(Of Stream)
            Return Task.FromResult(Source)
        End Function
    End Class

    Private Class FixtureStream
        Inherits Stream
        Public Block As Boolean
        Public Fail As Boolean
        Public Disposed As Boolean
        Public LargestRead As Integer
        Private ReadCount As Integer
        Public ReadOnly Entered As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Public Overrides Async Function ReadAsync(buffer As Byte(), offset As Integer, count As Integer, token As CancellationToken) As Task(Of Integer)
            Entered.TrySetResult(True)
            If Block Then Await Task.Delay(Timeout.Infinite, token)
            If Fail Then Throw New IOException("Interrupted download.")
            token.ThrowIfCancellationRequested()
            LargestRead = Math.Max(LargestRead, count)
            Dim result = Math.Min(count, 200000 - ReadCount)
            Array.Clear(buffer, offset, result)
            ReadCount += result
            Return result
        End Function
        Protected Overrides Sub Dispose(disposing As Boolean)
            Disposed = True
            MyBase.Dispose(disposing)
        End Sub
        Public Overrides ReadOnly Property CanRead As Boolean = True
        Public Overrides ReadOnly Property CanSeek As Boolean = False
        Public Overrides ReadOnly Property CanWrite As Boolean = False
        Public Overrides ReadOnly Property Length As Long = 200000
        Public Overrides Property Position As Long
            Get
                Return ReadCount
            End Get
            Set(value As Long)
                Throw New NotSupportedException()
            End Set
        End Property
        Public Overrides Function Read(buffer As Byte(), offset As Integer, count As Integer) As Integer
            Throw New AssertionException("Synchronous stream reads must not be used.")
        End Function
        Public Overrides Sub Flush()
            Throw New NotSupportedException()
        End Sub
        Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
            Throw New NotSupportedException()
        End Function
        Public Overrides Sub SetLength(value As Long)
            Throw New NotSupportedException()
        End Sub
        Public Overrides Sub Write(buffer As Byte(), offset As Integer, count As Integer)
            Throw New NotSupportedException()
        End Sub
    End Class

    Private Class DownloadCollector
        Implements IProgress(Of DmsTransferProgress)
        Friend ReadOnly Values As New List(Of DmsTransferProgress)
        Public Sub Report(value As DmsTransferProgress) Implements IProgress(Of DmsTransferProgress).Report
            Values.Add(value)
        End Sub
    End Class
End Class
