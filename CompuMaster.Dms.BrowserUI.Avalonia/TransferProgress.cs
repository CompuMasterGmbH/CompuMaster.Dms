using System.Diagnostics;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Threading;

namespace CompuMaster.Dms.BrowserUI.Avalonia;

internal enum TransferState { Waiting, Transferring, Finalizing, Completed, Failed, Cancelled, Skipped }
internal sealed class TransferEntry
{
    internal TransferEntry(string name) { Name = name; }
    internal string Name { get; }
    internal TransferState State { get; set; }
    internal DmsTransferProgress? Progress { get; set; }
    internal Exception? Failure { get; set; }
    internal string Display => Name + " — " + UiText.Get(State switch
    {
        TransferState.Waiting => "UploadWaiting", TransferState.Transferring => "UploadTransferring",
        TransferState.Finalizing => "UploadFinalizing", TransferState.Completed => "UploadCompleted",
        TransferState.Failed => "UploadFailed", TransferState.Cancelled => "UploadCancelled", _ => "UploadSkipped"
    });
}
internal sealed class TransferBatch
{
    internal TransferBatch(IEnumerable<string> names, IReadOnlyList<long?>? sizes = null)
    {
        Entries = names.Select((n, index) => new TransferEntry(n)
        { Progress = sizes is null ? null : new DmsTransferProgress(0, sizes[index], DmsTransferPhase.Transferring) }).ToArray();
    }
    internal TransferEntry[] Entries { get; }
    internal async Task RunAsync(Func<int, IProgress<DmsTransferProgress>, CancellationToken, Task<bool>> transfer,
        Action update, bool retryFailed, CancellationToken token)
    {
        for (var index = 0; index < Entries.Length; index++)
        {
            var entry = Entries[index];
            if (entry.State is TransferState.Completed or TransferState.Skipped || !retryFailed && entry.State != TransferState.Waiting) continue;
            token.ThrowIfCancellationRequested();
            entry.State = TransferState.Transferring; entry.Failure = null; update();
            var active = 1;
            var reporter = new CallbackProgress(value =>
            {
                if (Volatile.Read(ref active) == 0) return;
                entry.Progress = value;
                // Payload completion does not establish provider success.
                entry.State = value.Phase == DmsTransferPhase.Transferring ? TransferState.Transferring : TransferState.Finalizing;
                update();
            });
            try
            {
                var completed = await transfer(index, reporter, token);
                Volatile.Write(ref active, 0); entry.State = completed ? TransferState.Completed : TransferState.Skipped;
            }
            catch (Exception ex)
            {
                Volatile.Write(ref active, 0); entry.State = ex is OperationCanceledException ? TransferState.Cancelled : TransferState.Failed;
                entry.Failure = ex; update(); throw;
            }
            update();
        }
    }
}
internal sealed class CallbackProgress : IProgress<DmsTransferProgress>
{
    private readonly Action<DmsTransferProgress> callback;
    internal CallbackProgress(Action<DmsTransferProgress> callback) { this.callback = callback; }
    public void Report(DmsTransferProgress value) => callback(value);
}
internal enum TransferRecovery { Close, RetryFailed, Continue }
internal sealed class TransferWindow : Window
{
    private readonly ListBox rows = new();
    private readonly TextBlock detail = new() { TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
    private readonly ProgressBar current = new() { Minimum = 0, Maximum = 100, Height = 18, IsIndeterminate = true };
    private readonly ProgressBar total = new() { Minimum = 0, Maximum = 100, Height = 18 };
    private readonly Button cancel;
    private readonly Button retry;
    private readonly Button next;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private TaskCompletionSource<TransferRecovery>? recovery;
    private bool running = true;
    private long lastUpdate;
    private int lastState;
    private bool finished;
    internal CancellationTokenSource Cancellation { get; private set; } = new();
    internal TransferWindow(string title, bool allowRetry)
    {
        FlowDirection = UiText.LayoutDirection;
        Title = title; Width = 820; Height = 480; MinWidth = 480; MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        cancel = Dialogs.Button("ActionCancel", () => { if (running) Cancellation.Cancel(); else { recovery?.TrySetResult(TransferRecovery.Close); Close(); } });
        retry = Dialogs.Button("UploadRetryFailed", () => recovery?.TrySetResult(TransferRecovery.RetryFailed)); retry.IsVisible = false; retry.IsEnabled = allowRetry;
        next = Dialogs.Button("UploadContinueRemaining", () => recovery?.TrySetResult(TransferRecovery.Continue)); next.IsVisible = false; next.IsEnabled = allowRetry;
        var body = new DockPanel();
        var bars = new StackPanel { Spacing = 8, Children = { current, total, detail } };
        DockPanel.SetDock(bars, Dock.Bottom); body.Children.Add(bars); body.Children.Add(rows);
        Content = Dialogs.Layout(body, Dialogs.Buttons(cancel, retry, next));
        Closing += (_, e) => { if (running) { Cancellation.Cancel(); e.Cancel = true; } else recovery?.TrySetResult(TransferRecovery.Close); };
        Closed += (_, _) => Cancellation.Dispose();
    }
    internal void Update(TransferBatch batch)
    {
        var state = batch.Entries.Aggregate(17, (hash, e) => unchecked(hash * 31 + (int)e.State));
        var now = Environment.TickCount64;
        if (state == lastState && now - Interlocked.Read(ref lastUpdate) < 100) return;
        lastState = state; Interlocked.Exchange(ref lastUpdate, now);
        void Apply()
        {
            if (finished) return;
            rows.ItemsSource = batch.Entries.Select(e => e.Display).ToArray();
            var entry = batch.Entries.LastOrDefault(e => e.State is TransferState.Transferring or TransferState.Finalizing) ?? batch.Entries.LastOrDefault();
            var value = entry?.Progress;
            current.IsIndeterminate = value?.TotalBytes is null || value.TotalBytes == 0 && entry?.State != TransferState.Completed;
            current.Value = value?.TotalBytes > 0 ? Math.Min(100, 100d * (value.BytesTransferred ?? 0) / value.TotalBytes.Value) : 0;
            total.Value = batch.Entries.Length == 0 ? 0 : 100d * batch.Entries.Count(e => e.State is TransferState.Completed or TransferState.Skipped) / batch.Entries.Length;
            var bytes = batch.Entries.Sum(e => e.Progress?.BytesTransferred ?? 0);
            var speed = clock.Elapsed.TotalSeconds > 0 ? bytes / clock.Elapsed.TotalSeconds : 0;
            var knownTotal = batch.Entries.All(e => e.Progress?.TotalBytes.HasValue == true) ? batch.Entries.Sum(e => e.Progress!.TotalBytes!.Value) : (long?)null;
            if (knownTotal > 0) total.Value = Math.Min(100, 100d * bytes / knownTotal.Value);
            var eta = knownTotal.HasValue && speed > 0 ? TimeSpan.FromSeconds(Math.Max(0, (knownTotal.Value - bytes) / speed)).ToString("g") : UiText.Get("UnknownValue");
            detail.Text = (entry?.Name ?? "") + "\n" + UiText.ByteSize(bytes) + " | " + UiText.ByteSize((long)speed) + "/s | " +
                UiText.Extra("Elapsed: ", "Vergangen: ") + clock.Elapsed.ToString("g") + " | ETA: " + eta;
        }
        if (Dispatcher.UIThread.CheckAccess()) Apply(); else Dispatcher.UIThread.Post(Apply);
    }
    internal async Task<TransferRecovery> FailureAsync(Exception failure)
    {
        running = false;
        detail.Text = UiText.Get("BrowserOperationFailed");
        retry.IsVisible = next.IsVisible = true;
        cancel.Content = Dialogs.Caption("ActionClose");
        recovery = new(TaskCreationOptions.RunContinuationsAsynchronously);
        return await recovery.Task;
    }
    internal void Restart()
    {
        Cancellation.Dispose(); Cancellation = new();
        running = true; retry.IsVisible = next.IsVisible = false; cancel.Content = Dialogs.Caption("ActionCancel");
    }
    internal void Finish() { running = false; finished = true; Close(); }
}
