using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;
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
    public void ComputeOffset_InterpolateOnLastFrame_Looping_EasesTowardFirstFrame()
    {
        // A looping chain's last frame is followed by frame 0, so it eases back toward it.
        var chain = MakeChain((-10f, 0f), (10f, 0f));
        chain.Loop = true;
        var (x, y) = OffsetInterpolator.ComputeOffset(chain, frameIndex: 1, frameElapsed: 0.05, interpolate: true);
        Assert.Equal(0f, x, precision: 4);
        Assert.Equal(0f, y, precision: 4);
    }

    [Fact]
    public void ComputeOffset_InterpolateOnLastFrame_NotLooping_HoldsLastFrameOffset()
    {
        // A non-looping chain stops on its last frame, so that frame holds its own offset.
        var chain = MakeChain((10f, 20f), (50f, 60f));
        chain.Loop = false;
        var (x, y) = OffsetInterpolator.ComputeOffset(chain, frameIndex: 1, frameElapsed: 0.05, interpolate: true);
        Assert.Equal(50f, x, precision: 4);
        Assert.Equal(60f, y, precision: 4);
    }

    [Fact]
    public void ComputeOffset_InterpolateIgnoresCurvesAndSkipFlagsStoredInFile()
    {
        // The preview toggle predates per-frame curves (#1478): it eases every frame linearly, so a stored
        // Instant curve or skip flag changes nothing until the editor UI for them lands.
        var chain = MakeChain((0f, 0f), (999f, 999f), (100f, 0f));
        chain.Frames[0].OffsetInterpolation = "Instant";
        chain.Frames[1].SkipOffsetKeyframe = true;

        var (x, _) = OffsetInterpolator.ComputeOffset(chain, frameIndex: 0, frameElapsed: 0.05, interpolate: true);

        Assert.Equal(499.5f, x, precision: 3);
    }

    [Fact]
    public void ComputeOffset_SnapIgnoresCurvesStoredInFile()
    {
        var chain = MakeChain((10f, 0f), (50f, 0f));
        chain.Frames[0].OffsetInterpolation = "Linear";

        var (x, _) = OffsetInterpolator.ComputeOffset(chain, frameIndex: 0, frameElapsed: 0.05, interpolate: false);

        Assert.Equal(10f, x, precision: 4);
    }

    [Fact]
    public void ComputeOffset_InterpolateUnsetFrameLength_UsesPlaybackDefault()
    {
        // FrameLength 0 plays as 0.1s, so 0.05s in is halfway.
        var chain = MakeChain((0f, 0f), (100f, 0f));
        chain.Frames[0].FrameLength = 0f;

        var (x, _) = OffsetInterpolator.ComputeOffset(chain, frameIndex: 0, frameElapsed: 0.05, interpolate: true);

        Assert.Equal(50f, x, precision: 3);
    }

    [Fact]
    public void ComputeOffset_InterpolateElapsedPastFrameLength_ClampsToNextFrameOffset()
    {
        var chain = MakeChain((0f, 0f), (100f, 0f));

        var (x, _) = OffsetInterpolator.ComputeOffset(chain, frameIndex: 0, frameElapsed: 5.0, interpolate: true);

        Assert.Equal(100f, x, precision: 3);
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
