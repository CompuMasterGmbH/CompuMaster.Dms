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
