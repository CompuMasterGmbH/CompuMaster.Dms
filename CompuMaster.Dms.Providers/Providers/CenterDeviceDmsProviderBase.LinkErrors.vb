Imports CenterDevice.Rest.Exceptions
Imports CompuMaster.Dms.Data

Namespace Providers
    Partial Public MustInherit Class CenterDeviceDmsProviderBase
        Friend Shared Function CreateLinkServerFailure(failure As RestClientException) As DmsUserErrorMessageException
            Dim message = If(failure.ErrorResponse?.Message, failure.Message)
            If TypeOf failure Is ForbiddenException Then
                message = ProviderStrings.Format("Forbidden", message)
            ElseIf TypeOf failure Is BadRequestException AndAlso failure.ErrorResponse?.Data IsNot Nothing AndAlso failure.ErrorResponse.Data.ContainsKey("explanation") Then
                message = CStr(failure.ErrorResponse.Data("explanation"))
            End If
            Return New DmsUserErrorMessageException(message, failure)
        End Function
    End Class
End Namespace
