using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>A library chip dropped on the "+ new track" label in the header column makes a new track with the chain at time 0, like the dashed row.</summary>
public class SequenceChipDropOnNewTrackLabelScenarioTests
{
    private static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.Add(new SequenceTrackSave { Name = "Body" });
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static Point NewTrackLabel(AnimationEditorHarness editor)
    {
        var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        return editor.PointIn(timeline, 30, timeline.Viewport.LaneTop(TimelineViewport.NewTrackLane) + TimelineViewport.LaneHeight / 2);
    }

    private static Point DashedRow(AnimationEditorHarness editor)
    {
        var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        return editor.PointIn(timeline, timeline.Viewport.XAt(1.0), timeline.Viewport.LaneTop(TimelineViewport.NewTrackLane) + TimelineViewport.LaneHeight / 2);
    }

    private static List<(string Name, double Start)> Saved(string path) =>
        SequenceListSave.FromFile(path).Sequences[0].Tracks
            .Select(t => (t.Name, t.Chains.Count == 0 ? -1.0 : t.Chains[0].Start)).ToList();

    [AvaloniaFact]
    public async Task DroppingAChipOnTheNewTrackLabel_MakesATrackAtZero_AndUndoRemovesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        var chip = editor.Control<SequenceLibraryControl>("SequenceLibrary").ChipFor("hero.achx", "Walk")!;

        editor.Drag(editor.CenterOf(chip), NewTrackLabel(editor));

        Saved(boss).ShouldBe(new[] { ("Walk", 0.0), ("Body", -1.0) });
        editor.UndoLabels.ShouldBe(new[] { "Add Track 'Walk'" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        Saved(boss).ShouldBe(new[] { ("Body", -1.0) });
    }

    [AvaloniaFact]
    public async Task DroppingAChipOnTheDashedRow_StillMakesATrack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        var chip = editor.Control<SequenceLibraryControl>("SequenceLibrary").ChipFor("hero.achx", "Walk")!;

        editor.Drag(editor.CenterOf(chip), DashedRow(editor));

        Saved(boss).Select(t => t.Name).ShouldBe(new[] { "Walk", "Body" });
        editor.UndoLabels.ShouldBe(new[] { "Add Track 'Walk'" });
    }
}
