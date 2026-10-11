Imports System.IO

Namespace Providers
    Partial Public MustInherit Class CenterDeviceDmsProviderBase
        Friend Shared Function DirectoryCreationFailure(operation As String, remotePath As String, failure As CenterDevice.Rest.Exceptions.RestClientException) As IOException
            Dim message = ProviderStrings.Format("Failed", operation, remotePath)
            If failure.ErrorResponse Is Nothing Then Return New IOException(message, failure)
            Return New IOException(message, New Data.ResponseStatusCodeException(failure.ErrorResponse.Code, failure.ErrorResponse.Message, failure))
        End Function
    End Class
End Namespace
