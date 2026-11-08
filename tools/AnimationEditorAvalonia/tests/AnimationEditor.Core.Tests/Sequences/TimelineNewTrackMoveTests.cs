using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Moving dragged chains onto the new-track row: where they end up, and what the plan for that row says.</summary>
public class TimelineNewTrackMoveTests
{
    private static SequenceChainReferenceSave Ref(string chain, double start) => new() { Source = "hero.achj", Chain = chain, Start = start };

    private static SequenceSave Sequence(params (string Name, SequenceChainReferenceSave[] Chains)[] tracks)
    {
        var sequence = new SequenceSave();
        foreach (var (name, chains) in tracks)
        {
            var track = new SequenceTrackSave { Name = name };
            track.Chains.AddRange(chains);
            sequence.Tracks.Add(track);
        }
        return sequence;
    }

    [Fact]
    public void Apply_OneChain_LeavesItsTrackEmptyAndMakesAFrontTrackNamedAfterTheChain()
    {
        var walk = Ref("Walk", 1.5);
        var sequence = Sequence(("Body", new[] { walk, Ref("Run", 3) }));

        var created = TimelineNewTrackMove.Apply(sequence, new[] { walk });

        created.ShouldBe(new[] { 0 });
        sequence.Tracks.Select(t => t.Name).ShouldBe(new[] { "Walk", "Body" });
        sequence.Tracks[1].Chains.Select(c => c.Chain).ShouldBe(new[] { "Run" });
        sequence.Tracks[0].Chains.ShouldBe(new[] { walk });
        sequence.Tracks[0].Chains[0].Start.ShouldBe(1.5);
    }

    [Fact]
    public void Apply_TheSourceTrackStaysEvenWhenItsLastChainLeaves()
    {
        var walk = Ref("Walk", 0);
        var sequence = Sequence(("Body", new[] { walk }));

        TimelineNewTrackMove.Apply(sequence, new[] { walk });

        sequence.Tracks[1].Name.ShouldBe("Body");
        sequence.Tracks[1].Chains.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_ChainsFromTwoTracks_MakeOneNewTrackPerSourceTrack_InLaneOrder_KeepingTheirOrder()
    {
        var a = Ref("Walk", 0);
        var b = Ref("Run", 1);
        var c = Ref("Jump", 2);
        var sequence = Sequence(("Body", new[] { a, b }), ("Head", new[] { c }));

        // Handed over in reverse; the result follows the lanes, not the order given.
        var created = TimelineNewTrackMove.Apply(sequence, new[] { c, b, a });

        created.ShouldBe(new[] { 0, 1 });
        sequence.Tracks.Select(t => t.Name).ShouldBe(new[] { "Walk", "Jump", "Body", "Head" });
        sequence.Tracks[0].Chains.ShouldBe(new[] { a, b });
        sequence.Tracks[1].Chains.ShouldBe(new[] { c });
    }

    [Fact]
    public void Apply_ANameThatIsTaken_GetsANumber()
    {
        var walk = Ref("Walk", 0);
        var sequence = Sequence(("Walk", new[] { walk }));

        TimelineNewTrackMove.Apply(sequence, new[] { walk });

        sequence.Tracks.Select(t => t.Name).ShouldBe(new[] { "Walk 2", "Walk" });
    }

    [Fact]
    public void Apply_AChainWithNoName_MakesATrackCalledTrack()
    {
        var missing = Ref("", 0);
        var sequence = Sequence(("Body", new[] { missing }));

        TimelineNewTrackMove.Apply(sequence, new[] { missing });

        sequence.Tracks[0].Name.ShouldBe("Track");
    }

    [Fact]
    public void ForMove_OverTheNewTrackRowAtTheTop_TargetsANewTrackAtTheGivenStart()
    {
        var viewport = new TimelineViewport();
        double y = TimelineViewport.RulerHeight + TimelineViewport.LaneHeight / 2;

        var plan = TimelineDropPlan.ForMove(viewport, trackCount: 2, y, start: 0.85, length: 0.5);

        plan.Kind.ShouldBe(TimelineDropKind.NewTrack);
        plan.Row.ShouldBe(-1);
        plan.Start.ShouldBe(0.85);
        plan.Length.ShouldBe(0.5);
        plan.Left.ShouldBe(viewport.XAt(0.85), 1e-9);
        plan.Width.ShouldBe(0.5 * viewport.PixelsPerSecond, 1e-9);
    }

    [Fact]
    public void ForMove_OverALaneOrBelowTheLastLane_IsNone()
    {
        var viewport = new TimelineViewport();

        TimelineDropPlan.ForMove(viewport, 2, viewport.LaneTop(1) + 4, 1, 0.5).Kind.ShouldBe(TimelineDropKind.None);
        TimelineDropPlan.ForMove(viewport, 2, viewport.LaneTop(3) + 4, 1, 0.5).Kind.ShouldBe(TimelineDropKind.None);
    }

    [Fact]
    public void ForMove_WithTallEventLanes_TheNewTrackRowStaysAtTheTop_AndALaneStartsBelowIt()
    {
        var viewport = new TimelineViewport();
        viewport.SetLaneHeights(new[] { TimelineViewport.LaneHeight + 12, TimelineViewport.LaneHeight });

        TimelineDropPlan.ForMove(viewport, 2, TimelineViewport.RulerHeight + 2, 1, 0.5).Kind.ShouldBe(TimelineDropKind.NewTrack);
        // The tall lane 0's event row is part of lane 0, not a new-track target.
        TimelineDropPlan.ForMove(viewport, 2, viewport.LaneTop(0) + TimelineViewport.LaneHeight + 4, 1, 0.5).Kind.ShouldBe(TimelineDropKind.None);
    }

    [Fact]
    public void ForMove_ScrolledDownPastTheNewTrackRow_NoLongerTargetsIt()
    {
        var viewport = new TimelineViewport();
        viewport.Pan(0, -10000, trackCount: 10, viewHeight: 150);
        viewport.ScrollY.ShouldBeGreaterThan(TimelineViewport.LaneHeight);

        TimelineDropPlan.ForMove(viewport, 10, TimelineViewport.RulerHeight + 4, 1, 0.5).Kind.ShouldBe(TimelineDropKind.None);
    }

    [Fact]
    public void ForMove_ScrolledPartWayUp_StillFindsTheNewTrackRowUnderThePointer()
    {
        var viewport = new TimelineViewport();
        viewport.ScrollVertically(10, trackCount: 10, viewHeight: 150);

        TimelineDropPlan.ForMove(viewport, 10, TimelineViewport.RulerHeight + 4, 1, 0.5).Kind.ShouldBe(TimelineDropKind.NewTrack);
    }
}
