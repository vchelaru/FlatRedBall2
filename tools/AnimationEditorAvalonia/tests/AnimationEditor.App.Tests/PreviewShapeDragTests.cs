using AnimationEditor.App.Controls;
using AnimationEditor.Core.CommandsAndState;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for drag-to-move collision shapes (circles and axis-aligned rectangles)
/// in the PreviewControl — issue #131.
///
/// All tests use <see cref="PreviewControl.SimulateShapeDrag"/> which applies a
/// world-space delta and commits exactly as the live pointer path does.
/// </summary>
public class PreviewShapeDragTests
{
    private static AnimationFrameSave MakeFrame(
        AARectSave? rect = null,
        CircleSave? circle = null)
    {
        var frame = new AnimationFrameSave
        {
            FrameLength = 0.1f,
            ShapesSave = new ShapesSave()
        };
        if (rect   is not null) frame.ShapesSave.Shapes.Add(rect);
        if (circle is not null) frame.ShapesSave.Shapes.Add(circle);
        return frame;
    }

    // ── Circle drag ───────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void DragCircle_UpdatesXY()
    {
        var ctx    = TestHelpers.BuildServices();
        var circle = new CircleSave { X = 0f, Y = 0f, Radius = 10f };
        var frame  = MakeFrame(circle: circle);
        ctx.SelectedState.SelectedFrame  = frame;
        ctx.SelectedState.SelectedCircle = circle;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateShapeDrag(10f, 5f);

        Assert.Equal(10f, circle.X, precision: 3);
        Assert.Equal(5f,  circle.Y, precision: 3);
    }

    [AvaloniaFact]
    public void DragCircle_NonZeroStart_AppliesDeltaFromStart()
    {
        var ctx    = TestHelpers.BuildServices();
        var circle = new CircleSave { X = 20f, Y = -10f, Radius = 8f };
        var frame  = MakeFrame(circle: circle);
        ctx.SelectedState.SelectedFrame  = frame;
        ctx.SelectedState.SelectedCircle = circle;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateShapeDrag(-5f, 3f);

        Assert.Equal(15f, circle.X, precision: 3);
        Assert.Equal(-7f, circle.Y, precision: 3);
    }

    // ── Rectangle drag ────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void DragRectangle_UpdatesXY()
    {
        var ctx  = TestHelpers.BuildServices();
        var rect  = new AARectSave { X = 0f, Y = 0f, ScaleX = 16f, ScaleY = 16f };
        var frame = MakeFrame(rect: rect);
        ctx.SelectedState.SelectedFrame     = frame;
        ctx.SelectedState.SelectedRectangle = rect;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateShapeDrag(-3f, 7f);

        Assert.Equal(-3f, rect.X, precision: 3);
        Assert.Equal(7f,  rect.Y, precision: 3);
    }

    // ── Undo ──────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void DragCircle_AfterRelease_UndoRestoresPosition()
    {
        var ctx    = TestHelpers.BuildServices();
        var circle = new CircleSave { X = 0f, Y = 0f, Radius = 10f };
        var frame  = MakeFrame(circle: circle);
        ctx.SelectedState.SelectedFrame  = frame;
        ctx.SelectedState.SelectedCircle = circle;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateShapeDrag(20f, 10f);

        Assert.Equal(20f, circle.X, precision: 3);
        Assert.True(ctx.UndoManager.CanUndo);

        ctx.UndoManager.Undo();

        Assert.Equal(0f, circle.X, precision: 3);
        Assert.Equal(0f, circle.Y, precision: 3);
    }

    [AvaloniaFact]
    public void DragCircle_ZeroDelta_DoesNotRecordUndo()
    {
        var ctx    = TestHelpers.BuildServices();
        var circle = new CircleSave { X = 5f, Y = 5f, Radius = 10f };
        var frame  = MakeFrame(circle: circle);
        ctx.SelectedState.SelectedFrame  = frame;
        ctx.SelectedState.SelectedCircle = circle;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateShapeDrag(0f, 0f);

        Assert.False(ctx.UndoManager.CanUndo);
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void DragCircle_OnCommit_RaisesSelectionChanged()
    {
        var ctx    = TestHelpers.BuildServices();
        var circle = new CircleSave { X = 0f, Y = 0f, Radius = 10f };
        var frame  = MakeFrame(circle: circle);
        ctx.SelectedState.SelectedFrame  = frame;
        ctx.SelectedState.SelectedCircle = circle;

        bool changed = false;
        ctx.SelectedState.SelectionChanged += () => changed = true;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateShapeDrag(1f, 0f);

        Assert.True(changed, "SelectionChanged must be raised on commit so the property panel refreshes.");
    }

    // ── No-op when no frame ───────────────────────────────────────────────────

    [AvaloniaFact]
    public void SimulateShapeDrag_NoFrameSelected_IsNoOp()
    {
        var ctx  = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateShapeDrag(10f, 10f); // must not throw

        Assert.False(ctx.UndoManager.CanUndo);
    }

    // ── Non-default zoom/pan/offsetMultiplier ─────────────────────────────────

    [AvaloniaFact]
    public void DragCircle_NonDefaultZoomAndOffset_AppliesDeltaCorrectly()
    {
        var ctx    = TestHelpers.BuildServices();
        ctx.AppState.OffsetMultiplier = 2f;

        var circle = new CircleSave { X = 10f, Y = 0f, Radius = 5f };
        var frame  = MakeFrame(circle: circle);
        ctx.SelectedState.SelectedFrame  = frame;
        ctx.SelectedState.SelectedCircle = circle;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SetZoomPercent(300);
        ctrl.SetPan(50f, -20f);

        ctrl.SimulateShapeDrag(5f, -3f);

        Assert.Equal(15f, circle.X, precision: 3);
        Assert.Equal(-3f, circle.Y, precision: 3);
    }
}
