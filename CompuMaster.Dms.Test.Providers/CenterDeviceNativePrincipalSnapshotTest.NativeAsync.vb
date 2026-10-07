Option Explicit On
Option Strict On

Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class CenterDeviceNativePrincipalSnapshotTest
    <Test>
    Public Async Function SharedPrincipalIdsAreResolvedOnceWithoutReadingLazyCallbacks() As Task
        Dim provider As New PrincipalProvider()
        Dim first = Item(provider)
        Dim second = Item(provider)
        Dim originalShare = first.ExtendedInfosUserSharings(0)
        Dim originalPermissions = originalShare.AllowedActions.ToArray()
        Await provider.PrepareAsync(New List(Of DmsResourceItem) From {first, second}, CancellationToken.None)
        Assert.That(provider.Events, [Is].EqualTo(New String() {"user:user-id", "group:group-id"}))
        Assert.That(first.ExtendedInfosOwner.DisplayName, [Is].EqualTo("Native User"))
        Assert.That(first.ExtendedInfosLastModificationUser.EMailAddress, [Is].EqualTo("native@example.invalid"))
        Assert.That(first.ExtendedInfosLockedByUser.DisplayName, [Is].EqualTo("Native User"))
        Assert.That(first.ExtendedInfosUserSharings(0).User.DisplayName, [Is].EqualTo("Native User"))
        Assert.That(second.ExtendedInfosGroupSharings(0).Group.DisplayName, [Is].EqualTo("Native Group"))
        Assert.That(first.ExtendedInfosOwner.GetDisplayName, [Is].Null)
        Assert.That(first.ExtendedInfosOwner.GetEMailAddress, [Is].Null)
        Assert.That(first.ExtendedInfosUserSharings(0), [Is].SameAs(originalShare))
        Assert.That(first.ExtendedInfosUserSharings(0).AllowedActions.ToArray(), [Is].EqualTo(originalPermissions))
        Assert.That(first.ExtendedInfosHasHiddenUserSharings, [Is].True)
        Assert.That(first.ExtendedInfosHasHiddenGroupSharings, [Is].True)
    End Function

    <Test>
    Public Async Function EmptyNamesCannotInvokeOldCallbacksAndRetainIds() As Task
        Dim provider As New PrincipalProvider With {.EmptyNames = True}
        Dim itemValue = Item(provider)
        Await provider.PrepareAsync(New List(Of DmsResourceItem) From {itemValue}, CancellationToken.None)
        Assert.That(itemValue.ExtendedInfosOwner.DisplayName, [Is].EqualTo("user-id"))
        Assert.That(itemValue.ExtendedInfosOwner.EMailAddress, [Is].EqualTo(String.Empty))
        Assert.That(itemValue.ExtendedInfosGroupSharings(0).Group.DisplayName, [Is].EqualTo("group-id"))
        Assert.That(itemValue.ExtendedInfosGroupSharings(0).Group.GetName, [Is].Null)
    End Function

    <Test>
    Public Async Function UnknownPrincipalIdsAreNotRequestedAndKeepKnownNames() As Task
        Dim provider As New PrincipalProvider()
        Dim itemValue = Item(provider)
        itemValue.ExtendedInfosOwner = New DmsUser With {.ID = Nothing, .DisplayName = "Known anonymous owner", .GetDisplayName = Function(unusedProvider, unusedId) ThrowLazyUser()}
        itemValue.ExtendedInfosLastModificationUser = Nothing
        itemValue.ExtendedInfosLockedByUser = Nothing
        itemValue.ExtendedInfosUserSharings.Clear()
        itemValue.ExtendedInfosGroupSharings(0).Group = New DmsGroup With {.ID = Nothing, .Name = "Known anonymous group", .GetName = Function(unusedProvider, unusedId) ThrowLazyUser()}
        Await provider.PrepareAsync(New List(Of DmsResourceItem) From {itemValue}, CancellationToken.None)
        Assert.That(provider.Events, [Is].Empty)
        Assert.That(itemValue.ExtendedInfosOwner.DisplayName, [Is].EqualTo("Known anonymous owner"))
        Assert.That(itemValue.ExtendedInfosGroupSharings(0).Group.DisplayName, [Is].EqualTo("Known anonymous group"))
    End Function

    <TestCase(False), TestCase(True)>
    Public Sub MismatchedPrincipalSnapshotsAreNotInstalled(group As Boolean)
        Dim provider As New PrincipalProvider With {.WrongUser = Not group, .WrongGroup = group}
        Dim itemValue = Item(provider)
        Assert.ThrowsAsync(Of InvalidOperationException)(CType(Async Function()
                                                                   Await provider.PrepareAsync(New List(Of DmsResourceItem) From {itemValue}, CancellationToken.None)
                                                               End Function, Func(Of Task)))
        Assert.That(itemValue.ExtendedInfosOwner.GetDisplayName, [Is].Not.Null)
        Assert.That(itemValue.ExtendedInfosGroupSharings(0).Group.GetName, [Is].Not.Null)
    End Sub

    <Test>
    Public Async Function CancellationDuringLookupDoesNotPublishPartialSnapshots() As Task
        Dim provider As New PrincipalProvider With {.BlockGroup = True}
        Dim itemValue = Item(provider)
        Using cancellation As New CancellationTokenSource()
            Dim pending = provider.PrepareAsync(New List(Of DmsResourceItem) From {itemValue}, cancellation.Token)
            Assert.That(Await Task.WhenAny(provider.Entered.Task, Task.Delay(3000)), [Is].SameAs(provider.Entered.Task))
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                         Await pending
                                                                     End Function, Func(Of Task)))
            Assert.That(itemValue.ExtendedInfosOwner.GetDisplayName, [Is].Not.Null)
            Assert.That(itemValue.ExtendedInfosGroupSharings(0).Group.GetName, [Is].Not.Null)
        End Using
    End Function

    <Test>
    Public Sub PreCanceledPreparationDoesNotLoadPrincipals()
        Dim provider As New PrincipalProvider()
        Using cancellation As New CancellationTokenSource()
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                         Await provider.PrepareAsync(New List(Of DmsResourceItem) From {Item(provider)}, cancellation.Token)
                                                                     End Function, Func(Of Task)))
            Assert.That(provider.Events, [Is].Empty)
        End Using
    End Sub

    <TestCase("{""first-name"":"" Ada "",""last-name"":"" Lovelace ""}", "Ada Lovelace")>
    <TestCase("{""first-name"":""Ada""}", "Ada")>
    <TestCase("{""last-name"":""Lovelace""}", "Lovelace")>
    <TestCase("{""first-name"":null,""last-name"":42}", "")>
    <TestCase("invalid-json", "")>
    Public Async Function NativeScopevisioFallbackParsesTheExistingNameContract(json As String, expected As String) As Task
        Using handler As New NameHandler With {.Json = json}, client As New HttpClient(handler)
            Assert.That(Await ScopevisioTeamworkDmsProvider.ReadNativeScopevisioUserNameAsync(client, New Uri("https://fixture.invalid/user/user-id"), "fixture-token", CancellationToken.None), [Is].EqualTo(expected))
            Assert.That(handler.Authorization, [Is].EqualTo("Bearer fixture-token"))
            Assert.That(handler.Calls, [Is].EqualTo(1))
        End Using
    End Function

    <Test>
    Public Async Function UnavailableScopevisioNameRetainsIdFallback() As Task
        Using handler As New NameHandler With {.Status = HttpStatusCode.Forbidden}, client As New HttpClient(handler)
            Assert.That(Await ScopevisioTeamworkDmsProvider.ReadNativeScopevisioUserNameAsync(client, New Uri("https://fixture.invalid/user/user-id"), "fixture-token", CancellationToken.None), [Is].Empty)
        End Using
    End Function

    <Test>
    Public Async Function NativeScopevisioFallbackCancelsTheActiveRequest() As Task
        Using handler As New NameHandler With {.Block = True}, client As New HttpClient(handler), cancellation As New CancellationTokenSource()
            Dim pending = ScopevisioTeamworkDmsProvider.ReadNativeScopevisioUserNameAsync(client, New Uri("https://fixture.invalid/user/user-id"), "fixture-token", cancellation.Token)
            Assert.That(Await Task.WhenAny(handler.Entered.Task, Task.Delay(3000)), [Is].SameAs(handler.Entered.Task))
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                         Await pending
                                                                     End Function, Func(Of Task)))
        End Using
    End Function

    Private Shared Function Item(provider As PrincipalProvider) As DmsResourceItem
        Dim user As New DmsUser With {.ID = "user-id", .Provider = provider, .GetDisplayName = Function(unusedProvider, unusedId) ThrowLazyUser(), .GetEMailAddress = Function(unusedProvider, unusedId) ThrowLazyUser()}
        Dim result As New DmsResourceItem With {.ExtendedInfosOwner = user, .ExtendedInfosLastModificationUser = user, .ExtendedInfosLockedByUser = user, .ExtendedInfosHasHiddenUserSharings = True, .ExtendedInfosHasHiddenGroupSharings = True}
        result.ExtendedInfosUserSharings = New List(Of DmsShareForUser) From {New DmsShareForUser(result, user, True, False, True, False, False, False)}
        result.ExtendedInfosGroupSharings = New List(Of DmsShareForGroup) From {New DmsShareForGroup(result, New DmsGroup With {.ID = "group-id", .Provider = provider, .GetName = Function(unusedProvider, unusedId) ThrowLazyUser()}, True, True, False, False, False, False)}
        Return result
    End Function

    Private Shared Function ThrowLazyUser() As String
        Throw New InvalidOperationException("A lazy synchronous principal callback must never run.")
    End Function

    Private Class PrincipalProvider
        Inherits ScopevisioTeamworkDmsProvider
        Public ReadOnly Events As New List(Of String)()
        Public EmptyNames, WrongUser, WrongGroup, BlockGroup As Boolean
        Public ReadOnly Entered As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Public Function PrepareAsync(items As IList(Of DmsResourceItem), ct As CancellationToken) As Task
            Return Me.PrepareNativePrincipalSnapshotsAsync(items, ct)
        End Function
        Protected Overrides Function LoadNativeUserSnapshotAsync(id As String, ct As CancellationToken) As Task(Of DmsUser)
            ct.ThrowIfCancellationRequested()
            Events.Add("user:" & id)
            Return Task.FromResult(New DmsUser With {.ID = If(WrongUser, "wrong", id), .DisplayName = If(EmptyNames, String.Empty, "Native User"), .EMailAddress = If(EmptyNames, String.Empty, "native@example.invalid")})
        End Function
        Protected Overrides Async Function LoadNativeGroupSnapshotAsync(id As String, ct As CancellationToken) As Task(Of DmsGroup)
            ct.ThrowIfCancellationRequested()
            Events.Add("group:" & id)
            Entered.TrySetResult(True)
            If BlockGroup Then Await Task.Delay(Timeout.Infinite, ct)
            Return New DmsGroup With {.ID = If(WrongGroup, "wrong", id), .Name = If(EmptyNames, String.Empty, "Native Group")}
        End Function
    End Class

    Private Class NameHandler
        Inherits HttpMessageHandler
        Public Json As String = "{}"
        Public Status As HttpStatusCode = HttpStatusCode.OK
        Public Block As Boolean
        Public Calls As Integer
        Public Authorization As String
        Public ReadOnly Entered As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, ct As CancellationToken) As Task(Of HttpResponseMessage)
            Calls += 1
            Authorization = request.Headers.Authorization.ToString()
            Entered.TrySetResult(True)
            If Block Then Await Task.Delay(Timeout.Infinite, ct)
            Return New HttpResponseMessage(Status) With {.Content = New StringContent(Json)}
        End Function
    End Class
End Class
