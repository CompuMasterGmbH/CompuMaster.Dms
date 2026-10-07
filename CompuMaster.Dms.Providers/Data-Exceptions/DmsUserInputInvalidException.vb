Namespace Data

    ''' <summary>Reports user input that does not satisfy the requested DMS operation.</summary>
#Disable Warning CA2237 ' Mark ISerializable types with serializable
#Disable Warning CA1032 ' Implement standard exception constructors
    Public Class DmsUserInputInvalidException
#Enable Warning CA1032 ' Implement standard exception constructors
#Enable Warning CA2237 ' Mark ISerializable types with serializable
        Inherits System.Exception

        ''' <summary>Initializes the exception with the supplied failure details.</summary>
        ''' <param name="message">The display message describing the failure.</param>
        Public Sub New(message As String)
            MyBase.New(message)
        End Sub

    End Class

End Namespace
