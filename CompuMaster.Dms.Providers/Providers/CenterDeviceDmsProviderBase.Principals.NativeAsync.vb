Option Explicit On
Option Strict On

Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data

Namespace Providers
    Partial Public MustInherit Class CenterDeviceDmsProviderBase
        ''' <summary>Retrieves a user display name without invoking synchronous metadata callbacks.</summary>
        ''' <param name="userId">The visible user identifier.</param>
        ''' <param name="cancellationToken">Cancels queued or active requests.</param>
        ''' <returns>The user's name, or an empty string when unavailable.</returns>
        Protected Overridable Function LookupNativeUserDisplayNameAsync(userId As String, cancellationToken As CancellationToken) As Task(Of String)
            Return Me.IOClient.UserNameAsync(userId, cancellationToken)
        End Function

        ''' <summary>Retrieves a user snapshot through native asynchronous I/O.</summary>
        ''' <param name="userId">The visible user identifier.</param>
        ''' <param name="cancellationToken">Cancels queued or active requests.</param>
        ''' <returns>The user's identifier, display name, and email without lazy callbacks.</returns>
        Protected Overridable Async Function LoadNativeUserSnapshotAsync(userId As String, cancellationToken As CancellationToken) As Task(Of DmsUser)
            cancellationToken.ThrowIfCancellationRequested()
            Dim name = Await Me.LookupNativeUserDisplayNameAsync(userId, cancellationToken).ConfigureAwait(False)
            Dim email = Await Me.IOClient.UserEMailAddressAsync(userId, cancellationToken).ConfigureAwait(False)
            cancellationToken.ThrowIfCancellationRequested()
            Return New DmsUser With {.ID = userId, .Provider = Me, .DisplayName = If(name, String.Empty), .EMailAddress = If(email, String.Empty)}
        End Function

        ''' <summary>Retrieves a group snapshot through native asynchronous I/O.</summary>
        ''' <param name="groupId">The visible group identifier.</param>
        ''' <param name="cancellationToken">Cancels queued or active requests.</param>
        ''' <returns>The group's identifier and normalized name without lazy callbacks.</returns>
        Protected Overridable Async Function LoadNativeGroupSnapshotAsync(groupId As String, cancellationToken As CancellationToken) As Task(Of DmsGroup)
            cancellationToken.ThrowIfCancellationRequested()
            Dim name = Await Me.IOClient.GroupNameAsync(groupId, cancellationToken).ConfigureAwait(False)
            cancellationToken.ThrowIfCancellationRequested()
            Return New DmsGroup With {.ID = groupId, .Provider = Me, .Name = Me.NormalizeGroupDisplayName(groupId, name)}
        End Function

        ''' <summary>Prepares resource principal snapshots before publication without reading lazy names.</summary>
        ''' <param name="resources">The resource snapshots to prepare.</param>
        ''' <param name="cancellationToken">Cancels queued or active metadata requests.</param>
        ''' <returns>A task representing metadata preparation.</returns>
        ''' <remarks>Lookups are deduplicated within the batch. Snapshots are installed only after all lookups succeed; hidden principal counters and sharing permissions are retained.</remarks>
        Protected Async Function PrepareNativePrincipalSnapshotsAsync(resources As IList(Of DmsResourceItem), cancellationToken As CancellationToken) As Task
            If resources Is Nothing Then Throw New ArgumentNullException(NameOf(resources))
            Dim users As New Dictionary(Of String, DmsUser)(StringComparer.Ordinal)
            Dim groups As New Dictionary(Of String, DmsGroup)(StringComparer.Ordinal)
            For Each resource In resources
                If resource Is Nothing Then Throw New ArgumentException(ProviderStrings.GetText("AResourceSnapshotCannotBeNull"), NameOf(resources))
                cancellationToken.ThrowIfCancellationRequested()
                Dim selectedUsers As New List(Of DmsUser) From {resource.ExtendedInfosOwner, resource.ExtendedInfosLastModificationUser, resource.ExtendedInfosLockedByUser}
                If resource.ExtendedInfosUserSharings IsNot Nothing Then selectedUsers.AddRange(resource.ExtendedInfosUserSharings.Select(Function(share) share.User))
                For Each user In selectedUsers
                    If Not String.IsNullOrWhiteSpace(user.ID) AndAlso Not users.ContainsKey(user.ID) Then
                        Dim snapshot = Await Me.LoadNativeUserSnapshotAsync(user.ID, cancellationToken).ConfigureAwait(False)
                        If Not String.Equals(snapshot.ID, user.ID, StringComparison.Ordinal) Then Throw New InvalidOperationException(ProviderStrings.GetText("TheNativeUserSnapshotDoesNotMatchThe"))
                        users.Add(user.ID, snapshot)
                    End If
                Next
                If resource.ExtendedInfosGroupSharings IsNot Nothing Then
                    For Each share In resource.ExtendedInfosGroupSharings
                        Dim id = share.Group.ID
                        If Not String.IsNullOrWhiteSpace(id) AndAlso Not groups.ContainsKey(id) Then
                            Dim snapshot = Await Me.LoadNativeGroupSnapshotAsync(id, cancellationToken).ConfigureAwait(False)
                            If Not String.Equals(snapshot.ID, id, StringComparison.Ordinal) Then Throw New InvalidOperationException(ProviderStrings.GetText("TheNativeGroupSnapshotDoesNotMatchThe"))
                            groups.Add(id, snapshot)
                        End If
                    Next
                End If
            Next
            cancellationToken.ThrowIfCancellationRequested()
            For Each resource In resources
                resource.ExtendedInfosOwner = PreparedNativeUser(resource.ExtendedInfosOwner, users)
                resource.ExtendedInfosLastModificationUser = PreparedNativeUser(resource.ExtendedInfosLastModificationUser, users)
                resource.ExtendedInfosLockedByUser = PreparedNativeUser(resource.ExtendedInfosLockedByUser, users)
                If resource.ExtendedInfosUserSharings IsNot Nothing Then
                    For Each share In resource.ExtendedInfosUserSharings
                        share.User = PreparedNativeUser(share.User, users)
                    Next
                End If
                If resource.ExtendedInfosGroupSharings IsNot Nothing Then
                    For Each share In resource.ExtendedInfosGroupSharings
                        Dim previous = share.Group
                        Dim snapshot As DmsGroup = previous
                        If Not String.IsNullOrWhiteSpace(previous.ID) Then snapshot = groups(previous.ID)
                        snapshot.GetName = Nothing
                        snapshot.ID = previous.ID
                        share.Group = snapshot
                    Next
                End If
            Next
        End Function

        Private Shared Function PreparedNativeUser(previous As DmsUser, snapshots As Dictionary(Of String, DmsUser)) As DmsUser
            Dim result As DmsUser = previous
            If Not String.IsNullOrWhiteSpace(previous.ID) Then result = snapshots(previous.ID)
            result.ID = previous.ID
            result.GetDisplayName = Nothing
            result.GetEMailAddress = Nothing
            result.GetLoginName = Nothing
            Return result
        End Function
    End Class
End Namespace
