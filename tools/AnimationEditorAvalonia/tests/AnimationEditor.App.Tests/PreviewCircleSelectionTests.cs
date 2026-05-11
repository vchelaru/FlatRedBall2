using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Headless.XUnit;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
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

    private static void ResetSingletons()
    {
        ProjectManager.Self.AnimationChainListSave = new AnimationChainListSave();
        ProjectManager.Self.FileName               = null;
        SelectedState.Self.SelectedChain           = null;
        SelectedState.Self.SelectedFrame           = null;
        SelectedState.Self.SelectedNodes           = new List<object>();
        AppCommands.Self.DoOnUiThread              = a => a();
        AppCommands.Self.ConfirmAsync              = (_, _) => System.Threading.Tasks.Task.FromResult(true);
        AppCommands.Self.FileDialogService         = NullFileDialogService.Instance;
        AppState.Self.OffsetMultiplier             = 1f;
    }

    private static PreviewControl MakeControl()
    {
        var ctrl = new PreviewControl();
        ctrl.Measure(new Size(W, H));
        ctrl.Arrange(new Rect(0, 0, W, H));
        return ctrl;
    }

    private static AnimationFrameSave MakeFrameWithCircle(float worldX, float worldY, float radius, out CircleSave circle)
    {
        circle = new CircleSave { X = worldX, Y = worldY, Radius = radius };
        var frame = new AnimationFrameSave
        {
            ShapeCollectionSave = new ShapeCollectionSave()
        };
        frame.ShapeCollectionSave.CircleSaves.Add(circle);
        return frame;
    }

    // ── Circle selection ─────────────────────────────────────────────────────

    [AvaloniaFact]
    public void SimulateCanvasClick_SelectsCircle_WhenClickAtCenter()
    {
        ResetSingletons();
        var ctrl = MakeControl();

        var frame = MakeFrameWithCircle(0f, 0f, radius: 8f, out var circle);
        SelectedState.Self.SelectedFrame = frame;

        // At world (0,0), screen center is exactly (CX, CY).
        ctrl.SimulateCanvasClick(CX, CY);

        Assert.Same(circle, SelectedState.Self.SelectedCircle);
    }

    [AvaloniaFact]
    public void SimulateCanvasClick_SelectsCircle_WhenClickInsideRadius()
    {
        ResetSingletons();
        var ctrl = MakeControl();

        // Circle at world (10, 5), radius 20 → screen center (220, 155).
        var frame = MakeFrameWithCircle(10f, 5f, radius: 20f, out var circle);
        SelectedState.Self.SelectedFrame = frame;

        // Click 5px right of the screen center — still inside radius.
        ctrl.SimulateCanvasClick(CX + 10f + 5f, CY - 5f);

        Assert.Same(circle, SelectedState.Self.SelectedCircle);
    }

    [AvaloniaFact]
    public void SimulateCanvasClick_DoesNotSelectCircle_WhenClickFarAway()
    {
        ResetSingletons();
        var ctrl = MakeControl();

        var frame = MakeFrameWithCircle(0f, 0f, radius: 8f, out _);
        SelectedState.Self.SelectedFrame = frame;

        // Click 100px away from the circle center — well outside radius+tolerance.
        ctrl.SimulateCanvasClick(CX + 100f, CY + 100f);

        Assert.Null(SelectedState.Self.SelectedCircle);
    }

    [AvaloniaFact]
    public void SimulateCanvasClick_DoesNothing_WhenNoFrameSelected()
    {
        ResetSingletons();
        var ctrl = MakeControl();

        // No frame selected — TrySelectShapeAt must not throw.
        ctrl.SimulateCanvasClick(CX, CY);

        Assert.Null(SelectedState.Self.SelectedCircle);
        Assert.Null(SelectedState.Self.SelectedRectangle);
    }

    [AvaloniaFact]
    public void SimulateCanvasClick_DoesNothing_WhenClickInRulerStrip()
    {
        ResetSingletons();
        var ctrl = MakeControl();

        var frame = MakeFrameWithCircle(0f, 0f, radius: 200f, out _);
        SelectedState.Self.SelectedFrame = frame;

        // Click in the ruler area (px < RulerSize) — must be ignored.
        ctrl.SimulateCanvasClick(5f, CY);

        Assert.Null(SelectedState.Self.SelectedCircle);
    }

    // ── Rect selection ───────────────────────────────────────────────────────

    [AvaloniaFact]
    public void SimulateCanvasClick_SelectsRect_WhenClickAtRectCenter()
    {
        ResetSingletons();
        var ctrl = MakeControl();

        var rect = new AxisAlignedRectangleSave { X = 0f, Y = 0f, ScaleX = 15f, ScaleY = 10f };
        var frame = new AnimationFrameSave { ShapeCollectionSave = new ShapeCollectionSave() };
        frame.ShapeCollectionSave.AxisAlignedRectangleSaves.Add(rect);
        SelectedState.Self.SelectedFrame = frame;

        ctrl.SimulateCanvasClick(CX, CY);

        Assert.Same(rect, SelectedState.Self.SelectedRectangle);
    }

    // ── Priority: circle over rect ───────────────────────────────────────────

    [AvaloniaFact]
    public void SimulateCanvasClick_SelectsCircle_WhenCircleAndRectOverlap()
    {
        ResetSingletons();
        var ctrl = MakeControl();

        // Both at origin — circle is rendered on top (drawn after rect), so it wins.
        var rect   = new AxisAlignedRectangleSave { X = 0f, Y = 0f, ScaleX = 30f, ScaleY = 30f };
        var circle = new CircleSave              { X = 0f, Y = 0f, Radius = 20f };
        var frame  = new AnimationFrameSave { ShapeCollectionSave = new ShapeCollectionSave() };
        frame.ShapeCollectionSave.AxisAlignedRectangleSaves.Add(rect);
        frame.ShapeCollectionSave.CircleSaves.Add(circle);
        SelectedState.Self.SelectedFrame = frame;

        ctrl.SimulateCanvasClick(CX, CY);

        Assert.Same(circle, SelectedState.Self.SelectedCircle);
        Assert.Null(SelectedState.Self.SelectedRectangle);
    }
}
