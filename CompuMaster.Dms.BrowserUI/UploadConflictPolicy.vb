Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

'The operation owns this policy, including every retry/continue attempt.
Friend NotInheritable Class UploadConflictPolicy
    Private ReadOnly Choices As DownloadConflictPolicy
    Private ReadOnly Decisions As New Dictionary(Of String, Tuple(Of String, DownloadConflictAction))(StringComparer.Ordinal)
    Private ReadOnly Approved As New Dictionary(Of String, DmsResourceItem)(StringComparer.Ordinal)
    Friend Sub New(prompt As Func(Of String, DownloadConflictDecision))
        Choices = New DownloadConflictPolicy(prompt)
    End Sub

    Friend Async Function ResolveAsync(provider As BaseDmsProvider, destination As String, token As CancellationToken) As Task(Of DownloadConflictAction)
        Dim existing = Await ReadDestinationAsync(provider, destination, token)
        Approved.Remove(destination)
        If existing Is Nothing Then
            If Decisions.ContainsKey(destination) Then Throw New InvalidOperationException(UiStrings.Format("UploadDestinationChanged", destination))
            Return DownloadConflictAction.Replace
        End If
        If existing.ItemType <> DmsResourceItem.ItemTypes.File Then Throw New System.IO.IOException(UiStrings.Format("UploadSelectionCollision", destination))
        Dim identity = existing.ExtendedInfosFileID
        Dim previous As Tuple(Of String, DownloadConflictAction) = Nothing
        If Decisions.TryGetValue(destination, previous) AndAlso previous.Item1 <> identity Then
            Throw New InvalidOperationException(UiStrings.Format("UploadDestinationChanged", destination))
        End If
        Dim choice = If(previous Is Nothing, Choices.Resolve(destination), previous.Item2)
        token.ThrowIfCancellationRequested()
        If choice <> DownloadConflictAction.Cancel Then Decisions(destination) = Tuple.Create(identity, choice)
        If choice = DownloadConflictAction.Replace Then
            Dim current = Await ReadDestinationAsync(provider, destination, token)
            If current Is Nothing OrElse current.ItemType <> DmsResourceItem.ItemTypes.File OrElse current.ExtendedInfosFileID <> identity Then
                Throw New InvalidOperationException(UiStrings.Format("UploadDestinationChanged", destination))
            End If
            Approved(destination) = current
        End If
        Return choice
    End Function

    Friend Function ApprovedResource(destination As String) As DmsResourceItem
        Dim item As DmsResourceItem = Nothing
        Approved.TryGetValue(destination, item)
        Return item
    End Function

    Private Shared Async Function ReadDestinationAsync(provider As BaseDmsProvider, destination As String, token As CancellationToken) As Task(Of DmsResourceItem)
        token.ThrowIfCancellationRequested()
        Await provider.ResetCachesForRemoteItemsAsync(provider.ParentDirectoryPath(destination), BaseDmsProvider.SearchItemType.AllItems, token)
        Dim item = Await provider.ListRemoteItemAsync(destination, token)
        If item?.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(destination)
        Return item
    End Function
End Class
