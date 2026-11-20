using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>A library chip dropped on an existing track's header label appends at the track's latest block end; the body keeps the pointer time.</summary>
public class SequenceChipDropOnTrackLabelScenarioTests
{
    // Walk is 2 frames x 0.1 s = 0.2 s per cycle. Tracks: 0 Body (empty), 1 Legs (out of order, latest end 2.2), 2 Arms (fractional, ends 1.0).
    private static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.Add(new SequenceTrackSave { Name = "Body" });
        var legs = new SequenceTrackSave { Name = "Legs" };
        legs.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 2, Loops = 1 });
        legs.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 1, Loops = 1.5f });
        sequence.Tracks.Add(legs);
        var arms = new SequenceTrackSave { Name = "Arms" };
        arms.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0.5, Loops = 2.5f });
        sequence.Tracks.Add(arms);
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static Point Label(AnimationEditorHarness editor, int track)
    {
        var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        return editor.PointIn(timeline, 30, timeline.Viewport.LaneTop(track) + TimelineViewport.LaneHeight / 2);
    }

    private static Point Body(AnimationEditorHarness editor, int track, double seconds)
    {
        var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        return editor.PointIn(timeline, timeline.Viewport.XAt(seconds), timeline.Viewport.LaneTop(track) + TimelineViewport.LaneHeight / 2);
    }

    private static List<double> Starts(string path, int track) =>
        SequenceListSave.FromFile(path).Sequences[0].Tracks[track].Chains.Select(c => c.Start).ToList();

    private static async Task<(AnimationEditorHarness Editor, string Path, LibraryChipView Chip)> Open()
    {
        var editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        return (editor, boss, editor.Control<SequenceLibraryControl>("SequenceLibrary").ChipFor("hero.achx", "Walk")!);
    }

    [AvaloniaFact]
    public async Task LabelDropOnEmptyTrack_StartsAtZero_AndUndoRemovesIt()
    {
        var (editor, boss, chip) = await Open();
        using (editor)
        {
            editor.Drag(editor.CenterOf(chip), Label(editor, 0));

            Starts(boss, 0).ShouldBe(new[] { 0.0 });
            editor.UndoLabels.Count.ShouldBe(1);
            editor.Press(Key.Z, RawInputModifiers.Control);
            Starts(boss, 0).ShouldBeEmpty();
        }
    }

    [AvaloniaFact]
    public async Task LabelDropOnTrackWithOutOfOrderBlocks_AppendsAtLatestEnd()
    {
        var (editor, boss, chip) = await Open();
        using (editor)
        {
            editor.Drag(editor.CenterOf(chip), Label(editor, 1));

            var starts = Starts(boss, 1);
            starts.Count.ShouldBe(3);
            starts.Max().ShouldBe(2.2, 1e-4);
        }
    }

    [AvaloniaFact]
    public async Task LabelDropOnFractionalLoopsTrack_AppendsAtExactEnd()
    {
        var (editor, boss, chip) = await Open();
        using (editor)
        {
            editor.Drag(editor.CenterOf(chip), Label(editor, 2));

            Starts(boss, 2).Max().ShouldBe(1.0, 1e-4);
        }
    }

    [AvaloniaFact]
    public async Task BodyDrop_StillPlacesAtThePointer()
    {
        var (editor, boss, chip) = await Open();
        using (editor)
        {
            editor.Drag(editor.CenterOf(chip), Body(editor, 1, 4.0));

            Starts(boss, 1).Max().ShouldBe(4.0, 0.15);
        }
    }
}
