using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Hovering a timeline block outlines the same chain's chip in the Library, and leaving clears it (#1842).</summary>
public class SequenceBlockHoverLibraryScenarioTests
{
    // Lanes 0 and 1 both play Walk, lane 2 plays Idle, lane 3 points at a chain the source lacks.
    private static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Idle", "sheet.png", (0, 16, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        foreach (var (name, chain) in new[] { ("A", "Walk"), ("B", "Walk"), ("C", "Idle"), ("D", "Gone") })
        {
            var track = new SequenceTrackSave { Name = name };
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = chain, Start = 0 });
            sequence.Tracks.Add(track);
        }
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) => editor.Control<SequenceTimelineControl>("SequenceTimeline");
    private static SequenceLibraryControl Library(AnimationEditorHarness editor) => editor.Control<SequenceLibraryControl>("SequenceLibrary");

    private static Point OnBlock(AnimationEditorHarness editor, int lane)
    {
        var timeline = Timeline(editor);
        return editor.PointIn(timeline, timeline.Viewport.XAt(0.05), timeline.Viewport.LaneTop(lane) + TimelineLayout.BodyTop + 4);
    }

    [AvaloniaFact]
    public async Task HoveringABlock_HighlightsItsChip_AndLeavingClears()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var library = Library(editor);

        editor.Hover(OnBlock(editor, 2));
        library.ChipFor("hero.achx", "Idle")!.IsHighlighted.ShouldBeTrue();
        library.ChipFor("hero.achx", "Walk")!.IsHighlighted.ShouldBeFalse();

        editor.Hover(editor.PointIn(Timeline(editor), 2, 2));
        library.ChipFor("hero.achx", "Idle")!.IsHighlighted.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task TwoBlocksOfOneChain_HighlightOneChip()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var library = Library(editor);

        editor.Hover(OnBlock(editor, 0));
        library.ChipFor("hero.achx", "Walk")!.IsHighlighted.ShouldBeTrue();
        editor.Hover(OnBlock(editor, 1));
        library.ChipFor("hero.achx", "Walk")!.IsHighlighted.ShouldBeTrue();
        library.ChipFor("hero.achx", "Idle")!.IsHighlighted.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task HoveringABlockWhoseChainIsMissing_HighlightsNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var library = Library(editor);

        editor.Hover(OnBlock(editor, 3));

        library.ChipFor("hero.achx", "Walk")!.IsHighlighted.ShouldBeFalse();
        library.ChipFor("hero.achx", "Idle")!.IsHighlighted.ShouldBeFalse();
    }
}
