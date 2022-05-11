Namespace Data

#Disable Warning CA2237 ' Mark ISerializable types with serializable
#Disable Warning CA1032 ' Implement standard exception constructors
    Public Class ResponseStatusCodeException
#Enable Warning CA1032 ' Implement standard exception constructors
#Enable Warning CA2237 ' Mark ISerializable types with serializable
        Inherits System.Exception

        Public Sub New(statusCode As Integer, description As String)
            MyBase.New(statusCode & " " & description)
            Me.StatusCode = statusCode
            Me.Description = description
        End Sub

        Public Sub New(statusCode As Integer, description As String, innerException As Exception)
            MyBase.New(statusCode & " " & description, innerException)
            Me.StatusCode = statusCode
            Me.Description = description
        End Sub

        Public Property StatusCode As Integer
        Public Property Description As String

    End Class

End Namespace