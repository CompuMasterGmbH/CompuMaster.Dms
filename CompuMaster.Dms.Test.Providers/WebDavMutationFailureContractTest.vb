Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class WebDavMutationFailureContractTest
    <Test>
    Public Sub SingleItemListingRetainsTheSameHttpDiagnosticAcrossSyncAndAsync()
        Using handler As New StatusHandler(500), client As New HttpClient(handler)
            Dim provider As New WebDavDmsProvider With {.CustomWebApiUrl = "https://example.test/"}
            GetType(WebDavDmsProvider).GetField("WebDavClient", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(provider, New WebDav.WebDavClient(client))
            Dim synchronous = Assert.Throws(Of InvalidOperationException)(Sub() provider.ListRemoteItem("source.txt"))
            Dim asynchronous = Assert.Throws(Of InvalidOperationException)(Sub() provider.ListRemoteItemAsync("source.txt").GetAwaiter().GetResult())
            Assert.That(synchronous.Message, [Is].EqualTo(asynchronous.Message))
            Assert.That(synchronous.InnerException, [Is].TypeOf(Of ResponseStatusCodeException)())
            Assert.That(CType(synchronous.InnerException, ResponseStatusCodeException).StatusCode, [Is].EqualTo(500))
            Assert.That(CType(asynchronous.InnerException, ResponseStatusCodeException).StatusCode, [Is].EqualTo(500))
        End Using
    End Sub
    <TestCase(False), TestCase(True)>
    Public Sub PreconditionFailureExplainsThatTheSourceStillExists(asynchronous As Boolean)
        Using handler As New StatusHandler(412), client As New HttpClient(handler)
            Dim provider As New FixtureProvider(client)
            Dim failure = Assert.Throws(Of System.IO.IOException)(Sub() provider.CopyFixture(asynchronous))
            Assert.That(CType(failure.InnerException, ResponseStatusCodeException).StatusCode, [Is].EqualTo(412))
            Dim expected = Global.CompuMaster.Dms.ProviderStrings.Format("ButRemoteItemExists", Global.CompuMaster.Dms.ProviderStrings.GetText("CopyTaskFailed"), "source.txt")
            Assert.That(failure.Message, [Is].EqualTo(expected))
        End Using
    End Sub

    Private Class FixtureProvider
        Inherits WebDavDmsProvider
        Public ListCalls As Integer
        Public Sub New(client As HttpClient)
            CustomWebApiUrl = "https://example.test/"
            GetType(WebDavDmsProvider).GetField("WebDavClient", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, New WebDav.WebDavClient(client))
        End Sub
        Public Overrides Function ListRemoteItem(path As String) As DmsResourceItem
            ListCalls += 1
            If String.IsNullOrEmpty(path) Then Return New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Root, .FullName = ""}
            If path = "source.txt" Then Return New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .FullName = path}
            Return Nothing
        End Function
        Public Overrides Function ListRemoteItemAsync(path As String, Optional token As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            Return Task.FromResult(ListRemoteItem(path))
        End Function
        Public Sub CopyFixture(asynchronous As Boolean)
            If asynchronous Then
                Me.CopyFileItemAsync("source.txt", "destination.txt", False).GetAwaiter().GetResult()
            Else
                Me.CopyFileItem("source.txt", "destination.txt", False)
            End If
        End Sub
    End Class

    Private Class StatusHandler
        Inherits HttpMessageHandler
        Private ReadOnly Status As Integer
        Public Sub New(value As Integer)
            Status = value
        End Sub
        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Return Task.FromResult(New HttpResponseMessage(CType(Status, HttpStatusCode)) With {.ReasonPhrase = "fixture diagnostic", .Content = New StringContent("")})
        End Function
    End Class
End Class
