using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>How a dragged multi-selection moves: one time delta for all, one lane delta for all, clamped so nothing leaves the timeline.</summary>
public class TimelineMultiMoveTests
{
    private static TimelineMultiMove.Origin At(int track, double start) => new(track, start);

    [Fact]
    public void Plan_KeepsTheRequestedDeltas_WhenEveryChainStaysValid()
    {
        var plan = TimelineMultiMove.Plan(new[] { At(0, 1), At(1, 2) }, requestedTime: 0.5, requestedTracks: 1, trackCount: 3);

        plan.ShouldBe(new TimelineMultiMove.Result(0.5, 1));
    }

    [Fact]
    public void Plan_TheEarliestChainStopsAtZero_AndTheOthersStopWithIt()
    {
        var plan = TimelineMultiMove.Plan(new[] { At(0, 0.25), At(0, 2) }, requestedTime: -1, requestedTracks: 0, trackCount: 1);

        plan.TimeDelta.ShouldBe(-0.25, 1e-12);
    }

    [Fact]
    public void Plan_ALaneMoveThatWouldLeaveTheTimelineIsClamped_NotDropped()
    {
        // The selection spans lanes 1 and 2 of 4: moving down by 3 can only go down by 1, and up by 3 only by 1.
        var origins = new[] { At(1, 0), At(2, 1) };

        TimelineMultiMove.Plan(origins, 0, requestedTracks: 3, trackCount: 4).TrackDelta.ShouldBe(1);
        TimelineMultiMove.Plan(origins, 0, requestedTracks: -3, trackCount: 4).TrackDelta.ShouldBe(-1);
    }

    [Fact]
    public void Plan_ASingleChain_BehavesLikeTheOldSingleChainDrag()
    {
        var plan = TimelineMultiMove.Plan(new[] { At(0, 1) }, requestedTime: 0.3, requestedTracks: 2, trackCount: 3);

        plan.ShouldBe(new TimelineMultiMove.Result(0.3, 2));
    }

    [Fact]
    public void Plan_OnlyOneLane_CannotMoveToAnotherLane()
    {
        TimelineMultiMove.Plan(new[] { At(0, 1) }, 0, requestedTracks: 1, trackCount: 1).TrackDelta.ShouldBe(0);
    }

    [Fact]
    public void Others_WithSeveralDraggedChains_OffersNoEdgeOfAnyOfThem_AndNoPlayedEndCutByAnyOfThem()
    {
        var a = new SequenceChainReferenceSave { Source = "s", Chain = "Walk", Start = 0 };
        var b = new SequenceChainReferenceSave { Source = "s", Chain = "Walk", Start = 3 };
        var early = new SequenceChainReferenceSave { Source = "s", Chain = "Run", Start = 2, Loops = 2 };   // asks for 2..3, cut at 3 by b
        var far = new SequenceChainReferenceSave { Source = "s", Chain = "Jump", Start = 9 };
        var sequence = new SequenceSave();
        sequence.Tracks.Add(new SequenceTrackSave { Chains = { a } });
        sequence.Tracks.Add(new SequenceTrackSave { Chains = { early, b, far } });
        var blocks = TimelineLayout.Blocks(sequence, _ => 0.5);

        var targets = TimelineSnapTargets.Others(blocks, new[] { a, b }, playhead: 7);

        // early's start and full end, far's start and end; no edge of a or b, and no played end at b's start.
        targets.Where(t => t.Kind == SnapKind.ChainEdge).Select(t => t.Time).OrderBy(t => t).ToList()
            .ShouldBe(new[] { 2.0, 3.0, 9.0, 9.5 });
    }
}
