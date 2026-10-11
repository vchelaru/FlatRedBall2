using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ChainClockTests
{
    private static AnimationChainSave Chain(bool loop, params float[] lengths)
    {
        var chain = new AnimationChainSave { Name = "Test", Loop = loop };
        foreach (float length in lengths)
            chain.Frames.Add(new AnimationFrameSave { FrameLength = length });
        return chain;
    }

    [Fact]
    public void Locate_Looping_WrapsPastTheEndBackToTheFirstFrame()
    {
        var chain = Chain(loop: true, 0.1f, 0.1f, 0.1f);

        var (index, elapsed) = ChainClock.Locate(chain, 0.35);

        index.ShouldBe(0);
        elapsed.ShouldBe(0.05, tolerance: 1e-6);
    }

    [Fact]
    public void Locate_ExactlyOnAFrameBoundary_LandsOnTheLaterFrameDespiteFloatNoise()
    {
        // 0.1f is not exactly 0.1, so a naive running sum puts the boundary a hair after t = 0.2.
        var chain = Chain(loop: true, 0.1f, 0.1f, 0.1f);

        ChainClock.Locate(chain, 0.2).FrameIndex.ShouldBe(2);
    }

    [Fact]
    public void Locate_NonLooping_HoldsTheLastFrameAfterTheEnd()
    {
        var chain = Chain(loop: false, 0.1f, 0.2f);

        var (index, _) = ChainClock.Locate(chain, 5.0);

        index.ShouldBe(1);
    }

    [Fact]
    public void Locate_FrameWithNoLength_UsesTheTenthOfASecondPlaybackDefault()
    {
        var chain = Chain(loop: true, 0f, 0f);

        ChainClock.Locate(chain, 0.15).FrameIndex.ShouldBe(1);
    }

    [Fact]
    public void TotalLength_SumsFrameLengths_WithTheSameDefaultForUnsetLengths()
    {
        var chain = Chain(loop: true, 0.25f, 0f);

        ChainClock.TotalLength(chain).ShouldBe(0.35, tolerance: 1e-6);
    }
}
