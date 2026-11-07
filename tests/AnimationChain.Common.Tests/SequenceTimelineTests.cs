using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

public class SequenceTimelineTests
{
    private static SequenceChainReferenceSave Ref(string chain, double start, float loops = 1) =>
        new() { Source = "a.achj", Chain = chain, Start = start, Loops = loops };

    [Fact]
    public void ActiveAt_LaterChainStartsInsideEarlier_CutsTheEarlierAndNeverResumesIt()
    {
        var charge = Ref("Charge", start: 0); // 3 s cycle
        var hurt = Ref("Hurt", start: 1);     // 0.5 s cycle
        var track = new SequenceTrackSave { Chains = { charge, hurt } };
        double Cycle(SequenceChainReferenceSave r) => r == charge ? 3 : 0.5;

        SequenceTimeline.ActiveAt(track, 0.5, Cycle, out double localTime).ShouldBe(charge);
        localTime.ShouldBe(0.5, 1e-9);
        SequenceTimeline.ActiveAt(track, 1.2, Cycle, out _).ShouldBe(hurt);
        // Hurt ended at 1.5; Charge's tail (to 3.0) is cut, so the sprite is hidden.
        SequenceTimeline.ActiveAt(track, 2.0, Cycle, out _).ShouldBeNull();
        SequenceTimeline.PlayedLength(track, charge, cycleLength: 3).ShouldBe(1, 1e-9);
    }

    [Fact]
    public void PlayedLength_InterrupterMovesAway_EarlierChainPlaysInFullAgain()
    {
        var charge = Ref("Charge", start: 0, loops: 2);
        var hurt = Ref("Hurt", start: 1);
        var track = new SequenceTrackSave { Chains = { charge, hurt } };

        hurt.Start = 10;

        SequenceTimeline.PlayedLength(track, charge, cycleLength: 3).ShouldBe(6, 1e-9);
    }

    [Fact]
    public void PlayedLength_TwoChainsStartTogether_EarlierInListPlaysZeroSeconds()
    {
        var first = Ref("First", start: 2);
        var second = Ref("Second", start: 2);
        var track = new SequenceTrackSave { Chains = { first, second } };

        SequenceTimeline.PlayedLength(track, first, cycleLength: 1).ShouldBe(0);
        SequenceTimeline.PlayedLength(track, second, cycleLength: 1).ShouldBe(1, 1e-9);
        SequenceTimeline.ActiveAt(track, 2.5, _ => 1, out _).ShouldBe(second);
    }

    [Fact]
    public void ActiveAt_MissingChain_NeverPlaysButStillCutsTheChainBeforeIt()
    {
        var walk = Ref("Walk", start: 0);
        var missing = Ref("Deleted", start: 1);
        var track = new SequenceTrackSave { Chains = { walk, missing } };
        // A chain that can't be resolved has a cycle length of 0.
        double Cycle(SequenceChainReferenceSave r) => r == walk ? 3 : 0;

        SequenceTimeline.ActiveAt(track, 1.5, Cycle, out _).ShouldBeNull();
        SequenceTimeline.PlayedLength(track, walk, cycleLength: 3).ShouldBe(1, 1e-9);
    }

    [Fact]
    public void Length_FloatLoops_EndsPartWayThroughTheLastCycle()
    {
        var sequence = new SequenceSave { Tracks = { new SequenceTrackSave { Chains = { Ref("Walk", start: 1, loops: 2.5f) } } } };

        // A ping-pong chain of 0.4 s forward has a 0.8 s cycle.
        SequenceTimeline.Length(sequence, _ => 0.8).ShouldBe(3, 1e-6);
    }

    [Theory]
    [InlineData(0.5, 2, false, 0.5)]   // inside the sequence: shown as is
    [InlineData(2.5, 2, true, 0.5)]    // past the end with Loop: wraps
    [InlineData(2.5, 2, false, 2)]     // past the end without Loop: held at the length
    [InlineData(-1, 2, false, 0)]      // negative elapsed time shows 0
    [InlineData(-1, 2, true, 0)]
    [InlineData(1, 0, true, 0)]        // an empty sequence always shows 0
    public void WrapTime_WrapsOrHoldsAtTheEnd_AndNeverGoesNegative(double time, double length, bool loop, double expected)
    {
        SequenceTimeline.WrapTime(time, length, loop).ShouldBe(expected, 1e-9);
    }

    [Fact]
    public void FullLength_NegativeLoops_IsZero()
    {
        SequenceTimeline.FullLength(loops: -2, cycleLength: 3).ShouldBe(0);
    }

    [Fact]
    public void ActiveAt_ExactlyAtTheEndOrBeforeTheFirstStart_IsHidden()
    {
        var walk = Ref("Walk", start: 1);
        var track = new SequenceTrackSave { Chains = { walk } };

        SequenceTimeline.ActiveAt(track, 0.99, _ => 2, out _).ShouldBeNull();
        SequenceTimeline.ActiveAt(track, 1.0, _ => 2, out double atStart).ShouldBe(walk);
        atStart.ShouldBe(0, 1e-9);
        SequenceTimeline.ActiveAt(track, 3.0, _ => 2, out _).ShouldBeNull();
    }

