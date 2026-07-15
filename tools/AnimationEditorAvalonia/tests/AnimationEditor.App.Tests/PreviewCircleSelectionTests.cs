using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Integration tests that verify clicking on the preview canvas selects the
/// collision shape under the cursor. Issue #130.
/// </summary>
public class PreviewCircleSelectionTests
{
    // Control size used for all tests.
    private const float W = 400f;
    private const float H = 300f;

    // RulerSize from PreviewControl — must match the const there.
    private const float RulerSize = 20f;

    // Expected canvas center at zoom=1, pan=0 for a W×H control.
    private const float CX = (W - RulerSize) / 2f + RulerSize;   // 210
    private const float CY = (H - RulerSize) / 2f + RulerSize;   // 160

    private static TestServices ResetSingletons() {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.SelectedState.SelectedNodes           = new List<object>();
        ctx.AppCommands.DoOnUiThread              = a => a();
        ctx.AppCommands.ConfirmAsync              = (_, _) => System.Threading.Tasks.Task.FromResult(true);
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        ctx.AppState.OffsetMultiplier             = 1f;
        return ctx;
    }

    private static PreviewControl MakeControl(TestServices ctx)
    {
        var ctrl = ctx.CreatePreviewControl();
        ctrl.Measure(new Size(W, H));
        ctrl.Arrange(new Rect(0, 0, W, H));
        return ctrl;
    }

    private static AnimationFrameSave MakeFrameWithCircle(float worldX, float worldY, float radius, out CircleSave circle)
    {
        circle = new CircleSave { X = worldX, Y = worldY, Radius = radius };
        var frame = new AnimationFrameSave
        {
            ShapesSave = new ShapesSave()
        };
        frame.ShapesSave.Shapes.Add(circle);
        return frame;
    }

    // ── Circle selection ─────────────────────────────────────────────────────

    [AvaloniaFact]
    public void SimulateCanvasClick_SelectsCircle_WhenClickAtCenter()
    {
        var ctx = ResetSingletons();
        var ctrl = MakeControl(ctx);

        var frame = MakeFrameWithCircle(0f, 0f, radius: 8f, out var circle);
        ctx.SelectedState.SelectedFrame = frame;

        // At world (0,0), screen center is exactly (CX, CY).
        ctrl.SimulateCanvasClick(CX, CY);

        Assert.Same(circle, ctx.SelectedState.SelectedCircle);
    }

    [AvaloniaFact]
    public void SimulateCanvasClick_SelectsCircle_WhenClickInsideRadius()
    {
        var ctx = ResetSingletons();
        var ctrl = MakeControl(ctx);

        // Circle at world (10, 5), radius 20 → screen center (220, 155).
        var frame = MakeFrameWithCircle(10f, 5f, radius: 20f, out var circle);
        ctx.SelectedState.SelectedFrame = frame;

        // Click 5px right of the screen center — still inside radius.
        ctrl.SimulateCanvasClick(CX + 10f + 5f, CY - 5f);

        Assert.Same(circle, ctx.SelectedState.SelectedCircle);
    }

    [AvaloniaFact]
    public void SimulateCanvasClick_DoesNotSelectCircle_WhenClickFarAway()
    {
        var ctx = ResetSingletons();
        var ctrl = MakeControl(ctx);

        var frame = MakeFrameWithCircle(0f, 0f, radius: 8f, out _);
        ctx.SelectedState.SelectedFrame = frame;

        // Click 100px away from the circle center — well outside radius+tolerance.
        ctrl.SimulateCanvasClick(CX + 100f, CY + 100f);

        Assert.Null(ctx.SelectedState.SelectedCircle);
    }

    [AvaloniaFact]
    public void SimulateCanvasClick_DoesNothing_WhenNoFrameSelected()
    {
        var ctx = ResetSingletons();
        var ctrl = MakeControl(ctx);

        // No frame selected — TrySelectShapeAt must not throw.
        ctrl.SimulateCanvasClick(CX, CY);

        Assert.Null(ctx.SelectedState.SelectedCircle);
        Assert.Null(ctx.SelectedState.SelectedRectangle);
    }

