using CompuMaster.Dms.BrowserUI.Avalonia;
using CompuMaster.Dms.TestDemo.Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using NUnit.Framework;
namespace CompuMaster.Dms.Test.BrowserUI.Avalonia;

[TestFixture]
public sealed class ReceivedSharingVisualTests
{
    [AvaloniaTest]
    [TestCase(false, false, false), TestCase(false, true, false), TestCase(false, null, true)]
    [TestCase(null, false, true), TestCase(true, true, true)]
    public async Task Summary_panel_visibility_and_spacing_follow_available_information(bool? received, bool? outgoing, bool visible)
    {
        var item = ReceivedSharingTests.ReceivedItem(); item.ExtendedInfosIsReceivedShare = received; item.ExtendedInfosHasOutgoingSharings = outgoing;
        var dialog = new DmsItemSharings(new DemoDmsProvider(), item);
        try
        {
            dialog.Show();
            while (dialog.IsRunning) await Task.Delay(5);
            dialog.UpdateLayout();
            var panel = dialog.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "ReceivedSharingSummaryPanel");
            Assert.That(panel.IsVisible, Is.EqualTo(visible));
            if (!visible)
            {
                var content = (StackPanel)panel.Parent!;
                Assert.That(content.Children[1].Bounds.Top, Is.EqualTo(0), "A hidden summary must not leave a blank heading row.");
            }
        }
        finally { dialog.Close(); }
    }
    [AvaloniaTest]
    public async Task Received_grants_are_visible_above_the_editable_outgoing_lists()
    {
        var dialog = new DmsItemSharings(new DemoDmsProvider(), ReceivedSharingTests.ReceivedItem());
        try
        {
            dialog.Show();
            while (dialog.IsRunning) await Task.Delay(5);
            var summary = dialog.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "ReceivedSharingSummary");
            Assert.That(summary.Text, Does.Contain(UiText.Get("ReceivedSharingTitle")).And.Contain("Sharing person").And.Contain("Original owner"));
            Assert.That(dialog.GetVisualDescendants().OfType<DataGrid>().All(grid => grid.ItemsSource.Cast<object>().Count() == 0), Is.True);
        }
        finally { dialog.Close(); }
    }
}
