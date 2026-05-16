using AnimationEditor.App.Models;
using Avalonia.Media;
using Xunit;

namespace AnimationEditor.App.Tests;

public class HistoryEntryVmTests
{
    [Fact]
    public void Background_WhenIsCurrent_IsNonTransparent()
    {
        var vm = new HistoryEntryVm("Test", "#e6e8ec", IsCurrent: true);
        var brush = Assert.IsType<SolidColorBrush>(vm.Background);
        Assert.NotEqual(Colors.Transparent, brush.Color);
    }

    [Fact]
    public void Background_WhenNotCurrent_IsTransparent()
    {
        var vm = new HistoryEntryVm("Test", "#e6e8ec", IsCurrent: false);
        Assert.Same(Brushes.Transparent, vm.Background);
    }

    [Fact]
    public void Background_DefaultIsCurrent_IsTransparent()
    {
        var vm = new HistoryEntryVm("Test", "#e6e8ec");
        Assert.Same(Brushes.Transparent, vm.Background);
    }
}
