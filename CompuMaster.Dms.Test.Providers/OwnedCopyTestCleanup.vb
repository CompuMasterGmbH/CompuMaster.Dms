Option Explicit On
Option Strict On

Imports System.Runtime.ExceptionServices
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

'Runs every exact owned-child cleanup even when the copy or another cleanup fails.
Friend NotInheritable Class OwnedCopyTestCleanup
    Friend Shared Function IsProtectedStartingPath(provider As BaseDmsProvider, path As String) As Boolean
        Dim normalized = If(path, "").Trim(provider.DirectorySeparator)
        Dim configured = If(provider.BrowseInRootFolderName, "").Trim(provider.DirectorySeparator)
        Return normalized = "" OrElse String.Equals(normalized, configured, StringComparison.Ordinal) OrElse
            configured.StartsWith(normalized & provider.DirectorySeparator, StringComparison.Ordinal)
    End Function

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
                If IsProtectedStartingPath(provider, ownedParent) Then Return
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

    Friend Shared Sub CompleteScope(actions As IEnumerable(Of Action), root As OwnedCopyTestRoot, original As Exception)
        'A rejected or unconfirmed root never admitted child creation or recovery.
        Dim finalActions = If(root.IsAdmitted, actions.ToList(), New List(Of Action))
        finalActions.Add(AddressOf root.Cleanup)
        Complete(finalActions, original)
    End Sub

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

'Admits only an acknowledged creation with a confirmed resource snapshot as case-owned.
'Pre-existing/configured starting points and uncertain setup remain protected.
Friend NotInheritable Class OwnedCopyTestRoot
    Private ReadOnly Provider As BaseDmsProvider
    Private ReadOnly Root As String
    Private CreationAttempted As Boolean
    Private Confirmed As Boolean
    Private Permanent As Boolean
    Private OwnedKind As DmsResourceItem.ItemTypes
    Private OwnedId As String
    Private OwnedPath As String
    Private Admitted As Boolean
    Friend ReadOnly Property IsAdmitted As Boolean
        Get
            Return Admitted
        End Get
    End Property

    Friend Sub New(provider As BaseDmsProvider, root As String)
        Me.Provider = provider
        Me.Root = root
    End Sub

    Private Function Normalize(path As String) As String
        Return If(path, "").Trim(Provider.DirectorySeparator)
    End Function

    Friend Sub Prepare()
        Dim before = Provider.ListRemoteItem(Root)
        Dim normalizedRoot = Normalize(Root)
        Permanent = OwnedCopyTestCleanup.IsProtectedStartingPath(Provider, Root)
        If before IsNot Nothing Then
            If before.ExtendedInfosCollisionDetected OrElse (before.ItemType <> DmsResourceItem.ItemTypes.Folder AndAlso
                before.ItemType <> DmsResourceItem.ItemTypes.Collection AndAlso before.ItemType <> DmsResourceItem.ItemTypes.Root) OrElse
                Not String.Equals(Normalize(before.FullName), normalizedRoot, StringComparison.Ordinal) Then
                Throw New InvalidOperationException("The copy-test root has an unexpected kind or collision; it was preserved.")
            End If
            Admitted = True
            Return
        End If
        If normalizedRoot = "" Then Throw New InvalidOperationException("The provider root is unavailable; it must not be created by the copy test.")
        CreationAttempted = True
        If Provider.SupportsCollections Then
            Provider.CreateCollection(Root)
        Else
            Provider.CreateFolder(Root)
        End If
        Dim created = Provider.ListRemoteItem(Root)
        Dim expected = If(Provider.SupportsCollections, DmsResourceItem.ItemTypes.Collection, DmsResourceItem.ItemTypes.Folder)
        If created Is Nothing OrElse created.ExtendedInfosCollisionDetected OrElse created.ItemType <> expected OrElse
            Not String.Equals(Normalize(created.FullName), normalizedRoot, StringComparison.Ordinal) OrElse
            (expected = DmsResourceItem.ItemTypes.Collection AndAlso String.IsNullOrWhiteSpace(created.ExtendedInfosCollectionID)) Then
            Throw New InvalidOperationException("The copy-test root creation has no confirmed owned identity; reconciliation is required.")
        End If
        OwnedKind = created.ItemType
        OwnedId = Identity(created)
        OwnedPath = Normalize(created.FullName)
        Confirmed = True
        Admitted = True
    End Sub

    Private Shared Function Identity(item As DmsResourceItem) As String
        Return If(item.ItemType = DmsResourceItem.ItemTypes.Collection, item.ExtendedInfosCollectionID, item.ExtendedInfosFolderID)
    End Function

    Friend Sub Cleanup()
        Permanent = Permanent OrElse OwnedCopyTestCleanup.IsProtectedStartingPath(Provider, Root)
        If Not CreationAttempted OrElse (Permanent AndAlso Confirmed) Then Return
        Dim current = Provider.ListRemoteItem(Root)
        If current Is Nothing Then
            If Provider.RemoteItemExists(Root) Then Throw New InvalidOperationException("The copy-test root absence could not be confirmed.")
            Return
        End If
        If Not Confirmed OrElse current.ExtendedInfosCollisionDetected OrElse current.ItemType <> OwnedKind OrElse
            Not String.Equals(Normalize(current.FullName), OwnedPath, StringComparison.Ordinal) OrElse
            (Not String.IsNullOrEmpty(OwnedId) AndAlso Not String.Equals(Identity(current), OwnedId, StringComparison.Ordinal)) Then
            Throw New InvalidOperationException("The copy-test root is uncertain or its identity changed; it was preserved for reconciliation.")
        End If
        If Permanent Then Return
        If Provider.ListAllRemoteItems(Root, BaseDmsProvider.SearchItemType.AllItems).Count <> 0 Then
            Throw New InvalidOperationException("Unexpected copy-test root children remain; the root was preserved for reconciliation.")
        End If
        Provider.DeleteRemoteItem(current, OwnedKind)
        If Provider.RemoteItemExists(Root) Then Throw New InvalidOperationException("The newly created copy-test root remains after cleanup.")
    End Sub
End Class
