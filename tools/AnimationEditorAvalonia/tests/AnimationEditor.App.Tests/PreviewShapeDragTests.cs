using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
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
    private static void ResetSingletons()
    {
        ProjectManager.Self.AnimationChainListSave = new AnimationChainListSave();
        ProjectManager.Self.FileName               = null;
        SelectedState.Self.SelectedChain           = null;
        SelectedState.Self.SelectedFrame           = null;
        SelectedState.Self.SelectedNodes           = new System.Collections.Generic.List<object>();
        AppCommands.Self.DoOnUiThread              = a => a();
        AppCommands.Self.FileDialogService         = NullFileDialogService.Instance;
        AppCommands.Self.ConfirmAsync              = (_, _) => Task.FromResult(true);
        AppState.Self.OffsetMultiplier             = 1f;
        UndoManager.Self.Clear();
    }

    private static AnimationFrameSave MakeFrame(
        AxisAlignedRectangleSave? rect = null,
        CircleSave? circle = null)
    {
        var frame = new AnimationFrameSave
        {
            FrameLength = 0.1f,
            ShapeCollectionSave = new ShapeCollectionSave()
        };
        if (rect   is not null) frame.ShapeCollectionSave.AxisAlignedRectangleSaves.Add(rect);
        if (circle is not null) frame.ShapeCollectionSave.CircleSaves.Add(circle);
        return frame;
    }

    // ── Circle drag ───────────────────────────────────────────────────────────

    /// <summary>
    /// Dragging a selected circle by (+10, +5) world units updates Circle.X/Y.
    /// </summary>
    [AvaloniaFact]
    public void DragCircle_UpdatesXY()
    {
        ResetSingletons();
        var circle = new CircleSave { X = 0f, Y = 0f, Radius = 10f };
        var frame  = MakeFrame(circle: circle);
        SelectedState.Self.SelectedFrame  = frame;
        SelectedState.Self.SelectedCircle = circle;

        var ctrl = new PreviewControl();
        ctrl.SimulateShapeDrag(10f, 5f);

        Assert.Equal(10f, circle.X, precision: 3);
        Assert.Equal(5f,  circle.Y, precision: 3);
    }

    /// <summary>
    /// Dragging an unselected circle with non-zero starting position still
    /// applies the delta correctly (starting from the existing position).
    /// </summary>
    [AvaloniaFact]
    public void DragCircle_NonZeroStart_AppliesDeltaFromStart()
    {
        ResetSingletons();
        var circle = new CircleSave { X = 20f, Y = -10f, Radius = 8f };
        var frame  = MakeFrame(circle: circle);
        SelectedState.Self.SelectedFrame  = frame;
        SelectedState.Self.SelectedCircle = circle;

        var ctrl = new PreviewControl();
        ctrl.SimulateShapeDrag(-5f, 3f);

        Assert.Equal(15f, circle.X, precision: 3);
        Assert.Equal(-7f, circle.Y, precision: 3);
    }

    // ── Rectangle drag ────────────────────────────────────────────────────────

    /// <summary>
    /// Dragging a selected rectangle by (-3, +7) updates AxisAlignedRectangleSave.X/Y.
    /// </summary>
    [AvaloniaFact]
    public void DragRectangle_UpdatesXY()
    {
        ResetSingletons();
        var rect  = new AxisAlignedRectangleSave { X = 0f, Y = 0f, ScaleX = 16f, ScaleY = 16f };
        var frame = MakeFrame(rect: rect);
        SelectedState.Self.SelectedFrame     = frame;
        SelectedState.Self.SelectedRectangle = rect;

        var ctrl = new PreviewControl();
        ctrl.SimulateShapeDrag(-3f, 7f);

        Assert.Equal(-3f, rect.X, precision: 3);
        Assert.Equal(7f,  rect.Y, precision: 3);
    }

    // ── Undo ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// After committing a drag, undo restores the original X/Y.
    /// </summary>
    [AvaloniaFact]
    public void DragCircle_AfterRelease_UndoRestoresPosition()
    {
        ResetSingletons();
        var circle = new CircleSave { X = 0f, Y = 0f, Radius = 10f };
        var frame  = MakeFrame(circle: circle);
        SelectedState.Self.SelectedFrame  = frame;
        SelectedState.Self.SelectedCircle = circle;

        var ctrl = new PreviewControl();
        ctrl.SimulateShapeDrag(20f, 10f);

        Assert.Equal(20f, circle.X, precision: 3);
        Assert.True(UndoManager.Self.CanUndo);

        UndoManager.Self.Undo();

        Assert.Equal(0f, circle.X, precision: 3);
        Assert.Equal(0f, circle.Y, precision: 3);
    }

    /// <summary>
    /// A zero-delta drag must NOT push an undo entry.
    /// </summary>
    [AvaloniaFact]
    public void DragCircle_ZeroDelta_DoesNotRecordUndo()
    {
        ResetSingletons();
        var circle = new CircleSave { X = 5f, Y = 5f, Radius = 10f };
        var frame  = MakeFrame(circle: circle);
        SelectedState.Self.SelectedFrame  = frame;
        SelectedState.Self.SelectedCircle = circle;

        var ctrl = new PreviewControl();
        ctrl.SimulateShapeDrag(0f, 0f);

        Assert.False(UndoManager.Self.CanUndo);
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Dragging a shape re-fires SelectionChanged (via CommitShapeDrag re-assigning
    /// the selection), so the property panel picks up the updated position.
    /// </summary>
    [AvaloniaFact]
    public void DragCircle_OnCommit_RaisesSelectionChanged()
    {
        ResetSingletons();
        var circle = new CircleSave { X = 0f, Y = 0f, Radius = 10f };
        var frame  = MakeFrame(circle: circle);
        SelectedState.Self.SelectedFrame  = frame;
        SelectedState.Self.SelectedCircle = circle;

        bool changed = false;
        SelectedState.Self.SelectionChanged += () => changed = true;

        var ctrl = new PreviewControl();
        ctrl.SimulateShapeDrag(1f, 0f);

        Assert.True(changed, "SelectionChanged must be raised on commit so the property panel refreshes.");
    }

    // ── No-op when no frame ───────────────────────────────────────────────────

    /// <summary>
    /// When no frame is selected, SimulateShapeDrag is a no-op (no exception, no undo entry).
    /// </summary>
    [AvaloniaFact]
    public void SimulateShapeDrag_NoFrameSelected_IsNoOp()
    {
        ResetSingletons();
        // SelectedFrame is null
        var ctrl = new PreviewControl();
        ctrl.SimulateShapeDrag(10f, 10f); // must not throw

        Assert.False(UndoManager.Self.CanUndo);
    }

    // ── Non-default zoom/pan/offsetMultiplier ─────────────────────────────────

    /// <summary>
    /// SimulateShapeDrag applies the world-space delta directly, independent of zoom
    /// or OffsetMultiplier — those only affect the coordinate conversion from screen
    /// deltas, not from world deltas.
    /// </summary>
    [AvaloniaFact]
    public void DragCircle_NonDefaultZoomAndOffset_AppliesDeltaCorrectly()
    {
        ResetSingletons();
        AppState.Self.OffsetMultiplier = 2f;

        var circle = new CircleSave { X = 10f, Y = 0f, Radius = 5f };
        var frame  = MakeFrame(circle: circle);
        SelectedState.Self.SelectedFrame  = frame;
        SelectedState.Self.SelectedCircle = circle;

        var ctrl = new PreviewControl();
        ctrl.SetZoomPercent(300); // 3× zoom
        ctrl.SetPan(50f, -20f);

        ctrl.SimulateShapeDrag(5f, -3f);

        Assert.Equal(15f, circle.X, precision: 3);
        Assert.Equal(-3f, circle.Y, precision: 3);
    }
}
