Option Strict On

Imports System.Diagnostics
Imports System.Globalization
Imports System.IO

Namespace Providers
    'Opt-in, content-free diagnostics for comparing startup phases. Enum labels
    'prevent account names, URLs, paths, headers and resource contents being logged.
    Friend NotInheritable Class PerformanceMeasurement
        Implements IDisposable

        Friend Enum Phase
            BrowserStartup
            Authorization
            OcsDiscovery
            Propfind
            OcsShares
            EntryConversion
            FileRendering
            HttpQueue
            HttpTransport
            OcsHttpTransport
            OcsShareeDiscovery
            OcsConfiguration
        End Enum

        Private Shared ReadOnly WriteLock As New Object
        Private ReadOnly Label As Phase
        Private ReadOnly Destination As String
        Private ReadOnly Elapsed As Stopwatch

        Friend Sub New(phase As Phase)
            Label = phase
            Destination = Environment.GetEnvironmentVariable("DMS_PERFORMANCE_LOG")
            If Not String.IsNullOrWhiteSpace(Destination) Then Elapsed = Stopwatch.StartNew()
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            If Elapsed Is Nothing Then Return
            Elapsed.Stop()
            Try
                Dim processId As Integer
                Using currentProcess = Process.GetCurrentProcess()
                    processId = currentProcess.Id
                End Using
                Dim line = String.Join(",", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    processId.ToString(CultureInfo.InvariantCulture), Label.ToString(),
                    Elapsed.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)) & Environment.NewLine
                SyncLock WriteLock
                    File.AppendAllText(Destination, line, New System.Text.UTF8Encoding(False))
                End SyncLock
            Catch
                'A diagnostic output failure must never change the provider/UI behavior.
            End Try
        End Sub
    End Class
End Namespace
