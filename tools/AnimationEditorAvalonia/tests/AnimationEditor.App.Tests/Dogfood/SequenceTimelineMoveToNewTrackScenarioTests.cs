using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;
using static AnimationEditor.App.Tests.Dogfood.SequenceTimelineMultiSelectScenarioTests;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Dragging an existing chain (or a multi-selection) onto the dashed new-track row makes a track for it, with the same
/// highlight and time ghost as a library chip. One new track per source track, the old tracks stay, one undo entry.
/// Fixture (see <see cref="SequenceTimelineMultiSelectScenarioTests"/>): Body: Walk 0..0.5, Walk 1..1.5, Run 2..2.2.
/// Head: Run 0.5..0.7, Walk 1.5..2. Tail: empty. The new-track row is above lane 0, so a new track is the first lane.
/// </summary>
public class SequenceTimelineMoveToNewTrackScenarioTests
{
    private static SequenceTrackSave SavedTrack(string path, int track) => SequenceListSave.FromFile(path).Sequences[0].Tracks[track];

    private static string[] TrackNames(string path) => SequenceListSave.FromFile(path).Sequences[0].Tracks.Select(t => t.Name).ToArray();

    private static async Task<(AnimationEditorHarness Editor, string Path)> OpenAsync()
    {
        var editor = new AnimationEditorHarness();
        string path = WriteFixture(editor);
        await editor.OpenAsync(path);
        return (editor, path);
    }

