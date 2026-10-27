Option Explicit On
Option Strict On

Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Namespace Global.CompuMaster.Dms.BrowserUI
    Friend NotInheritable Class DmsResourceDetailsResolver
        Friend Shared Async Function LoadAsync(provider As BaseDmsProvider, entry As DmsResourceItem) As Task(Of DmsResourceItem)
            Dim details As DmsResourceItem
            'WebDAV file/folder IDs are optional identity metadata, not an ID-search capability.
            Dim webDavFolder = provider.DmsProviderID = BaseDmsProvider.DmsProviders.WebDAV AndAlso entry.ItemType = DmsResourceItem.ItemTypes.Folder
            Dim webDavFile = provider.DmsProviderID = BaseDmsProvider.DmsProviders.WebDAV AndAlso entry.ItemType = DmsResourceItem.ItemTypes.File
            If entry.ItemType = DmsResourceItem.ItemTypes.Collection AndAlso Not String.IsNullOrEmpty(entry.ExtendedInfosCollectionID) Then
                details = Await provider.FindCollectionByIdAsync(entry.ExtendedInfosCollectionID).ConfigureAwait(False)
            ElseIf entry.ItemType = DmsResourceItem.ItemTypes.Folder AndAlso Not String.IsNullOrEmpty(entry.ExtendedInfosFolderID) AndAlso Not webDavFolder Then
                details = Await provider.FindFolderByIdAsync(entry.ExtendedInfosFolderID).ConfigureAwait(False)
            ElseIf entry.ItemType = DmsResourceItem.ItemTypes.File AndAlso Not String.IsNullOrEmpty(entry.ExtendedInfosFileID) AndAlso Not webDavFile Then
                details = Await provider.FindFileByIdAsync(entry.ExtendedInfosFileID).ConfigureAwait(False)
            Else
                details = Await provider.ListRemoteItemAsync(entry.FullName).ConfigureAwait(False)
            End If
            If details Is Nothing Then
                If entry.ItemType = DmsResourceItem.ItemTypes.File Then Throw New Data.FileNotFoundException(entry.FullName)
                Throw New Data.DirectoryNotFoundException(entry.FullName)
            End If
            If webDavFolder AndAlso Not String.IsNullOrEmpty(entry.ExtendedInfosFolderID) AndAlso
                (details.ItemType <> DmsResourceItem.ItemTypes.Folder OrElse Not String.Equals(details.ExtendedInfosFolderID, entry.ExtendedInfosFolderID, StringComparison.Ordinal)) Then
                Throw New InvalidOperationException("The selected folder identity changed or is no longer available.")
            End If
            If webDavFile AndAlso Not String.IsNullOrEmpty(entry.ExtendedInfosFileID) AndAlso
                (details.ItemType <> DmsResourceItem.ItemTypes.File OrElse Not String.Equals(details.ExtendedInfosFileID, entry.ExtendedInfosFileID, StringComparison.Ordinal)) Then
                Throw New InvalidOperationException("The selected file identity changed or is no longer available.")
            End If
            'Identifier lookups can return detached SDK objects. Keep the selected browsing context.
            details.FullName = entry.FullName
            details.Collection = entry.Collection
            details.Folder = entry.Folder
            details.ExtendedInfosAssignedCollectionID = entry.ExtendedInfosAssignedCollectionID
            details.ExtendedInfosAssignedFolderID = entry.ExtendedInfosAssignedFolderID
            details.ExtendedInfosCollisionDetected = entry.ExtendedInfosCollisionDetected
            Return details
        End Function
    End Class
End Namespace
