Option Explicit On
Option Strict On

Imports System.Net
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest
Imports CenterDevice.Rest.Clients.OAuth
Imports CenterDevice.Rest.Clients.User
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports RestSharp

<TestFixture>
Public Class CenterDeviceNativeUserMetadataTest
    <TestCase("{""id"":""user-id"",""email"":""fixture@example.invalid"",""first-name"":""Ada"",""last-name"":""Lovelace""}", "Ada Lovelace")>
    <TestCase("{""id"":""user-id"",""email"":""fixture@example.invalid"",""firstName"":""Ada"",""lastName"":""Lovelace""}", "Ada Lovelace")>
    <TestCase("{""id"":""user-id"",""email"":""fixture@example.invalid"",""first-name"":""Ada"",""firstName"":""Old"",""last-name"":""Lovelace""}", "Ada Lovelace")>
    <TestCase("{""id"":""user-id"",""email"":""fixture@example.invalid"",""first-name"":null,""last-name"":null}", "")>
    Public Sub NativeReadMappingRetainsHyphenatedAndExistingSdkFields(json As String, expected As String)
        Dim metadata = JsonSerializer.Deserialize(Of NativeUserNameMetadata)(json, New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True})
        metadata.ApplyApiNameFields()
        Assert.That(metadata.GetFullName().Trim(), [Is].EqualTo(expected))
        Assert.That(metadata.Id, [Is].EqualTo("user-id"))
        Assert.That(metadata.Email, [Is].EqualTo("fixture@example.invalid"))
        If json.Contains("""first-name"":""Ada""") AndAlso Not json.Contains("""firstName""") Then
            Assert.That(JsonSerializer.Deserialize(Of BaseUserData)(json, New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}).GetFullName().Trim(), [Is].Empty, "The SDK baseline must reproduce the unmapped server-name regression.")
        End If
    End Sub

    <Test>
    Public Async Function UserListingUsesNativeAuthorizationAndRequestAndMapsNames() As Task
        Dim auth As New NativeAuthorization()
        Dim client As New MetadataClient(auth)
        Dim users = Await client.GetUsersAsync("context-user", CancellationToken.None)
        Assert.That(users.Users(0).GetFullName().Trim(), [Is].EqualTo("Ada Lovelace"))
        Assert.That(auth.Calls, [Is].EqualTo(1))
        Assert.That(client.Request.Resource, [Is].EqualTo("v2/users/"))
        Assert.That(client.Request.Parameters.Any(Function(parameter) parameter.Name = "status" AndAlso CStr(parameter.Value) = "active"), [Is].True)
        Assert.That(client.Request.Method, [Is].EqualTo(Method.Get))
    End Function

    <Test>
    Public Async Function NoContentReturnsAnInitializedEmptyList() As Task
        Dim client As New MetadataClient(New NativeAuthorization()) With {.Status = HttpStatusCode.NoContent}
        Assert.That((Await client.GetUsersAsync("context-user", CancellationToken.None)).Users, [Is].Empty)
    End Function

    <Test>
    Public Sub MissingUserArrayIsDiagnosed()
        Dim client As New MetadataClient(New NativeAuthorization()) With {.MissingUsers = True}
        Assert.ThrowsAsync(Of InvalidOperationException)(CType(Async Function()
                                                                   Await client.GetUsersAsync("context-user", CancellationToken.None)
                                                               End Function, Func(Of Task)))
    End Sub

    <Test>
    Public Async Function NativeUserListingCancelsActiveExecution() As Task
        Dim client As New MetadataClient(New NativeAuthorization()) With {.Block = True}
        Using cancellation As New CancellationTokenSource()
            Dim pending = client.GetUsersAsync("context-user", cancellation.Token)
            Assert.That(Await Task.WhenAny(client.Entered.Task, Task.Delay(3000)), [Is].SameAs(client.Entered.Task))
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                         Await pending
                                                                     End Function, Func(Of Task)))
        End Using
    End Function

    <Test>
    Public Sub PreCancellationPreventsAuthorizationAndDispatch()
        Dim auth As New NativeAuthorization()
        Dim client As New MetadataClient(auth)
        Using cancellation As New CancellationTokenSource()
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                         Await client.GetUsersAsync("context-user", cancellation.Token)
                                                                     End Function, Func(Of Task)))
            Assert.That(auth.Calls, [Is].Zero)
            Assert.That(client.Request, [Is].Null)
        End Using
    End Sub

    Private Class MetadataClient
        Inherits CenterDeviceUserMetadataClient
        Public Status As HttpStatusCode = HttpStatusCode.OK
        Public MissingUsers, Block As Boolean
        Public Request As RestRequest
        Public ReadOnly Entered As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Public Sub New(auth As IOAuthInfoProvider)
            MyBase.New(auth, New FixtureConfiguration(), Nothing, "v2/")
        End Sub
        Protected Overrides Function Execute(Of T As New)(info As OAuthInfo, request As RestRequest) As RestResponse(Of T)
            Throw New AssertionException("Synchronous execution is forbidden.")
        End Function
        Protected Overrides Async Function ExecuteAsync(Of T As New)(info As OAuthInfo, request As RestRequest, Optional cancellationToken As CancellationToken = Nothing) As Task(Of RestResponse(Of T))
            Me.Request = request
            Entered.TrySetResult(True)
            If Block Then Await Task.Delay(Timeout.Infinite, cancellationToken)
            Dim data As New UserList(Of NativeUserNameMetadata)()
            If Not MissingUsers Then data.Users = New List(Of NativeUserNameMetadata) From {New NativeUserNameMetadata With {.Id = "user-id", .ApiFirstName = "Ada", .ApiLastName = "Lovelace"}}
            Return New RestResponse(Of T)(request) With {.StatusCode = Status, .Data = CType(CObj(data), T)}
        End Function
    End Class

    Private Class NativeAuthorization
        Implements IOAuthInfoProvider, IAsyncOAuthInfoProvider
        Public Calls As Integer
        Public Function GetOAuthInfo(id As String) As OAuthInfo Implements IOAuthInfoProvider.GetOAuthInfo
            Throw New AssertionException("Synchronous authorization is forbidden.")
        End Function
        Public Function GetOAuthInfoAsync(id As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of OAuthInfo) Implements IAsyncOAuthInfoProvider.GetOAuthInfoAsync
            cancellationToken.ThrowIfCancellationRequested()
            Calls += 1
            Return Task.FromResult(New OAuthInfo With {.UserId = id, .access_token = "fixture-token"})
        End Function
    End Class

    Private Class FixtureConfiguration
        Implements IRestClientConfiguration
        Public ReadOnly Property BaseAddress As String = "https://fixture.invalid/" Implements IRestClientConfiguration.BaseAddress
        Public ReadOnly Property UserAgent As String = "native-user-metadata-fixture" Implements IRestClientConfiguration.UserAgent
    End Class
End Class
