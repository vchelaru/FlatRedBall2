using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>The preview's playback and stateless clock follow the chain's PingPong and Reverse (#1538).</summary>
[Collection("SequentialSingletons")]
public class PlaybackModePreviewTests
{
    // Three 1s frames.
    private static AnimationChainSave MakeChain(bool loop = true, bool pingPong = false, bool reverse = false)
    {
        var chain = new AnimationChainSave { Name = "Test", Loop = loop, PingPong = pingPong, Reverse = reverse };
        for (int i = 0; i < 3; i++)
            chain.Frames.Add(new AnimationFrameSave { FrameLength = 1f, RelativeX = i * 100 });
        return chain;
    }

    private static int[] Step(PlaybackController ctrl, int count)
    {
        var result = new int[count];
        for (int i = 0; i < count; i++)
        {
            ctrl.Advance(1);
            result[i] = ctrl.CurrentFrameIndex;
        }
        return result;
    }

    [Fact]
    public void Advance_PingPong_BouncesAndFlagsTheBackwardHalf()
    {
        var ctrl = new PlaybackController();
        ctrl.SetChain(MakeChain(pingPong: true));
        ctrl.Advance(0.5);

        Assert.Equal(new[] { 1, 2, 1, 0 }, Step(ctrl, 4));

        ctrl.Advance(1); // frame 1 going forward again
        Assert.False(ctrl.IsBackward);
        ctrl.Advance(1); // frame 2 starts the backward half
        Assert.True(ctrl.IsBackward);
    }

    [Fact]
    public void SetChain_Reverse_StartsOnTheLastFrameBackward()
    {
        var ctrl = new PlaybackController();

        ctrl.SetChain(MakeChain(reverse: true));

        Assert.Equal(2, ctrl.CurrentFrameIndex);
        Assert.True(ctrl.IsBackward);
        Assert.Equal(new[] { 1, 0, 2 }, Step(ctrl, 3).Select(i => i).ToArray());
    }

    [Fact]
    public void Advance_NonLoopingPingPong_StopsBackOnTheFirstFrame()
    {
        var ctrl = new PlaybackController();
        ctrl.SetChain(MakeChain(loop: false, pingPong: true));

        ctrl.Advance(100);

        Assert.Equal(0, ctrl.CurrentFrameIndex);
        Assert.False(ctrl.IsPlaying);
    }

    [Fact]
    public void Play_NonLoopingReverseAtTheEnd_RestartsFromTheLastFrame()
    {
        var ctrl = new PlaybackController();
        ctrl.SetChain(MakeChain(loop: false, reverse: true));
        ctrl.Advance(100);

        ctrl.Play();

        Assert.Equal(2, ctrl.CurrentFrameIndex);
    }

    [Fact]
    public void Locate_PingPong_ReportsFrameAndDirection()
    {
        var chain = MakeChain(pingPong: true);

        var at = ChainClock.LocateVisit(chain, 2.5);

        Assert.Equal(2, at.FrameIndex);
        Assert.True(at.IsBackward);
        Assert.Equal(0.5, at.Elapsed, 3);
        Assert.Equal(4.0, ChainClock.TotalLength(chain), 3);
    }

    [Fact]
    public void Sample_Backward_GlidesTowardThePreviousFrame()
    {
        var chain = MakeChain(reverse: true);
        chain.Frames[0].OffsetInterpolation = "Linear";

        var sample = KeyframeSampler.Sample(chain, 1, 0.25, backward: true);

        Assert.Equal(75f, sample.OffsetX, 3);
    }
}
