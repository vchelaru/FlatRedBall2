using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Third audit of the sequence timeline, for #1682 (a non-looping chain plays once and holds its last frame): the control
/// wiring that still passed its tests when the production line was broken (see plan/sequence-test-audit-3.md).
/// </summary>
public class SequenceHoldAudit3ScenarioTests
{
    // Walk: five 0.1 s frames (0.5 s cycle), Loop on. Once: the same with Loop off. "Start" is on frame 1 and "Step" on frame 3.
    private static AnimationChainSave WalkLike(string name, bool loop)
    {
        var chain = AnimationEditorHarness.Chain(name, "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16));
        chain.Loop = loop;
        chain.Frames[0].Events.Add(new AnimationFrameEvent { Name = "Start" });
        chain.Frames[2].Events.Add(new AnimationFrameEvent { Name = "Step" });
        return chain;
    }

    private static string WriteFixture(AnimationEditorHarness editor, params SequenceTrackSave[] tracks)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", WalkLike("Walk", loop: true), WalkLike("Once", loop: false));
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

    private static Point At(AnimationEditorHarness editor, int lane, double time, double dx = 0)
    {
        var timeline = Timeline(editor);
        double y = timeline.Viewport.LaneTop(lane) + (TimelineLayout.BodyTop + TimelineLayout.BodyBottom) / 2;
        return editor.PointIn(timeline, timeline.Viewport.XAt(time) + dx, y);
    }

    private static Point Film(AnimationEditorHarness editor, int lane, double time)
    {
        var timeline = Timeline(editor);
        double y = timeline.Viewport.LaneTop(lane) + TimelineLayout.FilmTop + TimelineLayout.FilmHeight / 2;
        return editor.PointIn(timeline, timeline.Viewport.XAt(time), y);
    }

    // ---- the edge drag magnets follow the chain's Loop flag

    [AvaloniaTheory]
    [InlineData("Walk", true, 1.2f)]
    [InlineData("Once", false, 1.21f)]
    public async Task DraggingTheRightEdge_SnapsToAFrameBoundaryInASecondCycle_OnlyWhenTheChainRepeats(string chain, bool snaps, float expectedLoops)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        // The block is 1.5 cycles (0.75 s). 0.6 s is the second cycle's first frame boundary, which only a repeating chain has.
        string boss = WriteFixture(editor, Track("Body", (chain, 0, 1.5f)));
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        timeline.Viewport.SetPixelsPerSecond(600);
        editor.Layout();

        // The pointer ends 3 px (0.005 s) past the boundary.
        editor.DragHold(At(editor, 0, 0.75, dx: -2), At(editor, 0, 0.6, dx: 1));

        if (snaps)
        {
            timeline.SnapGuide!.Value.Kind.ShouldBe(SnapKind.FrameBoundary);
            timeline.SnapGuideTime!.Value.ShouldBe(0.6, 1e-6);
        }
        else
        {
            timeline.SnapGuide.ShouldBeNull();
        }
        editor.DragRelease(At(editor, 0, 0.6, dx: 1));
        SequenceListSave.FromFile(boss).Sequences[0].Tracks[0].Chains[0].Loops.ShouldBe(expectedLoops, 1e-3f);
    }

    // ---- the hover card says "held"

    [AvaloniaFact]
    public async Task HoveringTheHeldTailOfANonLoopingBlock_SaysHeldAndCoversTheWholeHold_WhileAFrameOfTheCycleDoesNot()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Body", ("Once", 0, 3))));
        var timeline = Timeline(editor);

        editor.Hover(Film(editor, 0, 1.0));
        timeline.HoverCard!.Title.ShouldBe("Once");
        timeline.HoverCard.Lines.ShouldBe(new[] { "5 / 5", "1.10 s", "at 0.40 s", "held" });
        editor.Hover(Film(editor, 0, 0.25));
        timeline.HoverCard!.Lines.ShouldBe(new[] { "3 / 5", "0.10 s", "at 0.20 s" });
    }

    // ---- the inspector says what the field is

    [AvaloniaFact]
    public async Task TheInspectorsChainField_IsCalledCycles_AndExplainsWhatALoopFlagOffDoes()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Body", ("Once", 0, 3))));
        var inspector = editor.Control<SequenceInspectorControl>("SequenceInspector");

        var labels = inspector.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        labels.ShouldContain("Cycles");
        labels.ShouldNotContain("Loops");
        var label = inspector.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Cycles");
        ToolTip.GetTip(label).ShouldBeOfType<string>().ShouldContain("holds its last frame");
        ToolTip.GetTip(inspector.ChainLoopsField).ShouldBeOfType<string>().ShouldContain("holds its last frame");
    }

    // ---- the caches are keyed on the Loop flag

    // A document with Walk (Loop on) played for three cycles, shown on a bare timeline so the test can flip Loop on the same chain object.
    private static (SequenceTimelineControl Timeline, AnimationChainSave Walk) OpenBare()
    {
        string folder = Path.Combine(Path.GetTempPath(), "AnimationEditorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        list.AnimationChains.Add(WalkLike("Walk", loop: true));
        list.Save(Path.Combine(folder, "hero.achx"));
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.Add(Track("Body", ("Walk", 0, 3)));
        save.Sequences.Add(sequence);
        string seqj = Path.Combine(folder, "boss.seqj");
        save.Save(seqj);

        var document = SequenceDocument.Load(seqj);
        var timeline = new SequenceTimelineControl();
        var window = new Window { Width = 700, Height = 200, Content = timeline };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        timeline.Show(document, 0, new UndoManager());
        Dispatcher.UIThread.RunJobs();
        return (timeline, document.FindChain(document.Save.Sequences[0].Tracks[0].Chains[0])!);
    }

    [AvaloniaFact]
    public void TurningALoopFlagOffUnderAnOpenSequence_RedrawsItsFilmstripAsOneCycleAndAHold()
    {
        var (timeline, walk) = OpenBare();
        var block = timeline.Blocks.Single();

        timeline.FilmstripCells(block).Count.ShouldBe(15);
        walk.Loop = false;
        timeline.FilmstripCells(block).Count.ShouldBe(5);
        walk.Loop = true;
        timeline.FilmstripCells(block).Count.ShouldBe(15);
    }

    [AvaloniaFact]
    public void TurningALoopFlagOffUnderAnOpenSequence_DropsTheEventFlagsOfTheRepeats()
    {
        var (timeline, walk) = OpenBare();
        var block = timeline.Blocks.Single();

        timeline.EventMarkersOf(block).Count.ShouldBe(6);
        walk.Loop = false;
        timeline.EventMarkersOf(block).Count.ShouldBe(2);
        walk.Loop = true;
        timeline.EventMarkersOf(block).Count.ShouldBe(6);
    }
}
