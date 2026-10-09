Option Explicit On
Option Strict On

Imports System.Globalization
Imports System.IO

'An actual Windows executable supplies its own manifest/configuration on .NET Framework.
'A VSTest host's DPI configuration does not establish the demo application's DPI contract.
Friend Module PhysicalDpiProgram
    <STAThread>
    Friend Sub Main(args As String())
        Dim output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "physical-dpi-results", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture))
        Directory.CreateDirectory(output)
        Environment.SetEnvironmentVariable("DMS_PHYSICAL_DPI_TESTS", "1")
        Environment.SetEnvironmentVariable("DMS_GUI_TEST_NOTICE", "1")
        Environment.SetEnvironmentVariable("DMS_LOCALIZATION_PREVIEW_DIR", Path.Combine(output, "previews"))
        If args.Contains("--no-screenshots") Then Environment.SetEnvironmentVariable("DMS_LOCALIZATION_PREVIEW_DIR", Nothing)
        Dim notice As New DevelopmentGuiTestSession()
        Dim passed = 0
        Dim failed = 0
        Using log As New StreamWriter(Path.Combine(output, "results.txt"), False, New Text.UTF8Encoding(True))
            log.AutoFlush = True
            Console.SetOut(log)
            Console.SetError(log)
            Try
                Dim cultures = {"en-US", "de-DE", "fr-FR", "es-ES", "zh-CN", "zh-TW", "ja-JP", "ar-SA", "he-IL", "hi-IN"}
                If args.Any(Function(value) value <> "--no-screenshots" AndAlso Not cultures.Contains(value)) Then Throw New ArgumentException("Arguments must be supported culture names or --no-screenshots.")
                Dim selected = cultures.Where(Function(culture) Not args.Any(Function(value) value <> "--no-screenshots") OrElse args.Contains(culture)).ToArray()
                Dim screenshots = Not args.Contains("--no-screenshots")
                DevelopmentGuiTestSession.ConfigurePhysicalDpiHost()
                Dim displays = LocalizedMonitorDpiTest.ReadTimingConfiguration()
                Dim scenarios = LocalizedMonitorDpiTest.ReadSelectedScenarios()
                Environment.SetEnvironmentVariable("DMS_GUI_TEST_RUN_KEY", "physical-monitor-v2|" & Environment.Version.ToString() & "|screenshots=" & screenshots.ToString() & "|displays=" & displays.Key & "|scenarios=" & String.Join(",", scenarios))
                Environment.SetEnvironmentVariable("DMS_GUI_TEST_WORK_UNITS", (selected.Length * 2).ToString(CultureInfo.InvariantCulture))
                'Larger native surfaces cost more to lay out and repaint. Once measured,
                'history for this actual monitor configuration replaces the initial budget.
                Dim areaFactor = Math.Pow(Math.Max(125, displays.Value) / 125.0, 2)
                Dim fallback = CInt(Math.Ceiling(selected.Length * If(screenshots, 45, 20) * areaFactor * scenarios.Length / 13.0 + 5))
                Environment.SetEnvironmentVariable("DMS_GUI_TEST_FALLBACK_SECONDS", fallback.ToString(CultureInfo.InvariantCulture))
                notice.StartNotice()
                For Each culture In selected
                    For Each startOnRight In {False, True}
                        Try
                            Using context As New NUnit.Framework.Internal.TestExecutionContext.IsolatedContext()
                                Dim fixture As New LocalizedMonitorDpiTest()
                                fixture.ControlsFitAcrossNativeMonitors(culture, startOnRight)
                            End Using
                            passed += 1
                            log.WriteLine($"PASS {culture}; startOnRight={startOnRight}")
                        Catch ex As Exception
                            failed += 1
                            log.WriteLine($"FAIL {culture}; startOnRight={startOnRight}: {ex}")
                        End Try
                    Next
                Next
            Catch ex As Exception
                failed += 1
                log.WriteLine("SETUP FAILURE: " & ex.ToString())
            Finally
                Try
                    notice.CompletePhysicalRun(failed = 0 AndAlso passed > 0)
                    notice.StopNotice()
                Catch ex As Exception
                    failed += 1
                    log.WriteLine("NOTICE CLEANUP FAILURE: " & ex.ToString())
                End Try
                log.WriteLine($"Completed: passed={passed}; failed={failed}.")
                Environment.ExitCode = If(failed = 0, 0, 1)
            End Try
        End Using
    End Sub
End Module
