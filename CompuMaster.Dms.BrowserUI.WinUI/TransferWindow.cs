using System.Diagnostics;
using CompuMaster.Dms.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace CompuMaster.Dms.BrowserUI.WinUI;
internal sealed class TransferWindow : DialogWindow
{
    private readonly ListBox rows = new();
    private readonly TextBlock detail = new() { TextWrapping = TextWrapping.Wrap };
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
        Title = title; Width = 820; Height = 480;
        cancel = Dialogs.Button("ActionCancel", () => { if (running) Cancellation.Cancel(); else { recovery?.TrySetResult(TransferRecovery.Close); Close(); } });
        retry = Dialogs.Button("UploadRetryFailed", () => recovery?.TrySetResult(TransferRecovery.RetryFailed)); retry.Visibility = Visibility.Collapsed; retry.IsEnabled = allowRetry;
        next = Dialogs.Button("UploadContinueRemaining", () => recovery?.TrySetResult(TransferRecovery.Continue)); next.Visibility = Visibility.Collapsed; next.IsEnabled = allowRetry;
        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var bars = new StackPanel { Spacing = 8, Children = { current, total, detail } };
        Grid.SetRow(bars, 1); body.Children.Add(bars); body.Children.Add(rows);
        Content = Dialogs.Layout(body, Dialogs.Buttons(cancel, retry, next));
        AppWindow.Closing += (_, e) => { if (running) { Cancellation.Cancel(); e.Cancel = true; } else recovery?.TrySetResult(TransferRecovery.Close); };
        Closed += (_, _) => Cancellation.Dispose();
    }
    internal void Show(Microsoft.UI.Xaml.Window owner) => _ = Host.ShowAsync(owner);
    internal override void OnEscape()
    {
        if (running) Cancellation.Cancel(); else { recovery?.TrySetResult(TransferRecovery.Close); Close(); }
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
        if (DispatcherQueue.HasThreadAccess) Apply(); else DispatcherQueue.TryEnqueue(Apply);
    }
    internal async Task<TransferRecovery> FailureAsync(Exception failure)
    {
        running = false;
        detail.Text = UiText.Get("BrowserOperationFailed");
        retry.Visibility = next.Visibility = Visibility.Visible;
        cancel.Content = Dialogs.Caption("ActionClose");
        recovery = new(TaskCreationOptions.RunContinuationsAsynchronously);
        return await recovery.Task;
    }
    internal void Restart()
    {
        Cancellation.Dispose(); Cancellation = new();
        running = true; retry.Visibility = next.Visibility = Visibility.Collapsed; cancel.Content = Dialogs.Caption("ActionCancel");
    }
    internal void Finish() { running = false; finished = true; Close(); }
}
