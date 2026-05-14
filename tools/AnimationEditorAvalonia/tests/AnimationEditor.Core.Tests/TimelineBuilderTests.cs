using AnimationEditor.Core.ViewModels;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TimelineBuilderTests
{
    [Fact]
    public void BuildFrameItems_NullChain_ReturnsEmptyList()
    {
        var result = TimelineBuilder.BuildFrameItems(null);
        Assert.Empty(result);
    }

    [Fact]
    public void BuildFrameItems_UsesFrameLengthForRelativeWidths()
    {
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f });
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.35f });

        var result = TimelineBuilder.BuildFrameItems(chain);

        Assert.Equal(2, result.Count);
        Assert.True(result[1].Width > result[0].Width);
        Assert.Equal("1", result[0].IndexLabel);
        Assert.Equal("2", result[1].IndexLabel);
    }

    [Fact]
    public void BuildFrameItems_WidthsAreProportionalToDuration()
    {
        // Use frame lengths long enough that proportional width exceeds MinCellWidth.
        // 0.3s → 36px, 0.6s → 72px (both > 24px MinCellWidth).
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.3f });
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.6f });

        var result = TimelineBuilder.BuildFrameItems(chain);

        // A 0.6s frame must be exactly twice as wide as a 0.3s frame (no additive offset).
        Assert.Equal(result[0].Width * 2.0, result[1].Width, precision: 6);
    }

    [Fact]
    public void BuildFrameItems_ClampsNegativeFrameLengthToMinCellWidth()
    {
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = -1f });

        var result = TimelineBuilder.BuildFrameItems(chain);

        Assert.Single(result);
        Assert.Equal(TimelineBuilder.MinCellWidth, result[0].Width);
    }

    [Fact]
    public void BuildFrameItems_ZeroLengthFrame_GetsMinCellWidth()
    {
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0f });

        var result = TimelineBuilder.BuildFrameItems(chain);

        Assert.Single(result);
        Assert.Equal(TimelineBuilder.MinCellWidth, result[0].Width);
    }

    [Fact]
    public void BuildFrameItems_WidthEqualsFrameLengthTimesPixelsPerSecond()
    {
        // 0.5s → 60px, well above MinCellWidth, so no clamping.
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.5f });

        var result = TimelineBuilder.BuildFrameItems(chain);

        Assert.Equal(0.5 * TimelineBuilder.PixelsPerSecond, result[0].Width, precision: 6);
    }

    [Fact]
    public void BuildFrameItems_ShortFrame_ClampsToMinCellWidth()
    {
        // 0.1s → 12px proportional, but MinCellWidth=24px wins.
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f });

        var result = TimelineBuilder.BuildFrameItems(chain);

        Assert.Equal(TimelineBuilder.MinCellWidth, result[0].Width);
    }
}
