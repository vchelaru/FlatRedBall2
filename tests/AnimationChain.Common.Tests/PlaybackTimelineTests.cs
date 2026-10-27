using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// Reverse and PingPong as mirrors of the forward timeline (#1538): one mapping from cycle time to forward time,
/// which every player uses to pick the frame and evaluate keyframes.
/// </summary>
public class PlaybackTimelineTests
{
    private const double T = 1.2; // two 0.6s frames
    private const double Eps = 1e-9;

    [Theory]
    // cycleTime, pingPong, reverse, expected forward time, expected backward
    [InlineData(0.0, false, false, 0.0, false)]
    [InlineData(0.3, false, false, 0.3, false)]
    [InlineData(1.2, false, false, 1.2, false)]
    [InlineData(0.0, false, true, 1.2, true)]
    [InlineData(0.3, false, true, 0.9, true)]
    [InlineData(0.6, false, true, 0.6, true)]
    [InlineData(1.2, false, true, 0.0, true)]
    [InlineData(0.0, true, false, 0.0, false)]
    [InlineData(0.6, true, false, 0.6, false)]
    [InlineData(1.2 - Eps, true, false, 1.2 - Eps, false)]
    [InlineData(1.2, true, false, 1.2, true)]
    [InlineData(1.2 + Eps, true, false, 1.2 - Eps, true)]
    [InlineData(1.8, true, false, 0.6, true)]
    [InlineData(2.1, true, false, 0.3, true)]
    [InlineData(2.4, true, false, 0.0, true)]
    [InlineData(0.0, true, true, 1.2, true)]
    [InlineData(0.9, true, true, 0.3, true)]
    [InlineData(1.2, true, true, 0.0, false)]
    [InlineData(1.5, true, true, 0.3, false)]
    [InlineData(2.4, true, true, 1.2, false)]
    public void Map_MirrorsTheForwardTimeline(double cycleTime, bool pingPong, bool reverse, double forward, bool backward)
    {
        var point = PlaybackTimeline.Map(cycleTime, T, pingPong, reverse);

        point.ForwardTime.ShouldBe(forward, 1e-12);
        point.IsBackward.ShouldBe(backward);
    }

    [Fact]
    public void CycleLength_PingPongIsTwiceTheForwardLength()
    {
        PlaybackTimeline.CycleLength(T, pingPong: true).ShouldBe(2.4, 1e-12);
        PlaybackTimeline.CycleLength(T, pingPong: false).ShouldBe(1.2, 1e-12);
    }

    private static readonly double[] Lengths = { 0.6, 0.6 };

    private static PlaybackPosition At(double cycleTime, bool pingPong, bool reverse) =>
        PlaybackTimeline.Locate(Lengths, static l => l, cycleTime, pingPong, reverse);

    [Theory]
    // cycleTime, pingPong, reverse, frame, elapsed (forward seconds into the frame)
    [InlineData(0.3, true, false, 0, 0.3)]
    [InlineData(0.9, true, false, 1, 0.3)]
    [InlineData(1.2, true, false, 1, 0.6)]   // turnaround: B is still shown, at its end
    [InlineData(1.5, true, false, 1, 0.3)]   // B held a second 0.6s on the way back
    [InlineData(2.1, true, false, 0, 0.3)]   // A's bounce, played backward
    [InlineData(2.4, true, false, 0, 0.0)]
    [InlineData(0.3, false, true, 1, 0.3)]   // Reverse: B held first
    [InlineData(0.9, false, true, 0, 0.3)]   // then A played backward
    [InlineData(0.3, true, true, 1, 0.3)]
    [InlineData(1.5, true, true, 0, 0.3)]    // PingPong + Reverse: second half forward
    public void Locate_FrameAndForwardElapsed(double cycleTime, bool pingPong, bool reverse, int frame, double elapsed)
    {
        var at = At(cycleTime, pingPong, reverse);

        at.FrameIndex.ShouldBe(frame);
        at.ElapsedInFrame.ShouldBe(elapsed, 1e-9);
    }

    [Fact]
    public void Locate_OnAFrameBoundary_ShowsTheFrameBeingEntered()
    {
        // Binary-exact 0.5s frames so the boundaries are exact. Forward at 0.5: entering B. Backward at 1.5 (forward 0.5): entering A.
        double[] halves = { 0.5, 0.5 };
        int FrameAt(double t) =>
            PlaybackTimeline.Locate(halves, static l => l, t, pingPong: true, reverse: false).FrameIndex;

        FrameAt(0.5).ShouldBe(1);
        FrameAt(1.5).ShouldBe(0);
    }

    [Fact]
    public void Visits_PingPong_ShowEachFrameEachWay_AndOnlyFrameChangesAreEntries()
    {
        // A(fwd) B(fwd) | B(back) A(back): the turnaround and the loop point don't change the frame.
        int count = PlaybackTimeline.VisitCount(2, pingPong: true);
        count.ShouldBe(4);
        Enumerable.Range(0, count).Select(v => PlaybackTimeline.FrameOfVisit(v, 2, true, false)).ShouldBe(new[] { 0, 1, 1, 0 });
        Enumerable.Range(0, count).Select(v => PlaybackTimeline.EntersNewFrame(v, 2, true, false))
            .ShouldBe(new[] { false, true, false, true });
        At(0.3, true, false).Visit.ShouldBe(0);
        At(0.9, true, false).Visit.ShouldBe(1);
        At(1.5, true, false).Visit.ShouldBe(2);
        At(2.1, true, false).Visit.ShouldBe(3);
    }

    [Fact]
    public void Visits_Reverse_RunFromTheLastFrame_EveryVisitIsAnEntry()
    {
        Enumerable.Range(0, 3).Select(v => PlaybackTimeline.FrameOfVisit(v, 3, false, true)).ShouldBe(new[] { 2, 1, 0 });
        PlaybackTimeline.EntersNewFrame(0, 3, false, true).ShouldBeTrue();
        At(0.3, false, true).Visit.ShouldBe(0);
    }
}
