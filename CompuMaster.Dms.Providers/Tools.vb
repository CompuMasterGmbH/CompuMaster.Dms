Option Explicit On
Option Strict On

Friend NotInheritable Class Tools

    ''' <summary>
    ''' Check if a text contains all required search values
    ''' </summary>
    ''' <param name="text"></param>
    ''' <param name="searchValues"></param>
    ''' <returns></returns>
    Public Shared Function StringContainsAllMustValues(text As String, ParamArray searchValues As String()) As Boolean
        If text = Nothing Then Throw New ArgumentNullException(NameOf(text))
        If searchValues.Length = 0 Then Throw New ArgumentNullException(NameOf(searchValues))
        For MyCounter As Integer = 0 To searchValues.Length - 1
            If text.Contains(searchValues(MyCounter)) = False Then Return False
        Next
        Return True
    End Function

    ''' <summary>
    ''' Check if a text contains at least 1 of required search values
    ''' </summary>
    ''' <param name="text"></param>
    ''' <param name="searchValues"></param>
    ''' <returns></returns>
    Public Shared Function StringContainsAtLeastOneOfAllValues(text As String, ParamArray searchValues As String()) As Boolean
        If text = Nothing Then Throw New ArgumentNullException(NameOf(text))
        If searchValues.Length = 0 Then Throw New ArgumentNullException(NameOf(searchValues))
        For MyCounter As Integer = 0 To searchValues.Length - 1
            If text.Contains(searchValues(MyCounter)) = True Then Return True
        Next
        Return False
    End Function

    Public Shared Function NotNaNOrAlternativeValue(firstChoice As Double, alternativeChoice As Double) As Double
        If Not Double.IsNaN(firstChoice) Then
            Return firstChoice
        Else
            Return alternativeChoice
        End If
    End Function

    Public Shared Function NotNullOrEmptyStringValue(value As String) As String
        If value <> Nothing Then
            Return value
        Else
            Return String.Empty
        End If
    End Function

    Public Shared Function NotEmptyOrAlternativeValue(firstChoice As String, alternativeChoice As String) As String
        If firstChoice <> Nothing Then
            Return firstChoice
        Else
            Return alternativeChoice
        End If
    End Function

    Public Shared Function NotEmptyOrAlternativeValue(firstChoice As DateTime, alternativeChoice As DateTime) As DateTime
        If firstChoice <> Nothing Then
            Return firstChoice
        Else
            Return alternativeChoice
        End If
    End Function

    Public Shared Function NotZeroOrAlternativeValueToString(value As Integer, alternativeChoice As String) As String
        If value = Nothing Then
            Return alternativeChoice
        Else
            Return value.ToString
        End If
    End Function

    Public Shared Function IIf(expression As Boolean, firstChoice As String, alternativeChoice As String) As String
        If expression Then
            Return firstChoice
        Else
            Return alternativeChoice
        End If
    End Function

    Public Shared Function IIf(Of T)(expression As Boolean, firstChoice As T, alternativeChoice As T) As T
        If expression Then
            Return firstChoice
        Else
            Return alternativeChoice
        End If
    End Function

    ''' <summary>
    ''' Convert a nullable type to its string representation
    ''' </summary>
    ''' <typeparam name="T"></typeparam>
    ''' <param name="value"></param>
    ''' <returns></returns>
    Public Shared Function ConvertToString(Of T As Structure)(ByVal value As Nullable(Of T)) As String
        If value.HasValue() Then
            Return value.ToString
        Else
            Return Nothing
        End If
    End Function

    ''' <summary>
    ''' Safe conversion to double (with compatibility to Mono's .Net Framework)
    ''' </summary>
    ''' <param name="value"></param>
    ''' <returns></returns>
    Public Shared Function ConvertToDoubleWithCultureDE(value As Object) As Double
        If value Is Nothing Then
            Return 0.0
        ElseIf value.GetType Is GetType(String) Then
            Return Double.Parse(CType(value, String), System.Globalization.CultureInfo.GetCultureInfo("de-DE"))
        Else
            Return CType(value, Double)
        End If
    End Function

    ''' <summary>
    ''' Compare Double values and check for equality or equality after rounding with accepted difference of &lt; 0.01
    ''' </summary>
    ''' <param name="value1"></param>
    ''' <param name="value2"></param>
    ''' <returns></returns>
    Public Shared Function DoubleValueIsEqual(value1 As Double, value2 As Double) As Boolean
        Return DoubleValueIsEqual(value1, value2, 0.01)
    End Function

    ''' <summary>
    ''' Compare Double values and check for equality or equality after rounding with accepted difference of &lt; inAcceptableDifference
    ''' </summary>
    ''' <param name="value1"></param>
    ''' <param name="value2"></param>
    ''' <param name="inAcceptableDifference"></param>
    ''' <returns></returns>
    Public Shared Function DoubleValueIsEqual(value1 As Double, value2 As Double, inAcceptableDifference As Double) As Boolean
        If inAcceptableDifference < 0.0 Then Throw New ArgumentOutOfRangeException("Must be >= 0", NameOf(inAcceptableDifference))
        Dim Diff As Double = value1 - value2
        If Diff = 0.0 Then
            'Is equal
            Return True
        ElseIf Diff < inAcceptableDifference AndAlso Diff > -1 * inAcceptableDifference Then
            'Accept as equal
            Return True
        Else
            'Is not equal
            Return False
        End If
    End Function

    ''' <summary>
    ''' The value of an enum member matching to its System.ComponentModel.DescriptionAttribute or the enum member's name
    ''' </summary>
    ''' <typeparam name="T"></typeparam>
    ''' <param name="value"></param>
    ''' <returns></returns>
    Public Shared Function ParseEnumValueFromDescriptionOrNameOrValue(Of T)(value As String) As T
        Return ParseEnumValueFromDescriptionOrNameOrValue(Of T)(value, Nothing)
    End Function

    ''' <summary>
    ''' The value of an enum member matching to its System.ComponentModel.DescriptionAttribute or the enum member's name
    ''' </summary>
    ''' <typeparam name="T"></typeparam>
    ''' <param name="value"></param>
    ''' <param name="defaultValue"></param>
    ''' <returns></returns>
    Public Shared Function ParseEnumValueFromDescriptionOrNameOrValue(Of T)(value As String, defaultValue As T) As T
        If value = Nothing Then
            Return defaultValue
        Else
            For Each TValue As T In [Enum].GetValues(GetType(T))
                Dim TValueDescr As String = EnumValueDescriptionOrName(Of T)(TValue)
                If value = TValueDescr Then
                    Return TValue
                End If
            Next
            Return CType([Enum].Parse(GetType(T), value), T)
        End If
    End Function

    ''' <summary>
    ''' The description of an enum member as defined by System.ComponentModel.DescriptionAttribute or the enum member's name
    ''' </summary>
    ''' <typeparam name="T"></typeparam>
    ''' <param name="value"></param>
    ''' <returns></returns>
    Public Shared Function EnumValueDescriptionOrName(Of T)(value As T) As String
        'Tries to find a DescriptionAttribute for a potential friendly name
        'for the enum
        Dim memberInfo As System.Reflection.MemberInfo() = GetType(T).GetMember(value.ToString())
        If memberInfo IsNot Nothing AndAlso memberInfo.Length > 0 Then
            Dim attrs As Object() = memberInfo(0).GetCustomAttributes(GetType(System.ComponentModel.DescriptionAttribute), False)
            If (attrs IsNot Nothing AndAlso attrs.Length > 0 AndAlso attrs.Where(Function(attr As Object)
                                                                                     Return attr.GetType() = GetType(System.ComponentModel.DescriptionAttribute)
                                                                                 End Function).FirstOrDefault() IsNot Nothing) Then
                'Pull out the description value
                Return CType(attrs.Where(Function(attr As Object)
                                             Return attr.GetType() = GetType(System.ComponentModel.DescriptionAttribute)
                                         End Function).FirstOrDefault(), System.ComponentModel.DescriptionAttribute).Description
            End If
        End If
        'If we have no description attribute, just return the ToString of the enum
        Return value.ToString()
    End Function

    ''' <summary>
    ''' The descriptions of all enum members as defined by System.ComponentModel.DescriptionAttribute or the enum member's name
    ''' </summary>
    ''' <typeparam name="T"></typeparam>
    ''' <returns></returns>
    Public Shared Function AllEnumValueDescriptionsOrNames(Of T)() As List(Of String)
        Dim EnumValues As New List(Of String)
        For Each ValueObject As Object In [Enum].GetValues(GetType(T))
            Dim ValueT As T = CType(ValueObject, T)
            EnumValues.Add(EnumValueDescriptionOrName(ValueT))
        Next
        Return EnumValues
    End Function

End Class
