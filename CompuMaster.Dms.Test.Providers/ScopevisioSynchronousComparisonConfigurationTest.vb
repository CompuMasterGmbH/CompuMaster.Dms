Option Explicit On
Option Strict On

Imports NUnit.Framework

<TestFixture>
Public Class ScopevisioSynchronousComparisonConfigurationTest
    <Test>
    Public Sub DefaultDiagnosticRetainsItsEightAndTwelveMiBComparator()
        Assert.That(ScopevisioSynchronousUploadComparisonTest.ComparisonSizes(Nothing), [Is].EqualTo(New Integer() {8, 12}))
    End Sub

    <Test>
    Public Sub ExplicitLargeComparisonSelectsOnlyTheRequestedLargeFixture()
        Assert.That(ScopevisioSynchronousUploadComparisonTest.ComparisonSizes("256"), [Is].EqualTo(New Integer() {256}))
    End Sub

    <TestCase("0"), TestCase("12"), TestCase("4097")>
    Public Sub ExplicitLargeComparisonCannotReduceTheLargeTransferGate(value As String)
        Assert.Throws(Of InvalidOperationException)(Sub() ScopevisioSynchronousUploadComparisonTest.ComparisonSizes(value))
    End Sub

    <Test>
    Public Sub MalformedExplicitSizeFailsBeforeServerAccess()
        Assert.Throws(Of FormatException)(Sub() ScopevisioSynchronousUploadComparisonTest.ComparisonSizes("invalid"))
    End Sub
End Class
