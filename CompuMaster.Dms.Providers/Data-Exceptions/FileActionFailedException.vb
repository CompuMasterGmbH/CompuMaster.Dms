Namespace Data

#Disable Warning CA2237 ' Mark ISerializable types with serializable
#Disable Warning CA1032 ' Implement standard exception constructors
    Public Class FileActionFailedException
#Enable Warning CA1032 ' Implement standard exception constructors
#Enable Warning CA2237 ' Mark ISerializable types with serializable
        Inherits System.Exception

        Public Sub New(action As String, remotePath As String)
            MyBase.New(ProviderStrings.Format("ActionFailedForFile", action, remotePath))
            Me.RemotePathSource = remotePath
            Me.RemotePathDestination = remotePath
        End Sub

        Public Sub New(action As String, remotePath As String, innerException As Exception)
            MyBase.New(ProviderStrings.Format("ActionFailedForFile", action, remotePath), innerException)
            Me.RemotePathSource = remotePath
            Me.RemotePathDestination = remotePath
        End Sub

        Public Sub New(action As String, remoteSourcePath As String, remoteDestinationPath As String)
            MyBase.New(ProviderStrings.Format("ActionFailedForSourceFileAndDestination", action, remoteSourcePath, remoteDestinationPath))
            Me.RemotePathSource = remoteSourcePath
            Me.RemotePathDestination = remoteDestinationPath
        End Sub

        Public Sub New(action As String, remoteSourcePath As String, remoteDestinationPath As String, innerException As Exception)
            MyBase.New(ProviderStrings.Format("ActionFailedForSourceFileAndDestination", action, remoteSourcePath, remoteDestinationPath), innerException)
            Me.RemotePathSource = remoteSourcePath
            Me.RemotePathDestination = remoteDestinationPath
        End Sub

        Public Property RemotePathSource As String
        Public Property RemotePathDestination As String

    End Class

End Namespace