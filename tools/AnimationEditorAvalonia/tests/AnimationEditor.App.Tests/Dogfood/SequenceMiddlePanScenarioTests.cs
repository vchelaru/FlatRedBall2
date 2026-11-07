using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Holding the middle button and dragging pans the timeline, time and lanes together, like middle-drag pans the preview
/// and the wireframe. It never selects, moves or resizes anything, and the card and cursor follow the pan.
/// </summary>
public class SequenceMiddlePanScenarioTests
{
    private const int TrackTotal = 14;

    // Walk: five 0.1 s frames, a 0.5 s cycle.
    private static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        for (int i = 0; i < TrackTotal; i++)
        {
            var track = new SequenceTrackSave { Name = $"T{i}" };
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0.5 + i * 0.1 });
            sequence.Tracks.Add(track);
        }
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static Point OnChain(AnimationEditorHarness editor, int lane, double time)
    {
        var timeline = Timeline(editor);
        double y = timeline.Viewport.LaneTop(lane) + (TimelineLayout.BodyTop + TimelineLayout.BodyBottom) / 2;
        return editor.PointIn(timeline, timeline.Viewport.XAt(time), y);
    }

    [AvaloniaFact]
    public async Task MiddleDraggingFromAChain_PansTimeAndLanesTogether_AndChangesNothingElse()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        var timeline = Timeline(editor);
        timeline.Viewport.ContentHeight(TrackTotal).ShouldBeGreaterThan(timeline.Bounds.Height);
        string saved = File.ReadAllText(boss);
        editor.ClickAt(OnChain(editor, 1, 0.7));
        var selected = timeline.Selection;
        selected.ShouldBe(new TimelineChainId(1, 0));
        editor.UndoLabels.ShouldBeEmpty();
        int scrubs = 0;
        timeline.Scrubbed += _ => scrubs++;
        var from = OnChain(editor, 2, 0.8);

        // Left and up: later time and lower lanes.
        editor.MiddleDrag(from, new Point(from.X - 120, from.Y - 60));

        timeline.Viewport.ScrollSeconds.ShouldBe(1.0, 0.02);
        timeline.Viewport.ScrollY.ShouldBe(60, 1);
        timeline.Selection.ShouldBe(selected);
        timeline.SelectedTrack.ShouldBeNull();
        editor.UndoLabels.ShouldBeEmpty();
        File.ReadAllText(boss).ShouldBe(saved);

        // Back right and down, from the ruler: it pans too and does not scrub.
        var ruler = editor.PointIn(timeline, 400, TimelineViewport.RulerHeight / 2);
        editor.MiddleDrag(ruler, new Point(ruler.X + 100_000, ruler.Y + 100_000));
        timeline.Viewport.ScrollSeconds.ShouldBe(0);
        timeline.Viewport.ScrollY.ShouldBe(0);
        scrubs.ShouldBe(0);
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task WhilePanning_TheCursorIsThePanCursor_AndTheHoverCardIsHidden_ThenBothReturn()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);
        var film = editor.PointIn(timeline, timeline.Viewport.XAt(0.75),
            timeline.Viewport.LaneTop(0) + TimelineLayout.FilmTop + TimelineLayout.FilmHeight / 2);
        editor.Hover(film);
        timeline.HoverCard.ShouldNotBeNull();

        editor.MiddleDragHold(film, new Point(film.X - 40, film.Y - 5));
        timeline.ShownCursor.ShouldBe(TimelineCursor.Pan);
        timeline.HoverCard.ShouldBeNull();
        editor.MiddleDragMove(new Point(film.X - 80, film.Y - 10));
        timeline.ShownCursor.ShouldBe(TimelineCursor.Pan);
        timeline.HoverCard.ShouldBeNull();

        editor.MiddleRelease(new Point(film.X - 80, film.Y - 10));
        timeline.ShownCursor.ShouldBe(TimelineCursor.Default);
    }

    [AvaloniaFact]
    public async Task LeftDraggingEmptySpace_DoesNotPan()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);
        var empty = editor.PointIn(timeline, 700, timeline.Viewport.LaneTop(0) + 10);

        editor.Drag(empty, new Point(empty.X - 150, empty.Y - 40));

        timeline.Viewport.ScrollSeconds.ShouldBe(0);
        timeline.Viewport.ScrollY.ShouldBe(0);
        editor.UndoLabels.ShouldBeEmpty();
    }
}
