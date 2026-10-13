Option Explicit On
Option Strict On

Imports System.Runtime.ExceptionServices
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

'Runs every exact owned-child cleanup even when the copy or another cleanup fails.
Friend NotInheritable Class OwnedCopyTestCleanup
    Friend Shared Function ForChildren(provider As BaseDmsProvider, root As String, relativeNames As IEnumerable(Of String)) As List(Of Action)
        Dim names = relativeNames.Distinct().ToArray()
        Dim parents As New HashSet(Of String)(StringComparer.Ordinal)
        Dim actions As New List(Of Action)
        For Each name In names
            Dim parts = name.Split(provider.DirectorySeparator)
            If parts.Any(Function(part) String.IsNullOrWhiteSpace(part) OrElse part = "." OrElse part = "..") Then
                Throw New ArgumentException("Copy-test children must be explicit relative paths.", NameOf(relativeNames))
            End If
            Dim ownedPath = provider.CombinePath(root, name)
            actions.Add(Sub()
                Dim item = provider.ListRemoteItem(ownedPath)
                If item IsNot Nothing Then
                    If item.ItemType <> DmsResourceItem.ItemTypes.File OrElse item.ExtendedInfosCollisionDetected Then
                        Throw New InvalidOperationException("The exact copy-test file path has an unexpected kind or collision; it was preserved.")
                    End If
                    provider.DeleteRemoteItem(item, DmsResourceItem.ItemTypes.File)
                End If
                If provider.RemoteItemExists(ownedPath) Then Throw New InvalidOperationException("An exact copy-test file remains after cleanup.")
            End Sub)
            For count As Integer = 1 To parts.Length - 1
                parents.Add(String.Join(provider.DirectorySeparator.ToString(), parts.Take(count)))
            Next
        Next
        For Each relativeParent In parents.OrderByDescending(Function(parent) parent.Length)
            Dim ownedParent = provider.CombinePath(root, relativeParent)
            actions.Add(Sub()
                Dim item = provider.ListRemoteItem(ownedParent)
                If item IsNot Nothing Then
                    If item.ItemType <> DmsResourceItem.ItemTypes.Folder OrElse item.ExtendedInfosCollisionDetected OrElse
                        provider.ListAllRemoteItems(ownedParent, BaseDmsProvider.SearchItemType.AllItems).Count <> 0 Then
                        Throw New InvalidOperationException("Unexpected resources remain in the exact copy-test child scope; the parent was preserved.")
                    End If
                    provider.DeleteRemoteItem(item, DmsResourceItem.ItemTypes.Folder)
                End If
                If provider.RemoteItemExists(ownedParent) Then Throw New InvalidOperationException("An exact copy-test child folder remains after cleanup.")
            End Sub)
        Next
        Return actions
    End Function

    Friend Shared Sub Complete(actions As IEnumerable(Of Action), original As Exception)
        Dim failures As New List(Of Exception)
        If original IsNot Nothing Then failures.Add(original)
        For Each cleanup As Action In actions
            Try
                cleanup()
            Catch failure As Exception
                failures.Add(failure)
            End Try
        Next
        If failures.Count = 1 Then ExceptionDispatchInfo.Capture(failures(0)).Throw()
        If failures.Count > 1 Then Throw New AggregateException("Copy execution and owned-child cleanup failures.", failures)
    End Sub
End Class
