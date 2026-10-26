using System.Globalization;
using CompuMaster.Dms.BrowserUI.Avalonia;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using CompuMaster.Dms.TestDemo.Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless.NUnit;
using NUnit.Framework;

namespace CompuMaster.Dms.Test.BrowserUI.Avalonia;

[TestFixture]
public sealed class AdvancedWorkflowTests
{
    [Test]
    public void Recursive_upload_plan_preserves_empty_folders_and_deduplicates_selected_descendants()
    {
        using var local = new OwnedDirectory();
        var folder = Path.Combine(local.Path, "source"); Directory.CreateDirectory(Path.Combine(folder, "empty"));
        var file = Path.Combine(folder, "data.txt"); File.WriteAllText(file, "payload");
        var plan = LocalUploadPlan.Build(new[] { folder, file }, local.Path, CancellationToken.None);
        Assert.That(plan.Files.Single().Relative, Is.EqualTo("source/data.txt"));
        Assert.That(plan.Directories, Is.EqualTo(new[] { "source", "source/empty" }));
        Assert.That(LocalUploadPlan.Destination(new DemoDmsProvider(), "/Documents", "source/data.txt"), Is.EqualTo("/Documents/source/data.txt"));
    }
    [Test]
    public void Recursive_upload_plan_rejects_colliding_relative_names_before_writing()
    {
        using var local = new OwnedDirectory();
        var first = Path.Combine(local.Path, "one"); var second = Path.Combine(local.Path, "two");
        Directory.CreateDirectory(first); Directory.CreateDirectory(second);
        File.WriteAllText(Path.Combine(first, "same.txt"), "first"); File.WriteAllText(Path.Combine(second, "same.txt"), "second");
        Assert.Throws<IOException>((Action)(() => LocalUploadPlan.Build(new[] { Path.Combine(first, "same.txt"), Path.Combine(second, "same.txt") }, local.Path, CancellationToken.None)));
    }
    [Test]
    public async Task Recursive_download_plan_preserves_empty_directories_and_rejects_name_collisions()
    {
        using var local = new OwnedDirectory();
        var provider = new DemoDmsProvider();
        provider.AddDirectory("/Documents/EmptyNested");
        var plan = await RemoteDownloadPlan.BuildAsync(provider, provider.ListRemoteItem("/Documents")!, local.Path, CancellationToken.None);
        Assert.That(plan.Directories, Does.Contain(Path.Combine(local.Path, "Documents", "EmptyNested")));
        Assert.That(plan.Files.Any(f => f.Destination == Path.Combine(local.Path, "Documents", "Projects", "Notes.txt")), Is.True);
        provider.AddFile("/Documents/Welcome.txt", "duplicate");
        Assert.ThrowsAsync<IOException>((Func<Task>)(async () => await RemoteDownloadPlan.BuildAsync(provider, provider.ListRemoteItem("/Documents")!, local.Path, CancellationToken.None)));
    }
    [Test]
    public async Task Atomic_download_failure_preserves_existing_file_and_removes_partial_temporary_file()
    {
        using var local = new OwnedDirectory();
        var destination = Path.Combine(local.Path, "file.txt"); File.WriteAllText(destination, "original");
        var original = new IOException("Injected partial download.");
        var failure = Assert.ThrowsAsync<IOException>((Func<Task>)(async () => await AtomicDownload.SaveAsync(destination, true, path =>
        { File.WriteAllText(path, "partial"); throw original; })));
        Assert.That(failure, Is.SameAs(original));
        Assert.That(File.ReadAllText(destination), Is.EqualTo("original"));
        Assert.That(Directory.GetFiles(local.Path).Select(Path.GetFileName), Is.EqualTo(new[] { "file.txt" }));
        await AtomicDownload.SaveAsync(destination, true, path => File.WriteAllTextAsync(path, "complete"));
        Assert.That(File.ReadAllText(destination), Is.EqualTo("complete"));
    }
    [Test]
    public async Task Finalizing_progress_is_not_success_until_provider_task_returns()
    {
        var batch = new TransferBatch(new[] { "file" });
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = batch.RunAsync(async (_, progress, _) =>
        {
            progress.Report(new DmsTransferProgress(12, 12, DmsTransferPhase.Completed));
            entered.SetResult(); await complete.Task; return true;
        }, () => { }, true, CancellationToken.None);
        await entered.Task;
        Assert.That(batch.Entries.Single().State, Is.EqualTo(TransferState.Finalizing));
        complete.SetResult(); await running;
        Assert.That(batch.Entries.Single().State, Is.EqualTo(TransferState.Completed));
    }
    [Test]
    public async Task Upload_recovery_retries_only_failed_and_waiting_files_and_continue_skips_failed()
    {
        var batch = new TransferBatch(new[] { "first", "failed", "waiting" });
        var attempts = new List<int>();
        Assert.ThrowsAsync<IOException>((Func<Task>)(async () => await batch.RunAsync((index, _, _) =>
        { attempts.Add(index); if (index == 1) throw new IOException("injected"); return Task.FromResult(true); }, () => { }, true, CancellationToken.None)));
        Assert.That(batch.Entries.Select(e => e.State), Is.EqualTo(new[] { TransferState.Completed, TransferState.Failed, TransferState.Waiting }));
        await batch.RunAsync((index, _, _) => { attempts.Add(index); return Task.FromResult(true); }, () => { }, false, CancellationToken.None);
        Assert.That(attempts, Is.EqualTo(new[] { 0, 1, 2 }));
        await batch.RunAsync((index, _, _) => { attempts.Add(index); return Task.FromResult(true); }, () => { }, true, CancellationToken.None);
        Assert.That(attempts, Is.EqualTo(new[] { 0, 1, 2, 1 }));
    }
    [Test]
    public async Task Conflict_apply_to_all_is_operation_scoped_and_changed_identity_is_rejected()
    {
        var policy = new TransferConflicts(); var prompts = 0;
        Task<ConflictDecision> Prompt(string _) { prompts++; return Task.FromResult(new ConflictDecision(ConflictAction.Skip, true)); }
        Assert.That(await policy.ResolveAsync("/one", "id1", Prompt), Is.EqualTo(ConflictAction.Skip));
        Assert.That(await policy.ResolveAsync("/two", "id2", Prompt), Is.EqualTo(ConflictAction.Skip));
        Assert.That(prompts, Is.EqualTo(1));
        Assert.ThrowsAsync<InvalidOperationException>((Func<Task>)(async () => await policy.ResolveAsync("/one", "replacement", Prompt)));
        await new TransferConflicts().ResolveAsync("/three", "id3", Prompt);
        Assert.That(prompts, Is.EqualTo(2));
        Assert.Throws<InvalidOperationException>((Action)(() => policy.ValidateAbsent("/one")));
    }
    [Test]
    public async Task Late_progress_cannot_change_a_completed_transfer_back_to_finalizing()
    {
        var batch = new TransferBatch(new[] { "file" });
        IProgress<DmsTransferProgress>? late = null;
        await batch.RunAsync((_, progress, _) => { late = progress; return Task.FromResult(true); }, () => { }, true, CancellationToken.None);
        late!.Report(new DmsTransferProgress(10, 10, DmsTransferPhase.Finalizing));
        Assert.That(batch.Entries.Single().State, Is.EqualTo(TransferState.Completed));
    }
    [AvaloniaTest]
    public async Task Forged_remote_drag_context_cannot_authorize_a_move()
    {
        var provider = new DemoDmsProvider();
        var browser = new DmsBrowserControl(provider, new DmsBrowserOptions { AllowedActions = (FileOrFolderActions)2047 });
        var host = new Window { Content = browser };
        try
        {
            host.Show(); await browser.InitializeAsync(); await BrowserUiTests.IdleAsync(browser);
            var file = provider.Items.First(i => i.ItemType == DmsResourceItem.ItemTypes.File);
            var forged = new RemoteDrag(provider, provider.InstanceID, null, new[] { file });
            var target = browser.Session.Roots[0].Children.Single(n => n.Name == "Empty");
            Assert.That(browser.ValidMoveTarget(forged, target), Is.False);
            Assert.That(provider.Calls.Any(c => c.StartsWith("Copy:") || c.StartsWith("Delete:")), Is.False);
        }
        finally { host.Close(); }
    }
    [Test]
    public async Task Version_permissions_are_separate_and_historical_download_is_atomic()
    {
        using var local = new OwnedDirectory();
        var provider = new DemoDmsProvider();
        var file = provider.Items.First(i => i.Name == "Welcome.txt");
        var flags = FileOrFolderActions.AllowViewFileVersionHistory;
        var session = new VersionSession(provider, file, () => flags);
        var version = (await session.ListAsync()).Single();
        Assert.That(session.CanView, Is.True); Assert.That(session.CanDownload || session.CanRestore || session.CanRemove, Is.False);
        Assert.ThrowsAsync<InvalidOperationException>((Func<Task>)(async () => await session.RestoreAsync(version)));
        flags |= FileOrFolderActions.AllowDownloadFiles | FileOrFolderActions.AllowRestoreFileVersions | FileOrFolderActions.AllowRemoveFileVersions;
        var destination = Path.Combine(local.Path, "historical.txt");
        await session.DownloadAsync(version, destination, local.Path, false);
        Assert.That(File.ReadAllText(destination), Is.EqualTo("Historical content"));
        await session.RestoreAsync(version); await session.RemoveAsync(version);
        Assert.That(provider.Calls, Does.Contain("RestoreVersion:version-1").And.Contain("RemoveVersion:version-1"));
        flags &= ~FileOrFolderActions.AllowViewFileVersionHistory;
        Assert.That(session.CanDownload || session.CanRestore || session.CanRemove, Is.False);
    }
    [Test]
    public void Foreign_or_duplicate_version_snapshots_are_rejected()
    {
        var provider = new InvalidVersionsProvider();
        var file = provider.Items.First(i => i.Name == "Welcome.txt");
        var session = new VersionSession(provider, file, () => (FileOrFolderActions)2047);
        Assert.ThrowsAsync<InvalidDataException>((Func<Task>)(async () => await session.ListAsync()));
    }
    private sealed class InvalidVersionsProvider : DemoDmsProvider
    {
        public override List<DmsFileVersion> ListFileVersions(DmsResourceItem remoteFile) => new()
        { new DmsFileVersion(remoteFile, "duplicate"), new DmsFileVersion(remoteFile, "duplicate") };
    }
    [AvaloniaTest]
    public async Task Diagnostics_preserve_original_failure_without_allowing_subscribers_to_replace_it()
    {
        var browser = new DmsBrowserControl(new DemoDmsProvider());
        var failure = new IOException("sensitive request detail");
        DmsBrowserFailureEventArgs? captured = null;
        browser.OperationFailed += (_, _) => throw new Exception("Broken diagnostics.");
        browser.OperationFailed += (_, args) => { captured = args; args.SuppressDialog = true; };
        var result = browser.ReportFailure(failure, DmsBrowserFailurePhase.Refresh, "Refresh");
        Assert.That(result, Is.SameAs(captured));
        Assert.That(captured!.Exception, Is.SameAs(failure));
        Assert.That(captured.SuppressDialog, Is.True);
        await Task.CompletedTask;
    }
    [AvaloniaTest]
    public void Arabic_resources_and_right_to_left_layout_are_loaded_without_an_extra_framework_package()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar");
            var browser = new DmsBrowserControl(new DemoDmsProvider());
            Assert.That(browser.FlowDirection, Is.EqualTo(global::Avalonia.Media.FlowDirection.RightToLeft));
            Assert.That(UiText.Get("ActionUpload"), Is.Not.EqualTo("ActionUpload").And.Not.EqualTo("&Upload"));
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
    [AvaloniaTest]
    public async Task Version_history_dialog_is_capability_gated_and_loads_retained_metadata()
    {
        var provider = new DemoDmsProvider();
        var file = provider.Items.First(i => i.Name == "Welcome.txt");
        var owner = new Window(); owner.Show();
        var dialog = new FileVersionHistoryDialog(provider, file, new DmsBrowserOptions { AllowedActions = (FileOrFolderActions)2047 });
        try
        {
            dialog.Show(owner);
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (dialog.IsRunning && DateTime.UtcNow < timeout) await Task.Delay(5);
            Assert.That(dialog.List.ItemCount, Is.EqualTo(1));
            Assert.That(dialog.List.ItemsSource!.Cast<VersionRow>().Single().ToString(), Does.Contain("version-1").And.Contain("Alice"));
        }
        finally { dialog.Close(); owner.Close(); }
    }
}
