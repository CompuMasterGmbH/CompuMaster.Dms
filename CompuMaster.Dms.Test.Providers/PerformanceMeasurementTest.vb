Option Strict On

Imports System.IO
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, NonParallelizable>
Public Class PerformanceMeasurementTest
    <TestCase(False), TestCase(True)>
    Public Sub DiagnosticOutputIsOptInAndContainsOnlyFixedPhasesAndTiming(enabled As Boolean)
        Dim previous = Environment.GetEnvironmentVariable("DMS_PERFORMANCE_LOG")
        Dim path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DmsPerformance_" & Guid.NewGuid().ToString("N") & ".csv")
        Try
            Environment.SetEnvironmentVariable("DMS_PERFORMANCE_LOG", If(enabled, path, Nothing))
            For Each phase As PerformanceMeasurement.Phase In [Enum].GetValues(GetType(PerformanceMeasurement.Phase))
                Using measurement As New PerformanceMeasurement(phase)
                End Using
            Next
            Assert.That(File.Exists(path), [Is].EqualTo(enabled))
            If enabled Then
                Dim lines = File.ReadAllLines(path)
                Assert.That(lines.Length, [Is].EqualTo([Enum].GetValues(GetType(PerformanceMeasurement.Phase)).Length))
                For Each line In lines
                    Dim columns = line.Split(","c)
                    Assert.That(columns.Length, [Is].EqualTo(4))
                    Assert.That([Enum].IsDefined(GetType(PerformanceMeasurement.Phase), columns(2)), [Is].True)
                    Assert.That(Double.Parse(columns(3), Globalization.CultureInfo.InvariantCulture), [Is].GreaterThanOrEqualTo(0))
                Next
            End If
        Finally
            Environment.SetEnvironmentVariable("DMS_PERFORMANCE_LOG", previous)
            If File.Exists(path) Then File.Delete(path)
        End Try
    End Sub

    <Test>
    Public Sub InvalidDiagnosticDestinationDoesNotBreakAnOperation()
        Dim previous = Environment.GetEnvironmentVariable("DMS_PERFORMANCE_LOG")
        Try
            Environment.SetEnvironmentVariable("DMS_PERFORMANCE_LOG", Path.GetTempPath())
            Assert.DoesNotThrow(Sub()
                                    Using measurement As New PerformanceMeasurement(PerformanceMeasurement.Phase.Propfind)
                                    End Using
                                End Sub)
        Finally
            Environment.SetEnvironmentVariable("DMS_PERFORMANCE_LOG", previous)
        End Try
    End Sub
End Class
