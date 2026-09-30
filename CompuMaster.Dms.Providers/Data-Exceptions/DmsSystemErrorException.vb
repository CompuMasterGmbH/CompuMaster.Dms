Namespace Data

#Disable Warning CA2237 ' Mark ISerializable types with serializable
#Disable Warning CA1032 ' Implement standard exception constructors
    Public Class DmsSystemErrorException
#Enable Warning CA1032 ' Implement standard exception constructors
#Enable Warning CA2237 ' Mark ISerializable types with serializable
        Inherits System.Exception

        Public Sub New(message As String)
            MyBase.New(message)
        End Sub

        ''' <summary>
        ''' Initializes a new exception with a specified error message and the exception that caused the system error.
        ''' </summary>
        ''' <param name="message">The message that describes the system error.</param>
        ''' <param name="innerException">The exception that caused the system error.</param>
        Public Sub New(message As String, innerException As Exception)
            MyBase.New(message, innerException)
        End Sub

    End Class

End Namespace
