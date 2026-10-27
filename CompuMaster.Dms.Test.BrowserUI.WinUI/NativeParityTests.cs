using System.Reflection;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using CompuMaster.Dms.TestDemo.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NUnit.Framework;
using Ui = CompuMaster.Dms.BrowserUI.WinUI;
using Wf = CompuMaster.Dms.BrowserUI;
using Forms = System.Windows.Forms;

namespace CompuMaster.Dms.Test.BrowserUI.WinUI;

[TestFixture]
public sealed class NativeParityTests
{
    private sealed record State(bool Visible, bool Enabled);
    private static readonly (string Key, string Native)[] Toolbar =
    {
        ("ActionUpload", "ToolStripButtonUploadFile"), ("ActionUploadFolder", "UploadFolderButton"), ("ActionDownload", "ToolStripButtonDownloadFile"),
        ("ActionDownloadFolder", "DownloadFolderButton"), ("ActionOpen", "ToolStripButtonOpenFile"), ("ActionDelete", "ToolStripButtonDeleteFile"),
        ("ActionCopy", "ToolStripButtonCopyFile"), ("ActionRename", "ToolStripButtonRenameFile"), ("ActionMove", "ToolStripButtonMoveFile"),
        ("ActionFileSharings", "ToolStripButtonSharingsFile"), ("ActionFolderSharings", "ToolStripButtonSharingsFolder"),
        ("FileProperties", "ToolStripButtonPropertiesFile"), ("FolderProperties", "ToolStripButtonPropertiesFolder"),
        ("FileVersionHistory", "VersionHistoryButton"), ("ActionRefreshFiles", "ToolStripButtonRefreshFilesList")
    };
    private static readonly (string Key, string Native)[] FolderMenu =
    {
        ("ActionCreateFolder", "ToolStripFolderContextButtonNewFolder"), ("ActionCopy", "ToolStripFolderContextButtonCopyFolder"),
        ("ActionRename", "ToolStripFolderContextButtonRenameFolder"), ("ActionMove", "ToolStripFolderContextButtonMoveFolder"),
        ("ActionDelete", "ToolStripFolderContextButtonDeleteFolder"), ("ActionFolderSharings", "ToolStripFolderContextButtonShareFolder"),
        ("FolderProperties", "ToolStripFolderContextButtonProperties"), ("ActionRefreshFiles", "ToolStripFolderContextButtonRefreshFilesList"), ("ActionDownloadFolder", "DownloadFolderContext")
    };
    private static readonly (string Key, string Native)[] FileMenu =
    {
        ("ActionUpload", "ToolStripFileContextButtonUploadFile"), ("ActionUploadFolder", "UploadFolderContext"), ("ActionDownload", "ToolStripFileContextButtonDownloadFile"),
        ("ActionOpen", "ToolStripFileContextButtonOpenPreviewFile"), ("ActionDelete", "ToolStripFileContextButtonDeleteFile"),
        ("ActionCopy", "ToolStripFileContextButtonCopyFile"), ("ActionRename", "ToolStripFileContextButtonRenameFile"), ("ActionMove", "ToolStripFileContextButtonMoveFile"),
        ("ActionFileSharings", "ToolStripFileContextButtonShareFile"), ("FileProperties", "ToolStripFileContextButtonProperties"), ("FileVersionHistory", "VersionHistoryContext")
    };
    private static T Member<T>(object instance, string name)
    {
        for (var type = instance.GetType(); type is not null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? type.GetField("_" + name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field is not null) return (T)field.GetValue(instance)!;
        }
        throw new MissingFieldException(name);
    }
    private sealed class ReferenceBrowser : Wf.DmsBrowser
    {
        internal Exception? Failure;
        internal ReferenceBrowser(BaseDmsProvider provider, int flags, bool folders, bool noResults) : base(provider, null, "WinForms parity reference", null, null, "Documents",
            folders ? Wf.DmsBrowser.BrowseModes.Folders : Wf.DmsBrowser.BrowseModes.FoldersAndFiles, (Wf.DmsBrowser.FileOrFolderActions)flags,
            noResults ? Wf.DmsBrowser.DialogOperationModes.NoResults : Wf.DmsBrowser.DialogOperationModes.ReturnSelectedItems, "", "", "")
        {
            ShowInTaskbar = false; Opacity = 0; OperationFailed += (_, e) => Failure = e.Exception;
        }
        protected override void ShowFailureDialog(string message, string title, Forms.MessageBoxIcon icon) { }
    }
    private static Dictionary<string, State> Reference(int flags, bool folders, bool sharing, bool versions, int selection, bool noResults)
    {
        var completion = new TaskCompletionSource<Dictionary<string, State>>();
        var thread = new Thread(() =>
        {
            try
            {
                using var dispatcher = new BrowserUI.UiTestDispatcher();
                using var browser = new ReferenceBrowser(new DemoDmsProvider { SharingSupported = sharing, VersionsSupported = versions }, flags, folders, noResults);
                browser.Show(); dispatcher.WaitForEvents(); Assert.That(browser.Failure, Is.Null);
                foreach (Forms.ListViewItem item in Member<Forms.ListView>(browser, "ListViewDmsFiles").Items.Cast<Forms.ListViewItem>().Take(selection)) item.Selected = true;
                Forms.Application.DoEvents(); dispatcher.WaitForEvents();
                typeof(Forms.ContextMenuStrip).GetMethod("OnOpening", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Member<Forms.ContextMenuStrip>(browser, "ContextMenuStripFolder"), new object[] { new System.ComponentModel.CancelEventArgs() });
                var result = new Dictionary<string, State>();
                foreach (var (key, native) in Toolbar)
                {
                    var item = Member<Forms.ToolStripItem>(browser, native); var present = item.Available && item.Owner!.Visible;
                    result["toolbar:" + key] = new(present, present && item.Enabled);
                }
                foreach (var (kind, entries) in new[] { ("folder:", FolderMenu), ("file:", FileMenu) })
                    foreach (var (key, native) in entries)
                    { var item = Member<Forms.ToolStripItem>(browser, native); result[kind + key] = new(item.Available, item.Available && item.Enabled); }
                completion.TrySetResult(result);
            }
            catch (Exception ex) { completion.TrySetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task.GetAwaiter().GetResult();
    }
    [TestCase(0, false, true, true, 0, false)]
    [TestCase(2047, false, true, true, 0, false)]
    [TestCase(2047, false, true, true, 1, false)]
    [TestCase(2047, false, true, true, 2, true)]
    [TestCase(2047, true, true, true, 0, true)]
    [TestCase(2047, false, false, false, 1, false)]
    [TestCase(1, false, true, true, 0, false)]
    public async Task Action_availability_and_permissions_match_running_WinForms(int flags, bool folders, bool sharing, bool versions, int selection, bool noResults)
    {
        var expected = Reference(flags, folders, sharing, versions, selection, noResults);
        await UiThread.Run(async () =>
        {
            var browser = new Ui.DmsBrowser(new DemoDmsProvider { SharingSupported = sharing, VersionsSupported = versions }, new Ui.DmsBrowserOptions
            { AllowedActions = (Ui.FileOrFolderActions)flags, SelectedFolder = "Documents", BrowseMode = folders ? Ui.BrowseModes.Folders : Ui.BrowseModes.FoldersAndFiles,
                DialogOperationMode = noResults ? Ui.DialogOperationModes.NoResults : Ui.DialogOperationModes.ReturnSelectedItems });
            try
            {
                _ = browser.ShowAsync(); await browser.Browser.InitializeAsync(); await Task.Delay(100);
                foreach (var row in browser.Browser.FileGrid.Items.Take(selection).ToArray()) browser.Browser.FileGrid.SelectedItems.Add(row);
                var actual = new Dictionary<string, State>();
                foreach (var (key, _) in Toolbar)
                {
                    var button = NativeUiTests.Descendants(browser.Browser).OfType<Microsoft.UI.Xaml.Controls.Button>().Single(button => button.Name == key);
                    var present = !folders && button.Visibility == Visibility.Visible;
                    actual["toolbar:" + key] = new(present, present && button.IsEnabled);
                }
                foreach (var (kind, target, entries) in new[] { ("folder:", (Control)browser.Browser.FolderTree, FolderMenu), ("file:", (Control)browser.Browser.FileGrid, FileMenu) })
                    foreach (var (key, _) in entries)
                    {
                        var item = ((MenuFlyout)target.ContextFlyout).Items.OfType<MenuFlyoutItem>().Single(item => item.Name == key);
                        actual[kind + key] = new(item.Visibility == Visibility.Visible, item.Visibility == Visibility.Visible && item.IsEnabled);
                    }
                using (Assert.EnterMultipleScope())
                    foreach (var entry in expected) Assert.That(actual[entry.Key], Is.EqualTo(entry.Value), entry.Key);
            }
            finally { browser.Close(); }
        });
    }
}
