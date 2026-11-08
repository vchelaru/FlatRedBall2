using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;
using static AnimationEditor.App.Tests.Dogfood.SequenceTimelineMultiSelectScenarioTests;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The blank area under the last lane behaves like empty lane space: a plain click seeks to the time under the pointer and
/// clears the selection, a drag is a rubber band, Ctrl/Shift only edit the selection. The header column and the scrollbars
/// never scrub, and the new-track row keeps its own behavior.
/// Fixture: Body: Walk 0..0.5, Walk 1..1.5, Run 2..2.2. Head: Run 0.5..0.7, Walk 1.5..2. Tail: empty. The sequence is 2.2 s.
/// </summary>
public class SequenceTimelineBelowLanesScenarioTests
{
    // A window point below the last lane at `time`, 12 px under the lanes.
    private static Point Below(AnimationEditorHarness editor, double time, double gap = 12)
    {
        var timeline = Timeline(editor);
        double y = timeline.Viewport.LaneTop(timeline.TrackCount) + gap;
        timeline.Viewport.RowAt(y, timeline.TrackCount).Kind.ShouldBe(TimelineRowKind.None, "the point must be under the last lane");
        y.ShouldBeLessThan(timeline.Bounds.Height, "the pane must be taller than the lanes");
        return editor.PointIn(timeline, timeline.Viewport.XAt(time), y);
    }

