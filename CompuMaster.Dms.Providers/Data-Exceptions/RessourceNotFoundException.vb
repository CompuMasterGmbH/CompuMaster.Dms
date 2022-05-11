Namespace Data

#Disable Warning CA2237 ' Mark ISerializable types with serializable
#Disable Warning CA1032 ' Implement standard exception constructors
    Public Class RessourceNotFoundException
#Enable Warning CA1032 ' Implement standard exception constructors
#Enable Warning CA2237 ' Mark ISerializable types with serializable
        Inherits System.Exception

        Public Sub New(remotePath As String)
            MyBase.New("Ressource not found: " & remotePath)
            Me.RemotePath = remotePath
        End Sub

        Public Property RemotePath As String

    End Class

End Namespace