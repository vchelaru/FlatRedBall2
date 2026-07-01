using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// <see cref="RollingAverage"/> backs the preview draw-time diagnostics overlay (#514): it keeps
/// the last N per-frame durations and reports their mean so the on-canvas ms/frame readout is
/// smoothed instead of jittering every frame.
/// </summary>
public class RollingAverageTests
{
    [Fact]
    public void Average_MoreSamplesThanWindow_AveragesOnlyTheMostRecentWindow()
    {
        // Window of 3; feed 1,2,3,4,5 → only the last three (3,4,5) count → mean 4.
        var avg = new RollingAverage(3);
        avg.Add(1);
        avg.Add(2);
        avg.Add(3);
        avg.Add(4);
        avg.Add(5);

        Assert.Equal(4d, avg.Average, 6);
    }

    [Fact]
    public void Average_NoSamples_ReturnsZero()
    {
        var avg = new RollingAverage(10);

        Assert.Equal(0d, avg.Average);
    }

    [Fact]
    public void Average_PartiallyFilledWindow_AveragesOnlyTheSamplesAdded()
    {
        // Window of 10 but only two samples added → divide by 2, not 10.
        var avg = new RollingAverage(10);
        avg.Add(10);
        avg.Add(20);

        Assert.Equal(15d, avg.Average, 6);
    }
}
