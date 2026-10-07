Namespace Data

    ''' <summary>Reports a DMS error whose message can be presented to the user.</summary>
#Disable Warning CA2237 ' Mark ISerializable types with serializable
#Disable Warning CA1032 ' Implement standard exception constructors
    Public Class DmsUserErrorMessageException
#Enable Warning CA1032 ' Implement standard exception constructors
#Enable Warning CA2237 ' Mark ISerializable types with serializable
        Inherits System.Exception

        ''' <summary>Initializes the exception with the supplied failure details.</summary>
        ''' <param name="message">The display message describing the failure.</param>
        Public Sub New(message As String)
            MyBase.New(message)
        End Sub

        ''' <summary>Creates a user-facing error while retaining its original cause.</summary>
        ''' <param name="message">The message shown to the user.</param>
        ''' <param name="innerException">The original failure.</param>
        Public Sub New(message As String, innerException As Exception)
            MyBase.New(message, innerException)
        End Sub

    End Class

End Namespace
