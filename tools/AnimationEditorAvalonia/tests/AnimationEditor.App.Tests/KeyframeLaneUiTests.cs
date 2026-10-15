using AnimationEditor.App.Controls;
using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.ViewModels;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// The Offset and Color keyframe lanes under the single-chain timeline strip (#1478): when they show,
/// that they stay aligned with the cells (including while scrolled), that the dock grows with them,
/// and that a click or drag on a lane still scrubs.
/// </summary>
public class KeyframeLaneUiTests
{
    private static AnimationChainSave MakeChain(string name, int frames, float length = 0.1f)
    {
        var chain = new AnimationChainSave { Name = name };
        for (int i = 0; i < frames; i++)
            chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = length, ShapesSave = new ShapesSave() });
        return chain;
    }

    private static MainWindow ShowWindow(TestServices ctx, params AnimationChainSave[] chains)
    {
        var acls = new AnimationChainListSave();
        foreach (var c in chains) acls.AnimationChains.Add(c);

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        ctx.ProjectManager.AnimationChainListSave = acls;
        ctx.SelectedState.SelectedChain = chains[0];
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static T Find<T>(MainWindow w, string name) where T : Control =>
        w.FindControl<T>(name) ?? throw new InvalidOperationException($"{name} not found");

    // Offset keyed on frames 1 and 3 (2 and 4 opt out) with different values; the Color lane stays at its default.
    private static AnimationChainSave OffsetKeyedChain()
    {
        var chain = MakeChain("Run", 4);
        chain.Frames[2].RelativeX = 12;
        chain.Frames[1].SkipOffsetKeyframe = true;
        chain.Frames[3].SkipOffsetKeyframe = true;
        return chain;
    }

    private static Point Origin(Visual v, Visual relativeTo) => v.TranslatePoint(new Point(0, 0), relativeTo)!.Value;

    [AvaloniaFact]
    public void DefaultChain_ShowsNoLanes_AndTheDockKeepsItsOriginalHeight()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ShowWindow(ctx, MakeChain("Run", 4));
        try
        {
            Assert.False(Find<KeyframeLaneControl>(window, "OffsetLane").IsVisible);
            Assert.False(Find<KeyframeLaneControl>(window, "ColorLane").IsVisible);
            Assert.False(Find<TextBlock>(window, "OffsetLaneLabel").IsVisible);
            Assert.Equal(52, Find<Grid>(window, "TimelineDockGrid").Height);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ChainWithOffsetKeyframes_ShowsOnlyTheOffsetLane_AndGrowsTheDockAndScrollbarMargins()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ShowWindow(ctx, OffsetKeyedChain());
        try
        {
            var offset = Find<KeyframeLaneControl>(window, "OffsetLane");
            Assert.True(offset.IsVisible);
            Assert.NotNull(offset.Lane);
            Assert.True(Find<TextBlock>(window, "OffsetLaneLabel").IsVisible);
            Assert.False(Find<KeyframeLaneControl>(window, "ColorLane").IsVisible);
            Assert.False(Find<TextBlock>(window, "ColorLaneLabel").IsVisible);

            double expected = 52 + KeyframeLaneBuilder.LaneHeight;
            Assert.Equal(expected, Find<Grid>(window, "TimelineDockGrid").Height);
            Assert.Equal(expected, Find<ScrollBar>(window, "PreviewVScroll").Margin.Bottom);
            Assert.Equal(expected, Find<ScrollBar>(window, "PreviewHScroll").Margin.Bottom);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void BothLanes_GrowTheDockByTwoLaneHeights()
    {
        var ctx = TestHelpers.BuildServices();
        var chain = OffsetKeyedChain();
        chain.Frames[2].Red = 40;
        chain.Frames[1].SkipColorKeyframe = true;
        var window = ShowWindow(ctx, chain);
        try
        {
            Assert.Equal(52 + 2 * KeyframeLaneBuilder.LaneHeight, Find<Grid>(window, "TimelineDockGrid").Height);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PickingACurve_ShowsTheLaneLive_AndUndoHidesIt()
    {
        var ctx = TestHelpers.BuildServices();
        var chain = MakeChain("Run", 4);
        var window = ShowWindow(ctx, chain);
        try
        {
            var dock = Find<Grid>(window, "TimelineDockGrid");
            var color = Find<KeyframeLaneControl>(window, "ColorLane");
            Assert.False(color.IsVisible);
            Assert.Equal(52, dock.Height);

            ctx.AppCommands.SetFrameKeyframeCurve(new[] { chain.Frames[0] }, KeyframeGroup.Color, KeyframeCurveChoice.Linear);
            Dispatcher.UIThread.RunJobs();
            Assert.True(color.IsVisible);
            Assert.Equal(4, color.Lane!.KeyframeXs.Count);
            Assert.False(Find<KeyframeLaneControl>(window, "OffsetLane").IsVisible);
            Assert.Equal(52 + KeyframeLaneBuilder.LaneHeight, dock.Height);

            ctx.UndoManager.Undo();
            Dispatcher.UIThread.RunJobs();
            Assert.False(color.IsVisible);
            Assert.Equal(52, dock.Height);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void GroupTimeline_ShowsNoLanes_AndKeepsItsOwnHeight()
    {
        var ctx = TestHelpers.BuildServices();
        var a = OffsetKeyedChain();
        var b = MakeChain("B", 3);
        var window = ShowWindow(ctx, a, b);
        try
        {
            Assert.True(Find<KeyframeLaneControl>(window, "OffsetLane").IsVisible);

            ctx.SelectedState.SelectedNodes = new List<object> { a, b };
            Dispatcher.UIThread.RunJobs();

            Assert.False(Find<KeyframeLaneControl>(window, "OffsetLane").IsVisible);
            Assert.False(Find<KeyframeLaneControl>(window, "ColorLane").IsVisible);
            Assert.False(Find<TextBlock>(window, "OffsetLaneLabel").IsVisible);
            Assert.Equal(14 + 2.25 * 40, Find<Grid>(window, "TimelineDockGrid").Height);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Lane_SitsUnderTheCells_KeyframesOnEachFramesLeftEdge_LabelOnTheLane()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ShowWindow(ctx, OffsetKeyedChain());
        try
        {
            var strip = Find<ItemsControl>(window, "TimelineStrip");
            var lane = Find<KeyframeLaneControl>(window, "OffsetLane");
            var label = Find<TextBlock>(window, "OffsetLaneLabel");

            var stripOrigin = Origin(strip, window);
            var laneOrigin = Origin(lane, window);
            Assert.Equal(stripOrigin.X, laneOrigin.X, 3);   // x = 0 of the lane is x = 0 of the cells
            Assert.True(laneOrigin.Y >= stripOrigin.Y + strip.Bounds.Height - 0.5);   // below the cells

            // Each keyframe's x is the left edge of its real cell (frames 1 and 3).
            var keyframeFrames = new[] { 0, 2 };
            for (int k = 0; k < keyframeFrames.Length; k++)
            {
                var cell = strip.ContainerFromIndex(keyframeFrames[k])!;
                Assert.Equal(Origin(cell, window).X - stripOrigin.X, lane.Lane!.KeyframeXs[k], 3);
            }

            Assert.Equal(laneOrigin.Y, Origin(label, window).Y, 3);
            Assert.Equal(KeyframeLaneBuilder.LaneHeight, label.Bounds.Height);
            Assert.Equal(KeyframeLaneBuilder.LaneHeight, lane.Bounds.Height);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void WhenBothShow_EachLabelSitsOnItsOwnLane()
    {
        var ctx = TestHelpers.BuildServices();
        var chain = OffsetKeyedChain();
        chain.Frames[2].Red = 40;
        chain.Frames[1].SkipColorKeyframe = true;
        var window = ShowWindow(ctx, chain);
        try
        {
            foreach (var (laneName, labelName) in new[] { ("OffsetLane", "OffsetLaneLabel"), ("ColorLane", "ColorLaneLabel") })
            {
                var lane = Find<KeyframeLaneControl>(window, laneName);
                var label = Find<TextBlock>(window, labelName);
                Assert.Equal(Origin(lane, window).Y, Origin(label, window).Y, 3);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ScrollingTheCells_ScrollsTheLanes()
    {
        var ctx = TestHelpers.BuildServices();
        var chain = MakeChain("Long", 120);
        chain.Frames[2].RelativeX = 5;
        chain.Frames[2].Red = 40;
        chain.Frames[1].SkipOffsetKeyframe = true;
        chain.Frames[1].SkipColorKeyframe = true;
        var window = ShowWindow(ctx, chain);
        try
        {
            var scroll = Find<ScrollViewer>(window, "TimelineScroll");
            Assert.True(scroll.Extent.Width > scroll.Viewport.Width);

            scroll.Offset = new Vector(150, 0);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(150, Find<KeyframeLaneControl>(window, "OffsetLane").ScrollOffset);
            Assert.Equal(150, Find<KeyframeLaneControl>(window, "ColorLane").ScrollOffset);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ClickingALane_ScrubsTheTimelineToThatFrame()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ShowWindow(ctx, OffsetKeyedChain());
        try
        {
            var preview = Find<PreviewControl>(window, "PreviewCtrl");
            var strip = Find<ItemsControl>(window, "TimelineStrip");
            var lane = Find<KeyframeLaneControl>(window, "OffsetLane");

            // The middle of frame 3's cell, pressed on the lane below it.
            var cell = strip.ContainerFromIndex(2)!;
            var point = lane.TranslatePoint(new Point(cell.Bounds.X + cell.Bounds.Width / 2, KeyframeLaneBuilder.LaneHeight / 2), window)!.Value;

            window.MouseDown(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.False(preview.IsPlaying);
            Assert.Equal(2, preview.Playback.CurrentFrameIndex);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DraggingFromACellOntoALane_KeepsScrubbing()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ShowWindow(ctx, OffsetKeyedChain());
        try
        {
            var preview = Find<PreviewControl>(window, "PreviewCtrl");
            var strip = Find<ItemsControl>(window, "TimelineStrip");
            var lane = Find<KeyframeLaneControl>(window, "OffsetLane");

            var firstCell = strip.ContainerFromIndex(0)!;
            var start = firstCell.TranslatePoint(new Point(firstCell.Bounds.Width / 2, 10), window)!.Value;
            var fourthCell = strip.ContainerFromIndex(3)!;
            var overLane = lane.TranslatePoint(new Point(fourthCell.Bounds.X + fourthCell.Bounds.Width / 2, 8), window)!.Value;

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(overLane);
            Dispatcher.UIThread.RunJobs();
            window.MouseUp(overLane, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(3, preview.Playback.CurrentFrameIndex);
        }
        finally { window.Close(); }
    }
}
