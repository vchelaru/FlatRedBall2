using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.ViewModels;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// The preview's playback, the stateless clock behind export, and the keyframe lane follow Reverse and PingPong as
/// mirrors of the forward timeline (#1538). The chain is the user's repro: A at x=0 with Bounce Out to B at x=80, 0.6s each.
/// </summary>
[Collection("SequentialSingletons")]
public class PlaybackModePreviewTests
{
    private static AnimationChainSave BounceChain(bool loop = true, bool pingPong = false, bool reverse = false)
    {
        var chain = new AnimationChainSave { Name = "Bounce", Loop = loop, PingPong = pingPong, Reverse = reverse };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.6f, RelativeX = 0, OffsetInterpolation = "Bounce", OffsetEasing = "Out" });
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.6f, RelativeX = 80 });
        return chain;
    }

    // What the preview draws after playing `seconds` from the start.
    private static float PreviewXAt(AnimationChainSave chain, double seconds)
    {
        var ctrl = new PlaybackController();
        ctrl.SetChain(chain);
        ctrl.Advance(seconds);
        return KeyframeSampler.Sample(chain, ctrl.CurrentFrameIndex, ctrl.FrameElapsed).OffsetX;
    }

    private static float ClockXAt(AnimationChainSave chain, double seconds)
    {
        var (frame, elapsed) = ChainClock.Locate(chain, seconds);
        return KeyframeSampler.Sample(chain, frame, elapsed).OffsetX;
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.3)]
    [InlineData(0.5)]
    public void PingPong_SecondHalf_IsTheForwardTimelinePlayedBackward(double d)
    {
        var pingPong = BounceChain(pingPong: true);
        var forward = BounceChain();

        Assert.Equal(PreviewXAt(forward, 0.6 - d), PreviewXAt(pingPong, 1.8 + d), 3);
        Assert.Equal(80f, PreviewXAt(pingPong, 1.2 + d), 3);
        Assert.Equal(ClockXAt(forward, 0.6 - d), ClockXAt(pingPong, 1.8 + d), 3);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.7)]
    [InlineData(1.1)]
    public void Reverse_ValueAtT_IsTheForwardValueAtLengthMinusT(double t)
    {
        var reverse = BounceChain(reverse: true);
        var forward = BounceChain();

        Assert.Equal(PreviewXAt(forward, 1.2 - t), PreviewXAt(reverse, t), 3);
        Assert.Equal(ClockXAt(forward, 1.2 - t), ClockXAt(reverse, t), 3);
    }

    [Fact]
    public void TotalLength_PingPongCycleIsTwiceTheChain()
    {
        Assert.Equal(2.4, ChainClock.TotalLength(BounceChain(pingPong: true)), 5);
        Assert.Equal(1.2, ChainClock.TotalLength(BounceChain(reverse: true)), 5);
    }

    [Fact]
    public void SetChain_Reverse_StartsOnTheLastFrameAtItsEnd()
    {
        var ctrl = new PlaybackController();

        ctrl.SetChain(BounceChain(reverse: true));

        Assert.Equal(1, ctrl.CurrentFrameIndex);
        Assert.Equal(0.6, ctrl.FrameElapsed, 5);
    }

    [Fact]
    public void Advance_PingPong_FrameIndexChangedOnlyWhenTheShownFrameChanges()
    {
        var ctrl = new PlaybackController();
        ctrl.SetChain(BounceChain(pingPong: true));
        int changes = 0;
        ctrl.FrameIndexChanged += _ => changes++;

        for (int i = 0; i < 24; i++) ctrl.Advance(0.1); // one 2.4s cycle

        Assert.Equal(2, changes); // into B, back into A; not at the turnaround or the loop point
    }

    [Fact]
    public void Advance_NonLoopingPingPong_PlaysOneFullCycleThenStopsOnTheFirstFrame()
    {
        var ctrl = new PlaybackController();
        ctrl.SetChain(BounceChain(loop: false, pingPong: true));

        ctrl.Advance(2.3);
        Assert.True(ctrl.IsPlaying);
        ctrl.Advance(0.2);

        Assert.False(ctrl.IsPlaying);
        Assert.Equal(0, ctrl.CurrentFrameIndex);
        Assert.Equal(0, ctrl.FrameElapsed, 5);
    }

    [Fact]
    public void SeekToFrame_Reverse_FractionIsThroughTheFramesForwardTime()
    {
        // Clicking 25% across A's cell shows A at forward 0.15s, the same value the forward chain shows there.
        var reverse = BounceChain(reverse: true);
        var ctrl = new PlaybackController();
        ctrl.SetChain(reverse);

        ctrl.SeekToFrame(0, 0.25);

        Assert.Equal(0, ctrl.CurrentFrameIndex);
        Assert.Equal(0.15, ctrl.FrameElapsed, 5);
        Assert.Equal(1.05, ctrl.AnimTime, 5);
    }

    [Fact]
    public void KeyframeLane_PingPong_IsTheForwardLaneWithoutTheLoopWrap()
    {
        var lane = KeyframeLaneBuilder.Build(BounceChain(pingPong: true), KeyframeGroup.Offset, new double[] { 60, 60 })!;

        var span = Assert.Single(lane.Spans);
        Assert.Equal((0.0, 60.0), (span.StartX, span.EndX));
        Assert.Equal(new[] { new KeyframeLaneHold(60, 120) }, lane.Holds);
    }
}
