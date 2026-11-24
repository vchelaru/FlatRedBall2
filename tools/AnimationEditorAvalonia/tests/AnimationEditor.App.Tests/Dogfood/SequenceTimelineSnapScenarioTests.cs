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
/// Snapping on the <c>.seqj</c> timeline: a chain's right edge, a chain being moved and a chip being dropped all snap to
/// other chains' edges, the playhead and time 0, by a pixel distance, so the same gesture works at any zoom. Alt skips it.
/// </summary>
public class SequenceTimelineSnapScenarioTests
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

    private static SequenceTrackSave Track(string name, params (string Chain, double Start, double? Length)[] chains)
    {
        var track = new SequenceTrackSave { Name = name };
        foreach (var (chain, start, length) in chains)
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = chain, Start = start, Length = length });
        return track;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static Point At(AnimationEditorHarness editor, int lane, double time, double dx = 0)
    {
        var timeline = Timeline(editor);
        double y = timeline.Viewport.LaneTop(lane) + (TimelineLayout.BodyTop + TimelineLayout.BodyBottom) / 2;
        return editor.PointIn(timeline, timeline.Viewport.XAt(time) + dx, y);
    }

    private static SequenceChainReferenceSave Saved(string path, int track, int chain) =>
        SequenceListSave.FromFile(path).Sequences[0].Tracks[track].Chains[chain];

    private static void Zoom(AnimationEditorHarness editor, double pixelsPerSecond, double scrollSeconds)
    {
        var viewport = Timeline(editor).Viewport;
        viewport.SetPixelsPerSecond(pixelsPerSecond);
        viewport.SetScrollSeconds(scrollSeconds);
        editor.Layout();
    }

    [AvaloniaTheory]
    [InlineData(120, 0)]
    [InlineData(2000, 0.4)]
    public async Task DraggingTheRightEdge_NearAnotherChainsEnd_LandsExactlyOnIt_AtAnyZoom(double pixelsPerSecond, double scroll)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        // Run plays 0.55 to 0.75 on the other track; Walk's edge starts at 0.5.
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, null)), Track("Arms", ("Run", 0.55, null)));
        await editor.OpenAsync(boss);
        Zoom(editor, pixelsPerSecond, scroll);
        var timeline = Timeline(editor);

        // The pointer ends 3 px past Run's end.
        editor.DragHold(At(editor, 0, 0.5, dx: -2), At(editor, 0, 0.75, dx: 1));

        timeline.SnapGuideTime!.Value.ShouldBe(0.75, 1e-6);
        timeline.SnapGuide!.Value.Kind.ShouldBe(SnapKind.ChainEdge);
        timeline.SnapGuide!.Value.Track.ShouldBe(1);
        editor.DragRelease(At(editor, 0, 0.75, dx: 1));

        Saved(boss, 0, 0).Length.ShouldBe(0.75);
        timeline.SnapGuideTime.ShouldBeNull();
        editor.UndoLabels.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task DraggingAReferenceClip_NearAnotherClipsEnd_LandsExactlyOnIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, null)));
        var save = SequenceListSave.FromFile(boss);
        var combo = new SequenceSave { Name = "Combo" };
        combo.Tracks.Add(new SequenceTrackSave { Name = "A", Reference = new SequenceReferenceSave { Sequence = "Attack", Start = 0 } });
        combo.Tracks.Add(new SequenceTrackSave { Name = "B", Reference = new SequenceReferenceSave { Sequence = "Attack", Start = 1 } });
        save.Sequences.Insert(0, combo);
        save.Save(boss);
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);

        // Clip A plays 0 to 0.5. Grab B inside its body and drop its start 3 px past A's end.
        editor.DragHold(At(editor, 1, 1.1), At(editor, 1, 0.6, dx: 3));

        timeline.SnapGuideTime!.Value.ShouldBe(0.5, 1e-6);
        editor.DragRelease(At(editor, 1, 0.6, dx: 3));
        SequenceListSave.FromFile(boss).Sequences[0].Tracks[1].Reference!.Start.ShouldBe(0.5, 1e-6);
    }

    // Combo: a reference clip of Attack (0.5 s) on track 0 at 3.0, and a Walk block (0.5 s) on track 1 at walkStart.
    private static async Task<string> OpenClipBesideChain(AnimationEditorHarness editor, double walkStart)
    {
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, null)));
        var save = SequenceListSave.FromFile(boss);
        var combo = new SequenceSave { Name = "Combo" };
        combo.Tracks.Add(new SequenceTrackSave { Name = "Clip", Reference = new SequenceReferenceSave { Sequence = "Attack", Start = 3.0 } });
        combo.Tracks.Add(Track("Chain", ("Walk", walkStart, null)));
        save.Sequences.Insert(0, combo);
        save.Save(boss);
        await editor.OpenAsync(boss);
        return boss;
    }

    private static SequenceReferenceSave SavedClip(string path) => SequenceListSave.FromFile(path).Sequences[0].Tracks[0].Reference!;

    [AvaloniaFact]
    public async Task DraggingAReferenceClip_NearAChainBlocksStart_LandsItsEndExactlyOnIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        // Walk plays 1.13 to 1.63. The clip is 0.5 s, so its end on 1.13 puts its start at 0.63.
        string boss = await OpenClipBesideChain(editor, 1.13);
        var timeline = Timeline(editor);

        editor.DragHold(At(editor, 0, 3.2), At(editor, 0, 0.83, dx: 1));

        timeline.SnapGuideTime!.Value.ShouldBe(1.13, 1e-6);
        timeline.SnapGuide!.Value.Track.ShouldBe(1);
        editor.DragRelease(At(editor, 0, 0.83, dx: 1));
        SavedClip(boss).Start.ShouldBe(0.63, 1e-6);
    }

    [AvaloniaFact]
    public async Task DraggingAReferenceClip_NearAChainBlocksEnd_LandsItsStartExactlyOnIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        // Walk plays 1.13 to 1.63.
        string boss = await OpenClipBesideChain(editor, 1.13);
        var timeline = Timeline(editor);

        editor.DragHold(At(editor, 0, 3.2), At(editor, 0, 1.83, dx: 1));

        timeline.SnapGuideTime!.Value.ShouldBe(1.63, 1e-6);
        timeline.SnapGuide!.Value.Track.ShouldBe(1);
        editor.DragRelease(At(editor, 0, 1.83, dx: 1));
        SavedClip(boss).Start.ShouldBe(1.63, 1e-6);
    }

    [AvaloniaTheory]
    [InlineData(3.13, 3.63, 0.63)] // Walk plays 3.13 to 3.63: the clip's trim edge lands on Walk's end
    [InlineData(3.23, 3.23, 0.23)] // Walk plays 3.23 to 3.73: the trim edge lands on Walk's start
    public async Task TrimmingAReferenceClip_NearAChainBlockEdge_LandsExactlyOnIt(double walkStart, double target, double expectedLength)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = await OpenClipBesideChain(editor, walkStart);
        var timeline = Timeline(editor);

        // The clip plays 3.0 to 3.5; grab its right edge and drop it 1 px past the target.
        editor.DragHold(At(editor, 0, 3.5, dx: -2), At(editor, 0, target, dx: 1));

        timeline.SnapGuideTime!.Value.ShouldBe(target, 1e-6);
        editor.DragRelease(At(editor, 0, target, dx: 1));
        SavedClip(boss).Length!.Value.ShouldBe(expectedLength, 1e-6);
    }

    [AvaloniaFact]
    public async Task DraggingTheRightEdge_AtVeryHighZoom_IsFreeBetweenTargets()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, null)), Track("Arms", ("Run", 0.55, null)));
        await editor.OpenAsync(boss);
        Zoom(editor, 2000, 0.4);

        // 20 px (0.01 s) past Run's end, and no 0.05 s grid at this zoom.
        editor.Drag(At(editor, 0, 0.5, dx: -2), At(editor, 0, 0.75, dx: 18));

        Timeline(editor).SnapGuideTime.ShouldBeNull();
        Saved(boss, 0, 0).Length!.Value.ShouldBe(0.76, 1e-4);
    }

    [AvaloniaFact]
    public async Task DraggingTheRightEdge_WithAltHeld_DoesNotSnap()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, null)), Track("Arms", ("Run", 0.55, null)));
        await editor.OpenAsync(boss);

        editor.DragHold(At(editor, 0, 0.5, dx: -2), At(editor, 0, 0.75, dx: 1), RawInputModifiers.Alt);

        Timeline(editor).SnapGuideTime.ShouldBeNull();
        editor.DragRelease(At(editor, 0, 0.75, dx: 1), RawInputModifiers.Alt);
        // 3 px past 0.75 at 120 px/s is 0.775 s.
        Saved(boss, 0, 0).Length!.Value.ShouldBe(0.775, 5e-4);
    }

    [AvaloniaFact]
    public async Task Escape_DuringASnappedEdgeDrag_ClearsTheGuide_AndPutsTheLengthBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, null)), Track("Arms", ("Run", 0.55, null)));
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        editor.DragHold(At(editor, 0, 0.5, dx: -2), At(editor, 0, 0.75, dx: 1));
        timeline.SnapGuideTime.ShouldNotBeNull();

        editor.Press(Key.Escape);
        editor.DragRelease(At(editor, 0, 0.75, dx: 1));

        timeline.SnapGuideTime.ShouldBeNull();
        Saved(boss, 0, 0).Length.ShouldBeNull();
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task DraggingTheRightEdge_AwayFromEveryTarget_ShowsNoGuide()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, null)), Track("Arms", ("Run", 0.55, null)));
        await editor.OpenAsync(boss);

        editor.DragHold(At(editor, 0, 0.5, dx: -2), At(editor, 0, 0.75, dx: 40));

        Timeline(editor).SnapGuideTime.ShouldBeNull();
        editor.DragRelease(At(editor, 0, 0.75, dx: 40));
    }

    [AvaloniaFact]
    public async Task MovingAChain_ItsEndSnapsToAnotherChainsStart_AsOneUndoStep()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        // Walk (0.5 s) at 0; Run starts at 2.0 on another track. Walk is dropped with its end 3 px past 2.0.
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, null)), Track("Arms", ("Run", 2.0, null)));
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);

        editor.DragHold(At(editor, 0, 0.25), At(editor, 0, 1.75, dx: 3));

        timeline.SnapGuideTime!.Value.ShouldBe(2.0, 1e-6);
        editor.DragRelease(At(editor, 0, 1.75, dx: 3));
        Saved(boss, 0, 0).Start.ShouldBe(1.5, 1e-6);
        timeline.SnapGuideTime.ShouldBeNull();
        editor.UndoLabels.ShouldBe(new[] { "Move 'Walk'" });
    }

    [AvaloniaTheory]
    [InlineData(120, 0)]
    [InlineData(1000, 0.9)]
    public async Task MovingAChain_ItsStartSnapsToAnotherChainsEnd_AtAnyZoom(double pixelsPerSecond, double scroll)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        // Run plays 1.3 to 1.5 on the Arms track.
        string boss = WriteFixture(editor, Track("Body", ("Walk", 1.2, null)), Track("Arms", ("Run", 1.3, null)));
        await editor.OpenAsync(boss);
        Zoom(editor, pixelsPerSecond, scroll);

        // Walk's body at 1.4 is dragged by 0.3 s + 3 px: its start would be 1.5 + 3 px.
        editor.Drag(At(editor, 0, 1.4), At(editor, 0, 1.7, dx: 3));

        Saved(boss, 0, 0).Start.ShouldBe(1.5, 1e-6);
    }

    [AvaloniaFact]
    public async Task MovingAChain_SnapsItsStartToTimeZeroAndThePlayhead()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        // Three seconds long, so the playhead can sit at 2.013 (Alt scrub: free).
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0.8, 3.0)));
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        editor.Drag(OnRuler(editor, 2.0), OnRuler(editor, 2.013), RawInputModifiers.Alt);
        timeline.Playhead.ShouldBe(2.013, 1e-6);

        editor.Drag(At(editor, 0, 1.0), At(editor, 0, 0.2, dx: 2));
        Saved(boss, 0, 0).Start.ShouldBe(0, 1e-9);

        // The pointer says 2.02; the playhead is at 2.013 and the grid alone would say 2.0.
        editor.Drag(At(editor, 0, 0.25), At(editor, 0, 2.245, dx: 3));
        Saved(boss, 0, 0).Start.ShouldBe(2.013, 1e-6);
    }

    [AvaloniaFact]
    public async Task MovingAChain_WithAltHeld_FollowsThePointerFreely()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, null)), Track("Arms", ("Run", 2.0, null)));
        await editor.OpenAsync(boss);

        editor.Drag(At(editor, 0, 0.25), At(editor, 0, 1.75, dx: 3), RawInputModifiers.Alt);

        // 3 px is 0.025 s at 120 px/s; no target and no grid.
        Saved(boss, 0, 0).Start.ShouldBe(1.5 + 3 / 120.0, 1e-6);
    }

    [AvaloniaFact]
    public async Task DraggingAChipOntoALane_SnapsItsStartToAnotherChainsEnd_AndShowsTheGuide()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        // Walk plays 0.13 to 0.63; the grid alone would put the chip at 0.65.
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0.13, null)), Track("Arms"));
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        var target = At(editor, 1, 0.63, dx: 3);

        editor.DragHold(editor.CenterOf(editor.Control<SequenceLibraryControl>("SequenceLibrary").ChipFor("hero.achx", "Run")!), target);

        timeline.DropPreview!.Value.Start.ShouldBe(0.63, 1e-6);
        timeline.SnapGuideTime!.Value.ShouldBe(0.63, 1e-6);
        editor.DragRelease(target);
        Saved(boss, 1, 0).Start.ShouldBe(0.63, 1e-6);
        timeline.SnapGuideTime.ShouldBeNull();
    }

    private static Point OnRuler(AnimationEditorHarness editor, double time, double dx = 0)
    {
        var timeline = Timeline(editor);
        return editor.PointIn(timeline, timeline.Viewport.XAt(time) + dx, TimelineViewport.RulerHeight / 2);
    }

    [AvaloniaTheory]
    [InlineData(120, 0)]
    [InlineData(2000, 0.4)]
    public async Task ScrubbingTheRuler_NextToAChainsEnd_IsFree_AtAnyZoom_WithNoGuide(double pixelsPerSecond, double scroll)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0, null)), Track("Arms", ("Run", 0.55, null)));
        await editor.OpenAsync(boss);
        Zoom(editor, pixelsPerSecond, scroll);
        var timeline = Timeline(editor);
        var scrubbed = new List<double>();
        timeline.Scrubbed += scrubbed.Add;

        // 3 px short of Run's end at 0.75: a chain edge would snap here, the playhead does not.
        editor.DragHold(OnRuler(editor, 0.6), OnRuler(editor, 0.75, dx: -3));

        scrubbed[^1].ShouldBe(0.75 - 3 / pixelsPerSecond, 1e-6);
        timeline.SnapGuideTime.ShouldBeNull();
        editor.DragRelease(OnRuler(editor, 0.75, dx: -3));
        timeline.SnapGuideTime.ShouldBeNull();
    }
}
