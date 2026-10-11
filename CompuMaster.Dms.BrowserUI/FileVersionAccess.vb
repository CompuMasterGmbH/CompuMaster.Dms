Friend NotInheritable Class FileVersionAccess
    Friend ReadOnly CanViewHistory As Boolean
    Friend ReadOnly CanDownload As Boolean
    Friend ReadOnly CanRestore As Boolean
    Friend ReadOnly CanRemove As Boolean

    Friend Sub New(actions As DmsBrowser.FileOrFolderActions, historySupported As Boolean,
                   downloadSupported As Boolean, restoreSupported As Boolean, removalSupported As Boolean)
        CanViewHistory = historySupported AndAlso (actions And DmsBrowser.FileOrFolderActions.AllowViewFileVersionHistory) <> 0
        CanDownload = CanViewHistory AndAlso downloadSupported AndAlso (actions And DmsBrowser.FileOrFolderActions.AllowDownloadFiles) <> 0
        CanRestore = CanViewHistory AndAlso restoreSupported AndAlso (actions And DmsBrowser.FileOrFolderActions.AllowRestoreFileVersions) <> 0
        CanRemove = CanViewHistory AndAlso removalSupported AndAlso (actions And DmsBrowser.FileOrFolderActions.AllowRemoveFileVersions) <> 0
    End Sub
End Class
