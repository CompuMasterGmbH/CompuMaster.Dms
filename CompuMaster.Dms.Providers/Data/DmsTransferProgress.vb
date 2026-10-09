Namespace Data
    ''' <summary>Identifies the current phase of an asynchronous file transfer.</summary>
    Public Enum DmsTransferPhase
        ''' <summary>Indicates that file data is being uploaded or written to a download destination.</summary>
        Transferring = 0
        ''' <summary>Indicates that payload processing is finished and transfer finalization is pending.</summary>
        Finalizing = 1
        ''' <summary>Indicates that the provider has confirmed successful completion.</summary>
        Completed = 2
    End Enum

    ''' <summary>Describes an immutable file transfer progress snapshot.</summary>
    ''' <remarks>Upload counters describe source data consumed by the transport; download counters describe payload written locally. Both exclude protocol overhead. Only Completed confirms provider success. Nothing distinguishes unknown counters from a known zero.</remarks>
    Public NotInheritable Class DmsTransferProgress
        ''' <summary>Initializes a file transfer progress snapshot.</summary>
        ''' <param name="bytesTransferred">The processed payload bytes, or Nothing when unknown.</param>
        ''' <param name="totalBytes">The total payload size, or Nothing when unknown.</param>
        ''' <param name="phase">The current provider phase.</param>
        ''' <exception cref="ArgumentOutOfRangeException">A byte count is negative or the phase is undefined.</exception>
        Public Sub New(bytesTransferred As Long?, totalBytes As Long?, phase As DmsTransferPhase)
            If bytesTransferred.HasValue AndAlso bytesTransferred.Value < 0 Then Throw New ArgumentOutOfRangeException(NameOf(bytesTransferred))
            If totalBytes.HasValue AndAlso totalBytes.Value < 0 Then Throw New ArgumentOutOfRangeException(NameOf(totalBytes))
            If Not [Enum].IsDefined(GetType(DmsTransferPhase), phase) Then Throw New ArgumentOutOfRangeException(NameOf(phase))
            Me.BytesTransferred = bytesTransferred
            Me.TotalBytes = totalBytes
            Me.Phase = phase
        End Sub

        ''' <summary>Gets the processed payload bytes, or Nothing when unknown.</summary>
        Public ReadOnly Property BytesTransferred As Long?
        ''' <summary>Gets the total payload size, or Nothing when unknown.</summary>
        Public ReadOnly Property TotalBytes As Long?
        ''' <summary>Gets the current transfer phase.</summary>
        Public ReadOnly Property Phase As DmsTransferPhase
    End Class
End Namespace
