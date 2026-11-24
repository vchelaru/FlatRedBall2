using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Dropping a sequence from the list between existing tracks: the pure drop plan, and the reference track it inserts.</summary>
public class SequenceDropPlacementTests
{
    private const double X = 300;

    [Fact]
    public void ForSequence_AboveTheTopHalfOfALane_PointsAtTheGapAboveIt_AndTheLowerHalfAtTheGapBelow()
    {
        var viewport = new TimelineViewport();
        double lane1 = viewport.LaneTop(1);

        var above = TimelineDropPlan.ForSequence(viewport, 3, lane1 + 4);
        var below = TimelineDropPlan.ForSequence(viewport, 3, lane1 + TimelineViewport.LaneHeight - 4);

        above.Kind.ShouldBe(TimelineDropKind.Gap);
        above.Row.ShouldBe(1);
        below.Kind.ShouldBe(TimelineDropKind.Gap);
        below.Row.ShouldBe(2);
    }

    [Fact]
    public void ForSequence_OnTheNewTrackRow_IsTheFrontTrackNotAGap()
    {
        var plan = TimelineDropPlan.ForSequence(new TimelineViewport(), 3, TimelineViewport.RulerHeight + 4);

        plan.Kind.ShouldBe(TimelineDropKind.NewTrack);
        plan.Row.ShouldBe(TimelineViewport.NewTrackLane);
    }

    [Fact]
    public void ForSequence_BelowTheLastLane_IsTheBottomGap_AndTheRulerIsNoDrop()
    {
        var viewport = new TimelineViewport();
        double belowAll = viewport.LaneTop(3) + 40;

        var bottom = TimelineDropPlan.ForSequence(viewport, 3, belowAll);
        var ruler = TimelineDropPlan.ForSequence(viewport, 3, TimelineViewport.RulerHeight - 2);

        bottom.Kind.ShouldBe(TimelineDropKind.Gap);
        bottom.Row.ShouldBe(3);
        ruler.Kind.ShouldBe(TimelineDropKind.None);
    }

    [Fact]
    public void ForSequence_OverATallExpandedLane_SplitsAtThatLanesOwnMiddle()
    {
        var viewport = new TimelineViewport();
        viewport.SetLaneHeights(new[] { TimelineViewport.LaneHeight * 3, TimelineViewport.LaneHeight });
        double middle = viewport.LaneTop(0) + TimelineViewport.LaneHeight * 1.5;

        TimelineDropPlan.ForSequence(viewport, 2, middle - 2).Row.ShouldBe(0);
        TimelineDropPlan.ForSequence(viewport, 2, middle + 2).Row.ShouldBe(1);
    }

    // Idle: Body, Mid, Back (chains). Earth: Ring. Action: Hit.
    private static SequenceDocument Load(string folder)
    {
        var list = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.25f, RightCoordinate = 1, BottomCoordinate = 1 });
        list.AnimationChains.Add(chain);
        list.SaveJson(Path.Combine(folder, "body.achj"));
        var save = new SequenceListSave { Sources = { "body.achj" } };
        save.Sequences.Add(Sequence("Idle", "Body", "Mid", "Back"));
        save.Sequences.Add(Sequence("Earth", "Ring"));
        save.Sequences.Add(Sequence("Action", "Hit"));
        string path = Path.Combine(folder, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static SequenceSave Sequence(string name, params string[] tracks)
    {
        var sequence = new SequenceSave { Name = name };
        foreach (string track in tracks)
            sequence.Tracks.Add(new SequenceTrackSave { Name = track, Chains = { new SequenceChainReferenceSave { Source = "body.achj", Chain = "Walk" } } });
        return sequence;
    }

    [Theory]
    [InlineData(0, new[] { "Earth", "Body", "Mid", "Back" })]
    [InlineData(1, new[] { "Body", "Earth", "Mid", "Back" })]
    [InlineData(2, new[] { "Body", "Mid", "Earth", "Back" })]
    [InlineData(3, new[] { "Body", "Mid", "Back", "Earth" })]
    public void AddSequenceReference_AtAGap_InsertsTheNewTrackThere_AsOneUndoStep(int slot, string[] expected)
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir.Path);
        var undo = new UndoManager();

        document.AddSequenceReference(undo, 0, "Earth", 0.5, slot).ShouldBeNull();

        var tracks = SequenceListSave.FromFile(document.Path.FullPath).Sequences[0].Tracks;
        tracks.Select(t => t.Name).ShouldBe(expected);
        tracks[slot].References![0].Sequence.ShouldBe("Earth");
        document.TakeTrackSelection().ShouldBe(slot);
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Add Reference 'Earth'" });
        undo.Undo();
        document.Save.Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Body", "Mid", "Back" });
    }

    [Fact]
    public void AddSequenceReference_ThatWouldCycleOrIsMissing_ChangesNothingAtAnyGap()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir.Path);
        var undo = new UndoManager();
        document.AddSequenceReference(undo, 0, "Earth", 0, 3).ShouldBeNull();

        document.AddSequenceReference(undo, 1, "Idle", 0, 1).ShouldNotBeNull(); // Idle plays Earth now
        document.AddSequenceReference(undo, 0, "Idle", 0, 1).ShouldNotBeNull(); // itself
        document.AddSequenceReference(undo, 0, "Nope", 0, 1).ShouldNotBeNull();

        document.Save.Sequences[0].Tracks.Count.ShouldBe(4);
        document.Save.Sequences[1].Tracks.Count.ShouldBe(1);
        undo.UndoHistory.Count().ShouldBe(1);
    }
}