    private static void SeekByRuler(AnimationEditorHarness editor, double time)
    {
        var timeline = Timeline(editor);
        editor.ClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(time), TimelineViewport.RulerHeight / 2));
        timeline.Playhead.ShouldBe(time, 1e-6);
    }

    private static async Task<AnimationEditorHarness> OpenAsync()
    {
        var editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        return editor;
    }

    [AvaloniaFact]
    public async Task ClickingBelowTheLastLane_PutsThePlayheadAtThatTime_AndClearsTheSelection()
    {
        using var editor = await OpenAsync();
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 0.25));
        timeline.SelectedChains.Count.ShouldBe(1);

        foreach (double time in new[] { 0.37, 1.0123, 1.8 })
        {
            editor.ClickAt(Below(editor, time));
            timeline.Playhead.ShouldBe(time, 1e-6);
        }

        timeline.SelectedChains.ShouldBeEmpty();
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task ClickingBelowTheLastLane_PastTheEnd_ClampsLikeAnEmptyLaneClick()
    {
        using var editor = await OpenAsync();

        editor.ClickAt(Below(editor, 5.0));

        Timeline(editor).Playhead.ShouldBe(2.2, 1e-6);
    }

    [AvaloniaFact]
    public async Task ClickingBelowTheLastLane_AtTheSameTimeAsAnEmptyLane_GivesTheSamePlayhead()
    {
        using var editor = await OpenAsync();
        var timeline = Timeline(editor);
        timeline.SetZoomPercent(300);
        timeline.Viewport.SetScrollSeconds(1.0);
        editor.Layout();

        editor.ClickAt(At(editor, 2, 1.6));
        double byLane = timeline.Playhead;
        editor.ClickAt(Below(editor, 1.6));

        byLane.ShouldBe(1.6, 1e-6);
        timeline.Playhead.ShouldBe(byLane, 1e-9);
    }

    [AvaloniaFact]
    public async Task DraggingFromBelowTheLastLane_IsARubberBand_LikeEmptyLaneSpace()
    {
        using var editor = await OpenAsync();
        var timeline = Timeline(editor);
        SeekByRuler(editor, 0.4);

        editor.DragHold(Below(editor, 1.2), At(editor, 0, 0.2));
        timeline.IsMarqueeActive.ShouldBeTrue();
        editor.DragRelease(At(editor, 0, 0.2));

        timeline.SelectedChains.Count.ShouldBeGreaterThan(2);
        timeline.Playhead.ShouldBe(0.4, 1e-6, "a band does not seek");
    }

    [AvaloniaFact]
    public async Task CtrlAndShiftClickBelowTheLastLane_DoNotSeek_AndCtrlKeepsTheSelection()
    {
        using var editor = await OpenAsync();
        var timeline = Timeline(editor);
        SeekByRuler(editor, 0.4);
        editor.ClickAt(At(editor, 0, 0.25));

        editor.ClickAt(Below(editor, 1.5), RawInputModifiers.Control);
        timeline.Playhead.ShouldBe(0.4, 1e-6);
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 0) });

        editor.ClickAt(Below(editor, 1.5), RawInputModifiers.Shift);
        timeline.Playhead.ShouldBe(0.4, 1e-6);
    }

    [AvaloniaFact]
    public async Task AltClickBelowTheLastLane_OnlySeeks()
    {
        using var editor = await OpenAsync();
        var timeline = Timeline(editor);

        editor.ClickAt(Below(editor, 1.1), RawInputModifiers.Alt);

        timeline.Playhead.ShouldBe(1.1, 1e-6);
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task ClickingBelowTheLastLane_InTheHeaderColumn_DoesNotScrub()
    {
        using var editor = await OpenAsync();
        var timeline = Timeline(editor);
        SeekByRuler(editor, 0.4);

        editor.ClickAt(editor.PointIn(timeline, 70, timeline.Viewport.LaneTop(timeline.TrackCount) + 12));

        timeline.Playhead.ShouldBe(0.4, 1e-6);
    }

    [AvaloniaFact]
    public async Task ClickingTheNewTrackRow_StillDoesNotSeek_WhileTheRowBelowItDoes()
    {
        using var editor = await OpenAsync();
        var timeline = Timeline(editor);
        SeekByRuler(editor, 0.4);

        editor.ClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(1.0), timeline.Viewport.LaneTop(TimelineViewport.NewTrackLane) + 20));

        timeline.Playhead.ShouldBe(0.4, 1e-6);
    }

    [AvaloniaFact]
    public async Task ASequenceWithNoTracks_ClickBelowTheNewTrackRow_Seeks_AndTheRowDoesNot()
    {
        using var editor = new AnimationEditorHarness();
        string path = Path.Combine(editor.ProjectFolder, "empty.seqj");
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "Idle" });
        save.Save(path);
        await editor.OpenAsync(path);
        var timeline = Timeline(editor);
        timeline.TrackCount.ShouldBe(0);
        var scrubs = new List<double>();
        timeline.Scrubbed += scrubs.Add;

        // The lane space under the dashed row; a sequence with no chains is 0 s long, so the time clamps to 0.
        editor.ClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(0.5), timeline.Viewport.LaneTop(0) + 12));
        scrubs.Count.ShouldBe(1, "the blank area seeks like empty lane space");
        scrubs[0].ShouldBe(0.0, 1e-9);

        scrubs.Clear();
        editor.ClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(0.5), timeline.Viewport.LaneTop(TimelineViewport.NewTrackLane) + 20));
        scrubs.Count.ShouldBe(0, "the dashed new-track row is a drop target and keeps its behavior");
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task WithManyLanesScrolledToTheEnd_TheBottomPixelsStillSeek_AndTheScrollbarsDoNot()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "many.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Big" };
        for (int i = 0; i < 14; i++) sequence.Tracks.Add(Track("T" + i, ("Walk", 0, 1)));
        sequence.Tracks.Add(Track("Last"));
        save.Sequences.Add(sequence);
        save.Save(path);
        await editor.OpenAsync(path);
        var timeline = Timeline(editor);
        timeline.Viewport.SetScrollY(10000, timeline.TrackCount, timeline.Bounds.Height);
        editor.Layout();
        timeline.Viewport.ScrollY.ShouldBeGreaterThan(0);

        editor.ClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(0.3), timeline.Bounds.Height - 3));
        timeline.Playhead.ShouldBe(0.3, 1e-6);

        editor.ClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(0.1), timeline.Bounds.Height - 3));
        editor.ClickAt(editor.CenterOf(editor.Control<Avalonia.Controls.Primitives.ScrollBar>("SequenceTimelineHScroll")));
        editor.ClickAt(editor.CenterOf(editor.Control<Avalonia.Controls.Primitives.ScrollBar>("SequenceTimelineVScroll")));
        timeline.Playhead.ShouldBe(0.1, 1e-6, "scrollbar presses never scrub");
    }
}
