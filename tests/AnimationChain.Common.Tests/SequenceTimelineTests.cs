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
