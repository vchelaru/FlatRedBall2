using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// #1648: the <c>.seqj</c> timeline zooms through fixed preset notches, so zooming out and back in returns to the same
/// levels and 100% is always reachable. Real Ctrl+wheel events and the toolbar zoom field.
/// </summary>
public class SequenceTimelineZoomScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, SequenceTimelineControl Timeline)> OpenAsync()
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        var track = new SequenceTrackSave { Name = "Body" };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0, Loops = 1 });
        sequence.Tracks.Add(track);
        save.Sequences.Add(sequence);
        save.Save(path);
        await editor.OpenAsync(path);
        return (editor, editor.Control<SequenceTimelineControl>("SequenceTimeline"));
    }

    private static Point PointOnLane(AnimationEditorHarness editor, SequenceTimelineControl timeline, double x) =>
        editor.PointIn(timeline, x, timeline.Viewport.LaneTop(0) + 10);

    private static void CtrlWheel(AnimationEditorHarness editor, Point point, int notches)
    {
        for (int i = 0; i < Math.Abs(notches); i++)
            editor.Wheel(point, Math.Sign(notches), RawInputModifiers.Control);
    }

    [AvaloniaFact]
    public async Task CtrlWheelOutToTheMinimum_ThenTheSameNotchesIn_ReturnsToExactly100()
    {
        var (editor, timeline) = await OpenAsync();
        using (editor)
        {
            Point point = PointOnLane(editor, timeline, TimelineViewport.HeaderWidth + 300);
            var levels = new List<double> { timeline.Viewport.PixelsPerSecond };

            // 20 notches is far more than the ladder has rungs, so the end is the clamp.
            for (int i = 0; i < 20; i++)
            {
                CtrlWheel(editor, point, -1);
                if (levels[^1] != timeline.Viewport.PixelsPerSecond) levels.Add(timeline.Viewport.PixelsPerSecond);
            }

            timeline.Viewport.PixelsPerSecond.ShouldBe(TimelineViewport.MinPixelsPerSecond);
            int rungs = levels.Count - 1;
            for (int i = 0; i < rungs; i++)
            {
                CtrlWheel(editor, point, +1);
                timeline.Viewport.PixelsPerSecond.ShouldBe(levels[rungs - 1 - i], 1e-9, $"notch {i + 1} back in");
            }

            timeline.Viewport.PixelsPerSecond.ShouldBe(TimelineViewport.DefaultPixelsPerSecond);
            timeline.Zoom.ShouldBe(1f);
            editor.Control<ZoomControl>("SequenceTimelineZoom").Text.ShouldBe("100%");
        }
    }

    [AvaloniaFact]
    public async Task CtrlWheel_AFewNotchesOutThenTheSameBackIn_ReturnsToExactly100()
    {
        var (editor, timeline) = await OpenAsync();
        using (editor)
        {
            Point point = PointOnLane(editor, timeline, TimelineViewport.HeaderWidth + 300);

            CtrlWheel(editor, point, -5);
            timeline.Zoom.ShouldBeLessThan(1f);
            CtrlWheel(editor, point, +5);

            timeline.Viewport.PixelsPerSecond.ShouldBe(TimelineViewport.DefaultPixelsPerSecond);
        }
    }

    [AvaloniaFact]
    public async Task CtrlWheel_KeepsTheTimeUnderThePointer_AtEveryNotch()
    {
        var (editor, timeline) = await OpenAsync();
        using (editor)
        {
            timeline.Viewport.SetPixelsPerSecond(600);
            timeline.Viewport.SetScrollSeconds(3);
            double x = TimelineViewport.HeaderWidth + 300;
            double time = timeline.Viewport.TimeAt(x);
            Point point = PointOnLane(editor, timeline, x);

            for (int i = 0; i < 6; i++)
            {
                CtrlWheel(editor, point, +1);
                timeline.Viewport.TimeAt(x).ShouldBe(time, 1e-6, $"zooming in, notch {i + 1}");
            }

            for (int i = 0; i < 6; i++)
            {
                CtrlWheel(editor, point, -1);
                timeline.Viewport.TimeAt(x).ShouldBe(time, 1e-6, $"zooming out, notch {i + 1}");
            }
        }
    }

    [AvaloniaFact]
    public async Task CtrlWheelFromATypedValue_StepsToTheNearestPresetInThatDirection()
    {
        var (editor, timeline) = await OpenAsync();
        using (editor)
        {
            Point point = PointOnLane(editor, timeline, TimelineViewport.HeaderWidth + 300);
            timeline.SetZoomPercent(87);

            CtrlWheel(editor, point, +1);
            timeline.Viewport.PixelsPerSecond.ShouldBe(TimelineViewport.DefaultPixelsPerSecond);

            timeline.SetZoomPercent(87);
            CtrlWheel(editor, point, -1);
            (timeline.Zoom * 100f).ShouldBe(75f, 0.001f);
        }
    }

    [AvaloniaFact]
    public async Task ToolbarPlusAndMinus_WalkTheSamePresetsAsTheWheel()
    {
        var (editor, timeline) = await OpenAsync();
        using (editor)
        {
            var zoom = editor.Control<ZoomControl>("SequenceTimelineZoom");

            zoom.StepDown();
            (timeline.Zoom * 100f).ShouldBe(75f, 0.001f);
            zoom.StepUp();
            timeline.Viewport.PixelsPerSecond.ShouldBe(TimelineViewport.DefaultPixelsPerSecond);
            zoom.StepUp();
            (timeline.Zoom * 100f).ShouldBe(150f, 0.001f);
        }
    }

    [AvaloniaFact]
    public async Task CtrlWheelAtTheLimits_StaysClamped()
    {
        var (editor, timeline) = await OpenAsync();
        using (editor)
        {
            Point point = PointOnLane(editor, timeline, TimelineViewport.HeaderWidth + 300);

            CtrlWheel(editor, point, +30);
            timeline.Viewport.PixelsPerSecond.ShouldBe(TimelineViewport.MaxPixelsPerSecond);
            CtrlWheel(editor, point, -30);
            timeline.Viewport.PixelsPerSecond.ShouldBe(TimelineViewport.MinPixelsPerSecond);
        }
    }
}
