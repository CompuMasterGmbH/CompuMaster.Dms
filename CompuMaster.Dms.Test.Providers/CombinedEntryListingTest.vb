Option Strict On

Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class CombinedEntryListingTest
    <TestCase(""), TestCase("/")>
    Public Async Function RootEntriesDoNotRequestUnsupportedRootFiles(path As String) As Task
        Dim provider As New EntryProvider
        Dim items = Await provider.ListEntriesAsync(path)
        Assert.That(provider.Calls, [Is].EqualTo(New String() {"directories:" & path}))
        Assert.That(items, [Is].EqualTo(provider.Directories))
    End Function
    <Test>
    Public Async Function DefaultWorkflowPreservesDerivedEntryOverridesWithoutFullSnapshotsOrListMutation() As Task
        Dim provider As New EntryProvider
        Dim items = Await provider.ListEntriesAsync("Parent")
        Assert.That(provider.Calls, [Is].EqualTo(New String() {"directories:Parent", "files:Parent"}))
        Assert.That(items, [Is].EqualTo(provider.Directories.Concat(provider.Files)))
        Assert.That(provider.Directories.Count, [Is].EqualTo(1), "Combining results must not modify a provider-owned list.")
        Assert.That(items(0), [Is].SameAs(provider.Directories(0)))
        Assert.That(items(1), [Is].SameAs(provider.Files(0)))
    End Function

    <TestCase(False), TestCase(True)>
    Public Sub CancellationDoesNotDispatchRemainingEntryRequests(afterDirectories As Boolean)
        Dim provider As New EntryProvider
        Using cancellation As New CancellationTokenSource()
            If afterDirectories Then
                provider.AfterDirectories = Sub() cancellation.Cancel()
            Else
                cancellation.Cancel()
            End If
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                       Await provider.ListEntriesAsync("Parent", cancellation.Token)
                                                                   End Function, Func(Of Task)))
            Assert.That(provider.Calls.Count, [Is].EqualTo(If(afterDirectories, 1, 0)))
        End Using
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub ListingFailuresKeepTheirOriginalException(files As Boolean)
        Dim provider As New EntryProvider With {.Failure = New UnauthorizedAccessException("Expected denial"), .FailFiles = files}
        Dim failure = Assert.ThrowsAsync(Of UnauthorizedAccessException)(CType(Async Function()
                                                                                  Await provider.ListEntriesAsync("Parent")
                                                                              End Function, Func(Of Task)))
        Assert.That(failure, [Is].SameAs(provider.Failure))
        Assert.That(provider.Calls.Count, [Is].EqualTo(If(files, 2, 1)))
    End Sub

    Private Class EntryProvider
        Inherits NoDmsProvider
        Public Overrides ReadOnly Property SupportsFilesInRootFolder As Boolean
            Get
                Return False
            End Get
        End Property
        Public ReadOnly Directories As New List(Of DmsResourceItem) From {New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .FullName = "Parent/Folder", .HasChildDirectories = Nothing}}
        Public ReadOnly Files As New List(Of DmsResourceItem) From {New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .FullName = "Parent/File"}}
        Public ReadOnly Calls As New List(Of String)
        Public AfterDirectories As Action
        Public Failure As Exception
        Public FailFiles As Boolean
        Public Overrides Function ListDirectoryEntriesAsync(path As String, Optional token As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Calls.Add("directories:" & path)
            If Failure IsNot Nothing AndAlso Not FailFiles Then Throw Failure
            AfterDirectories?.Invoke()
            Return Task.FromResult(Directories)
        End Function
        Public Overrides Function ListFileEntriesAsync(path As String, Optional token As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Calls.Add("files:" & path)
            If Failure IsNot Nothing AndAlso FailFiles Then Throw Failure
            Return Task.FromResult(Files)
        End Function
    End Class
End Class
