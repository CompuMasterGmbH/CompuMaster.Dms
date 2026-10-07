Namespace Data

    ''' <summary>Reports an existing remote file at the requested destination.</summary>
#Disable Warning CA2237 ' Mark ISerializable types with serializable
#Disable Warning CA1032 ' Implement standard exception constructors
    Public Class FileAlreadyExistsException
#Enable Warning CA1032 ' Implement standard exception constructors
#Enable Warning CA2237 ' Mark ISerializable types with serializable
        Inherits System.Exception

        ''' <summary>Initializes the exception with the supplied failure details.</summary>
        ''' <param name="remotePath">The affected remote resource path.</param>
        Public Sub New(remotePath As String)
            MyBase.New(ProviderStrings.Format("FileAlreadyExists", remotePath))
            Me.RemotePath = remotePath
        End Sub

        ''' <summary>Initializes the exception with the supplied failure details.</summary>
        ''' <param name="remotePath">The affected remote resource path.</param>
        ''' <param name="innerException">The original failure retained as the inner exception.</param>
        Public Sub New(remotePath As String, innerException As Exception)
            MyBase.New(ProviderStrings.Format("FileAlreadyExists", remotePath), innerException)
            Me.RemotePath = remotePath
        End Sub

        ''' <summary>Gets or sets the affected remote DMS path, independent of the localized message.</summary>
        Public Property RemotePath As String

    End Class

End Namespace
