using AnimationEditor.App.Controls;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// A locked chain (issue #1032) must be inert to Preview-panel drag gestures — it can be
/// displayed (e.g. as a multi-selected "background reference" alongside an unlocked chain being
/// edited) but dragging must never move its frames or shapes. Gating happens at drag-start, not
/// just at commit, so a locked target simply never starts following the pointer.
/// </summary>
public class PreviewLockedChainDragTests
{
    private static AnimationFrameSave MakeFrame(float relativeX, float relativeY)
    {
        return new AnimationFrameSave
        {
            FrameLength = 0.1f,
            RelativeX   = relativeX,
            RelativeY   = relativeY,
            ShapesSave  = new ShapesSave()
        };
    }

    [AvaloniaFact]
    public void SimulateFrameDrag_ChainLocked_IsNoOp()
    {
        var ctx   = TestHelpers.BuildServices();
        var frame = MakeFrame(relativeX: 0f, relativeY: 0f);
        var chain = new AnimationChainSave { Name = "Locked", IsLocked = true };
        chain.Frames.Add(frame);
        // SelectedFrame's setter re-derives SelectedChain from the project's ACLS (see
        // FindChainForFrame), so the chain must actually be in the project, not just assigned
        // directly to SelectedState, or the lock lookup can't resolve it.
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(chain);
        ctx.SelectedState.SelectedFrame = frame;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateFrameDrag(10f, 10f);

        Assert.Equal(0f, frame.RelativeX, precision: 3);
        Assert.Equal(0f, frame.RelativeY, precision: 3);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [AvaloniaFact]
    public void SimulateChainDrag_ChainLocked_IsNoOp()
    {
        var ctx    = TestHelpers.BuildServices();
        var frameA = MakeFrame(relativeX: 0f, relativeY: 0f);
        var chain  = new AnimationChainSave { Name = "Locked", IsLocked = true };
        chain.Frames.Add(frameA);
        ctx.SelectedState.SelectedChain = chain;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateChainDrag(3f, 2f);

        Assert.Equal(0f, frameA.RelativeX, precision: 3);
        Assert.Equal(0f, frameA.RelativeY, precision: 3);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [AvaloniaFact]
    public void SimulateShapeDrag_ShapeInLockedChain_IsNoOp()
    {
        var ctx    = TestHelpers.BuildServices();
        var circle = new CircleSave { X = 0f, Y = 0f, Radius = 10f };
        var frame  = MakeFrame(relativeX: 0f, relativeY: 0f);
        frame.ShapesSave!.Shapes.Add(circle);
        var chain  = new AnimationChainSave { Name = "Locked", IsLocked = true };
        chain.Frames.Add(frame);
        // See the comment in SimulateFrameDrag_ChainLocked_IsNoOp: SelectedFrame re-derives
        // SelectedChain from the project's ACLS, so the chain must be added there.
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(chain);
        ctx.SelectedState.SelectedFrame = frame;
        ctx.SelectedState.SelectedCircle = circle;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateShapeDrag(10f, 10f);

        Assert.Equal(0f, circle.X, precision: 3);
        Assert.Equal(0f, circle.Y, precision: 3);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    /// <summary>
    /// The exact reported scenario: chain A is locked and shown as a multi-selected "background
    /// reference" alongside unlocked chain B. Dragging must move only B's frames.
    /// </summary>
    [AvaloniaFact]
    public void SimulateMultiFrameDrag_SpansLockedAndUnlockedChains_MovesOnlyUnlockedChainFrames()
    {
        var ctx = TestHelpers.BuildServices();

        var lockedFrame = MakeFrame(relativeX: 0f, relativeY: 0f);
        var lockedChain = new AnimationChainSave { Name = "A", IsLocked = true };
        lockedChain.Frames.Add(lockedFrame);

        var unlockedFrame = MakeFrame(relativeX: 10f, relativeY: -5f);
        var unlockedChain = new AnimationChainSave { Name = "B" };
        unlockedChain.Frames.Add(unlockedFrame);

        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(lockedChain);
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(unlockedChain);
        ctx.SelectedState.SelectedNodes = new List<object> { lockedFrame, unlockedFrame };

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateMultiFrameDrag(3f, 2f);

        Assert.Equal(0f, lockedFrame.RelativeX, precision: 3);
        Assert.Equal(0f, lockedFrame.RelativeY, precision: 3);
        Assert.Equal(13f, unlockedFrame.RelativeX, precision: 3);
        Assert.Equal(-3f, unlockedFrame.RelativeY, precision: 3);
        Assert.True(ctx.UndoManager.CanUndo);
    }
}
