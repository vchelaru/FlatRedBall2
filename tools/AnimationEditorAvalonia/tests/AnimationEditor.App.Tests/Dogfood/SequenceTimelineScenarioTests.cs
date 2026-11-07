using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A <c>.seqj</c> tab's timeline: chips dropped from the library, chains moved and resized, chains and tracks removed,
/// zoom and scroll. Every gesture is one undo entry.
/// </summary>
public class SequenceTimelineScenarioTests
{
    // Walk: five 0.1 s frames, a 0.5 s cycle. Run: two frames, 0.2 s.
    private static string WriteFixture(AnimationEditorHarness editor, params SequenceTrackSave[] tracks)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 32, 16, 16), (16, 32, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.AddRange(tracks);
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static SequenceTrackSave Track(string name, params (string Chain, double Start, float Loops)[] chains)
    {
        var track = new SequenceTrackSave { Name = name };
        foreach (var (chain, start, loops) in chains)
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = chain, Start = start, Loops = loops });
        return track;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    /// <summary>A window point on <paramref name="lane"/> at <paramref name="time"/>, in the chain-body band.</summary>
    private static Point At(AnimationEditorHarness editor, int lane, double time, double dx = 0)
    {
        var timeline = Timeline(editor);
        double y = timeline.Viewport.LaneTop(lane) + (TimelineLayout.BodyTop + TimelineLayout.BodyBottom) / 2;
        return editor.PointIn(timeline, timeline.Viewport.XAt(time) + dx, y);
    }

    private static SequenceTrackSave SavedTrack(string path, int track) =>
        SequenceListSave.FromFile(path).Sequences[0].Tracks[track];

    private static LibraryChipView Chip(AnimationEditorHarness editor, string chain) =>
        editor.Control<SequenceLibraryControl>("SequenceLibrary").ChipFor("hero.achx", chain)!;

    [AvaloniaFact]
    public async Task DraggingAChipOntoALane_AddsTheChainAtTheDropTime_AndCtrlZRemovesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body"));
        await editor.OpenAsync(boss);

        editor.Drag(editor.CenterOf(Chip(editor, "Walk")), At(editor, 0, 1.0));

        SavedTrack(boss, 0).Chains.Select(c => (c.Chain, c.Start)).ShouldBe(new[] { ("Walk", 1.0) });
        editor.UndoLabels.ShouldBe(new[] { "Add 'Walk' to 'Body'" });
        Timeline(editor).Selection.ShouldBe(new TimelineChainId(0, 0));
        editor.Press(Key.Z, RawInputModifiers.Control);
        SavedTrack(boss, 0).Chains.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task DraggingAChipOntoTheNewTrackRow_AddsATrackNamedAfterTheChain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        // With no tracks, row 0 is the new-track row.
        editor.Drag(editor.CenterOf(Chip(editor, "Run")), At(editor, 0, 0.5));

        var tracks = SequenceListSave.FromFile(boss).Sequences[0].Tracks;
        tracks.Select(t => t.Name).ShouldBe(new[] { "Run" });
        tracks[0].Chains.Single().Start.ShouldBe(0.5);
        editor.UndoLabels.ShouldBe(new[] { "Add Track 'Run'" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        SequenceListSave.FromFile(boss).Sequences[0].Tracks.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task DroppingAChipOutsideTheTimeline_AddsNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body"));
        await editor.OpenAsync(boss);
        var chip = Chip(editor, "Walk");

        editor.Drag(editor.CenterOf(chip), editor.PointIn(chip, chip.Bounds.Width / 2, chip.Bounds.Height + 30));

        SavedTrack(boss, 0).Chains.ShouldBeEmpty();
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task DraggingAChipOverALane_HighlightsThatTrack_AndLeavingClearsIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body"), Track("Head"));
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        var chip = Chip(editor, "Walk");

        editor.DragHold(editor.CenterOf(chip), At(editor, 1, 3.0));

        timeline.DropTargetTrack.ShouldBe(1);
        timeline.DropPreview!.Value.Kind.ShouldBe(TimelineDropKind.Track);
        editor.DragMove(At(editor, 0, 3.0));
        timeline.DropTargetTrack.ShouldBe(0);
        editor.DragMove(editor.PointIn(chip, chip.Bounds.Width / 2, chip.Bounds.Height + 30));
        timeline.DropTargetTrack.ShouldBeNull();
        timeline.DropPreview.ShouldBeNull();
        editor.DragRelease(editor.PointIn(chip, chip.Bounds.Width / 2, chip.Bounds.Height + 30));
        SavedTrack(boss, 0).Chains.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task DraggingAChipOverTheNewTrackRow_PreviewsTheDropTime_AndTheDropLandsThere()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body"));
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        var target = At(editor, 1, 0.87);

        editor.DragHold(editor.CenterOf(Chip(editor, "Run")), target);

        timeline.DropTargetTrack.ShouldBeNull();
        timeline.IsDropTargetNewTrack.ShouldBeTrue();
        var preview = timeline.DropPreview!.Value;
        preview.Kind.ShouldBe(TimelineDropKind.NewTrack);
        preview.Start.ShouldBe(0.85, 1e-9);
        preview.TimeLabel.ShouldBe("0.85 s");
        preview.Length.ShouldBe(0.2, 1e-6);
        editor.DragRelease(target);

        SequenceListSave.FromFile(boss).Sequences[0].Tracks[1].Chains.Single().Start.ShouldBe(preview.Start, 1e-9);
        timeline.IsDropTargetNewTrack.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task DraggingAChain_MovesItInTimeAsOneUndoStep_AndCtrlZPutsItBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 1)));
        await editor.OpenAsync(boss);

        editor.Drag(At(editor, 0, 0.25), At(editor, 0, 1.25));

        SavedTrack(boss, 0).Chains.Single().Start.ShouldBe(1.0, 1e-9);
        editor.UndoLabels.ShouldBe(new[] { "Move 'Walk'" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        SavedTrack(boss, 0).Chains.Single().Start.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task DraggingAChainToAnotherLane_MovesItToThatTrack_AndCtrlZPutsItBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 1)), Track("Arms"));
        await editor.OpenAsync(boss);

        editor.Drag(At(editor, 0, 0.25), At(editor, 1, 0.25));

        SavedTrack(boss, 0).Chains.ShouldBeEmpty();
        SavedTrack(boss, 1).Chains.Single().Chain.ShouldBe("Walk");
        editor.UndoLabels.ShouldBe(new[] { "Move 'Walk' to 'Arms'" });
        Timeline(editor).Selection.ShouldBe(new TimelineChainId(1, 0));
        editor.Press(Key.Z, RawInputModifiers.Control);
        SavedTrack(boss, 0).Chains.Single().Chain.ShouldBe("Walk");
        SavedTrack(boss, 1).Chains.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task DraggingTheRightEdge_SnapsToCyclesAndTheNextChain_AndCanOverlapIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        // Walk's 0.5 s cycle at 0; Run starts at 1.25.
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 1), ("Run", 1.25, 1)));
        await editor.OpenAsync(boss);
        float WalkLoops() => SavedTrack(boss, 0).Chains[0].Loops;

        // A few pixels past two cycles: the magnet pulls it to exactly 2.
        editor.Drag(At(editor, 0, 0.5, dx: -2), At(editor, 0, 1.0, dx: 1));
        WalkLoops().ShouldBe(2f);
        // A few pixels short of Run's start: pulled to it, 2.5 loops.
        editor.Drag(At(editor, 0, 1.0, dx: -2), At(editor, 0, 1.25, dx: -6));
        WalkLoops().ShouldBe(2.5f, 1e-5f);
        // On past Run: allowed; the part under Run is the cut tail.
        editor.Drag(At(editor, 0, 1.25, dx: -3), At(editor, 0, 1.9, dx: -3));
        WalkLoops().ShouldBe(3.8f, 1e-4f);

        var walk = Timeline(editor).Blocks.Single(b => b.Reference.Chain == "Walk");
        walk.IsCut.ShouldBeTrue();
        walk.Played.ShouldBe(1.25, 1e-6);
        editor.UndoLabels.ShouldBe(new[] { "Set Loops of 'Walk' to 2", "Set Loops of 'Walk' to 2.5", "Set Loops of 'Walk' to 3.8" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        WalkLoops().ShouldBe(2.5f, 1e-5f);
    }

    [AvaloniaFact]
    public async Task Escape_DuringADrag_PutsTheChainBackWithNoUndoEntry()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 1)));
        await editor.OpenAsync(boss);
        Point from = At(editor, 0, 0.25);
        Point to = At(editor, 0, 1.25);

        editor.Window.MouseDown(from, MouseButton.Left, RawInputModifiers.None);
        editor.Window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        editor.Press(Key.Escape);
        editor.Window.MouseUp(to, MouseButton.Left, RawInputModifiers.None);
        editor.Layout();

        Timeline(editor).Blocks.Single().Start.ShouldBe(0);
        SavedTrack(boss, 0).Chains.Single().Start.ShouldBe(0);
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task DeleteKey_RemovesTheSelectedChain_NotTheHiddenAchxSelection()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 1), ("Run", 1, 1)));
        await editor.OpenAsync(Path.Combine(editor.ProjectFolder, "hero.achx"));
        editor.ClickRow(editor.ChainNamed("Run"));
        await editor.OpenAsync(boss);

        editor.ClickAt(At(editor, 0, 1.1));
        editor.Press(Key.Delete);

        SavedTrack(boss, 0).Chains.Select(c => c.Chain).ShouldBe(new[] { "Walk" });
        editor.UndoLabels.ShouldBe(new[] { "Remove 'Run' from 'Body'" });
        Timeline(editor).Selection.ShouldBeNull();
        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk", "Run" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        SavedTrack(boss, 0).Chains.Select(c => c.Chain).ShouldBe(new[] { "Walk", "Run" });
    }

    [AvaloniaFact]
    public async Task TrackHeaderMenu_RenamesAndDeletesATrack_EachUndoable()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 1)), Track("Arms"));
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        Point armsHeader = editor.PointIn(timeline, 20, timeline.Viewport.LaneTop(1) + 10);
        Point bodyHeader = editor.PointIn(timeline, 20, timeline.Viewport.LaneTop(0) + 10);

        editor.RightClickAt(armsHeader);
        editor.Dialogs.AnswerNextPrompt("Legs");
        editor.PickMenuItem(timeline.Menu, "Rename Track…");
        editor.Layout();
        editor.RightClickAt(bodyHeader);
        editor.PickMenuItem(timeline.Menu, "Delete Track");

        SequenceListSave.FromFile(boss).Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Legs" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Track 'Arms' to 'Legs'", "Delete Track 'Body'" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        SequenceListSave.FromFile(boss).Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Body", "Legs" });
    }

    [AvaloniaFact]
    public async Task AMissingChain_IsFlaggedAndDrawnOneSecondLong()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Gone", 0.5, 3)));
        await editor.OpenAsync(boss);

        var block = Timeline(editor).Blocks.Single();

        block.IsMissing.ShouldBeTrue();
        block.Full.ShouldBe(TimelineLayout.MissingLength);
        SavedTrack(boss, 0).Chains.Single().Loops.ShouldBe(3f);
    }

    [AvaloniaFact]
    public async Task CtrlWheel_ZoomsAroundThePointer_AndShiftWheelScrollsSideways()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 1)));
        await editor.OpenAsync(boss);
        var viewport = Timeline(editor).Viewport;
        double x = TimelineViewport.HeaderWidth + 300;
        double timeUnderPointer = viewport.TimeAt(x);
        Point point = editor.PointIn(Timeline(editor), x, viewport.LaneTop(0) + 10);

        editor.Wheel(point, 2, RawInputModifiers.Control);

        viewport.PixelsPerSecond.ShouldBeGreaterThan(TimelineViewport.DefaultPixelsPerSecond);
        viewport.TimeAt(x).ShouldBe(timeUnderPointer, 1e-6);
        editor.Wheel(point, -3, RawInputModifiers.Shift);
        viewport.ScrollSeconds.ShouldBeGreaterThan(0);
        editor.UndoLabels.ShouldBeEmpty();
    }

    private static double EdgeX(SequenceTimelineControl timeline, double time) => timeline.Viewport.XAt(time);

    private static Point Hover(AnimationEditorHarness editor, double x, double y)
    {
        var point = editor.PointIn(Timeline(editor), x, y);
        editor.Window.MouseMove(point, RawInputModifiers.None);
        return point;
    }

    [AvaloniaTheory]
    [InlineData(40)]
    [InlineData(120)]
    [InlineData(600)]
    public async Task HoveringTheRightEdgeGrabZone_ShowsResize_AndOneStepInsideTheBodyShowsOpenHand(double pixelsPerSecond)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 1)));
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        timeline.Viewport.SetPixelsPerSecond(pixelsPerSecond);
        editor.Layout();
        double bodyY = timeline.Viewport.LaneTop(0) + (TimelineLayout.BodyTop + TimelineLayout.BodyBottom) / 2;
        double right = EdgeX(timeline, 0.5);
        double width = right - EdgeX(timeline, 0);
        double grab = Math.Min(TimelineLayout.EdgeGrabPixels, width / 3);

        Hover(editor, right, bodyY);
        timeline.ShownCursor.ShouldBe(TimelineCursor.ResizeHorizontal);
        Hover(editor, right + TimelineLayout.EdgeGrabPixels, bodyY);
        timeline.ShownCursor.ShouldBe(TimelineCursor.ResizeHorizontal);
        Hover(editor, right - grab + 0.5, bodyY);
        timeline.ShownCursor.ShouldBe(TimelineCursor.ResizeHorizontal);

        Hover(editor, right - grab - 1, bodyY);
        timeline.ShownCursor.ShouldBe(TimelineCursor.Grab);
        Hover(editor, EdgeX(timeline, 0) + 1, bodyY);
        timeline.ShownCursor.ShouldBe(TimelineCursor.Grab);
        Hover(editor, right + TimelineLayout.EdgeGrabPixels + 1, bodyY);
        timeline.ShownCursor.ShouldBe(TimelineCursor.Default);
    }

    [AvaloniaFact]
    public async Task HoveringACutTail_ShowsResizeAtItsEdge_AndOpenHandOnItsBody()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        // Walk loops 3 times (1.5 s) but Run starts at 1.0 s and cuts it.
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 3), ("Run", 1.0, 1)));
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        double tailY = timeline.Viewport.LaneTop(0) + TimelineLayout.TailTop + 2;

        Hover(editor, EdgeX(timeline, 1.25), tailY);
        timeline.ShownCursor.ShouldBe(TimelineCursor.Grab);
        Hover(editor, EdgeX(timeline, 1.5), tailY);
        timeline.ShownCursor.ShouldBe(TimelineCursor.ResizeHorizontal);
    }

    [AvaloniaFact]
    public async Task DraggingTheEdge_KeepsResizeOutsideTheBlockAndTheTimeline_AndReleaseRestoresIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 1)));
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);

        Point edge = At(editor, 0, 0.5);
        Hover(editor, EdgeX(timeline, 0.5), timeline.Viewport.LaneTop(0) + 10);
        editor.Window.MouseDown(edge, MouseButton.Left, RawInputModifiers.None);
        editor.Window.MouseMove(At(editor, 0, 1.0), RawInputModifiers.LeftMouseButton);
        timeline.ShownCursor.ShouldBe(TimelineCursor.ResizeHorizontal);
        // Above the timeline entirely, and back over the chain's own body.
        editor.Window.MouseMove(new Point(edge.X + 40, edge.Y - 200), RawInputModifiers.LeftMouseButton);
        timeline.ShownCursor.ShouldBe(TimelineCursor.ResizeHorizontal);
        editor.Window.MouseMove(At(editor, 0, 0.1), RawInputModifiers.LeftMouseButton);
        timeline.ShownCursor.ShouldBe(TimelineCursor.ResizeHorizontal);

        Point away = At(editor, 0, 3.0);
        editor.Window.MouseUp(away, MouseButton.Left, RawInputModifiers.None);
        editor.Layout();
        timeline.ShownCursor.ShouldBe(TimelineCursor.Default);
    }

    [AvaloniaFact]
    public async Task MovingAChain_ShowsTheClosedHand_EvenPassingOverAnEdge_AndEscapeEndsTheEdgeCursor()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 1)), Track("Other", ("Run", 3, 1)));
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);

        editor.Window.MouseMove(At(editor, 0, 0.25), RawInputModifiers.None);
        timeline.ShownCursor.ShouldBe(TimelineCursor.Grab);
        editor.Window.MouseDown(At(editor, 0, 0.25), MouseButton.Left, RawInputModifiers.None);
        timeline.ShownCursor.ShouldBe(TimelineCursor.Grabbing);
        editor.Window.MouseMove(At(editor, 0, 1.0), RawInputModifiers.LeftMouseButton);
        // Over Run's right edge (it ends at 3.2 s).
        editor.Window.MouseMove(At(editor, 1, 3.2), RawInputModifiers.LeftMouseButton);
        timeline.ShownCursor.ShouldBe(TimelineCursor.Grabbing);
        editor.Window.MouseMove(editor.PointIn(timeline, 5, 5), RawInputModifiers.LeftMouseButton);
        timeline.ShownCursor.ShouldBe(TimelineCursor.Grabbing);
        editor.Press(Key.Escape);
        editor.Window.MouseUp(At(editor, 0, 0.25), MouseButton.Left, RawInputModifiers.None);

        editor.Window.MouseDown(At(editor, 0, 0.5), MouseButton.Left, RawInputModifiers.None);
        editor.Window.MouseMove(At(editor, 0, 0.9), RawInputModifiers.LeftMouseButton);
        timeline.ShownCursor.ShouldBe(TimelineCursor.ResizeHorizontal);
        editor.Press(Key.Escape);
        timeline.ShownCursor.ShouldBe(TimelineCursor.Default);
        editor.Window.MouseUp(At(editor, 0, 0.9), MouseButton.Left, RawInputModifiers.None);
    }
}
