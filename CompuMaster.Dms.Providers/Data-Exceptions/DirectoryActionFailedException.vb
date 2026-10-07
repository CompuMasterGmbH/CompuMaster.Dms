Namespace Data

    ''' <summary>Reports a failed action on a remote directory and retains the affected paths.</summary>
#Disable Warning CA2237 ' Mark ISerializable types with serializable
#Disable Warning CA1032 ' Implement standard exception constructors
    Public Class DirectoryActionFailedException
#Enable Warning CA1032 ' Implement standard exception constructors
#Enable Warning CA2237 ' Mark ISerializable types with serializable
        Inherits System.Exception

        ''' <summary>Initializes the exception with the supplied failure details.</summary>
        ''' <param name="action">The technical name of the action that failed.</param>
        ''' <param name="remotePath">The affected remote resource path.</param>
        Public Sub New(action As String, remotePath As String)
            MyBase.New(ProviderStrings.Format("ActionFailedForDirectory", action, remotePath))
            Me.RemotePathSource = remotePath
            Me.RemotePathDestination = remotePath
        End Sub

        ''' <summary>Initializes the exception with the supplied failure details.</summary>
        ''' <param name="action">The technical name of the action that failed.</param>
        ''' <param name="remotePath">The affected remote resource path.</param>
        ''' <param name="innerException">The original failure retained as the inner exception.</param>
        Public Sub New(action As String, remotePath As String, innerException As Exception)
            MyBase.New(ProviderStrings.Format("ActionFailedForDirectory", action, remotePath), innerException)
            Me.RemotePathSource = remotePath
            Me.RemotePathDestination = remotePath
        End Sub

        ''' <summary>Initializes the exception with the supplied failure details.</summary>
        ''' <param name="action">The technical name of the action that failed.</param>
        ''' <param name="remoteSourcePath">The remote source path.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        Public Sub New(action As String, remoteSourcePath As String, remoteDestinationPath As String)
            MyBase.New(ProviderStrings.Format("ActionFailedForSourceDirectoryAndDestination", action, remoteSourcePath, remoteDestinationPath))
            Me.RemotePathSource = remoteSourcePath
            Me.RemotePathDestination = remoteDestinationPath
        End Sub

        ''' <summary>Initializes the exception with the supplied failure details.</summary>
        ''' <param name="action">The technical name of the action that failed.</param>
        ''' <param name="remoteSourcePath">The remote source path.</param>
        ''' <param name="remoteDestinationPath">The remote destination path.</param>
        ''' <param name="innerException">The original failure retained as the inner exception.</param>
        Public Sub New(action As String, remoteSourcePath As String, remoteDestinationPath As String, innerException As Exception)
            MyBase.New(ProviderStrings.Format("ActionFailedForSourceDirectoryAndDestination", action, remoteSourcePath, remoteDestinationPath), innerException)
            Me.RemotePathSource = remoteSourcePath
            Me.RemotePathDestination = remoteDestinationPath
        End Sub

        ''' <summary>Gets or sets the affected source path, independent of the localized message.</summary>
        Public Property RemotePathSource As String
        ''' <summary>Gets or sets the affected destination path, independent of the localized message.</summary>
        Public Property RemotePathDestination As String

    End Class

End Namespace
