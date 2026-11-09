using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Dragging the right edge of the sequence timeline's track-name column (#1765).</summary>
public class SequenceTimelineColumnResizeScenarioTests
{
    private const string LongName = "1_ShubbyMetalonHeadbutt";

    private static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        foreach (var name in new[] { LongName, "B" })
        {
            var track = new SequenceTrackSave { Name = name };
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 1, Loops = 1 });
            sequence.Tracks.Add(track);
        }
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    [AvaloniaFact]
    public async Task DraggingTheColumnEdge_ResizesTheColumn_AndMovesTheLaneOrigin()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);
        double y = timeline.Viewport.LaneTop(0) + 10;
        double xBefore = timeline.Viewport.XAt(1);

        editor.Drag(editor.PointIn(timeline, TimelineViewport.HeaderWidth, y), editor.PointIn(timeline, TimelineViewport.HeaderWidth + 100, y));

        timeline.Viewport.ColumnWidth.ShouldBe(TimelineViewport.HeaderWidth + 100, 1e-6);
        timeline.Viewport.XAt(1).ShouldBe(xBefore + 100, 1e-6);
        timeline.Viewport.TimeAt(timeline.Viewport.ColumnWidth).ShouldBe(timeline.Viewport.ScrollSeconds, 1e-9);
        timeline.IsResizingColumn.ShouldBeFalse();
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task DraggingTheColumnEdge_PastTheLimits_IsClamped()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);
        double y = timeline.Viewport.LaneTop(0) + 10;

        editor.Drag(editor.PointIn(timeline, TimelineViewport.HeaderWidth, y), editor.PointIn(timeline, 5, y));
        timeline.Viewport.ColumnWidth.ShouldBe(TimelineViewport.MinColumnWidth);
    }

    [AvaloniaFact]
    public async Task DoubleClickingTheColumnEdge_FitsTheLongestName()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);
        double y = timeline.Viewport.LaneTop(0) + 10;
        Point edge = editor.PointIn(timeline, TimelineViewport.HeaderWidth, y);

        editor.DoubleClickAt(edge);

        timeline.Viewport.ColumnWidth.ShouldBeGreaterThan(TimelineViewport.HeaderWidth);
    }

    [AvaloniaFact]
    public async Task APressInsideTheColumnAwayFromTheEdge_StillSelectsTheTrack_NotResizes()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);

        editor.ClickAt(editor.PointIn(timeline, 60, timeline.Viewport.LaneTop(0) + 30));

        timeline.Viewport.ColumnWidth.ShouldBe(TimelineViewport.HeaderWidth);
    }
}
