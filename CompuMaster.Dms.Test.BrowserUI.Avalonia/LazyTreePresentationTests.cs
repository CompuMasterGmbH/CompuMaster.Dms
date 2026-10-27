using CompuMaster.Dms.BrowserUI.Avalonia;
using CompuMaster.Dms.TestDemo.Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless.NUnit;
using global::Avalonia.VisualTree;
using NUnit.Framework;

namespace CompuMaster.Dms.Test.BrowserUI.Avalonia;

[TestFixture]
public sealed class LazyTreePresentationTests
{
    [AvaloniaTest]
    [TestCase("Documents", "Projects")]
    [TestCase("Empty", null)]
    public async Task Expanding_unknown_children_never_displays_the_internal_placeholder(string folder, string? expectedChild)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new DemoDmsProvider { OmitChildMetadata = true };
        provider.BeforeListing = async (path, token) =>
        {
            if (path?.Trim('/') != folder) return;
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        };
        var window = new DmsBrowser(provider, new DmsBrowserOptions());
        try
        {
            window.Show(); await window.Browser.InitializeAsync(); await BrowserUiTests.IdleAsync(window.Browser);
            window.UpdateLayout();
            var parent = window.Browser.Session.Roots.Single().Children.Single(n => n.Name == folder);
            var placeholder = parent.Children.Single();
            Assert.That(placeholder.Placeholder, Is.True);
            Assert.That(parent.Loaded, Is.False);
            Assert.That(provider.Calls.Any(c => c.StartsWith("List:/" + folder + ":")), Is.False);
            var container = window.Browser.FolderTree.GetVisualDescendants().OfType<TreeViewItem>()
                .Single(item => ReferenceEquals(item.DataContext, parent));
            container.IsExpanded = true; // Same expansion event as clicking the chevron.
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            window.UpdateLayout();
            Assert.That(window.Browser.IsBusy, Is.True, "Inspect the UI while the request is still pending.");
            Assert.That(parent.Children.Single(), Is.SameAs(placeholder), "The expander must remain available for unknown metadata.");
            Assert.That(placeholder.Name, Is.Empty);
            Assert.That(placeholder.Icon, Is.Null);
            var placeholderContainer = container.GetVisualDescendants().OfType<TreeViewItem>()
                .Single(item => ReferenceEquals(item.DataContext, placeholder));
            Assert.That(placeholderContainer.IsEffectivelyVisible, Is.False);
            Assert.That(placeholderContainer.Bounds.Height, Is.Zero, "A hidden placeholder must not leave a blank row.");
            release.TrySetResult();
            await BrowserUiTests.IdleAsync(window.Browser); window.UpdateLayout();
            Assert.That(parent.Loaded, Is.True);
            Assert.That(parent.Children.Any(n => n.Placeholder), Is.False);
            Assert.That(parent.Children.Select(n => n.Name), Is.EqualTo(expectedChild is null ? Array.Empty<string>() : new[] { expectedChild }));
            Assert.That(parent.Item!.ChildDirectoryCount, Is.EqualTo(expectedChild is null ? 0 : 1));
            Assert.That(window.Browser.Session.Roots.Single().Name, Is.EqualTo("/"), "The actual root keeps its name and home icon.");
            Assert.That(window.Browser.Session.Roots.Single().Icon, Is.Not.Null);
        }
        finally
        {
            release.TrySetResult();
            await BrowserUiTests.IdleAsync(window.Browser);
            window.Close();
        }
    }
}
