using System.Linq;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// The visit order of a chain's frames under PingPong and Reverse (#1538), shared by every player.
/// </summary>
public class PlaybackOrderTests
{
    private static (int Frame, bool Backward)[] Visits(int frameCount, bool loop, bool pingPong, bool reverse) =>
        Enumerable.Range(0, PlaybackOrder.VisitCount(frameCount, loop, pingPong))
            .Select(v => PlaybackOrder.VisitAt(v, frameCount, pingPong, reverse))
            .Select(v => (v.FrameIndex, v.IsBackward))
            .ToArray();

    [Fact]
    public void VisitAt_Default_PlaysFramesForward()
    {
        Visits(3, loop: true, pingPong: false, reverse: false)
            .ShouldBe(new[] { (0, false), (1, false), (2, false) });
    }

    [Fact]
    public void VisitAt_Reverse_PlaysFramesBackwardFromTheLast()
    {
        Visits(3, loop: true, pingPong: false, reverse: true)
            .ShouldBe(new[] { (2, true), (1, true), (0, true) });
    }

    [Fact]
    public void VisitAt_LoopingPingPong_DoesNotRepeatTheEndFrames()
    {
        // A B C B, then the cycle restarts on A. C starts the backward half.
        Visits(3, loop: true, pingPong: true, reverse: false)
            .ShouldBe(new[] { (0, false), (1, false), (2, true), (1, true) });
    }

    [Fact]
    public void VisitAt_NonLoopingPingPong_EndsBackOnTheFirstFrame()
    {
        Visits(3, loop: false, pingPong: true, reverse: false)
            .ShouldBe(new[] { (0, false), (1, false), (2, true), (1, true), (0, true) });
    }

    [Fact]
    public void VisitAt_ReversePingPong_StartsAtTheEndGoesBackwardThenForward()
    {
        Visits(3, loop: true, pingPong: true, reverse: true)
            .ShouldBe(new[] { (2, true), (1, true), (0, false), (1, false) });
        Visits(3, loop: false, pingPong: true, reverse: true)
            .ShouldBe(new[] { (2, true), (1, true), (0, false), (1, false), (2, false) });
    }

    [Fact]
    public void VisitAt_PingPongWithOneFrame_HasOneVisit()
    {
        Visits(1, loop: true, pingPong: true, reverse: false).ShouldBe(new[] { (0, false) });
        Visits(1, loop: false, pingPong: true, reverse: false).ShouldBe(new[] { (0, false) });
    }
}
