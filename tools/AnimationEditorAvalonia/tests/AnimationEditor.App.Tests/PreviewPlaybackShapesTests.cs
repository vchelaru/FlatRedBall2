using AnimationEditor.App.Controls;
using AnimationEditor.Core.CommandsAndState;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using System.Linq;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Verifies that collision-shape overlays follow the currently-playing frame during
/// free playback (no frame pinned), so artists can see collision geometry in motion.
/// Issue #190.
/// </summary>
public class PreviewPlaybackShapesTests
{
    private static (AnimationChainSave Chain, AnimationFrameSave Frame0, AnimationFrameSave Frame1)
        MakeTwoFrameChain(float frame0Length = 0.1f, float frame1Length = 0.1f)
    {
        var rect0 = new AARectSave { X = 10f, Y = 0f, ScaleX = 5f, ScaleY = 5f };
        var rect1 = new AARectSave { X = 20f, Y = 0f, ScaleX = 5f, ScaleY = 5f };

        var frame0 = new AnimationFrameSave { FrameLength = frame0Length, ShapesSave = new ShapesSave() };
        frame0.ShapesSave.Shapes.Add(rect0);

        var frame1 = new AnimationFrameSave { FrameLength = frame1Length, ShapesSave = new ShapesSave() };
        frame1.ShapesSave.Shapes.Add(rect1);

        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frame0);
        chain.Frames.Add(frame1);

        return (chain, frame0, frame1);
    }

    // ── Shapes follow playback frame ──────────────────────────────────────────

    /// <summary>
    /// When no frame is pinned and playback advances to frame 1, BuildShapeInfos should
    /// return the shapes attached to frame 1, not frame 0.
    /// </summary>
    [AvaloniaFact]
    public void NoFramePinned_PlaybackOnFrame1_ReturnsFrame1Shapes()
    {
        var ctx = TestHelpers.BuildServices();
        var (chain, _, frame1) = MakeTwoFrameChain();

        var ctrl = ctx.CreatePreviewControl();
        ctrl.PauseAutoPlayback();

        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = null;
        Dispatcher.UIThread.RunJobs();

        // Advance past frame 0 (0.1 s) so playback lands on frame 1.
        ctrl.Playback.Play();
        ctrl.Playback.Advance(0.15);

        var shapes = ctrl.GetShapeInfosForTest();

        Assert.Single(shapes);
        Assert.Equal(PreviewControl.PreviewShapeKind.Rect, shapes[0].Kind);
        Assert.Equal(frame1.ShapesSave!.AARectSaves.First().X, shapes[0].X);
    }

    /// <summary>
    /// When a frame is pinned via tree selection, shapes still come from that pinned
    /// frame regardless of playback state — existing behaviour must not regress.
    /// </summary>
    [AvaloniaFact]
    public void FramePinned_ReturnsPinnedFrameShapes_RegardlessOfPlaybackIndex()
    {
        var ctx = TestHelpers.BuildServices();
        var (chain, frame0, _) = MakeTwoFrameChain();

        var ctrl = ctx.CreatePreviewControl();
        ctrl.PauseAutoPlayback();

        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame0;
        Dispatcher.UIThread.RunJobs();

        ctrl.Playback.Play();
        ctrl.Playback.Advance(0.15); // would advance to frame 1 if frame were not pinned

        var shapes = ctrl.GetShapeInfosForTest();

        Assert.Single(shapes);
        Assert.Equal(frame0.ShapesSave!.AARectSaves.First().X, shapes[0].X);
    }

    /// <summary>
    /// When playback is on frame 0 (no advancement), frame 0 shapes are returned.
    /// </summary>
    [AvaloniaFact]
    public void NoFramePinned_PlaybackAtFrame0_ReturnsFrame0Shapes()
    {
        var ctx = TestHelpers.BuildServices();
        var (chain, frame0, _) = MakeTwoFrameChain();

        var ctrl = ctx.CreatePreviewControl();
        ctrl.PauseAutoPlayback();

        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = null;
        Dispatcher.UIThread.RunJobs();

        // No advance — stays on frame 0.
        var shapes = ctrl.GetShapeInfosForTest();

        Assert.Single(shapes);
        Assert.Equal(frame0.ShapesSave!.AARectSaves.First().X, shapes[0].X);
    }

    /// <summary>
    /// During free playback there is no shape selection; all shapes have IsSelected = false.
    /// </summary>
    [AvaloniaFact]
    public void NoFramePinned_DuringPlayback_ShapesAreUnselected()
    {
        var ctx = TestHelpers.BuildServices();
        var (chain, _, _) = MakeTwoFrameChain();

        var ctrl = ctx.CreatePreviewControl();
        ctrl.PauseAutoPlayback();

        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = null;
        Dispatcher.UIThread.RunJobs();

        ctrl.Playback.Play();
        ctrl.Playback.Advance(0.15);

        var shapes = ctrl.GetShapeInfosForTest();

        Assert.All(shapes, s => Assert.False(s.IsSelected));
    }

    /// <summary>
    /// When there is no chain, BuildShapeInfos returns empty — no crash.
    /// </summary>
    [AvaloniaFact]
    public void NoChainNoFrame_ReturnsEmpty()
    {
        var ctx = TestHelpers.BuildServices();

        var ctrl = ctx.CreatePreviewControl();
        ctx.SelectedState.SelectedChain = null;
        ctx.SelectedState.SelectedFrame = null;

        var shapes = ctrl.GetShapeInfosForTest();

        Assert.Empty(shapes);
    }
}
