Imports System.Diagnostics
Imports System.Globalization
Imports CompuMaster.Dms.Data

Friend NotInheritable Class TransferDisplay
    Private ReadOnly Clock As Func(Of Double)
    Private ReadOnly Samples As New Queue(Of Tuple(Of Double, Long))
    Private Started As Double
    Private LastBytes As Long
    Private ActiveFile As Integer = -1

    Friend Sub New(Optional clock As Func(Of Double) = Nothing)
        Dim timer = Stopwatch.StartNew()
        Me.Clock = If(clock, Function() timer.Elapsed.TotalSeconds)
    End Sub

    Friend Function Describe(file As Integer, counter As DmsTransferProgress, state As UploadFileState) As String
        Dim now = Clock()
        If ActiveFile <> file Then Reset(file, now)
        Dim bytes = counter?.BytesTransferred
        If bytes.HasValue AndAlso bytes.Value < LastBytes Then Reset(file, now)
        Dim processed As String
        If Not bytes.HasValue Then
            processed = UiStrings.GetText("UploadBytesUnknown")
        Else
            LastBytes = bytes.Value
            Dim unit = UnitIndex(If(counter.TotalBytes, bytes.Value))
            processed = If(counter.TotalBytes.HasValue,
                UiStrings.Format("UploadSourceBytes", Amount(bytes.Value, unit), Amount(counter.TotalBytes.GetValueOrDefault(), unit), UnitName(unit)),
                UiStrings.Format("UploadSourceBytesUnknownTotal", Amount(bytes.Value, unit), UnitName(unit)))
        End If
        Dim speed As String = UiStrings.GetText("TransferUnknown")
        Dim eta As String = UiStrings.GetText("TransferUnknown")
        If bytes.HasValue Then
            Samples.Enqueue(Tuple.Create(now, bytes.Value))
            While Samples.Count > 1 AndAlso Samples.ElementAt(1).Item1 <= now - 2
                Samples.Dequeue()
            End While
            Dim span = now - Samples.Peek().Item1
            If span > 0 Then
                Dim rate = If(state = UploadFileState.Transferring, (bytes.Value - Samples.Peek().Item2) / span, 0)
                speed = FormatBytes(CDec(Math.Max(0, rate)))
            End If
            Dim elapsed = now - Started
            If state = UploadFileState.Completed Then
                eta = "00:00:00"
            ElseIf state = UploadFileState.Transferring AndAlso counter.TotalBytes.HasValue AndAlso elapsed > 0 AndAlso bytes.Value > 0 AndAlso bytes.Value < counter.TotalBytes.Value Then
                Dim seconds = (counter.TotalBytes.Value - CDec(bytes.Value)) * CDec(elapsed) / bytes.Value
                If seconds < CDec(TimeSpan.MaxValue.TotalSeconds) - 1 Then eta = TimeSpan.FromSeconds(CDbl(Math.Ceiling(seconds))).ToString("c", CultureInfo.InvariantCulture)
            End If
        End If
        Return UiStrings.Format("TransferDetails", processed, speed, eta)
    End Function

    Private Sub Reset(file As Integer, now As Double)
        ActiveFile = file
        Started = now
        LastBytes = 0
        Samples.Clear()
        Samples.Enqueue(Tuple.Create(now, 0L))
    End Sub

    Friend Shared Function FormatBytes(value As Decimal) As String
        Dim unit = UnitIndex(value)
        Return Amount(value, unit) & " " & UnitName(unit)
    End Function

    Private Shared Function UnitIndex(value As Decimal) As Integer
        Dim unit As Integer
        While value >= 1024 AndAlso unit < 6
            value /= 1024
            unit += 1
        End While
        Return unit
    End Function

    Private Shared Function Amount(value As Decimal, unit As Integer) As String
        For index As Integer = 1 To unit
            value /= 1024
        Next
        Return value.ToString(If(unit = 0, "N0", "N1"), CultureInfo.CurrentCulture)
    End Function

    Private Shared Function UnitName(unit As Integer) As String
        Return If(unit = 0, UiStrings.GetText("TransferUnitBytes"), {"Bytes", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB"}(unit))
    End Function
End Class
