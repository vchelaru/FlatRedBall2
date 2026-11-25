using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// A continuous track keeps its chain's playback phase across a looping sequence's wrap when the chain playing at the wrap
/// is the same chain as the first block (at start 0). Otherwise, and for an ordinary track, the wrap restarts it.
/// </summary>
public class SequenceTimelineContinuousTests
{
    private static SequenceChainReferenceSave Ref(string chain, double start, double? length = null) =>
        new() { Source = "a.achj", Chain = chain, Start = start, Length = length };

    // Chain "A" has a 0.5 s cycle, "B" a 1 s cycle.
    private static double? Cycle(SequenceChainReferenceSave reference) => reference.Chain == "A" ? 0.5 : 1;

    private static SequenceTrackSave Track(bool continuous, params SequenceChainReferenceSave[] chains)
    {
        var track = new SequenceTrackSave { Continuous = continuous };
        track.Chains.AddRange(chains);
        return track;
    }

    private static double Local(SequenceTrackSave track, double time, int pass, double length, out SequenceChainReferenceSave? active)
    {
        active = SequenceTimeline.ActiveAtPass(track, time, pass, length, Cycle, out double local);
        return local;
    }

    [Theory]
    [InlineData(0, 0.15)]
    [InlineData(1, 1.35)]
    [InlineData(2, 2.55)]
    [InlineData(3, 3.75)]
    public void ActiveAtPass_AFractionalBlock_CarriesItsPhaseAcrossThreeWraps(int pass, double expectedLocal)
    {
        // A x 2.4 = 1.2 s, the whole sequence: every wrap lands mid-cycle.
        var first = Ref("A", 0, 1.2);
        var track = Track(true, first);

        Local(track, 0.15, pass, 1.2, out var active).ShouldBe(expectedLocal, 1e-6);

        active.ShouldBe(first);
    }

    [Fact]
    public void ActiveAtPass_AnOrdinaryTrack_RestartsAtEveryWrap()
    {
        var track = Track(false, Ref("A", 0, 1.2));

        Local(track, 0.15, pass: 3, length: 1.2, out _).ShouldBe(0.15, 1e-6);
    }

    [Fact]
    public void ActiveAtPass_PassZero_IsActiveAt()
    {
        var track = Track(true, Ref("A", 0, 1.2));

        SequenceTimeline.ActiveAt(track, 0.15, Cycle, out double plain);

        Local(track, 0.15, pass: 0, length: 1.2, out _).ShouldBe(plain, 1e-9);
    }

    [Fact]
    public void ActiveAtPass_ADifferentChainPlayingAtTheWrap_Restarts()
    {
        // A (0..0.5) then B (0.5..1.5): B is playing when the 1.5 s sequence wraps.
        var track = Track(true, Ref("A", 0), Ref("B", 0.5));

        Local(track, 0.2, pass: 2, length: 1.5, out _).ShouldBe(0.2, 1e-6);
    }

    [Fact]
    public void ActiveAtPass_TheTrackIsHiddenAtTheWrap_Restarts()
    {
        // A ends at 0.5 s; the sequence (1.5 s, set by other tracks) wraps while this track is hidden.
        var track = Track(true, Ref("A", 0));

        Local(track, 0.2, pass: 1, length: 1.5, out _).ShouldBe(0.2, 1e-6);
    }

    [Fact]
    public void ActiveAtPass_TheSameChainPlayingAsALaterBlock_CarriesThatBlocksPhaseIntoTheFirst()
    {
        // A@0 (0.5 s), B@0.5 (1 s), A@1.5 x2 (1 s): at the 2.5 s wrap, A's third block has played 1.0 s.
        var track = Track(true, Ref("A", 0), Ref("B", 0.5), Ref("A", 1.5, 1.0));

        Local(track, 0.2, pass: 1, length: 2.5, out var first).ShouldBe(1.2, 1e-6);
        first!.Start.ShouldBe(0);
        // The later block's phase is its own at the next wrap too, not the first block's carry added again.
        Local(track, 0.2, pass: 2, length: 2.5, out _).ShouldBe(1.2, 1e-6);
    }

    [Fact]
    public void ActiveAtPass_OnlyTheFirstBlockCarries()
    {
        var track = Track(true, Ref("A", 0), Ref("B", 0.5), Ref("A", 1.5, 1.0));

        Local(track, 0.7, pass: 2, length: 2.5, out var active).ShouldBe(0.2, 1e-6);

        active!.Chain.ShouldBe("B");
    }

    [Fact]
    public void ActiveAtPass_AFirstBlockThatDoesNotStartAtZero_Restarts()
    {
        var track = Track(true, Ref("A", 0.25, 1.15));

        Local(track, 0.4, pass: 2, length: 1.4, out _).ShouldBe(0.15, 1e-6);
    }

    [Fact]
    public void CarryOver_IsTheSequenceLengthPerPass_ForOneFullBlock()
    {
        var track = Track(true, Ref("A", 0, 1.2));

        SequenceTimeline.CarryOver(track, 1.2, 0, Cycle).ShouldBe(0);
        SequenceTimeline.CarryOver(track, 1.2, 3, Cycle).ShouldBe(3.6, 1e-6);
    }
}