    // A fixture with its own tracks, for the lane-height and scroll cases. Walk has an event, so its lane is taller.
    private static string WriteCustomFixture(AnimationEditorHarness editor, params SequenceTrackSave[] tracks)
    {
        editor.WritePng("sheet.png", 64, 64);
        var walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16));
        walk.Frames[0].Events.Add(new AnimationFrameEvent { Name = "Start" });
        editor.WriteAchx("hero.achx", walk,
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 32, 16, 16), (16, 32, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.AddRange(tracks);
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static Point Row(AnimationEditorHarness editor, int lane, double time, double dx = 0)
    {
        var timeline = Timeline(editor);
        return editor.PointIn(timeline, timeline.Viewport.XAt(time) + dx,timeline.Viewport.LaneTop(lane) + (TimelineLayout.BodyTop + TimelineLayout.BodyBottom) / 2);
    }

    [AvaloniaFact]
    public async Task DraggingAChainToTheNewTrackRow_MakesATrackForIt_AsOneUndoEntry_AndSelectsIt()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);

        // Alt skips snapping: 60 px at 120 px/s is half a second, so Run (at 0.5) lands at 1.0.
        editor.Drag(At(editor, 1, 0.6), At(editor, TimelineViewport.NewTrackLane, 0.6, dx: 60), RawInputModifiers.Alt);

        TrackNames(boss).ShouldBe(new[] { "Run", "Body", "Head", "Tail" });
        SavedTrack(boss, 2).Chains.Select(c => c.Chain).ShouldBe(new[] { "Walk" });
        SavedTrack(boss, 0).Chains.Select(c => (c.Chain, c.Start)).ShouldBe(new[] { ("Run", 1.0) });
        editor.UndoLabels.ShouldBe(new[] { "Move 'Run' to new track 'Run'" });
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 0) });
        timeline.IsDropTargetNewTrack.ShouldBeFalse();

        editor.Press(Key.Z, RawInputModifiers.Control);
        TrackNames(boss).ShouldBe(new[] { "Body", "Head", "Tail" });
        SavedTrack(boss, 1).Chains.Select(c => (c.Chain, c.Start)).ShouldBe(new[] { ("Run", 0.5), ("Walk", 1.5) });
        editor.Press(Key.Y, RawInputModifiers.Control);
        TrackNames(boss).ShouldBe(new[] { "Run", "Body", "Head", "Tail" });
        SavedTrack(boss, 0).Chains.Single().Start.ShouldBe(1.0);
    }

    [AvaloniaFact]
    public async Task OverTheNewTrackRow_TheDragShowsTheChipHighlightAndGhost_AndLeavingTheRowClearsThem()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);

        editor.DragHold(At(editor, 1, 0.6), At(editor, TimelineViewport.NewTrackLane, 0.6, dx: 60), RawInputModifiers.Alt);

        timeline.IsDropTargetNewTrack.ShouldBeTrue();
        var preview = timeline.DropPreview!.Value;
        preview.Kind.ShouldBe(TimelineDropKind.NewTrack);
        preview.Row.ShouldBe(TimelineViewport.NewTrackLane);
        preview.Start.ShouldBe(1.0, 1e-9);
        preview.TimeLabel.ShouldBe("1 s");
        preview.Length.ShouldBe(0.2, 1e-6);
        TrackNames(boss).ShouldBe(new[] { "Body", "Head", "Tail" }, "nothing is created until the drop");

        editor.DragMove(At(editor, 2, 0.6, dx: 60), RawInputModifiers.Alt);
        timeline.IsDropTargetNewTrack.ShouldBeFalse();
        timeline.DropPreview.ShouldBeNull();

        editor.DragMove(At(editor, TimelineViewport.NewTrackLane, 0.6, dx: 60), RawInputModifiers.Alt);
        timeline.IsDropTargetNewTrack.ShouldBeTrue();
        editor.DragRelease(At(editor, TimelineViewport.NewTrackLane, 0.6, dx: 60), RawInputModifiers.Alt);
        timeline.IsDropTargetNewTrack.ShouldBeFalse();
        TrackNames(boss).ShouldBe(new[] { "Run", "Body", "Head", "Tail" });
    }

    [AvaloniaFact]
    public async Task ReleasingOnALaneAfterVisitingTheNewTrackRow_IsAnOrdinaryMove()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;

        editor.DragHold(At(editor, 1, 0.6), At(editor, TimelineViewport.NewTrackLane, 0.6, dx: 60), RawInputModifiers.Alt);
        editor.DragMove(At(editor, 2, 0.6, dx: 60), RawInputModifiers.Alt);
        editor.DragRelease(At(editor, 2, 0.6, dx: 60), RawInputModifiers.Alt);

        TrackNames(boss).ShouldBe(new[] { "Body", "Head", "Tail" });
        SavedTrack(boss, 2).Chains.Select(c => (c.Chain, c.Start)).ShouldBe(new[] { ("Run", 1.0) });
        editor.UndoLabels.ShouldBe(new[] { "Move 'Run' to 'Tail'" });
    }

    [AvaloniaFact]
    public async Task EscapeOverTheNewTrackRow_PutsTheChainBack_WithNoNewTrackAndNoUndoEntry()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);

        editor.DragHold(At(editor, 1, 0.6), At(editor, TimelineViewport.NewTrackLane, 0.6, dx: 60), RawInputModifiers.Alt);
        timeline.IsDropTargetNewTrack.ShouldBeTrue();
        editor.Press(Key.Escape);
        editor.DragRelease(At(editor, TimelineViewport.NewTrackLane, 0.6, dx: 60));

        timeline.IsDropTargetNewTrack.ShouldBeFalse();
        timeline.DropPreview.ShouldBeNull();
        TrackNames(boss).ShouldBe(new[] { "Body", "Head", "Tail" });
        SavedTrack(boss, 1).Chains.Select(c => (c.Chain, c.Start)).ShouldBe(new[] { ("Run", 0.5), ("Walk", 1.5) });
        editor.UndoLabels.ShouldBeEmpty();
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(1, 0) });
    }

    [AvaloniaFact]
    public async Task OverTheNewTrackRow_BothEndsSnap_AndAltBypassesIt()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);

        // Run's start would be 0.983 s, 2 px short of the second Walk's start at 1.0.
        editor.DragHold(At(editor, 1, 0.6), At(editor, TimelineViewport.NewTrackLane, 0.6, dx: 58));

        var preview = timeline.DropPreview!.Value;
        preview.Start.ShouldBe(1.0, 1e-9);
        preview.Snapped.ShouldNotBeNull();
        timeline.SnapGuideTime!.Value.ShouldBe(1.0, 1e-9);
        editor.DragMove(At(editor, TimelineViewport.NewTrackLane, 0.6, dx: 58), RawInputModifiers.Alt);
        timeline.DropPreview!.Value.Start.ShouldBe(0.5 + 58 / 120.0, 1e-6);
        editor.DragMove(At(editor, TimelineViewport.NewTrackLane, 0.6, dx: 58));
        editor.DragRelease(At(editor, TimelineViewport.NewTrackLane, 0.6, dx: 58));

        SavedTrack(boss, 0).Chains.Single().Start.ShouldBe(1.0, 1e-9);
    }

    [AvaloniaFact]
    public async Task OverTheNewTrackRow_TheChainStopsAtZero()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);

        editor.DragHold(At(editor, 1, 0.6), At(editor, TimelineViewport.NewTrackLane, 0.6, dx: -300), RawInputModifiers.Alt);

        timeline.DropPreview!.Value.Start.ShouldBe(0.0, 1e-9);
        editor.DragRelease(At(editor, TimelineViewport.NewTrackLane, 0.6, dx: -300), RawInputModifiers.Alt);
        SavedTrack(boss, 0).Chains.Single().Start.ShouldBe(0.0, 1e-9);
    }

    [AvaloniaFact]
    public async Task ASelection_GetsOneNewTrackPerSourceTrack_InLaneOrder_KeepingItsShape()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 1, 1.75), RawInputModifiers.Control);

        editor.Drag(At(editor, 1, 1.75, dx: 5), At(editor, TimelineViewport.NewTrackLane, 1.75, dx: 65), RawInputModifiers.Alt);

        TrackNames(boss).ShouldBe(new[] { "Walk", "Walk 2", "Body", "Head", "Tail" });
        SavedTrack(boss, 2).Chains.Select(c => (c.Chain, c.Start)).ShouldBe(new[] { ("Walk", 1.0), ("Run", 2.0) });
        SavedTrack(boss, 3).Chains.Select(c => (c.Chain, c.Start)).ShouldBe(new[] { ("Run", 0.5) });
        SavedTrack(boss, 0).Chains.Select(c => (c.Chain, c.Start)).ShouldBe(new[] { ("Walk", 0.5) });
        SavedTrack(boss, 1).Chains.Select(c => (c.Chain, c.Start)).ShouldBe(new[] { ("Walk", 2.0) });
        editor.UndoLabels.ShouldBe(new[] { "Move 2 chains to 2 new tracks" });
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 0), new TimelineChainId(1, 0) });
        editor.Press(Key.Z, RawInputModifiers.Control);
        TrackNames(boss).ShouldBe(new[] { "Body", "Head", "Tail" });
        SavedTrack(boss, 0).Chains.Count.ShouldBe(3);
        SavedTrack(boss, 1).Chains.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task ASelectionInOneTrack_GetsOneNewTrackHoldingAllOfIt()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 0, 1.25), RawInputModifiers.Control);

        editor.Drag(At(editor, 0, 1.25, dx: 5), At(editor, TimelineViewport.NewTrackLane, 1.25, dx: 5), RawInputModifiers.Alt);

        TrackNames(boss).ShouldBe(new[] { "Walk", "Body", "Head", "Tail" });
        SavedTrack(boss, 0).Chains.Select(c => (c.Chain, c.Start)).ShouldBe(new[] { ("Walk", 0.0), ("Walk", 1.0) });
        SavedTrack(boss, 1).Chains.Select(c => c.Chain).ShouldBe(new[] { "Run" });
        editor.UndoLabels.ShouldBe(new[] { "Move 2 chains to new track 'Walk'" });
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 0), new TimelineChainId(0, 1) });
    }

    [AvaloniaFact]
    public async Task FromTheLastLane_TheChainLeavesItsTrackEmpty_AndTheTrackStays()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteCustomFixture(editor, Track("Only", ("Run", 0, 1)));
        await editor.OpenAsync(boss);

        editor.Drag(Row(editor, 0, 0.1), Row(editor, TimelineViewport.NewTrackLane, 0.1, dx: 60), RawInputModifiers.Alt);

        TrackNames(boss).ShouldBe(new[] { "Run", "Only" });
        SavedTrack(boss, 1).Chains.ShouldBeEmpty();
        SavedTrack(boss, 0).Chains.Single().Start.ShouldBe(0.5, 1e-9);
    }

    [AvaloniaFact]
    public async Task AMissingChain_MovesToo_WithItsOneSecondPlaceholderAsTheGhost()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteCustomFixture(editor, Track("Body", ("Gone", 0, 1)));
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);

        editor.DragHold(Row(editor, 0, 0.5), Row(editor, TimelineViewport.NewTrackLane, 0.5, dx: 60), RawInputModifiers.Alt);

        timeline.DropPreview!.Value.Length.ShouldBe(TimelineLayout.MissingLength, 1e-9);
        editor.DragRelease(Row(editor, TimelineViewport.NewTrackLane, 0.5, dx: 60), RawInputModifiers.Alt);
        TrackNames(boss).ShouldBe(new[] { "Gone", "Body" });
        SavedTrack(boss, 0).Chains.Select(c => (c.Chain, c.Start)).ShouldBe(new[] { ("Gone", 0.5) });
    }

    [AvaloniaFact]
    public async Task WithATallEventLane_TheNewTrackRowStaysAtTheTop_AndTheLanesStartBelowIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteCustomFixture(editor, Track("A", ("Walk", 0, 1)), Track("B", ("Run", 0, 1)));
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        timeline.Viewport.LaneHeightOf(0).ShouldBeGreaterThan(timeline.Viewport.LaneHeightOf(1));

        editor.DragHold(Row(editor, 1, 0.1), Row(editor, TimelineViewport.NewTrackLane, 0.1, dx: 60), RawInputModifiers.Alt);
        timeline.IsDropTargetNewTrack.ShouldBeTrue();
        editor.DragRelease(Row(editor, TimelineViewport.NewTrackLane, 0.1, dx: 60), RawInputModifiers.Alt);

        TrackNames(boss).ShouldBe(new[] { "Run", "A", "B" });
        SavedTrack(boss, 2).Chains.ShouldBeEmpty();
        SavedTrack(boss, 0).Chains.Single().Start.ShouldBe(0.5, 1e-9);
    }

    [AvaloniaFact]
    public async Task ScrolledAndZoomed_TheChainStillLandsWhereTheGhostSays()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var tracks = Enumerable.Range(0, 14).Select(i => Track($"T{i}", (i % 2 == 0 ? "Walk" : "Run", 0, 1))).ToArray();
        string boss = WriteCustomFixture(editor, tracks);
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        // Scrolled so the new-track row is only partly in view.
        timeline.Viewport.SetScrollY(10, 14, timeline.Bounds.Height);
        editor.Layout();
        timeline.Viewport.ScrollY.ShouldBeGreaterThan(0);
        timeline.Viewport.ZoomAround(TimelineViewport.HeaderWidth, 2);
        double pixelsPerSecond = timeline.Viewport.PixelsPerSecond;

        // The second lane, then straight up onto the row above the lanes, 0.5 s later.
        editor.DragHold(Row(editor, 1, 0.1), Row(editor, TimelineViewport.NewTrackLane, 0.1, dx: 0.5 * pixelsPerSecond), RawInputModifiers.Alt);
        timeline.IsDropTargetNewTrack.ShouldBeTrue();
        double ghostStart = timeline.DropPreview!.Value.Start;
        editor.DragRelease(Row(editor, TimelineViewport.NewTrackLane, 0.1, dx: 0.5 * pixelsPerSecond), RawInputModifiers.Alt);

        SequenceListSave.FromFile(boss).Sequences[0].Tracks.Count.ShouldBe(15);
        SavedTrack(boss, 2).Chains.ShouldBeEmpty();
        SavedTrack(boss, 0).Name.ShouldBe("Run");
        SavedTrack(boss, 0).Chains.Single().Start.ShouldBe(ghostStart, 1e-9);
        ghostStart.ShouldBe(0.5, 1e-6);
    }
}
