Namespace Data

    ''' <summary>Reports a remote response status and its description.</summary>
#Disable Warning CA2237 ' Mark ISerializable types with serializable
#Disable Warning CA1032 ' Implement standard exception constructors
    Public Class ResponseStatusCodeException
#Enable Warning CA1032 ' Implement standard exception constructors
#Enable Warning CA2237 ' Mark ISerializable types with serializable
        Inherits System.Exception

        ''' <summary>Initializes the exception with the supplied failure details.</summary>
        ''' <param name="statusCode">The numeric response status.</param>
        ''' <param name="description">The response description.</param>
        Public Sub New(statusCode As Integer, description As String)
            MyBase.New(statusCode & " " & description)
            Me.StatusCode = statusCode
            Me.Description = description
        End Sub

        ''' <summary>Initializes the exception with the supplied failure details.</summary>
        ''' <param name="statusCode">The numeric response status.</param>
        ''' <param name="description">The response description.</param>
        ''' <param name="innerException">The original failure retained as the inner exception.</param>
        Public Sub New(statusCode As Integer, description As String, innerException As Exception)
            MyBase.New(statusCode & " " & description, innerException)
            Me.StatusCode = statusCode
            Me.Description = description
        End Sub

        ''' <summary>Gets or sets the numeric status returned by the remote service.</summary>
        Public Property StatusCode As Integer
        ''' <summary>Gets or sets the remote response description.</summary>
        Public Property Description As String

    End Class

End Namespace
