Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture>
Public Class ScopevisioUserDisplayNameTest

    <TestCase("{""first-name"":"" Ada "",""last-name"":"" Lovelace ""}", "Ada Lovelace")>
    <TestCase("{""first-name"":""TECHNICAL_USER_123"",""last-name"":""""}", "TECHNICAL_USER_123")>
    <TestCase("{""first-name"":"""",""last-name"":""Lovelace""}", "Lovelace")>
    Public Sub HyphenatedApiNameFieldsProduceDisplayName(json As String, expected As String)
        ClassicAssert.AreEqual(expected, ScopevisioTeamworkDmsProvider.ParseUserDisplayName(json))
    End Sub

    <TestCase("{""first-name"":null,""last-name"":"" ""}")>
    <TestCase("{""firstName"":""Ignored"",""lastName"":""Ignored""}")>
    <TestCase("{""groups"":[{""name"":""Unrelated group""}]}")>
    <TestCase("{}")>
    <TestCase("")>
    Public Sub MissingApiNameFieldsRetainIdFallback(json As String)
        ClassicAssert.AreEqual(String.Empty, ScopevisioTeamworkDmsProvider.ParseUserDisplayName(json))
    End Sub

End Class
