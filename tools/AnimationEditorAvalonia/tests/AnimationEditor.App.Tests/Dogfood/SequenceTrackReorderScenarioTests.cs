using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Dragging a track header up or down reorders the lanes: the bottom lane is in front, one undo step per drop, a line
/// marks the gap, Escape cancels, and the moved track stays selected with the Inspector on it.
/// </summary>
public class SequenceTrackReorderScenarioTests
{
    // Four tracks, back to front, each on its own chain whose frames are offset 10, 20, 30, 40, so the preview's draw order
    // can be read off the frame offsets. The second chain has frame events, so lane 1 ("Second") is taller than the others.
    private static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        var chains = new List<AnimationChainSave>();
        for (int i = 0; i < 4; i++)
        {
            var chain = AnimationEditorHarness.Chain("C" + (i + 1), "sheet.png", (0, 16 * i, 16, 16), (16, 16 * i, 16, 16));
            foreach (var frame in chain.Frames) frame.RelativeX = 10 * (i + 1);
            if (i == 1) chain.Frames[0].Events.Add(new FlatRedBall2.Animation.AnimationFrameEvent { Name = "Start" });
            chains.Add(chain);
        }
        editor.WriteAchx("hero.achx", chains.ToArray());
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        string[] names = { "Top", "Second", "Third", "Bottom" };
        for (int i = 0; i < names.Length; i++)
        {
            var track = new SequenceTrackSave { Name = names[i] };
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "C" + (i + 1) });
            sequence.Tracks.Add(track);
        }
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static List<string> SavedNames(string path) =>
        SequenceListSave.FromFile(path).Sequences[0].Tracks.Select(track => track.Name).ToList();

    // The preview's sprites back to front, as the frame offset of each one's chain.
    private static List<float> DrawnBackToFront(AnimationEditorHarness editor) =>
        editor.Control<PreviewControl>("SequencePreview").Composite!.Select(sprite => sprite.Frame.RelativeX).ToList();

    private static Point Header(AnimationEditorHarness editor, int lane) =>
        editor.PointIn(Timeline(editor), 20, Timeline(editor).Viewport.LaneTop(lane) + 10);

    // A point in the upper half of `lane`: the gap above it.
    private static Point GapAbove(AnimationEditorHarness editor, int lane) =>
        editor.PointIn(Timeline(editor), 20, Timeline(editor).Viewport.LaneTop(lane) + 4);

    private static Point GapBelowLast(AnimationEditorHarness editor)
    {
        var timeline = Timeline(editor);
        int last = timeline.Blocks.Max(b => b.Track);
        return editor.PointIn(timeline, 20, timeline.Viewport.LaneTop(last) + timeline.Viewport.LaneHeightOf(last) - 4);
    }

    [AvaloniaFact]
    public async Task DraggingTheTopHeaderToTheBottom_MovesItToTheBack_AsOneUndoStep_AndKeepsItSelected()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        DrawnBackToFront(editor).ShouldBe(new[] { 40f, 30f, 20f, 10f });

        editor.Drag(Header(editor, 0), GapBelowLast(editor));

        SavedNames(boss).ShouldBe(new[] { "Second", "Third", "Bottom", "Top" });
        File.ReadAllText(boss).ShouldNotContain("\"z\"");
        editor.UndoLabels.ShouldBe(new[] { "Move Track 'Top'" });
        DrawnBackToFront(editor).ShouldBe(new[] { 10f, 40f, 30f, 20f });
        Timeline(editor).SelectedTrack.ShouldBe(3);
        var inspector = editor.Control<SequenceInspectorControl>("SequenceInspector");
        inspector.TrackPanel.IsVisible.ShouldBeTrue();
        inspector.TrackNameBox.Text.ShouldBe("Top");
        Timeline(editor).DraggedTrack.ShouldBeNull();
        Timeline(editor).TrackDropSlot.ShouldBeNull();
        Timeline(editor).ShownCursor.ShouldBe(TimelineCursor.Default);
    }

    [AvaloniaFact]
    public async Task UndoAndRedo_PutTheOrderBack_AndTheSelectionFollowsTheTrack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        editor.Drag(Header(editor, 0), GapBelowLast(editor));

        editor.Press(Key.Z, RawInputModifiers.Control);

        SavedNames(boss).ShouldBe(new[] { "Top", "Second", "Third", "Bottom" });
        DrawnBackToFront(editor).ShouldBe(new[] { 40f, 30f, 20f, 10f });
        Timeline(editor).SelectedTrack.ShouldBe(0);
        editor.Control<SequenceInspectorControl>("SequenceInspector").TrackNameBox.Text.ShouldBe("Top");

        editor.Press(Key.Y, RawInputModifiers.Control);

        SavedNames(boss).ShouldBe(new[] { "Second", "Third", "Bottom", "Top" });
        Timeline(editor).SelectedTrack.ShouldBe(3);
    }

    [AvaloniaFact]
    public async Task WhileDragging_TheGapLineSitsBetweenLanesOfDifferentHeights_TheHeaderIsMarked_AndTheCursorStays()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        timeline.Viewport.LaneHeightOf(1).ShouldBeGreaterThan(timeline.Viewport.LaneHeightOf(0));

        editor.DragHold(Header(editor, 3), GapAbove(editor, 2));

        timeline.DraggedTrack.ShouldBe(3);
        timeline.TrackDropSlot.ShouldBe(2);
        timeline.TrackDropLineY.ShouldBe(timeline.Viewport.LaneTop(2));
        timeline.ShownCursor.ShouldBe(TimelineCursor.Reorder);

        // Lower half of the tall lane 1 points at the gap below it, which is where lane 2 begins.
        editor.DragMove(editor.PointIn(timeline, 20, timeline.Viewport.LaneTop(1) + timeline.Viewport.LaneHeightOf(1) - 3));
        timeline.TrackDropSlot.ShouldBe(2);
        timeline.TrackDropLineY.ShouldBe(timeline.Viewport.LaneTop(2));
        timeline.ShownCursor.ShouldBe(TimelineCursor.Reorder);

        editor.DragMove(GapAbove(editor, 0));
        timeline.TrackDropSlot.ShouldBe(0);

        // Beside its own lane the drop would change nothing, so no line.
        editor.DragMove(GapAbove(editor, 3));
        timeline.TrackDropSlot.ShouldBeNull();
        editor.DragRelease(GapAbove(editor, 3));
        editor.UndoLabels.ShouldBeEmpty();
        SavedNames(boss).ShouldBe(new[] { "Top", "Second", "Third", "Bottom" });
    }

    [AvaloniaFact]
    public async Task Escape_DuringTheDrag_CancelsWithNoUndoEntry()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        editor.DragHold(Header(editor, 0), GapBelowLast(editor));
        Timeline(editor).DraggedTrack.ShouldBe(0);
        editor.Press(Key.Escape);
        editor.DragRelease(GapBelowLast(editor));

        Timeline(editor).DraggedTrack.ShouldBeNull();
        Timeline(editor).TrackDropSlot.ShouldBeNull();
        SavedNames(boss).ShouldBe(new[] { "Top", "Second", "Third", "Bottom" });
        editor.UndoLabels.ShouldBeEmpty();
        Timeline(editor).SelectedTrack.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task APressThatBarelyMoves_IsAClick_NotADrag()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        Point press = Header(editor, 1);

        editor.Window.MouseDown(press, MouseButton.Left, RawInputModifiers.None);
        editor.Window.MouseMove(new Point(press.X, press.Y + 2), RawInputModifiers.LeftMouseButton);
        Timeline(editor).DraggedTrack.ShouldBeNull();
        editor.Window.MouseUp(new Point(press.X, press.Y + 2), MouseButton.Left, RawInputModifiers.None);
        editor.Layout();

        Timeline(editor).SelectedTrack.ShouldBe(1);
        editor.UndoLabels.ShouldBeEmpty();
        SavedNames(boss).ShouldBe(new[] { "Top", "Second", "Third", "Bottom" });
    }

    [AvaloniaFact]
    public async Task DraggingAHeaderUpOverATallLane_LandsInTheGapTheLineShowed()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        // Over the tall lane's event row (its lower half): the gap below "Second", so "Bottom" lands between "Second" and "Third".
        var timeline = Timeline(editor);
        editor.Drag(Header(editor, 3), editor.PointIn(timeline, 20, timeline.Viewport.LaneTop(1) + timeline.Viewport.LaneHeightOf(1) - 3));

        SavedNames(boss).ShouldBe(new[] { "Top", "Second", "Bottom", "Third" });
        editor.UndoLabels.ShouldBe(new[] { "Move Track 'Bottom'" });
        timeline.SelectedTrack.ShouldBe(2);
        DrawnBackToFront(editor).ShouldBe(new[] { 30f, 40f, 20f, 10f });
    }

    [AvaloniaFact]
    public async Task DoubleClickingAHeader_StillRenamesIt_AndRightClickStillOpensItsMenu()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        editor.DoubleClickAt(Header(editor, 2));

        Timeline(editor).IsRenamingTrack.ShouldBeTrue();
        editor.UndoLabels.ShouldBeEmpty();
        editor.Press(Key.Escape);

        editor.RightClickAt(Header(editor, 1));
        Timeline(editor).Menu.Items.OfType<Avalonia.Controls.MenuItem>().Count().ShouldBe(11);
        Timeline(editor).SelectedTrack.ShouldBe(1);
    }
}
