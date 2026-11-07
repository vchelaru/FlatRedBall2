using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>A block on the sequence timeline shows its chain's real frames as a filmstrip row under the label strip.</summary>
public class SequenceFilmstripScenarioTests
{
    // Walk: five 0.1 s frames (0.5 s cycle). Run: two 0.1 s frames. Gone is not in hero.achx.
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

    [AvaloniaFact]
    public async Task ABlock_ShowsOneCellPerFrame_ToScaleInTime()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Body", ("Walk", 0, 1))));
        var timeline = Timeline(editor);

        var cells = timeline.FilmstripCells(timeline.Blocks.Single());

        cells.Select(c => c.Frame).ShouldBe(new[] { 0, 1, 2, 3, 4 });
        cells[0].Width.ShouldBe(0.1 * timeline.Viewport.PixelsPerSecond, 1e-6);
    }

    [AvaloniaFact]
    public async Task ACutBlock_DrawsFramesOnlyUpToTheCut()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        // Walk x2 would run to 1.0 s, but Run at 0.25 s cuts it.
        await editor.OpenAsync(WriteFixture(editor, Track("Body", ("Walk", 0, 2), ("Run", 0.25, 1))));
        var timeline = Timeline(editor);

        var cells = timeline.FilmstripCells(timeline.Blocks.Single(b => b.Reference.Chain == "Walk"));

        cells.Last().End.ShouldBe(0.25, 1e-9);
    }

    [AvaloniaFact]
    public async Task AMissingChain_HasNoFilmstrip()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Body", ("Gone", 0, 1))));
        var timeline = Timeline(editor);

        timeline.FilmstripCells(timeline.Blocks.Single()).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task PressingAFilmstripCell_OnlySelectsTheChain_AndLeavesThePlayheadAlone()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Body", ("Walk", 0, 1))));
        var timeline = Timeline(editor);
        double y = timeline.Viewport.LaneTop(0) + TimelineLayout.FilmTop + TimelineLayout.FilmHeight / 2;

        editor.ClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(0.25), y));

        timeline.Selection.ShouldBe(new TimelineChainId(0, 0));
        timeline.Playhead.ShouldBe(0);
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task ThePlayheadStaysPutWhenAFilmstripCellIsClicked_WhileItIsParked()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Body", ("Walk", 0, 1)), Track("Head", ("Run", 0.1, 1))));
        var timeline = Timeline(editor);
        timeline.Playhead = 0.4;
        double y = timeline.Viewport.LaneTop(1) + TimelineLayout.FilmTop + TimelineLayout.FilmHeight / 2;

        editor.ClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(0.15), y));

        timeline.Selection.ShouldBe(new TimelineChainId(1, 0));
        timeline.Playhead.ShouldBe(0.4);
    }
}
