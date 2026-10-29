using AnimationEditor.Core.ViewModels;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TimelineDockHeightTests
{
    [Fact]
    public void Resolve_NeverDragged_UsesDefaultHeight()
    {
        Assert.Equal(104, TimelineDockHeight.Resolve(defaultHeight: 104, neededHeight: 294, userHeight: null));
    }

    [Fact]
    public void Resolve_Dragged_TallerThanNeeded_CollapsesToNeeded()
    {
        Assert.Equal(94, TimelineDockHeight.Resolve(defaultHeight: 104, neededHeight: 94, userHeight: 300));
    }

    [Fact]
    public void Resolve_Dragged_ShorterThanNeeded_UsesDraggedHeight()
    {
        Assert.Equal(200, TimelineDockHeight.Resolve(defaultHeight: 104, neededHeight: 294, userHeight: 200));
    }

    [Fact]
    public void ClampDrag_StaysBetweenMinimumAndAvailable()
    {
        Assert.Equal(52, TimelineDockHeight.ClampDrag(10, available: 400));
        Assert.Equal(400, TimelineDockHeight.ClampDrag(900, available: 400));
        Assert.Equal(180, TimelineDockHeight.ClampDrag(180, available: 400));
    }

    [Fact]
    public void ClampDrag_AvailableBelowMinimum_UsesMinimum()
    {
        Assert.Equal(52, TimelineDockHeight.ClampDrag(100, available: 30));
    }
}
