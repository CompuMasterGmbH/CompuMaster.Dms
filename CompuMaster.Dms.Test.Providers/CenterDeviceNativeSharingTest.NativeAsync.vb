Option Explicit On
Option Strict On

Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest.Clients.Collections
Imports CenterDevice.Rest.Clients.Common
Imports CenterDevice.Rest.Clients.Groups
Imports CenterDevice.Rest.Clients.User
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class CenterDeviceNativeSharingTest
    <TestCase(DmsResourceItem.ItemTypes.Collection, False, False), TestCase(DmsResourceItem.ItemTypes.Collection, False, True),
     TestCase(DmsResourceItem.ItemTypes.Collection, True, False), TestCase(DmsResourceItem.ItemTypes.Collection, True, True),
     TestCase(DmsResourceItem.ItemTypes.Folder, False, False), TestCase(DmsResourceItem.ItemTypes.Folder, False, True),
     TestCase(DmsResourceItem.ItemTypes.Folder, True, False), TestCase(DmsResourceItem.ItemTypes.Folder, True, True)>
    Public Async Function NativeSharingRetainsSelectedResourceAndPrincipalIds(kind As DmsResourceItem.ItemTypes, group As Boolean, remove As Boolean) As Task
        Dim provider As New SharingProvider()
        Dim item = SelectedItem(kind)
        Using cancellation As New CancellationTokenSource()
            Await ChangeSharing(provider, item, group, remove, cancellation.Token)
            Assert.That(provider.Selected, [Is].SameAs(item))
            If group Then
                Assert.That(provider.Users, [Is].Null)
                Assert.That(provider.Groups, [Is].EqualTo(New String() {"group-id"}))
            Else
                Assert.That(provider.Users, [Is].EqualTo(New String() {"user-id"}))
                Assert.That(provider.Groups, [Is].Null)
            End If
            Assert.That(provider.Remove, [Is].EqualTo(remove))
            Assert.That(provider.RequestToken, [Is].EqualTo(cancellation.Token))
            Assert.That(provider.Calls, [Is].EqualTo(1))
            Assert.That(provider.Io.CollectionCalls, [Is].Zero, "Selected IDs must not resolve an ambiguous parent path.")
        End Using
    End Function

    <TestCase(DmsResourceItem.ItemTypes.Root), TestCase(DmsResourceItem.ItemTypes.File)>
    Public Sub UnsupportedResourceKindsFailBeforeNativeDispatch(kind As DmsResourceItem.ItemTypes)
        Dim provider As New SharingProvider()
        Assert.ThrowsAsync(Of NotSupportedException)(CType(Async Function()
                                                               Await ChangeSharing(provider, SelectedItem(kind), False, False, CancellationToken.None)
                                                           End Function, Func(Of Task)))
        Assert.That(provider.Calls, [Is].Zero)
    End Sub

    <Test>
    Public Sub MissingSelectedIdentifierCannotShareAnArbitrarySibling()
        Dim provider As New SharingProvider()
        Dim item = SelectedItem(DmsResourceItem.ItemTypes.Collection)
        item.ExtendedInfosCollectionID = Nothing
        Assert.ThrowsAsync(Of InvalidOperationException)(CType(Async Function()
                                                                   Await ChangeSharing(provider, item, False, False, CancellationToken.None)
                                                               End Function, Func(Of Task)))
        Assert.That(provider.Calls, [Is].Zero)
    End Sub

    <Test>
    Public Sub HiddenPrincipalWithoutAnIdentifierCannotBeSubmitted()
        Dim provider As New SharingProvider()
        Dim item = SelectedItem(DmsResourceItem.ItemTypes.Collection)
        Dim share As New DmsShareForUser(item, New DmsUser(), True, True, True, True, True, True)
        Assert.ThrowsAsync(Of ArgumentException)(CType(Async Function()
                                                           Await provider.CreateSharingAsync(item, share)
                                                       End Function, Func(Of Task)))
        Assert.That(provider.Calls, [Is].Zero)
    End Sub

    <TestCase(False), TestCase(True)>
    Public Async Function UncertainFailureOrPartialRejectionInvalidatesCacheWithoutReplay(partialRejection As Boolean) As Task
        Dim provider As New SharingProvider With {.Fail = Not partialRejection}
        If partialRejection Then provider.Response = New SharingResponse With {.FailedUsers = New List(Of String) From {"user-id"}}
        Await provider.ListAllCollectionNamesAsync("/")
        Assert.CatchAsync(Ctype(Async Function()
                                   Await ChangeSharing(provider, SelectedItem(DmsResourceItem.ItemTypes.Collection), False, False, CancellationToken.None)
                               End Function, Func(Of Task)))
        Await provider.ListAllCollectionNamesAsync("/")
        Assert.That(provider.Io.CollectionCalls, [Is].EqualTo(2))
        Assert.That(provider.Calls, [Is].EqualTo(1))
    End Function

    <Test>
    Public Async Function ActiveSharingCancellationInvalidatesCacheAndDoesNotReplay() As Task
        Dim provider As New SharingProvider With {.Block = True}
        Await provider.ListAllCollectionNamesAsync("/")
        Using cancellation As New CancellationTokenSource()
            Dim operation = ChangeSharing(provider, SelectedItem(DmsResourceItem.ItemTypes.Folder), True, True, cancellation.Token)
            Await provider.Entered.Task
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                       Await operation
                                                                   End Function, Func(Of Task)))
        End Using
        Await provider.ListAllCollectionNamesAsync("/")
        Assert.That(provider.Io.CollectionCalls, [Is].EqualTo(2))
        Assert.That(provider.Calls, [Is].EqualTo(1))
    End Function

    <Test>
    Public Sub CanceledSharingCannotDispatch()
        Dim provider As New SharingProvider()
        Using cancellation As New CancellationTokenSource()
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                       Await ChangeSharing(provider, SelectedItem(DmsResourceItem.ItemTypes.Collection), False, False, cancellation.Token)
                                                                   End Function, Func(Of Task)))
        End Using
        Assert.That(provider.Calls, [Is].Zero)
    End Sub

    <Test>
    Public Async Function NativePrincipalListsRetainIdentifiersNamesAndEmailWithoutLazySyncLookups() As Task
        Dim provider As New SharingProvider()
        Dim groups = Await provider.GetAllGroupsAsync()
        Dim users = Await provider.GetAllUsersAsync()
        Assert.That(groups.Select(Function(group) group.ID), [Is].EqualTo(New String() {"group-id"}))
        Assert.That(groups(0).DisplayName, [Is].EqualTo("Fixture group"))
        Assert.That(groups(0).GetName, [Is].Null)
        Assert.That(users.Select(Function(user) user.ID), [Is].EqualTo(New String() {"user-id", "unnamed-id"}))
        Assert.That(users(0).DisplayName, [Is].EqualTo("First Last"))
        Assert.That(users(0).EMailAddress, [Is].EqualTo("fixture@example.invalid"))
        Assert.That(users(1).DisplayName, [Is].EqualTo("unnamed-id"))
        Assert.That(users(0).GetDisplayName, [Is].Null)
    End Function

    <TestCase(False), TestCase(True)>
    Public Sub MalformedPrincipalListsFailClearly(groups As Boolean)
        Dim provider As New SharingProvider With {.Malformed = True}
        Assert.ThrowsAsync(Of InvalidOperationException)(CType(Async Function()
                                                                   If groups Then
                                                                       Await provider.GetAllGroupsAsync()
                                                                   Else
                                                                       Await provider.GetAllUsersAsync()
                                                                   End If
                                                               End Function, Func(Of Task)))
    End Sub

    Private Shared Function SelectedItem(kind As DmsResourceItem.ItemTypes) As DmsResourceItem
        Return New DmsResourceItem With {.ItemType = kind, .FullName = "duplicate", .ExtendedInfosCollisionDetected = True, .ExtendedInfosCollectionID = "selected-collection", .ExtendedInfosFolderID = "selected-folder"}
    End Function

    Private Shared Function ChangeSharing(provider As SharingProvider, item As DmsResourceItem, group As Boolean, remove As Boolean, cancellationToken As CancellationToken) As Task
        If group Then
            Dim share As New DmsShareForGroup(item, New DmsGroup With {.ID = "group-id"}, True, True, True, True, True, True)
            If remove Then Return provider.DeleteSharingAsync(share, cancellationToken)
            Return provider.CreateSharingAsync(item, share, cancellationToken)
        Else
            Dim share As New DmsShareForUser(item, New DmsUser With {.ID = "user-id"}, True, True, True, True, True, True)
            If remove Then Return provider.DeleteSharingAsync(share, cancellationToken)
            Return provider.CreateSharingAsync(item, share, cancellationToken)
        End If
    End Function

    Private Class SharingProvider
        Inherits ScopevisioTeamworkDmsProvider
        Public ReadOnly Io As New FixtureIo()
        Public Selected As DmsResourceItem
        Public Users As List(Of String)
        Public Groups As List(Of String)
        Public Remove As Boolean
        Public RequestToken As CancellationToken
        Public Calls As Integer
        Public Fail As Boolean
        Public Block As Boolean
        Public Malformed As Boolean
        Public Response As New SharingResponse With {.FailedGroups = New List(Of String)(), .FailedUsers = New List(Of String)()}
        Public ReadOnly Entered As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Public Sub New()
            Me.IOClient = Io
        End Sub
        Protected Overrides Async Function ChangeNativeSharingAsync(resource As DmsResourceItem, users As List(Of String), groups As List(Of String), remove As Boolean, cancellationToken As CancellationToken) As Task(Of SharingResponse)
            Calls += 1
            Selected = resource
            Me.Users = users
            Me.Groups = groups
            Me.Remove = remove
            RequestToken = cancellationToken
            Entered.TrySetResult(True)
            If Block Then Await Task.Delay(Timeout.Infinite, cancellationToken)
            If Fail Then Throw New IOException("Uncertain sharing result.")
            Return Response
        End Function
        Protected Overrides Function LoadNativeGroupsAsync(cancellationToken As CancellationToken) As Task(Of GroupList)
            Return Task.FromResult(If(Malformed, New GroupList(), New GroupList With {.Groups = New List(Of Group) From {New Group With {.Id = "group-id", .Name = "Fixture group"}}}))
        End Function
        Protected Overrides Function LoadNativeUsersAsync(cancellationToken As CancellationToken) As Task(Of UserList(Of BaseUserData))
            Return Task.FromResult(If(Malformed, New UserList(Of BaseUserData)(), New UserList(Of BaseUserData) With {.Users = New List(Of BaseUserData) From {
                New BaseUserData With {.Id = "user-id", .FirstName = " First", .LastName = "Last", .Email = "fixture@example.invalid"}, New BaseUserData With {.Id = "unnamed-id"}
            }}))
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
            Return Task.FromResult(New List(Of Collection) From {New Collection With {.Id = "selected-collection", .Name = "duplicate"}})
        End Function
    End Class
End Class