    [Fact]
    public void ActiveAt_BeforeTheFirstStart_ReturnsNull()
    {
        // Hidden before the first chain on purpose: this is how a sprite appears partway through a sequence.
        var walk = Ref("Walk", start: 0.4);
        var track = new SequenceTrackSave { Chains = { walk } };

        SequenceTimeline.ActiveAt(track, 0, _ => 2, out _).ShouldBeNull();
        SequenceTimeline.ActiveAt(track, 0.39, _ => 2, out _).ShouldBeNull();
        SequenceTimeline.ActiveAt(track, 0.4, _ => 2, out _).ShouldBe(walk);
    }

    [Fact]
    public void ActiveAt_AfterTheLastChainEnds_ReturnsNull()
    {
        // Hidden after the last chain on purpose: this is how a sprite disappears partway through a sequence.
        var walk = Ref("Walk", start: 1);
        var track = new SequenceTrackSave { Chains = { walk } };

        SequenceTimeline.ActiveAt(track, 2.9, _ => 2, out _).ShouldBe(walk);
        SequenceTimeline.ActiveAt(track, 3, _ => 2, out _).ShouldBeNull();
        SequenceTimeline.ActiveAt(track, 10, _ => 2, out _).ShouldBeNull();
    }

    [Fact]
    public void Length_AMissingChainCutsTheOneBeforeIt_AndNoChainsIsZero()
    {
        var long_ = Ref("Long", start: 0, loops: 10);
        var missing = Ref("Deleted", start: 1);
        var sequence = new SequenceSave { Tracks = { new SequenceTrackSave { Chains = { long_, missing } } } };
        double Cycle(SequenceChainReferenceSave r) => r == long_ ? 1 : 0;

        // Long asks for 10 s but is cut at 1 s by the missing chain, which itself plays 0 s.
        SequenceTimeline.Length(sequence, Cycle).ShouldBe(1, 1e-9);
        SequenceTimeline.Length(new SequenceSave(), _ => 1).ShouldBe(0);
    }

    [Fact]
    public void LoopsForEdgeDrag_BeforeTheStart_NeverShrinksBelowTheMinimum()
    {
        SequenceTimeline.LoopsForEdgeDrag(start: 2, edgeTime: 1, cycleLength: 1, nextStart: null, magnetSeconds: 0.05)
            .ShouldBe(SequenceTimeline.MinimumLoops);
        SequenceTimeline.LoopsForEdgeDrag(start: 2, edgeTime: 2, cycleLength: 1, nextStart: null, magnetSeconds: 0.05)
            .ShouldBe(SequenceTimeline.MinimumLoops);
    }

    [Fact]
    public void LoopsForEdgeDrag_NoCycleToMeasure_ReturnsOne()
    {
        SequenceTimeline.LoopsForEdgeDrag(start: 0, edgeTime: 5, cycleLength: 0, nextStart: null, magnetSeconds: 0.05).ShouldBe(1);
    }

    [Fact]
    public void LoopsForEdgeDrag_CycleAndNextChainBothInReach_PullsToTheNearerOne()
    {
        // Cycle 1 s from 0: whole cycle at 2.0, next chain at 2.04, edge dragged to 2.01, magnet 0.05.
        SequenceTimeline.LoopsForEdgeDrag(start: 0, edgeTime: 2.01, cycleLength: 1, nextStart: 2.04, magnetSeconds: 0.05)
            .ShouldBe(2f, 1e-5f);
        // Edge at 2.035: the next chain is the nearer.
        SequenceTimeline.LoopsForEdgeDrag(start: 0, edgeTime: 2.035, cycleLength: 1, nextStart: 2.04, magnetSeconds: 0.05)
            .ShouldBe(2.04f, 1e-5f);
    }

    [Fact]
    public void LoopsForEdgeDrag_NextChainStartingTogether_IsNotAMagnet()
    {
        // A tie (next starts where this one does) has no edge to snap to: 0.4 s of a 4 s cycle stays 0.1 loops.
        SequenceTimeline.LoopsForEdgeDrag(start: 1, edgeTime: 1.4, cycleLength: 4, nextStart: 1, magnetSeconds: 1)
            .ShouldBe(0.1f, 1e-5f);
    }

    [Theory]
    // edge time, expected loops (cycle 1 s, start 0, magnet 0.05 s, next chain at 2.5 s)
    [InlineData(1.7, 1.7)]
    [InlineData(2.04, 2.0)]   // within the magnet: pulled to a whole cycle
    [InlineData(2.53, 2.5)]   // within the magnet: pulled to the next chain's start
    [InlineData(3.6, 3.6)]    // past the next chain: allowed, the overlap is cut at playback
    public void LoopsForEdgeDrag_FreeWithMagnetsAtCyclesAndNextChain(double edgeTime, double expected)
    {
        SequenceTimeline.LoopsForEdgeDrag(start: 0, edgeTime, cycleLength: 1, nextStart: 2.5, magnetSeconds: 0.05)
            .ShouldBe((float)expected, 1e-5f);
    }
}
