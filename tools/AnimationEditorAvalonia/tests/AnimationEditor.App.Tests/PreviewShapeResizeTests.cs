using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.ViewModels;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for resize-handle drag on collision shapes (circles and axis-aligned rectangles)
/// in the PreviewControl — issue #131.
///
/// All tests use <see cref="PreviewControl.SimulateShapeResize"/> which applies handle
/// resize logic and commits exactly as the live pointer path does.
/// </summary>
public class PreviewShapeResizeTests
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

    // ── Rect resize ───────────────────────────────────────────────────────────

    /// <summary>
    /// Dragging MidRight handle by (+5) increases ScaleX and keeps center X unchanged.
    /// </summary>
    [AvaloniaFact]
    public void ResizeRect_MidRight_IncreasesScaleX()
    {
        var ctx   = TestHelpers.BuildServices();
        var rect  = new AARectSave { X = 0f, Y = 0f, ScaleX = 10f, ScaleY = 10f };
        var frame = MakeFrame(rect: rect);
        ctx.SelectedState.SelectedFrame     = frame;
        ctx.SelectedState.SelectedRectangle = rect;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateShapeResize(HandleKind.MidRight, newParam1: 15f, newParam2: 10f);

        Assert.Equal(15f, rect.ScaleX, precision: 3);
        Assert.Equal(10f, rect.ScaleY, precision: 3);
        Assert.Equal(0f,  rect.X, precision: 3);
    }

    /// <summary>
    /// Dragging MidLeft handle by an amount that reduces ScaleX works symmetrically.
    /// </summary>
    [AvaloniaFact]
    public void ResizeRect_MidLeft_DecreasesScaleX()
    {
        var ctx   = TestHelpers.BuildServices();
        var rect  = new AARectSave { X = 0f, Y = 0f, ScaleX = 10f, ScaleY = 10f };
        var frame = MakeFrame(rect: rect);
        ctx.SelectedState.SelectedFrame     = frame;
        ctx.SelectedState.SelectedRectangle = rect;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateShapeResize(HandleKind.MidLeft, newParam1: 5f, newParam2: 10f);

        Assert.Equal(5f,  rect.ScaleX, precision: 3);
        Assert.Equal(10f, rect.ScaleY, precision: 3);
    }

    /// <summary>
    /// Dragging TopCenter handle increases ScaleY.
    /// </summary>
    [AvaloniaFact]
    public void ResizeRect_TopCenter_IncreasesScaleY()
    {
        var ctx   = TestHelpers.BuildServices();
        var rect  = new AARectSave { X = 0f, Y = 0f, ScaleX = 10f, ScaleY = 10f };
        var frame = MakeFrame(rect: rect);
        ctx.SelectedState.SelectedFrame     = frame;
        ctx.SelectedState.SelectedRectangle = rect;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateShapeResize(HandleKind.TopCenter, newParam1: 10f, newParam2: 20f);

        Assert.Equal(10f, rect.ScaleX, precision: 3);
        Assert.Equal(20f, rect.ScaleY, precision: 3);
        Assert.Equal(0f,  rect.Y, precision: 3);
    }

    /// <summary>
    /// Dragging BotCenter handle reduces ScaleY.
    /// </summary>
    [AvaloniaFact]
    public void ResizeRect_BotCenter_DecreasesScaleY()
    {
        var ctx   = TestHelpers.BuildServices();
        var rect  = new AARectSave { X = 0f, Y = 0f, ScaleX = 10f, ScaleY = 10f };
        var frame = MakeFrame(rect: rect);
        ctx.SelectedState.SelectedFrame     = frame;
        ctx.SelectedState.SelectedRectangle = rect;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateShapeResize(HandleKind.BotCenter, newParam1: 10f, newParam2: 4f);

        Assert.Equal(10f, rect.ScaleX, precision: 3);
        Assert.Equal(4f,  rect.ScaleY, precision: 3);
    }

    // ── Circle resize ─────────────────────────────────────────────────────────

    /// <summary>
    /// Dragging a resize handle on a circle changes its Radius.
    /// </summary>
    [AvaloniaFact]
    public void ResizeCircle_MidRight_UpdatesRadius()
    {
        var ctx    = TestHelpers.BuildServices();
        var circle = new CircleSave { X = 0f, Y = 0f, Radius = 10f };
        var frame  = MakeFrame(circle: circle);
        ctx.SelectedState.SelectedFrame  = frame;
        ctx.SelectedState.SelectedCircle = circle;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateShapeResize(HandleKind.MidRight, newParam1: 15f); // new Radius

        Assert.Equal(15f, circle.Radius, precision: 3);
    }

    // ── Undo ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// After resizing a shape, undo restores the original dimensions.
    /// </summary>
    [AvaloniaFact]
    public void ResizeRect_AfterCommit_UndoRestoresDimensions()
    {
        var ctx   = TestHelpers.BuildServices();
        var rect  = new AARectSave { X = 0f, Y = 0f, ScaleX = 10f, ScaleY = 10f };
        var frame = MakeFrame(rect: rect);
        ctx.SelectedState.SelectedFrame     = frame;
        ctx.SelectedState.SelectedRectangle = rect;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateShapeResize(HandleKind.MidRight, newParam1: 20f, newParam2: 10f);

        Assert.Equal(20f, rect.ScaleX, precision: 3);
        Assert.True(ctx.UndoManager.CanUndo);

        ctx.UndoManager.Undo();

        Assert.Equal(10f, rect.ScaleX, precision: 3);
        Assert.Equal(10f, rect.ScaleY, precision: 3);
    }

    // ── No-op when no change ──────────────────────────────────────────────────

    /// <summary>
    /// A zero-delta resize must NOT push an undo entry.
    /// </summary>
    [AvaloniaFact]
    public void ResizeRect_ZeroDelta_DoesNotRecordUndo()
    {
        var ctx   = TestHelpers.BuildServices();
        var rect  = new AARectSave { X = 0f, Y = 0f, ScaleX = 10f, ScaleY = 10f };
        var frame = MakeFrame(rect: rect);
        ctx.SelectedState.SelectedFrame     = frame;
        ctx.SelectedState.SelectedRectangle = rect;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateShapeResize(HandleKind.MidRight, newParam1: 10f, newParam2: 10f); // same value — no change

        Assert.False(ctx.UndoManager.CanUndo);
    }

    // ── Tree selection pins frame ─────────────────────────────────────────────

    /// <summary>
    /// Selecting a shape node in the tree stops animation playback by setting
    /// SelectedFrame to the shape's parent frame.
    /// </summary>
    [AvaloniaFact]
    public void TreeSelection_ShapeNode_SetsParentFrameAndShape()
    {
        var ctx    = TestHelpers.BuildServices();
        var circle = new CircleSave { X = 0f, Y = 0f, Radius = 5f };
        var frame  = MakeFrame(circle: circle);
        var chain  = new AnimationChainSave { Name = "Idle" };
        chain.Frames.Add(frame);

        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        ctx.ProjectManager.AnimationChainListSave = acls;

        // Simulate the tree node selection for the circle (MainWindow passes vm.Data).
        TreeBuilder.RouteNodeSelection(circle, ctx.SelectedState, acls);

        Assert.Equal(frame,  ctx.SelectedState.SelectedFrame);
        Assert.Equal(circle, ctx.SelectedState.SelectedCircle);
        // SelectedRectangle must be cleared.
        Assert.Null(ctx.SelectedState.SelectedRectangle);
    }
}
