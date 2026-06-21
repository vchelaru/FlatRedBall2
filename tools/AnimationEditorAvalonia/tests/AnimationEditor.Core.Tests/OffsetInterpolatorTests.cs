using AnimationEditor.Core.Rendering;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class OffsetInterpolatorTests
{
    private static AnimationChainSave MakeChain(params (float x, float y)[] offsets)
    {
        var chain = new AnimationChainSave { Name = "Test" };
        foreach (var (x, y) in offsets)
            chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f, RelativeX = x, RelativeY = y });
        return chain;
    }

    [Fact]
    public void ComputeOffset_SnapMode_IgnoresElapsed_ReturnsCurrentFrameOffset()
    {
        // Snap mirrors the FlatRedBall runtime: the offset is exactly the current frame's,
        // regardless of how far into the frame playback has progressed.
        var chain = MakeChain((10f, 20f), (50f, 60f));
        var (x, y) = OffsetInterpolator.ComputeOffset(chain, frameIndex: 0, frameElapsed: 0.05, interpolate: false);
        Assert.Equal(10f, x, precision: 4);
        Assert.Equal(20f, y, precision: 4);
    }

    [Fact]
    public void ComputeOffset_InterpolateAtMidFrame_ReturnsMidpointTowardNextFrame()
    {
        // frame 0 = (10,20), frame 1 = (50,60); 0.1s frame, halfway (0.05s) → midpoint (30,40).
        var chain = MakeChain((10f, 20f), (50f, 60f));
        var (x, y) = OffsetInterpolator.ComputeOffset(chain, frameIndex: 0, frameElapsed: 0.05, interpolate: true);
        Assert.Equal(30f, x, precision: 4);
        Assert.Equal(40f, y, precision: 4);
    }

    [Fact]
    public void ComputeOffset_InterpolateOnLastFrame_HoldsLastFrameOffset()
    {
        // The last frame has no successor to ease toward, so it holds its own offset
        // regardless of elapsed — the loop snaps back to frame 0 only when the image hard-cuts.
        var chain = MakeChain((10f, 20f), (50f, 60f));
        var (x, y) = OffsetInterpolator.ComputeOffset(chain, frameIndex: 1, frameElapsed: 0.05, interpolate: true);
        Assert.Equal(50f, x, precision: 4);
        Assert.Equal(60f, y, precision: 4);
    }

    [Fact]
    public void ComputeOffset_InterpolateSingleFrameChain_ReturnsThatFrameOffset()
    {
        // Nothing to interpolate toward; return the only frame's offset.
        var chain = MakeChain((10f, 20f));
        var (x, y) = OffsetInterpolator.ComputeOffset(chain, frameIndex: 0, frameElapsed: 0.05, interpolate: true);
        Assert.Equal(10f, x, precision: 4);
        Assert.Equal(20f, y, precision: 4);
    }
}