    [AvaloniaFact]
    public void SimulateCanvasClick_DoesNothing_WhenClickInRulerStrip()
    {
        var ctx = ResetSingletons();
        var ctrl = MakeControl(ctx);

        var frame = MakeFrameWithCircle(0f, 0f, radius: 200f, out _);
        ctx.SelectedState.SelectedFrame = frame;

        // Click in the ruler area (px < RulerSize) — must be ignored.
        ctrl.SimulateCanvasClick(5f, CY);

        Assert.Null(ctx.SelectedState.SelectedCircle);
    }

    // ── Rect selection ───────────────────────────────────────────────────────

    [AvaloniaFact]
    public void SimulateCanvasClick_SelectsRect_WhenClickAtRectCenter()
    {
        var ctx = ResetSingletons();
        var ctrl = MakeControl(ctx);

        var rect = new AARectSave { X = 0f, Y = 0f, ScaleX = 15f, ScaleY = 10f };
        var frame = new AnimationFrameSave { ShapesSave = new ShapesSave() };
        frame.ShapesSave.Shapes.Add(rect);
        ctx.SelectedState.SelectedFrame = frame;

        ctrl.SimulateCanvasClick(CX, CY);

        Assert.Same(rect, ctx.SelectedState.SelectedRectangle);
    }

    // ── Clearing stale tree multi-selection (#727) ──────────────────────────

    [AvaloniaFact]
    public void SimulateCanvasClick_ClearsSelectedNodes_WhenCircleHit()
    {
        var ctx = ResetSingletons();
        var ctrl = MakeControl(ctx);

        var frame = MakeFrameWithCircle(0f, 0f, radius: 8f, out _);
        ctx.SelectedState.SelectedFrame = frame;
        ctx.SelectedState.SelectedNodes = new List<object> { new object(), new object() };

        ctrl.SimulateCanvasClick(CX, CY);

        Assert.Empty(ctx.SelectedState.SelectedNodes);
    }

    [AvaloniaFact]
    public void SimulateCanvasClick_ClearsSelectedNodes_WhenRectHit()
    {
        var ctx = ResetSingletons();
        var ctrl = MakeControl(ctx);

        var rect = new AARectSave { X = 0f, Y = 0f, ScaleX = 15f, ScaleY = 10f };
        var frame = new AnimationFrameSave { ShapesSave = new ShapesSave() };
        frame.ShapesSave.Shapes.Add(rect);
        ctx.SelectedState.SelectedFrame = frame;
        ctx.SelectedState.SelectedNodes = new List<object> { new object(), new object() };

        ctrl.SimulateCanvasClick(CX, CY);

        Assert.Empty(ctx.SelectedState.SelectedNodes);
    }

    [AvaloniaFact]
    public void SimulateCanvasClick_DoesNotClearSelectedNodes_WhenClickFarAway()
    {
        var ctx = ResetSingletons();
        var ctrl = MakeControl(ctx);

        var frame = MakeFrameWithCircle(0f, 0f, radius: 8f, out _);
        ctx.SelectedState.SelectedFrame = frame;
        var staleNode = new object();
        ctx.SelectedState.SelectedNodes = new List<object> { staleNode };

        // Click 100px away from the circle center — no shape is hit.
        ctrl.SimulateCanvasClick(CX + 100f, CY + 100f);

        Assert.Same(staleNode, Assert.Single(ctx.SelectedState.SelectedNodes));
    }

    // ── Priority: circle over rect ───────────────────────────────────────────

    [AvaloniaFact]
    public void SimulateCanvasClick_SelectsCircle_WhenCircleAndRectOverlap()
    {
        var ctx = ResetSingletons();
        var ctrl = MakeControl(ctx);

        // Both at origin — circle is rendered on top (drawn after rect), so it wins.
        var rect   = new AARectSave { X = 0f, Y = 0f, ScaleX = 30f, ScaleY = 30f };
        var circle = new CircleSave              { X = 0f, Y = 0f, Radius = 20f };
        var frame  = new AnimationFrameSave { ShapesSave = new ShapesSave() };
        frame.ShapesSave.Shapes.Add(rect);
        frame.ShapesSave.Shapes.Add(circle);
        ctx.SelectedState.SelectedFrame = frame;

        ctrl.SimulateCanvasClick(CX, CY);

        Assert.Same(circle, ctx.SelectedState.SelectedCircle);
        Assert.Null(ctx.SelectedState.SelectedRectangle);
    }
}
