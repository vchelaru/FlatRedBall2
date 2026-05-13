using AnimationEditor.App.Controls;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for <see cref="PreviewControl.CenterOnEntityPoint"/> — issue #188.
///
/// Verifies that the pan offset is set so the requested entity-space point
/// renders at the canvas centre. All tests are pure-math: no layout, no
/// ScrollViewer, no bitmap needed.
/// </summary>
public class PreviewCenterOnEntityPointTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a PreviewControl with services, sets <paramref name="zoomPct"/> %, and
    /// optionally sets <see cref="IAppState.OffsetMultiplier"/> before returning it.
    /// </summary>
    private static PreviewControl BuildCtrl(int zoomPct = 100, float offMult = 1f)
    {
        var ctx = TestHelpers.BuildServices();
        ctx.AppState.OffsetMultiplier = offMult;
        var ctrl = ctx.CreatePreviewControl();
        ctrl.SetZoomPercent(zoomPct);
        return ctrl;
    }

    // ── CenterOnEntityPoint math ──────────────────────────────────────────────

    [AvaloniaFact]
    public void CenterOnEntityPoint_Origin_PanStaysZero()
    {
        var ctrl = BuildCtrl(zoomPct: 200);
        ctrl.CenterOnEntityPoint(0f, 0f);
        var (px, py) = ctrl.PanOffset;
        Assert.Equal(0f, px, precision: 4);
        Assert.Equal(0f, py, precision: 4);
    }

    [AvaloniaFact]
    public void CenterOnEntityPoint_PositiveEntityX_NegatesPanX()
    {
        // entityX=10, zoom=200% (2×), offMult=1 → panX = -(10 * 1 * 2) = -20
        var ctrl = BuildCtrl(zoomPct: 200);
        ctrl.CenterOnEntityPoint(10f, 0f);
        var (px, py) = ctrl.PanOffset;
        Assert.Equal(-20f, px, precision: 4);
        Assert.Equal(  0f, py, precision: 4);
    }

    [AvaloniaFact]
    public void CenterOnEntityPoint_PositiveEntityY_PositivesPanY()
    {
        // entityY=5, zoom=200% (2×), offMult=1 → panY = +(5 * 1 * 2) = +10
        var ctrl = BuildCtrl(zoomPct: 200);
        ctrl.CenterOnEntityPoint(0f, 5f);
        var (px, py) = ctrl.PanOffset;
        Assert.Equal( 0f, px, precision: 4);
        Assert.Equal(10f, py, precision: 4);
    }

    [AvaloniaFact]
    public void CenterOnEntityPoint_UsesOffsetMultiplier()
    {
        // entityX=5, entityY=5, zoom=100% (1×), offMult=2
        // panX = -(5 * 2 * 1) = -10,  panY = +(5 * 2 * 1) = +10
        var ctrl = BuildCtrl(zoomPct: 100, offMult: 2f);
        ctrl.CenterOnEntityPoint(5f, 5f);
        var (px, py) = ctrl.PanOffset;
        Assert.Equal(-10f, px, precision: 4);
        Assert.Equal( 10f, py, precision: 4);
    }

    [AvaloniaFact]
    public void CenterOnEntityPoint_NegativeCoordinates_InvertsPan()
    {
        // entityX=-10, entityY=-5, zoom=200% (2×), offMult=1
        // panX = -(-10 * 1 * 2) = +20,  panY = (-5 * 1 * 2) = -10
        var ctrl = BuildCtrl(zoomPct: 200);
        ctrl.CenterOnEntityPoint(-10f, -5f);
        var (px, py) = ctrl.PanOffset;
        Assert.Equal( 20f, px, precision: 4);
        Assert.Equal(-10f, py, precision: 4);
    }

    // ── Dispatch via HandleAnimTreeNodeDoubleTap ──────────────────────────────

    [AvaloniaFact]
    public void HandleDoubleTap_RectNode_PansPreviewToRectCenter()
    {
        var ctx   = TestHelpers.BuildServices();
        ctx.AppState.OffsetMultiplier = 1f;
        var window = ctx.CreateMainWindow();
        window.Show();

        PreviewControl preview = window.FindControl<PreviewControl>("PreviewCtrl")
            ?? throw new InvalidOperationException("PreviewCtrl not found");
        // Zoom to 200% so the expected pan is clearly non-zero and easy to verify.
        preview.SetZoomPercent(200);

        var rect = new AARectSave { X = 8f, Y = 3f, ScaleX = 1f, ScaleY = 1f };
        var vm   = new TreeNodeVm { Data = rect };

        window.HandleAnimTreeNodeDoubleTap(vm);

        (float px, float py) = preview.PanOffset;
        // panX = -(8 * 1 * 2) = -16,  panY = +(3 * 1 * 2) = +6
        Assert.Equal(-16f, px, precision: 3);
        Assert.Equal(  6f, py, precision: 3);

        window.Close();
    }

    [AvaloniaFact]
    public void HandleDoubleTap_CircleNode_PansPreviewToCircleCenter()
    {
        var ctx   = TestHelpers.BuildServices();
        ctx.AppState.OffsetMultiplier = 1f;
        var window = ctx.CreateMainWindow();
        window.Show();

        PreviewControl preview = window.FindControl<PreviewControl>("PreviewCtrl")
            ?? throw new InvalidOperationException("PreviewCtrl not found");
        preview.SetZoomPercent(200);

        var circle = new CircleSave { X = -4f, Y = 6f, Radius = 5f };
        var vm     = new TreeNodeVm { Data = circle };

        window.HandleAnimTreeNodeDoubleTap(vm);

        (float px, float py) = preview.PanOffset;
        // panX = -(-4 * 1 * 2) = +8,  panY = +(6 * 1 * 2) = +12
        Assert.Equal( 8f, px, precision: 3);
        Assert.Equal(12f, py, precision: 3);

        window.Close();
    }

    [AvaloniaFact]
    public void HandleDoubleTap_UnknownNodeType_ReturnsFalse()
    {
        var ctx    = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        var vm     = new TreeNodeVm { Data = new object() };

        bool handled = window.HandleAnimTreeNodeDoubleTap(vm);

        Assert.False(handled);
        window.Close();
    }
}
