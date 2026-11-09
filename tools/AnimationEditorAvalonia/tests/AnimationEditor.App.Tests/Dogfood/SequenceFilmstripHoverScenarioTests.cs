using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Hovering a filmstrip cell shows a small card for the frame under the pointer; it never gets in the way of anything else.</summary>
public class SequenceFilmstripHoverScenarioTests
{
    // Walk: five 0.1 s frames (0.5 s cycle). Run: two 0.1 s frames.
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

    private static Point Film(AnimationEditorHarness editor, int lane, double time)
    {
        var timeline = Timeline(editor);
        double y = timeline.Viewport.LaneTop(lane) + TimelineLayout.FilmTop + TimelineLayout.FilmHeight / 2;
        return editor.PointIn(timeline, timeline.Viewport.XAt(time), y);
    }

    [AvaloniaFact]
    public async Task Hovering_NamesTheFrameAndItsStartTime_AtSeveralZoomsAndCycles()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Body", ("Walk", 0.5, 2))));
        var timeline = Timeline(editor);

        // Block starts at 0.5 s; 0.75 s is frame 3 of the first cycle, 1.15 s is frame 2 of the second.
        editor.Hover(Film(editor, 0, 0.75));
        timeline.HoverCard!.Title.ShouldBe("Walk");
        timeline.HoverCard.Lines.ShouldBe(new[] { "3 / 5", "Frame 0.10 s", "Start 0.70 s" });
        editor.Hover(Film(editor, 0, 1.15));
        timeline.HoverCard!.Lines.ShouldBe(new[] { "2 / 5", "Frame 0.10 s", "Start 1.10 s" });

        timeline.SetZoomPercent(400);
        editor.Hover(Film(editor, 0, 1.15));
        timeline.HoverCard!.Lines.ShouldBe(new[] { "2 / 5", "Frame 0.10 s", "Start 1.10 s" });
        timeline.SetZoomPercent(50);
        editor.Hover(Film(editor, 0, 1.15));
        timeline.HoverCard!.Lines[0].ShouldBe("2 / 5");
    }

    [AvaloniaFact]
    public async Task LeavingTheFilmstrip_HidesTheCard()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Body", ("Walk", 0, 1))));
        var timeline = Timeline(editor);
        double labelY = timeline.Viewport.LaneTop(0) + TimelineLayout.BodyTop + 3;

        editor.Hover(Film(editor, 0, 0.25));
        timeline.HoverCard.ShouldNotBeNull();
        editor.Hover(editor.PointIn(timeline, timeline.Viewport.XAt(0.25), labelY));
        timeline.HoverCard.ShouldBeNull();
        editor.Hover(Film(editor, 0, 0.25));
        editor.Hover(Film(editor, 0, 2));
        timeline.HoverCard.ShouldBeNull();
        editor.Hover(Film(editor, 0, 0.25));
        editor.Hover(editor.PointIn(timeline, 20, timeline.Viewport.LaneTop(0) + 10));
        timeline.HoverCard.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task ScrollingOrZooming_HidesTheCard()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Body", ("Walk", 0, 1))));
        var timeline = Timeline(editor);

        editor.Hover(Film(editor, 0, 0.25));
        editor.Wheel(Film(editor, 0, 0.25), -1);
        timeline.HoverCard.ShouldBeNull();

        editor.Hover(Film(editor, 0, 0.25));
        editor.Wheel(Film(editor, 0, 0.25), 1, RawInputModifiers.Control);
        timeline.HoverCard.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task WhileDraggingAChain_NoCardShows()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Body", ("Walk", 0, 1))));
        var timeline = Timeline(editor);

        editor.Hover(Film(editor, 0, 0.25));
        timeline.HoverCard.ShouldNotBeNull();
        editor.DragHold(Film(editor, 0, 0.25), Film(editor, 0, 0.55));

        timeline.HoverCard.ShouldBeNull();
        editor.DragMove(Film(editor, 0, 0.65));
        timeline.HoverCard.ShouldBeNull();
        editor.DragRelease(Film(editor, 0, 0.65));
    }

    [AvaloniaFact]
    public async Task WhileDraggingAChip_NoCardShows()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, Track("Body", ("Walk", 0, 1))));
        var timeline = Timeline(editor);
        var chip = editor.Control<SequenceLibraryControl>("SequenceLibrary").ChipFor("hero.achx", "Run")!;

        editor.Hover(Film(editor, 0, 0.25));
        timeline.HoverCard.ShouldNotBeNull();
        editor.DragHold(editor.CenterOf(chip), Film(editor, 0, 0.25));

        timeline.HoverCard.ShouldBeNull();
        editor.DragRelease(Film(editor, 0, 0.25));
    }

    [AvaloniaFact]
    public async Task TheCard_DoesNotInterceptAClickOnTheCellItCovers()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        // Head's block sits where the card (drawn below and right of the pointer) would cover it.
        await editor.OpenAsync(WriteFixture(editor, Track("Body", ("Walk", 0, 1)), Track("Head", ("Run", 0, 1))));
        var timeline = Timeline(editor);

        editor.Hover(Film(editor, 0, 0.25));
        timeline.HoverCard.ShouldNotBeNull();
        editor.ClickAt(Film(editor, 1, 0.1));

        timeline.Selection.ShouldBe(new TimelineChainId(1, 0));
        timeline.HoverCard.ShouldBeNull();
    }
}
