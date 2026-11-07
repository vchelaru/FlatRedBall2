using AnimationEditor.App.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Spike: a .seqj tab driven through the window.</summary>
public class SequenceScenarioTests
{
    private static string WriteSequence(AnimationEditorHarness editor, string name, params string[] sources)
    {
        var save = new SequenceListSave();
        save.Sources.AddRange(sources);
        save.Sequences.Add(new SequenceSave { Name = "Attack" });
        string path = Path.Combine(editor.ProjectFolder, name);
        save.Save(path);
        return path;
    }

    [AvaloniaFact]
    public async Task DragChipToNewTrackRow_ThenEdgeDrag_PlacesChainSetsFloatLoops_AndUndoes()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("hero.png", 64, 32);
        // Two 0.1s frames: a 0.2s cycle.
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "hero.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string seqj = WriteSequence(editor, "boss.seqj", "hero.achx");

        await editor.OpenAsync(seqj);
        SequenceTimelineControl timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        Border chip = editor.Control<Border>("Chip:hero.achx:Walk");
        double newTrackY = SequenceTimelineControl.RulerHeight + SequenceTimelineControl.LaneHeight / 2;
        editor.Drag(editor.CenterOf(chip), editor.PointIn(timeline, timeline.XAt(1.0), newTrackY));

        SequenceSave sequence = editor.Window.SequenceHost.Sequence!;
        SequenceChainReferenceSave walk = sequence.Tracks.ShouldHaveSingleItem().Chains.ShouldHaveSingleItem();
        walk.Start.ShouldBe(1.0, 0.01);

        // Grab the right edge (1.2s) and pull it to 2.5 cycles (1.5s), away from the whole-cycle magnet.
        double laneY = SequenceTimelineControl.RulerHeight + SequenceTimelineControl.LaneHeight / 2;
        editor.Drag(editor.PointIn(timeline, timeline.XAt(walk.Start + 0.2) - 2, laneY),
            editor.PointIn(timeline, timeline.XAt(walk.Start + 0.5) - 2, laneY));

        sequence = editor.Window.SequenceHost.Sequence!;
        sequence.Tracks[0].Chains[0].Loops.ShouldBe(2.5f, 0.05f);
        editor.UndoLabels.ShouldBe(new[] { "Place 'Walk'", "Set Loops 'Walk'" });
        SequenceListSave.FromFile(seqj).Sequences[0].Tracks[0].Chains[0].Loops.ShouldBe(2.5f, 0.05f);

        editor.Press(Key.Z, RawInputModifiers.Control);

        editor.Window.SequenceHost.Sequence!.Tracks[0].Chains[0].Loops.ShouldBe(1f);
    }

    [AvaloniaFact]
    public async Task Preview_TwoTracksFromTwoSources_DrawsBothTexturesAtOnce_AndHidesOutsideChains()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("hero.png", 32, 32);
        editor.WritePng("fx.png", 32, 32);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "hero.png", (0, 0, 16, 16)));
        string fx = editor.WriteAchx("fx.achx", AnimationEditorHarness.Chain("Spark", "fx.png", (0, 0, 8, 8)));
        string seqj = WriteSequence(editor, "boss.seqj", "hero.achx");
        await editor.OpenAsync(seqj);
        SequenceModeHost host = editor.Window.SequenceHost;
        // The library's file drop funnels here; OS drag-drop can't be driven headlessly.
        host.AddSource(fx).ShouldBeNull();
        SequenceTimelineControl timeline = host.Timeline;
        double row0 = SequenceTimelineControl.RulerHeight + 5;
        timeline.DropChain(new Avalonia.Point(timeline.XAt(0), row0), "hero.achx", "Walk");
        timeline.DropChain(new Avalonia.Point(timeline.XAt(0), row0 + SequenceTimelineControl.LaneHeight), "fx.achx", "Spark");

        host.SetTime(0.05);

        host.Preview.Layers.Select(l => Path.GetFileName(l.TexturePath)).ShouldBe(new[] { "hero.png", "fx.png" });
        host.SetTime(0.5);
        host.Preview.Layers.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task SwitchingToAnAchxTabAndBack_KeepsTheSequenceUndoHistory()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("hero.png", 32, 32);
        string achx = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "hero.png", (0, 0, 16, 16)));
        string seqj = WriteSequence(editor, "boss.seqj", "hero.achx");
        await editor.OpenAsync(seqj);
        editor.Click(editor.Control<Button>("AddSequenceButton"));
        editor.UndoLabels.ShouldBe(new[] { "Add Sequence 'Sequence'" });

        await editor.OpenAsync(achx);
        editor.UndoLabels.ShouldBeEmpty();
        editor.ClickTab("boss.seqj");

        editor.UndoLabels.ShouldBe(new[] { "Add Sequence 'Sequence'" });
        editor.Control<ListBox>("SequenceList").ItemCount.ShouldBe(2);
    }
}
