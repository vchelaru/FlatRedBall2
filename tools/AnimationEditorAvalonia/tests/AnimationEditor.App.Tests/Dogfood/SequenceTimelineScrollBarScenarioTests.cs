using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>The sequence timeline's scrollbars: they follow the wheel, zoom and track count, and dragging them scrolls the timeline.</summary>
public class SequenceTimelineScrollBarScenarioTests
{
    // Walk: a 0.5 s cycle. A chain starting at `lastStart` makes the sequence lastStart + 0.5 s long.
    private static string WriteFixture(AnimationEditorHarness editor, int tracks, double lastStart)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        for (int i = 0; i < tracks; i++)
        {
            var track = new SequenceTrackSave { Name = "T" + i };
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = i == 0 ? lastStart : 0, Loops = 1 });
            sequence.Tracks.Add(track);
        }
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static ScrollBar HBar(AnimationEditorHarness editor) => editor.Control<ScrollBar>("SequenceTimelineHScroll");

    private static ScrollBar VBar(AnimationEditorHarness editor) => editor.Control<ScrollBar>("SequenceTimelineVScroll");

    private static Point Thumb(AnimationEditorHarness editor, ScrollBar bar)
    {
        editor.Layout();
        var thumb = bar.GetVisualDescendants().OfType<Thumb>().First();
        return editor.CenterOf(thumb);
    }

    [AvaloniaFact]
    public async Task HorizontalBar_DraggingTheThumb_ScrollsTheTimelineToThatTime()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, tracks: 1, lastStart: 30));
        var timeline = Timeline(editor);
        var bar = HBar(editor);
        bar.IsEnabled.ShouldBeTrue();
        timeline.Viewport.ScrollSeconds.ShouldBe(0);
        Point grip = Thumb(editor, bar);

        editor.Drag(grip, new Point(grip.X + 80, grip.Y));

        timeline.Viewport.ScrollSeconds.ShouldBeGreaterThan(1);
        bar.Value.ShouldBe(timeline.Viewport.ScrollSeconds, 1e-6);
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task HorizontalBar_FollowsTheWheel_AndTheThumbReachesTheEndAtTheSequenceEnd()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, tracks: 1, lastStart: 30));
        var timeline = Timeline(editor);
        var bar = HBar(editor);
        Point point = editor.PointIn(timeline, TimelineViewport.HeaderWidth + 200, timeline.Viewport.LaneTop(0) + 10);

        editor.Wheel(point, -3, RawInputModifiers.Shift);

        timeline.Viewport.ScrollSeconds.ShouldBeGreaterThan(0);
        bar.Value.ShouldBe(timeline.Viewport.ScrollSeconds, 1e-6);
        timeline.Viewport.SetScrollSeconds(1e6);
        editor.Layout();
        bar.Value.ShouldBe(bar.Maximum, 1e-6);
    }

    [AvaloniaFact]
    public async Task HorizontalBar_ZoomingIn_ShrinksTheThumb()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, tracks: 1, lastStart: 30));
        var timeline = Timeline(editor);
        var bar = HBar(editor);
        double thumbBefore = bar.GetVisualDescendants().OfType<Thumb>().First().Bounds.Width;
        double viewBefore = bar.ViewportSize;
        Point point = editor.PointIn(timeline, TimelineViewport.HeaderWidth + 200, timeline.Viewport.LaneTop(0) + 10);

        editor.Wheel(point, 4, RawInputModifiers.Control);

        bar.ViewportSize.ShouldBeLessThan(viewBefore);
        bar.GetVisualDescendants().OfType<Thumb>().First().Bounds.Width.ShouldBeLessThan(thumbBefore);
    }

    [AvaloniaFact]
    public async Task HorizontalBar_SequenceShorterThanTheView_IsDisabled()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, tracks: 1, lastStart: 0));

        HBar(editor).IsEnabled.ShouldBeFalse();
        HBar(editor).IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task VerticalBar_WhenEveryLaneFits_IsHidden()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, tracks: 2, lastStart: 0));

        VBar(editor).IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task VerticalBar_WhenTheLanesOverflow_ShowsAndDraggingItScrollsTheLanes()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, tracks: 14, lastStart: 0));
        var bar = VBar(editor);
        bar.IsVisible.ShouldBeTrue();
        Timeline(editor).Viewport.ScrollY.ShouldBe(0);
        Point grip = Thumb(editor, bar);

        editor.Drag(grip, new Point(grip.X, grip.Y + 40));

        Timeline(editor).Viewport.ScrollY.ShouldBeGreaterThan(0);
        bar.Value.ShouldBe(Timeline(editor).Viewport.ScrollY, 1e-6);
    }

    [AvaloniaFact]
    public async Task VerticalBar_FollowsTheWheel()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, tracks: 14, lastStart: 0));
        var timeline = Timeline(editor);
        Point point = editor.PointIn(timeline, TimelineViewport.HeaderWidth + 200, timeline.Viewport.LaneTop(0) + 10);

        editor.Wheel(point, -2);

        timeline.Viewport.ScrollY.ShouldBeGreaterThan(0);
        VBar(editor).Value.ShouldBe(timeline.Viewport.ScrollY, 1e-6);
    }

    [AvaloniaFact]
    public async Task Scrollbars_SitOutsideTheTimeline_SoItsHitTestingNeverUnderThem()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, tracks: 14, lastStart: 30));
        var timeline = Timeline(editor);
        editor.Layout();

        var h = HBar(editor);
        var v = VBar(editor);
        var timelineTopLeft = timeline.TranslatePoint(default, editor.Window)!.Value;
        var hTopLeft = h.TranslatePoint(default, editor.Window)!.Value;
        var vTopLeft = v.TranslatePoint(default, editor.Window)!.Value;
        hTopLeft.Y.ShouldBeGreaterThanOrEqualTo(timelineTopLeft.Y + timeline.Bounds.Height - 1e-6);
        vTopLeft.X.ShouldBeGreaterThanOrEqualTo(timelineTopLeft.X + timeline.Bounds.Width - 1e-6);
        // The bars line up with the lanes: the horizontal one starts after the header, the vertical one below the ruler.
        hTopLeft.X.ShouldBe(timelineTopLeft.X + TimelineViewport.HeaderWidth, 1e-6);
        vTopLeft.Y.ShouldBe(timelineTopLeft.Y + TimelineViewport.RulerHeight, 1e-6);
    }
}
