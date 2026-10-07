Namespace Data

#Disable Warning CA2237 ' Mark ISerializable types with serializable
#Disable Warning CA1032 ' Implement standard exception constructors
    ''' <summary>
    ''' Thrown when a remote item exists multiple times with the very same name
    ''' </summary>
    Public Class RemotePathNotUniqueException
#Enable Warning CA1032 ' Implement standard exception constructors
#Enable Warning CA2237 ' Mark ISerializable types with serializable
        Inherits System.Exception

        ''' <summary>Initializes the exception with the supplied failure details.</summary>
        ''' <param name="remotePath">The affected remote resource path.</param>
        Public Sub New(remotePath As String)
            MyBase.New(ProviderStrings.Format("RemoteItemExistsMultipleTimes", remotePath))
            Me.RemotePath = remotePath
        End Sub

        ''' <summary>Gets or sets the affected remote DMS path, independent of the localized message.</summary>
        Public Property RemotePath As String

    End Class

End Namespace
