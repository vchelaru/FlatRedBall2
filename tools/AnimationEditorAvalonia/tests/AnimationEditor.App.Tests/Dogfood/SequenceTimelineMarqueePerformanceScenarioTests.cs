using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Reflection;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Selecting must cost what is selected, not what is on the timeline. A rubber band over 200 chains changes the selection on
/// many moves; none of that may rebuild the layout, run the content refresh, redraw every block, or run the Inspector once per
/// touched chain. Counted, not timed, so the bound holds on any machine.
/// </summary>
public class SequenceTimelineMarqueePerformanceScenarioTests
{
    // 7 tracks of 29 chains each, 0.6 s apart: 203 chains.
    private static string WriteBig(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "big.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Big" };
        for (int t = 0; t < 7; t++)
        {
            var track = new SequenceTrackSave { Name = "T" + t };
            for (int i = 0; i < 29; i++)
                track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = i * 0.6 });
            sequence.Tracks.Add(track);
        }
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) => editor.Control<SequenceTimelineControl>("SequenceTimeline");

    [AvaloniaFact]
    public async Task ARubberBandOver200Chains_RedrawsOnlyTheSelectionLayer_AndNeverRebuildsOrRefreshesPerChain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteBig(editor));
        var timeline = Timeline(editor);
        timeline.Viewport.SetPixelsPerSecond(30);
        editor.Layout();
        int contentChanged = 0, selectionChanged = 0;
        timeline.ContentChanged += () => contentChanged++;
        timeline.SelectionChanged += () => selectionChanged++;
        // The throttle reads the wall clock, so how many events 60 moves raise depends on how fast this machine runs them. Hold it
        // open (its own tick-driven behavior is pinned in TimelineMarqueeSelectorTests) so the count is the same on any runner.
        typeof(SequenceTimelineControl).GetField("_marqueeThrottle", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(timeline, new SelectionChangeThrottle(long.MaxValue));
        int contentRenders = timeline.RenderCount;
        int layerRenders = timeline.SelectionLayerRenderCount;
        double X(double time) => timeline.Viewport.XAt(time);
        // Below the last lane, past the last chain's end: empty space, so the press starts a rubber band.
        double bottom = Math.Min(timeline.Viewport.LaneTop(7) + 20, timeline.Bounds.Height - 4);
        var from = editor.PointIn(timeline, X(17.45), bottom);

        editor.Window.MouseMove(from);
        editor.Window.MouseDown(from, MouseButton.Left);
        for (int step = 1; step <= 60; step++)
        {
            double f = step / 60.0;
            editor.Window.MouseMove(editor.PointIn(timeline, X(17.45) + (X(0) - X(17.45)) * f, bottom - (bottom - 30) * f), RawInputModifiers.LeftMouseButton);
            editor.Layout();
        }
        editor.Window.MouseUp(from, MouseButton.Left);
        editor.Layout();

        timeline.SelectedChains.Count.ShouldBe(203);
        contentChanged.ShouldBe(0, "a selection change is not a content change: no layout rebuild, no preview refresh");
        timeline.RenderCount.ShouldBe(contentRenders, "the chains, lanes and filmstrips are not redrawn while selecting");
        timeline.SelectionLayerRenderCount.ShouldBeGreaterThan(layerRenders);
        // Listeners are told on the first change and once more when the band ends, not once per touched chain: the 60 moves above
        // touch new chains on most of them.
        selectionChanged.ShouldBe(2);
        editor.Control<SequenceInspectorControl>("SequenceInspector").ChainPanel.IsVisible.ShouldBeTrue("the Inspector edits the whole selection");
        editor.Control<TextBlock>("StatusCounts").Text.ShouldBe("203 chains selected");
    }

    [AvaloniaFact]
    public async Task ARubberBandThatTouchesNothingNew_ReportsNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteBig(editor));
        var timeline = Timeline(editor);
        timeline.Viewport.SetPixelsPerSecond(30);
        editor.Layout();
        int selectionChanged = 0;
        timeline.SelectionChanged += () => selectionChanged++;
        double X(double time) => timeline.Viewport.XAt(time);
        double y = timeline.Viewport.LaneTop(0) + TimelineLayout.BodyTop + 20;
        // Past the last chain (they end near 17 s), where nothing is to be touched.
        var from = editor.PointIn(timeline, X(30), y);

        editor.Window.MouseMove(from);
        editor.Window.MouseDown(from, MouseButton.Left);
        for (int step = 1; step <= 20; step++)
        {
            editor.Window.MouseMove(editor.PointIn(timeline, X(30) + step * 3, y + step), RawInputModifiers.LeftMouseButton);
            editor.Layout();
        }
        editor.Window.MouseUp(from, MouseButton.Left);

        timeline.SelectedChains.ShouldBeEmpty();
        selectionChanged.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task CtrlClickingAChain_RedrawsOnlyTheSelectionLayer()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteBig(editor));
        var timeline = Timeline(editor);
        editor.Layout();
        // On the label strip: over the filmstrip the hover card appears, which is a content redraw of its own.
        var onChain = editor.PointIn(timeline, timeline.Viewport.XAt(0.2), timeline.Viewport.LaneTop(0) + 8);
        editor.ClickAt(onChain);
        editor.Layout();
        int contentRenders = timeline.RenderCount;
        int layerRenders = timeline.SelectionLayerRenderCount;

        // A different chain, with Ctrl: the selection grows to two.
        editor.ClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(0.2 + 0.6), timeline.Viewport.LaneTop(0) + 8), RawInputModifiers.Control);
        editor.Layout();

        timeline.SelectedChains.Count.ShouldBe(2);
        timeline.SelectionLayerRenderCount.ShouldBeGreaterThan(layerRenders);
        timeline.RenderCount.ShouldBe(contentRenders);
    }

    [AvaloniaFact]
    public async Task ScrollingAndZooming_RedrawTheSelectionLayerToo_SoTheOutlinesFollowTheirChains()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteBig(editor));
        var timeline = Timeline(editor);
        editor.ClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(0.2), timeline.Viewport.LaneTop(0) + 8));
        editor.Layout();
        int layerRenders = timeline.SelectionLayerRenderCount;

        timeline.Viewport.SetScrollSeconds(1);
        editor.Layout();
        timeline.SelectionLayerRenderCount.ShouldBeGreaterThan(layerRenders);

        layerRenders = timeline.SelectionLayerRenderCount;
        timeline.Viewport.SetPixelsPerSecond(60);
        editor.Layout();
        timeline.SelectionLayerRenderCount.ShouldBeGreaterThan(layerRenders);
    }

    [AvaloniaFact]
    public async Task SelectingATrackHeader_StillRedrawsTheHeaders_AndSoDoesLeavingIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteBig(editor));
        var timeline = Timeline(editor);
        editor.Layout();
        int renders = timeline.RenderCount;

        editor.ClickAt(editor.PointIn(timeline, 70, timeline.HeaderCenters(1).Name));
        editor.Layout();
        timeline.RenderCount.ShouldBeGreaterThan(renders, "the selected header is drawn with the content");

        renders = timeline.RenderCount;
        editor.ClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(0.2), timeline.Viewport.LaneTop(0) + 30));
        editor.Layout();
        timeline.RenderCount.ShouldBeGreaterThan(renders, "the header highlight goes when the track is deselected");
    }
}
