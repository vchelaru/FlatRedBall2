using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A track whose chains carry frame events gets a thin event row under its filmstrip, so its lane is taller than a track
/// without events. The flags repeat each loop, mirror on a ping-pong, stop at a cut, merge when close, and say what they
/// are on hover. The row is read-only: pressing it selects the chain like any other part of the block.
/// </summary>
public class SequenceEventRowScenarioTests
{
    private const double BaseLane = TimelineViewport.LaneHeight;
    private const double TallLane = TimelineViewport.LaneHeight + TimelineLayout.EventRowHeight;

    private static AnimationFrameSave Event(AnimationFrameSave frame, params string[] names)
    {
        foreach (string name in names) frame.Events.Add(new AnimationFrameEvent { Name = name });
        return frame;
    }

    // Walk: five 0.1 s frames, "Start" on the first and "Step" on the third. Run: no events. Chomp: a ping-pong of three
    // 0.1 s frames with "Bite" on the middle one. Once: Walk with Loop off. Rapid: Hit and Spark 0.02 s apart, then Sfx half a second later.
    private static string WriteFixture(AnimationEditorHarness editor, params SequenceTrackSave[] tracks)
    {
        editor.WritePng("sheet.png", 64, 64);
        var walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16));
        Event(walk.Frames[0], "Start");
        Event(walk.Frames[2], "Step");
        var run = AnimationEditorHarness.Chain("Run", "sheet.png", (0, 32, 16, 16), (16, 32, 16, 16));
        var chomp = AnimationEditorHarness.Chain("Chomp", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16));
        chomp.PingPong = true;
        Event(chomp.Frames[1], "Bite");
        var rapid = AnimationEditorHarness.Chain("Rapid", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16));
        rapid.Frames[0].FrameLength = 0.02f;
        rapid.Frames[1].FrameLength = 0.02f;
        rapid.Frames[2].FrameLength = 0.5f;
        rapid.Frames[3].FrameLength = 0.5f;
        Event(rapid.Frames[1], "Hit");
        Event(rapid.Frames[2], "Spark");
        Event(rapid.Frames[3], "Sfx");
        var once = AnimationEditorHarness.Chain("Once", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16));
        once.Loop = false;
        Event(once.Frames[0], "Start");
        Event(once.Frames[2], "Step");
        editor.WriteAchx("hero.achx", walk, run, chomp, rapid, once);

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

    private static TimelineBlock BlockOf(SequenceTimelineControl timeline, string chain) =>
        timeline.Blocks.Single(b => b.Reference.Chain == chain);

    // The window point on a lane's event row, `dx` pixels right of the block's `marker`-th flag.
    private static Point OnFlag(AnimationEditorHarness editor, string chain, int marker, double dx = 3)
    {
        var timeline = Timeline(editor);
        var block = BlockOf(timeline, chain);
        double x = timeline.Viewport.XAt(block.Start) + timeline.EventMarkersOf(block)[marker].X + dx;
        return editor.PointIn(timeline, x, timeline.Viewport.LaneTop(block.Track) + TimelineLayout.EventRowTop + TimelineLayout.EventRowHeight / 2);
    }

    private static Point Body(AnimationEditorHarness editor, int lane, double time)
    {
        var timeline = Timeline(editor);
        return editor.PointIn(timeline, timeline.Viewport.XAt(time), timeline.Viewport.LaneTop(lane) + (TimelineLayout.BodyTop + TimelineLayout.BodyBottom) / 2);
    }

    private static SequenceTrackSave SavedTrack(string path, int track) => SequenceListSave.FromFile(path).Sequences[0].Tracks[track];

    [AvaloniaFact]
    public async Task OnlyTracksWithEvents_GetTheEventRow_SoLanesDifferInHeight()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Plain", ("Run", 0, 1)), Track("Body", ("Walk", 0, 1)), Track("Plain2", ("Run", 0, 1))));
        var viewport = Timeline(editor).Viewport;

        new[] { viewport.LaneHeightOf(0), viewport.LaneHeightOf(1), viewport.LaneHeightOf(2) }.ShouldBe(new[] { BaseLane, TallLane, BaseLane });
        viewport.LaneTop(2).ShouldBe(viewport.LaneTop(0) + BaseLane + TallLane);
    }

    [AvaloniaFact]
    public async Task AMissingChain_OrAChainWithNoEvents_AddsNoRow()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Gone", ("Missing", 0, 1)), Track("Run", ("Run", 0, 2))));
        var timeline = Timeline(editor);

        timeline.Viewport.LaneHeightOf(0).ShouldBe(BaseLane);
        timeline.Viewport.LaneHeightOf(1).ShouldBe(BaseLane);
        timeline.EventMarkersOf(BlockOf(timeline, "Missing")).ShouldBeEmpty();
        timeline.EventMarkersOf(BlockOf(timeline, "Run")).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task Flags_SitAtTheirFramesStart_AndRepeatEachLoop()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Body", ("Walk", 0.5, 2))));
        var timeline = Timeline(editor);
        double pps = timeline.Viewport.PixelsPerSecond;

        var markers = timeline.EventMarkersOf(timeline.Blocks.Single());

        // Walk's cycle is 0.5 s: Start at 0 and Step at 0.2 s, both again in the second cycle.
        markers.Select(m => m.X).ShouldBe(new[] { 0, 0.2 * pps, 0.5 * pps, 0.7 * pps }, 1e-3);
        markers.Select(m => m.Occurrences[0].Name).ShouldBe(new[] { "Start", "Step", "Start", "Step" });
    }

    [AvaloniaFact]
    public async Task ANonLoopingChain_FlagsOnlyItsFirstCycle_NotTheHeldLastFrame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Body", ("Once", 0.5, 2))));
        var timeline = Timeline(editor);
        double pps = timeline.Viewport.PixelsPerSecond;

        var markers = timeline.EventMarkersOf(timeline.Blocks.Single());

        // Two cycles long, but it plays once and holds: Start at 0 and Step at 0.2 s, nothing in the hold.
        markers.Select(m => m.X).ShouldBe(new[] { 0, 0.2 * pps }, 1e-3);
    }

    [AvaloniaFact]
    public async Task APingPong_MirrorsItsFlagOnTheReturnHalf()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Pet", ("Chomp", 0, 1.5f))));
        var timeline = Timeline(editor);
        double pps = timeline.Viewport.PixelsPerSecond;

        var markers = timeline.EventMarkersOf(timeline.Blocks.Single());

        // Forward 0.1 s, back 0.4 s of a 0.6 s cycle; the next cycle's forward Bite lands at 0.7 s, its return is past the 0.9 s end.
        markers.Select(m => m.X).ShouldBe(new[] { 0.1 * pps, 0.4 * pps, 0.7 * pps }, 1e-3);
        markers.Select(m => m.Occurrences[0].IsReturn).ShouldBe(new[] { false, true, false });
    }

    [AvaloniaFact]
    public async Task ACutChain_DrawsNoFlagsInItsCutTail()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        // Walk x3 would run to 1.5 s; Run at 0.35 s cuts it, so only the flags before 0.35 s ever fire.
        await editor.OpenAsync(WriteFixture(editor, Track("Body", ("Walk", 0, 3), ("Run", 0.35, 1))));
        var timeline = Timeline(editor);

        var markers = timeline.EventMarkersOf(BlockOf(timeline, "Walk"));

        markers.Select(m => m.Occurrences[0].Name).ShouldBe(new[] { "Start", "Step" });
        timeline.Viewport.LaneHeightOf(0).ShouldBe(TallLane);
    }

    [AvaloniaFact]
    public async Task CloseFlags_MergeIntoACount_AndZoomingInSeparatesThem()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Combo", ("Rapid", 0, 1))));
        var timeline = Timeline(editor);
        var block = timeline.Blocks.Single();

        timeline.EventMarkersOf(block).Select(m => m.Count).ShouldBe(new[] { 2, 1 });
        timeline.SetZoomPercent(800);
        timeline.EventMarkersOf(block).Select(m => m.Count).ShouldBe(new[] { 1, 1, 1 });
    }

    [AvaloniaFact]
    public async Task HoveringAFlag_ShowsItsNameFrameAndTime_InTheSameCardAsTheFilmstrip()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Body", ("Walk", 1, 1))));
        var timeline = Timeline(editor);

        editor.Hover(OnFlag(editor, "Walk", 1));

        timeline.HoverCard!.Title.ShouldBe("Walk");
        timeline.HoverCard.Frame.ShouldBe(2);
        timeline.HoverCard.Lines.ShouldBe(new[] { "Step", "frame 3 / 5", "at 1.20 s" });
    }

    [AvaloniaFact]
    public async Task HoveringAMergedFlag_ListsEveryEventItHolds()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Combo", ("Rapid", 0, 1))));
        var timeline = Timeline(editor);

        editor.Hover(OnFlag(editor, "Rapid", 0));

        timeline.HoverCard!.Lines.ShouldBe(new[] { "2 events", "Hit: frame 2, 0.02 s", "Spark: frame 3, 0.04 s" });
    }

    [AvaloniaFact]
    public async Task HoveringTheEventRowBetweenFlags_ShowsNoCard()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Body", ("Walk", 0, 1))));
        var timeline = Timeline(editor);

        editor.Hover(OnFlag(editor, "Walk", 0));
        timeline.HoverCard.ShouldNotBeNull();
        editor.Hover(OnFlag(editor, "Walk", 0, dx: 15));

        timeline.HoverCard.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task PressingAFlag_OnlySelectsTheChain_AndLeavesPlayheadAndUndoAlone()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Plain", ("Run", 0, 1)), Track("Body", ("Walk", 0, 1)));
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        timeline.Playhead = 0.3;

        editor.ClickAt(OnFlag(editor, "Walk", 1));

        timeline.Selection.ShouldBe(new TimelineChainId(1, 0));
        timeline.Playhead.ShouldBe(0.3);
        editor.UndoLabels.ShouldBeEmpty();
        SavedTrack(boss, 1).Chains.Single().Start.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task TheEventRowUnderACutTail_IsNotTheTailsResizeEdge()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, 3), ("Run", 0.35, 1)));
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        var walk = BlockOf(timeline, "Walk");
        double y = timeline.Viewport.LaneTop(0) + TimelineLayout.EventRowTop + 6;

        // Far inside the cut-tail span and at its far end: neither grabs Walk, whose tail the strip above still owns.
        editor.ClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(walk.FullEnd), y));

        timeline.Selection.ShouldBeNull();
        editor.UndoLabels.ShouldBeEmpty();
        SavedTrack(boss, 0).Chains.First(c => c.Chain == "Walk").Loops.ShouldBe(3);
    }

    [AvaloniaFact]
    public async Task OnATallThenNormalMix_ClicksSelectTheRightChainInEveryLane()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("A", ("Walk", 0, 1)), Track("B", ("Run", 0, 1)), Track("C", ("Chomp", 0, 1)), Track("D", ("Run", 0, 1))));
        var timeline = Timeline(editor);

        foreach (int lane in new[] { 3, 2, 1, 0 })
        {
            editor.ClickAt(Body(editor, lane, 0.15));
            timeline.Selection.ShouldBe(new TimelineChainId(lane, 0));
        }
        // The new-track row sits above the first lane, whatever the lane heights below it.
        timeline.Viewport.RowAt(timeline.Viewport.LaneTop(TimelineViewport.NewTrackLane) + 4, 4).ShouldBe(new TimelineRow(TimelineRowKind.NewTrack));
        timeline.Viewport.RowAt(timeline.Viewport.LaneTop(4) + 4, 4).ShouldBe(new TimelineRow(TimelineRowKind.None));
    }

    [AvaloniaFact]
    public async Task DroppingAChipAfterATallLane_LandsOnTheTrackUnderThePointer()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("A", ("Walk", 0, 1)), Track("B"));
        await editor.OpenAsync(boss);
        var chip = editor.Control<SequenceLibraryControl>("SequenceLibrary").ChipFor("hero.achx", "Run")!;

        editor.Drag(editor.CenterOf(chip), Body(editor, 1, 1.0));

        SavedTrack(boss, 1).Chains.Select(c => (c.Chain, c.Start)).ShouldBe(new[] { ("Run", 1.0) });
        SavedTrack(boss, 0).Chains.Select(c => c.Chain).ShouldBe(new[] { "Walk" });
    }

    [AvaloniaFact]
    public async Task MovingAChainFromANormalLaneToATallOne_AndDraggingAnEdgeBelowIt_StillWork()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("A", ("Walk", 0, 1)), Track("B", ("Run", 0, 1)), Track("C", ("Run", 2, 1)));
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);

        // Lane 1 is a normal lane under a tall one: move its chain up into the tall lane (after Walk ends at 0.5 s).
        editor.Drag(Body(editor, 1, 0.1), Body(editor, 0, 1.0));
        SavedTrack(boss, 0).Chains.Select(c => c.Chain).ShouldBe(new[] { "Walk", "Run" });
        SavedTrack(boss, 1).Chains.ShouldBeEmpty();

        // Lane 2 sits below the tall lane 0: drag Run's right edge (0.2 s long, at 2 s) out to 2.6 s for 3 loops.
        var run = timeline.Blocks.Single(b => b.Track == 2);
        double edgeX = timeline.Viewport.XAt(run.PlayedEnd);
        double y = timeline.Viewport.LaneTop(2) + (TimelineLayout.BodyTop + TimelineLayout.BodyBottom) / 2;
        editor.Drag(editor.PointIn(timeline, edgeX - 1, y), editor.PointIn(timeline, timeline.Viewport.XAt(2.6), y));
        SavedTrack(boss, 2).Chains.Single().Loops.ShouldBe(3, 0.51f);
        editor.UndoLabels.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task VerticalScrolling_CoversTheTallLanes_AndEndsWithTheLastLaneInView()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var tracks = Enumerable.Range(0, 14).Select(i => Track($"T{i}", (i % 2 == 0 ? "Walk" : "Run", 0, 1))).ToArray();
        await editor.OpenAsync(WriteFixture(editor, tracks));
        var timeline = Timeline(editor);
        var viewport = timeline.Viewport;

        for (int i = 0; i < 40; i++) editor.Wheel(editor.PointIn(timeline, 300, 100), -1);

        // Seven tall and seven normal lanes under the new-track row, ruler on top.
        double content = TimelineViewport.RulerHeight + 7 * TallLane + 7 * BaseLane + BaseLane;
        viewport.ContentHeight(14).ShouldBe(content);
        viewport.ScrollY.ShouldBe(content - timeline.Bounds.Height, 1e-3);
        viewport.LaneTop(14).ShouldBe(timeline.Bounds.Height, 1e-3);
    }
}
