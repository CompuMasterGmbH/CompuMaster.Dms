Imports System.Diagnostics
Imports System.Globalization
Imports CompuMaster.Dms.Data

Friend NotInheritable Class TransferDisplay
    Private ReadOnly Clock As Func(Of Double)
    Private ReadOnly Samples As New Queue(Of Tuple(Of Double, Long))
    Private Started As Double
    Private LastBytes As Long
    Private ActiveFile As Integer = -1
    Private BatchStarted As Double?
    Private LastBatchBytes As Decimal
    Private BatchInitialBytes As Decimal
    Private ReadOnly BatchSamples As New Queue(Of Tuple(Of Double, Decimal))

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

    Friend Function DescribeBatch(snapshot As UploadBatchSnapshot) As String
        Dim now = Clock()
        Dim consumed = snapshot.Counters.Where(Function(value) value IsNot Nothing AndAlso value.BytesTransferred.HasValue).Sum(Function(value) CDec(value.BytesTransferred.Value))
        If Not BatchStarted.HasValue OrElse consumed < LastBatchBytes Then
            BatchStarted = now
            BatchInitialBytes = consumed
            BatchSamples.Clear()
            BatchSamples.Enqueue(Tuple.Create(now, consumed))
        End If
        LastBatchBytes = consumed
        BatchSamples.Enqueue(Tuple.Create(now, consumed))
        While BatchSamples.Count > 1 AndAlso BatchSamples.ElementAt(1).Item1 <= now - 2
            BatchSamples.Dequeue()
        End While
        Dim knownBytes = Not Enumerable.Range(0, snapshot.Files.Length).Any(Function(index) snapshot.States(index) <> UploadFileState.Waiting AndAlso (snapshot.Counters(index) Is Nothing OrElse Not snapshot.Counters(index).BytesTransferred.HasValue))
        Dim totals = Enumerable.Range(0, snapshot.Files.Length).Select(Function(index) If(snapshot.Counters(index)?.TotalBytes, If(snapshot.PlannedTotals Is Nothing, Nothing, snapshot.PlannedTotals(index)))).ToArray()
        Dim knownTotal = totals.All(Function(value) value.HasValue)
        Dim total As Decimal = If(knownTotal, totals.Sum(Function(value) CDec(value.Value)), 0D)
        Dim processed = If(knownBytes, FormatBytes(consumed), UiStrings.GetText("UploadBytesUnknown"))
        If knownBytes AndAlso knownTotal Then
            Dim unit = UnitIndex(total)
            processed = UiStrings.Format("UploadSourceBytes", Amount(consumed, unit), Amount(total, unit), UnitName(unit))
        End If
        Dim rate = UiStrings.GetText("TransferUnknown")
        Dim span = now - BatchSamples.Peek().Item1
        If knownBytes AndAlso span > 0 Then rate = FormatBytes(Math.Max(0D, (consumed - BatchSamples.Peek().Item2) / CDec(span)))
        Dim eta = UiStrings.GetText("TransferAwaitingSamples")
        If snapshot.States.All(Function(state) state = UploadFileState.Completed) Then
            eta = "00:00:00"
        ElseIf Not knownTotal Then
            eta = UiStrings.GetText("TransferBatchSizeUnknown")
        ElseIf Not knownBytes Then
            eta = UiStrings.GetText("TransferAwaitingSamples")
        ElseIf total > consumed AndAlso consumed > BatchInitialBytes AndAlso now > BatchStarted.Value Then
            eta = UiStrings.GetText("TransferUnknown")
            Dim seconds = (total - consumed) * CDec(now - BatchStarted.Value) / (consumed - BatchInitialBytes)
            If seconds < CDec(TimeSpan.MaxValue.TotalSeconds) - 1 Then eta = TimeSpan.FromSeconds(CDbl(Math.Ceiling(seconds))).ToString("c", CultureInfo.InvariantCulture)
        ElseIf snapshot.States.Any(Function(state) state = UploadFileState.Finalizing) Then
            eta = UiStrings.GetText("TransferAwaitingConfirmation")
        End If
        Return UiStrings.Format("TransferBatchDetails", processed, rate, eta)
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
