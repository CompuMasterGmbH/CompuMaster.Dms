Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class SelectedReplacementContractTest
    <TestCase(False), TestCase(True)>
    Public Async Function DefaultReplacementRetainsUniquePathOverridesAndRejectsUnboundDuplicates(nonUnique As Boolean) As Task
        Dim provider As New ReplacementProvider With {.NonUnique = nonUnique}
        Dim resource As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .FullName = "folder/file", .ExtendedInfosFileID = "selected"}
        Using cancellation As New CancellationTokenSource()
            If nonUnique Then
                Assert.Throws(Of NotSupportedException)(Sub() provider.ReplaceFileWithProgressAsync(resource, "local", Nothing, cancellation.Token))
                Assert.That(provider.Calls, [Is].Zero)
            Else
                Await provider.ReplaceFileWithProgressAsync(resource, "local", Nothing, cancellation.Token)
                Assert.That(provider.Calls, [Is].EqualTo(1))
                Assert.That(provider.Path, [Is].EqualTo(resource.FullName))
                Assert.That(provider.Token, [Is].EqualTo(cancellation.Token))
            End If
        End Using
    End Function

    <Test>
    Public Sub InvalidReplacementResourcesAreRejectedBeforeAnyProviderWrite()
        Dim provider As New ReplacementProvider()
        Assert.Throws(Of ArgumentNullException)(Sub() provider.ReplaceFileWithProgressAsync(Nothing, "local", Nothing))
        Assert.Throws(Of ArgumentException)(Sub() provider.ReplaceFileWithProgressAsync(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .FullName = "folder"}, "local", Nothing))
        Assert.Throws(Of ArgumentException)(Sub() provider.ReplaceFileWithProgressAsync(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File}, "local", Nothing))
        Assert.That(provider.Calls, [Is].Zero)
    End Sub

    <Test>
    Public Sub PreCancelledSelectedCenterDeviceReplacementDoesNotStartIdentityLookup()
        Using cancellation As New CancellationTokenSource()
            cancellation.Cancel()
            Dim provider As New ScopevisioTeamworkDmsProvider()
            Assert.CatchAsync(Of OperationCanceledException)(Function() provider.ReplaceFileWithProgressAsync(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .ExtendedInfosFileID = "selected"}, "unused", Nothing, cancellation.Token))
        End Using
    End Sub

    Private Class ReplacementProvider
        Inherits NoDmsProvider
        Friend NonUnique As Boolean
        Friend Calls As Integer
        Friend Path As String
        Friend Token As CancellationToken
        Public Overrides ReadOnly Property SupportsNonUniqueRemoteItems As Boolean
            Get
                Return NonUnique
            End Get
        End Property
        Public Overrides Function UploadFileWithProgressAsync(remoteFilePath As String, localFilePath As String, progress As IProgress(Of DmsTransferProgress), Optional cancellationToken As CancellationToken = Nothing) As Task
            Calls += 1
            Path = remoteFilePath
            Token = cancellationToken
            Return Task.CompletedTask
        End Function
    End Class
End Class
