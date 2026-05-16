using AnimationEditor.App.Helpers;
using Xunit;

namespace AnimationEditor.App.Tests;

public class HistoryScrollHelperTests
{
    [Fact]
    public void ComputeScrollOffset_EmptyList_ReturnsNull()
    {
        var result = HistoryScrollHelper.ComputeScrollOffset(0, 0, 500, 200, 0);
        Assert.Null(result);
    }

    [Fact]
    public void ComputeScrollOffset_ExtentFitsInViewport_ReturnsNull()
    {
        // extent (100) <= viewport (200) → everything fits, no scroll needed
        var result = HistoryScrollHelper.ComputeScrollOffset(3, 10, 100, 200, 0);
        Assert.Null(result);
    }

    [Fact]
    public void ComputeScrollOffset_ItemAlreadyVisible_ReturnsNull()
    {
        // 10 items, 500px extent → each item is 50px tall
        // currentIndex=3 → itemTop=150, itemBottom=200
        // viewport=300, currentOffsetY=0 → item is fully visible (0..300 contains 150..200)
        var result = HistoryScrollHelper.ComputeScrollOffset(3, 10, 500, 300, 0);
        Assert.Null(result);
    }

    [Fact]
    public void ComputeScrollOffset_ItemAboveViewport_ReturnsItemTop()
    {
        // 10 items, 500px extent → each item is 50px tall
        // currentIndex=2 → itemTop=100, itemBottom=150
        // viewport=100, currentOffsetY=200 → item is above viewport (100 < 200)
        var result = HistoryScrollHelper.ComputeScrollOffset(2, 10, 500, 100, 200);
        Assert.Equal(100.0, result);
    }

    [Fact]
    public void ComputeScrollOffset_ItemBelowViewport_ReturnsItemBottomMinusViewport()
    {
        // 10 items, 500px extent → each item is 50px tall
        // currentIndex=7 → itemTop=350, itemBottom=400
        // viewport=100, currentOffsetY=0 → item is below viewport (400 > 0+100)
        var result = HistoryScrollHelper.ComputeScrollOffset(7, 10, 500, 100, 0);
        Assert.Equal(300.0, result); // 400 - 100
    }
}
