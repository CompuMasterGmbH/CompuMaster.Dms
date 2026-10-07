Namespace Data

    ''' <summary>Reports failed authentication or a session requiring reauthorization.</summary>
#Disable Warning CA2237 ' Mark ISerializable types with serializable
#Disable Warning CA1032 ' Implement standard exception constructors
    Public Class DmsUserAuthenticationException
#Enable Warning CA1032 ' Implement standard exception constructors
#Enable Warning CA2237 ' Mark ISerializable types with serializable
        Inherits System.Exception

        ''' <summary>Initializes the exception with the supplied failure details.</summary>
        ''' <param name="message">The display message describing the failure.</param>
        Public Sub New(message As String)
            MyBase.New(message)
        End Sub

        ''' <summary>
        ''' Initializes a new exception with a specified error message and the exception that caused the authentication failure.
        ''' </summary>
        ''' <param name="message">The message that describes the authentication failure.</param>
        ''' <param name="innerException">The exception that caused the authentication failure.</param>
        Public Sub New(message As String, innerException As Exception)
            MyBase.New(message, innerException)
        End Sub

    End Class

End Namespace
