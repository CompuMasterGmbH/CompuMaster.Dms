Option Explicit On
Option Strict On

Imports System.IO
Imports System.Linq
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Friend Class ScopevisioLiveTestResourceScope
    Friend Const OwnedCollection As String = "ZZZ_UnitTests_CM.Dms_NativeAsync"
    Friend Shared Async Function WithOwnedCollectionAsync(body As Func(Of ScopevisioTeamworkDmsProvider, CancellationToken, Task), Optional allowLocalBoundary As Boolean = False, Optional localExecutionMinutes As Integer = 2, Optional localCleanupMinutes As Integer = 2) As Task
        'A repository concurrency group alone cannot exclude other repositories or local sessions.
        Dim localBoundary = allowLocalBoundary AndAlso Environment.GetEnvironmentVariable("NATIVE_SCOPEVISIO_EXCLUSIVE_WKS08") = "true"
        If localBoundary Then
            If localExecutionMinutes < 1 OrElse localExecutionMinutes > 60 OrElse localCleanupMinutes < 1 OrElse localCleanupMinutes > 15 Then Throw New InvalidOperationException("Invalid bounded local test/cleanup deadlines.")
            ValidateExclusiveLocalBoundary(Environment.MachineName, Environment.GetEnvironmentVariable("CI"), Environment.GetEnvironmentVariable("NATIVE_SCOPEVISIO_LOCAL_WINDOW_UNTIL"), DateTimeOffset.UtcNow, localExecutionMinutes + localCleanupMinutes + 1)
        Else
            ValidateExclusiveCi(Environment.GetEnvironmentVariable("CI"), Environment.GetEnvironmentVariable("NATIVE_SCOPEVISIO_EXCLUSIVE_CI"))
        End If
        Dim provider As New ScopevisioTeamworkDmsProvider()
        Dim authorized As Boolean
        Dim failures As New List(Of Exception)()
        Using deadline As New CancellationTokenSource(TimeSpan.FromMinutes(If(localBoundary, localExecutionMinutes, 60)))
            Try
                Dim profile As New DmsLoginProfile With {.DmsProvider = BaseDmsProvider.DmsProviders.Scopevisio,
                    .Username = RequiredEnvironment("TEST_SCOPEVISIOTEAMWORK_USERNAME"), .CustomerInstance = RequiredEnvironment("TEST_SCOPEVISIOTEAMWORK_CUSTOMERNO"),
                    .Password = RequiredEnvironment("TEST_SCOPEVISIOTEAMWORK_PASSWORD")}
                Await provider.AuthorizeAsync(profile, deadline.Token)
                authorized = True
                Await RemoveOwnedCollectionAsync(provider, deadline.Token)
                Await provider.CreateCollectionAsync(OwnedCollection, deadline.Token)
                Await body(provider, deadline.Token)
            Catch ex As Exception
                failures.Add(ex)
            End Try
        End Using
        If authorized Then
            Using cleanup As New CancellationTokenSource(TimeSpan.FromMinutes(If(localBoundary, localCleanupMinutes, 15)))
                Try
                    Await RemoveOwnedCollectionAsync(provider, cleanup.Token)
                Catch ex As Exception
                    failures.Add(New IOException("Native live cleanup failed for the explicitly owned collection " & OwnedCollection & ".", ex))
                End Try
            End Using
        End If
        ThrowNativeLiveFailures(failures)
    End Function

    Friend Shared Sub ValidateExclusiveLocalBoundary(machine As String, ci As String, windowEnd As String, now As DateTimeOffset, Optional requiredMinutes As Integer = 5)
        Dim ending As DateTimeOffset
        If Not String.Equals(machine, "WKS08", StringComparison.OrdinalIgnoreCase) OrElse String.Equals(ci, "true", StringComparison.OrdinalIgnoreCase) OrElse
            String.IsNullOrWhiteSpace(windowEnd) OrElse Not Text.RegularExpressions.Regex.IsMatch(windowEnd, "(Z|[+-]\d{2}:\d{2})$") OrElse
            requiredMinutes < 1 OrElse Not DateTimeOffset.TryParse(windowEnd, Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.None, ending) OrElse ending < now.AddMinutes(requiredMinutes) Then
            Throw New InvalidOperationException("The targeted local test requires WKS08, an explicitly coordinated exclusive window covering execution and cleanup, and a separate local execution marker. No server request was started.")
        End If
    End Sub

    Friend Shared Sub ValidateExclusiveCi(ci As String, coordinated As String)
        If Not String.Equals(ci, "true", StringComparison.OrdinalIgnoreCase) OrElse coordinated <> "true" Then
            Throw New InvalidOperationException("Native live tests require a coordinated exclusive CI window and the Scopevisio account lock throughout setup, execution, and cleanup.")
        End If
    End Sub

    Friend Shared Sub ThrowNativeLiveFailures(failures As IList(Of Exception))
        If failures.Count = 1 Then ExceptionDispatchInfo.Capture(failures(0)).Throw()
        If failures.Count > 1 Then Throw New AggregateException("Native live execution and cleanup failed; original causes are retained.", failures)
    End Sub

    Friend Shared Async Function RemoveOwnedCollectionAsync(provider As ScopevisioTeamworkDmsProvider, token As CancellationToken) As Task
        Await provider.ResetCachesForRemoteItemsAsync("/", BaseDmsProvider.SearchItemType.AllItems, token)
        Dim count = (Await provider.ListAllCollectionNamesAsync("/", token)).Where(Function(name) String.Equals(name, OwnedCollection, StringComparison.Ordinal)).Count()
        If count > 1 Then Throw New InvalidOperationException("Ambiguous test-owned collection; absence cannot be established safely: " & OwnedCollection)
        If count = 1 Then
            Dim root = Await provider.ListRemoteItemAsync(OwnedCollection, token)
            If root Is Nothing OrElse root.ItemType <> DmsResourceItem.ItemTypes.Collection OrElse String.IsNullOrEmpty(root.ExtendedInfosCollectionID) OrElse root.ExtendedInfosCollisionDetected Then Throw New InvalidOperationException("Cannot identify the test-owned collection uniquely.")
            Await RemoveOwnedChildrenAsync(provider, root, root.ExtendedInfosCollectionID, token)
            Await provider.DeleteRemoteItemAsync(root, DmsResourceItem.ItemTypes.Collection, token)
        End If
        Await provider.ResetCachesForRemoteItemsAsync("/", BaseDmsProvider.SearchItemType.AllItems, token)
        If (Await provider.ListAllCollectionNamesAsync("/", token)).Any(Function(name) String.Equals(name, OwnedCollection, StringComparison.Ordinal)) Then Throw New IOException("Test-owned collection remains after cleanup; refusing uncertain setup.")
    End Function

    Private Shared Async Function RemoveOwnedChildrenAsync(provider As ScopevisioTeamworkDmsProvider, parent As DmsResourceItem, collectionId As String, token As CancellationToken) As Task
        Await provider.ResetCachesForRemoteItemsAsync(parent.FullName, BaseDmsProvider.SearchItemType.AllItems, token)
        Dim children = Await provider.ListAllRemoteItemsAsync(parent.FullName, BaseDmsProvider.SearchItemType.AllItems, token)
        For Each child In children
            If child.ItemType = DmsResourceItem.ItemTypes.File Then
                If child.ExtendedInfosReferencedFromCollectionIDs IsNot Nothing AndAlso child.ExtendedInfosReferencedFromCollectionIDs.Any(Function(id) id <> collectionId) Then Throw New IOException("A test file is referenced by another collection; cleanup refuses to delete it.")
            ElseIf child.ItemType = DmsResourceItem.ItemTypes.Folder Then
                Await RemoveOwnedChildrenAsync(provider, child, collectionId, token)
            Else
                Throw New InvalidOperationException("Unexpected nested resource kind in the test-owned collection.")
            End If
            If child.ExtendedInfosLinks IsNot Nothing Then
                For Each link In child.ExtendedInfosLinks
                    Await provider.DeleteLinkAsync(link, token)
                Next
            End If
            Await provider.DeleteRemoteItemAsync(child, token)
        Next
        Await provider.ResetCachesForRemoteItemsAsync(parent.FullName, BaseDmsProvider.SearchItemType.AllItems, token)
        If (Await provider.ListAllRemoteItemsAsync(parent.FullName, BaseDmsProvider.SearchItemType.AllItems, token)).Count <> 0 Then Throw New IOException("Owned children remain after cleanup; refusing parent deletion: " & parent.FullName)
    End Function

    Friend Shared Function RequiredEnvironment(name As String) As String
        Dim value = Environment.GetEnvironmentVariable(name)
        If String.IsNullOrWhiteSpace(value) Then Throw New InvalidOperationException("Required CI environment variable is missing: " & name)
        Return value
    End Function
End Class
