using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>A sequence dragged from the list lands where it is dropped: on a gap between lanes, below the last, or on the new-track row. A reference track's header moves like any other.</summary>
public class SequenceReferenceDropScenarioTests
{
    // Idle: Body, Mid. Earth: Ring, Dust. Action: Hit.
    private static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 32, 16, 16), (16, 32, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        save.Sequences.Add(Sequence("Idle", Track("Body", "Walk"), Track("Mid", "Walk")));
        save.Sequences.Add(Sequence("Earth", Track("Ring", "Run"), Track("Dust", "Walk")));
        save.Sequences.Add(Sequence("Action", Track("Hit", "Run")));
        save.Save(path);
        return path;
    }

    private static SequenceSave Sequence(string name, params SequenceTrackSave[] tracks)
    {
        var sequence = new SequenceSave { Name = name };
        sequence.Tracks.AddRange(tracks);
        return sequence;
    }

    private static SequenceTrackSave Track(string name, string chain) =>
        new() { Name = name, Chains = { new SequenceChainReferenceSave { Source = "hero.achx", Chain = chain } } };

    private static Control Row(AnimationEditorHarness editor, int row) =>
        (Control)editor.Control<ListBox>("SequenceList").ContainerFromIndex(row)!;

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) => editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static List<string> Names(string path, string sequence) =>
        SequenceListSave.FromFile(path).Sequences.First(s => s.Name == sequence).Tracks.Select(t => t.Name).ToList();

    private static Point At(AnimationEditorHarness editor, double y) => editor.PointIn(Timeline(editor), Timeline(editor).Viewport.XAt(1.0), y);

    [AvaloniaFact]
    public async Task DroppingASequenceOnAGapBetweenLanes_InsertsTheReferenceThere_WithTheInsertionLine_AndUndoes()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        double lane1 = timeline.Viewport.LaneTop(1);

        // The upper half of Mid points at the gap between Body and Mid.
        editor.DragHold(editor.CenterOf(Row(editor, 1)), At(editor, lane1 + 6));
        timeline.IsSequenceDropTarget.ShouldBeTrue();
        timeline.SequenceDropSlot.ShouldBe(1);
        timeline.SequenceDropLineY.ShouldBe(lane1);
        editor.DragRelease(At(editor, lane1 + 6));

        Names(boss, "Idle").ShouldBe(new[] { "Body", "Earth", "Mid" });
        timeline.SelectedTrack.ShouldBe(1);
        editor.UndoLabels.ShouldBe(new[] { "Add Reference 'Earth'" });
        timeline.SequenceDropSlot.ShouldBeNull();
        editor.Press(Key.Z, RawInputModifiers.Control);
        Names(boss, "Idle").ShouldBe(new[] { "Body", "Mid" });
    }

    [AvaloniaFact]
    public async Task DroppingASequenceBelowTheLastLane_AddsItAtTheBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);

        editor.Drag(editor.CenterOf(Row(editor, 1)), At(editor, timeline.Viewport.LaneTop(2) + 20));

        Names(boss, "Idle").ShouldBe(new[] { "Body", "Mid", "Earth" });
        timeline.SelectedTrack.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task DroppingASequenceOnTheNewTrackRow_StillMakesTheFrontTrack_WithoutAGapLine()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        double newTrackRow = TimelineViewport.RulerHeight + 10;

        editor.DragHold(editor.CenterOf(Row(editor, 1)), At(editor, newTrackRow));
        timeline.IsSequenceDropTarget.ShouldBeTrue();
        timeline.SequenceDropSlot.ShouldBeNull();
        editor.DragRelease(At(editor, newTrackRow));

        Names(boss, "Idle").ShouldBe(new[] { "Earth", "Body", "Mid" });
        timeline.SelectedTrack.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task DroppingASequenceThatWouldCycleOnAGap_ShowsNoLineAndAddsNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        double y = timeline.Viewport.LaneTop(1) + 6;

        editor.DragHold(editor.CenterOf(Row(editor, 0)), At(editor, y)); // Idle onto Idle
        timeline.IsSequenceDropTarget.ShouldBeFalse();
        timeline.SequenceDropSlot.ShouldBeNull();
        editor.DragRelease(At(editor, y));

        Names(boss, "Idle").ShouldBe(new[] { "Body", "Mid" });
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DraggingAReferenceTrackHeader_MovesTheWholeTrack_WithTheInsertionLine_CollapsedAndExpanded(bool expanded)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        timeline.AddSequenceReference("Earth").ShouldBeNull();
        editor.Layout();
        if (expanded)
        {
            timeline.ToggleReferenceExpanded(0);
            editor.Layout();
            timeline.IsReferenceExpanded(0).ShouldBeTrue();
        }
        var viewport = timeline.Viewport;
        var from = editor.PointIn(timeline, 100, viewport.LaneTop(0) + TimelineLayout.HeaderCenterY);
        double belowLast = viewport.LaneTop(2) + viewport.LaneHeightOf(2) - 4; // lower half of the last lane: the gap under it

        editor.DragHold(from, editor.PointIn(timeline, 100, belowLast));
        timeline.DraggedTrack.ShouldBe(0);
        timeline.TrackDropSlot.ShouldBe(3);
        timeline.TrackDropLineY.ShouldBe(viewport.LaneTop(3));
        editor.DragRelease(editor.PointIn(timeline, 100, belowLast));

        Names(boss, "Idle").ShouldBe(new[] { "Body", "Mid", "Earth" });
        SequenceListSave.FromFile(boss).Sequences[0].Tracks[2].Reference!.Sequence.ShouldBe("Earth");
        timeline.SelectedTrack.ShouldBe(2);
        timeline.IsReferenceExpanded(2).ShouldBe(expanded, "the expansion follows the track");
        editor.UndoLabels.ShouldBe(new[] { "Add Reference 'Earth'", "Move Track 'Earth'" });
    }
}
