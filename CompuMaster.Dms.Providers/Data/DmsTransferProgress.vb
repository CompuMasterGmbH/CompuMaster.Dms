Namespace Data
    ''' <summary>Identifies the current phase of an asynchronous file upload.</summary>
    Public Enum DmsTransferPhase
        ''' <summary>Indicates that file data is being supplied to the transport.</summary>
        Transferring = 0
        ''' <summary>Indicates that file data has been read and provider confirmation is pending.</summary>
        Finalizing = 1
        ''' <summary>Indicates that the provider has confirmed successful completion.</summary>
        Completed = 2
    End Enum

    ''' <summary>Describes an immutable upload progress snapshot.</summary>
    ''' <remarks>Byte counters describe source data consumed by the transport, excluding protocol overhead. They do not establish remote durability; only Completed confirms provider success. Nothing distinguishes unknown counters from a known zero.</remarks>
    Public NotInheritable Class DmsTransferProgress
        ''' <summary>Initializes an upload progress snapshot.</summary>
        ''' <param name="bytesTransferred">The source bytes consumed, or Nothing when unknown.</param>
        ''' <param name="totalBytes">The total source size, or Nothing when unknown.</param>
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

        ''' <summary>Gets the source bytes consumed by the upload transport, or Nothing when unknown.</summary>
        Public ReadOnly Property BytesTransferred As Long?
        ''' <summary>Gets the total source size, or Nothing when unknown.</summary>
        Public ReadOnly Property TotalBytes As Long?
        ''' <summary>Gets the current upload phase.</summary>
        Public ReadOnly Property Phase As DmsTransferPhase
    End Class
End Namespace
