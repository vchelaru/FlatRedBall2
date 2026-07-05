using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AppCommandsReorderTests
{
    // ── HandleReorder — chain selected ───────────────────────────────────────

    [Fact]
    public void HandleReorder_ChainSelected_DeltaPos1_MovesChainDown()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var walk = TestHelpers.MakeChain(acls, "Walk");
        var run  = TestHelpers.MakeChain(acls, "Run");
        ctx.SelectedState.SelectedChain = walk;

        ctx.AppCommands.HandleReorder(+1);

        Assert.Equal(run,  acls.AnimationChains[0]);
        Assert.Equal(walk, acls.AnimationChains[1]);
    }

    [Fact]
    public void HandleReorder_ChainSelected_DeltaNeg1_MovesChainUp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var walk = TestHelpers.MakeChain(acls, "Walk");
        var run  = TestHelpers.MakeChain(acls, "Run");
        ctx.SelectedState.SelectedChain = run;

        ctx.AppCommands.HandleReorder(-1);

        Assert.Equal(run,  acls.AnimationChains[0]);
        Assert.Equal(walk, acls.AnimationChains[1]);
    }

    [Fact]
    public void HandleReorder_ChainSelected_AtTop_DeltaNeg1_IsNoOp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var walk = TestHelpers.MakeChain(acls, "Walk");
        TestHelpers.MakeChain(acls, "Run");
        ctx.SelectedState.SelectedChain = walk;

        ctx.AppCommands.HandleReorder(-1);

        Assert.Equal(walk, acls.AnimationChains[0]);
    }

    // ── HandleReorder — multiple chains selected (issue #566) ────────────────

    [Fact]
    public void HandleReorder_MultiChainContiguous_DeltaPos1_MovesBlockDown()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var c = ctx.Acls;
        TestHelpers.MakeChain(c, "A");
        TestHelpers.MakeChain(c, "B");
        TestHelpers.MakeChain(c, "C");
        TestHelpers.MakeChain(c, "D");
        var chains = c.AnimationChains.ToArray();
        ctx.SelectedState.SelectedChain = chains[0];
        ctx.SelectedState.SelectedNodes = new List<object> { chains[0], chains[1] };

        ctx.AppCommands.HandleReorder(+1);

        Assert.Equal(new[] { chains[2], chains[0], chains[1], chains[3] }, c.AnimationChains.ToArray());
    }

    [Fact]
    public void HandleReorder_MultiChainContiguous_DeltaNeg1_MovesBlockUp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var c = ctx.Acls;
        TestHelpers.MakeChain(c, "A");
        TestHelpers.MakeChain(c, "B");
        TestHelpers.MakeChain(c, "C");
        TestHelpers.MakeChain(c, "D");
        var chains = c.AnimationChains.ToArray();
        ctx.SelectedState.SelectedChain = chains[2];
        ctx.SelectedState.SelectedNodes = new List<object> { chains[2], chains[3] };

        ctx.AppCommands.HandleReorder(-1);

        Assert.Equal(new[] { chains[0], chains[2], chains[3], chains[1] }, c.AnimationChains.ToArray());
    }

    [Fact]
    public void HandleReorder_MultiChainNonContiguous_DeltaPos1_PreservesGaps()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var c = ctx.Acls;
        for (int i = 0; i < 6; i++)
            TestHelpers.MakeChain(c, $"Chain{i}");
        var chains = c.AnimationChains.ToArray();
        ctx.SelectedState.SelectedChain = chains[0];
        ctx.SelectedState.SelectedNodes = new List<object> { chains[0], chains[2], chains[4] };

        ctx.AppCommands.HandleReorder(+1);

        // 0,2,4 → 1,3,5; gaps preserved, non-selected fill remaining slots in order.
        Assert.Equal(
            new[] { chains[1], chains[0], chains[3], chains[2], chains[5], chains[4] },
            c.AnimationChains.ToArray());
    }

    [Fact]
    public void HandleReorder_MultiChainAtBottom_DeltaPos1_IsNoOp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var c = ctx.Acls;
        TestHelpers.MakeChain(c, "A");
        TestHelpers.MakeChain(c, "B");
        TestHelpers.MakeChain(c, "C");
        TestHelpers.MakeChain(c, "D");
        var chains = c.AnimationChains.ToArray();
        ctx.SelectedState.SelectedChain = chains[2];
        ctx.SelectedState.SelectedNodes = new List<object> { chains[2], chains[3] };

        ctx.AppCommands.HandleReorder(+1);

        // Bottommost selected chain is already at the end — whole group is clamped.
        Assert.Equal(chains, c.AnimationChains.ToArray());
    }

    [Fact]
    public void HandleReorder_MultiChain_SingleUndo_RevertsWholeMove()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var c = ctx.Acls;
        for (int i = 0; i < 6; i++)
            TestHelpers.MakeChain(c, $"Chain{i}");
        var chains = c.AnimationChains.ToArray();
        ctx.SelectedState.SelectedChain = chains[0];
        ctx.SelectedState.SelectedNodes = new List<object> { chains[0], chains[2], chains[4] };

        ctx.AppCommands.HandleReorder(+1);
        ctx.UndoManager.Undo();

        Assert.Equal(chains, c.AnimationChains.ToArray());
    }

    [Fact]
    public void HandleReorder_SingleChainSelectedInNodesToo_UsesSingleChainMove()
    {
        // SelectedChains resolves to exactly one chain — must still go through the
        // single-chain MoveChain path, not MoveChainsRelative.
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var walk = TestHelpers.MakeChain(acls, "Walk");
        var run = TestHelpers.MakeChain(acls, "Run");
        ctx.SelectedState.SelectedChain = walk;
        ctx.SelectedState.SelectedNodes = new List<object> { walk };

        ctx.AppCommands.HandleReorder(+1);

        Assert.Equal(new[] { run, walk }, acls.AnimationChains.ToArray());
    }

    // ── MoveChainToIndex ─────────────────────────────────────────────────────

    [Fact]
    public void MoveChainToIndex_EarlierPosition_MovesChainUp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var a = TestHelpers.MakeChain(acls, "A");
        var b = TestHelpers.MakeChain(acls, "B");
        var c = TestHelpers.MakeChain(acls, "C");

        // Insert C before A (index 0).
        ctx.AppCommands.MoveChainToIndex(c, 0);

        Assert.Equal(new[] { c, a, b }, acls.AnimationChains.ToArray());
    }

    [Fact]
    public void MoveChainToIndex_LaterPosition_AdjustsForRemoval()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var a = TestHelpers.MakeChain(acls, "A");
        var b = TestHelpers.MakeChain(acls, "B");
        var c = TestHelpers.MakeChain(acls, "C");

        // insertIndex 3 (after C) is interpreted against the pre-removal list; removing A
        // ahead of it shifts the landing spot left by one → A ends up last.
        ctx.AppCommands.MoveChainToIndex(a, 3);

        Assert.Equal(new[] { b, c, a }, acls.AnimationChains.ToArray());
    }

    [Fact]
    public void MoveChainToIndex_OwnIndex_IsNoOp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var a = TestHelpers.MakeChain(acls, "A");
        var b = TestHelpers.MakeChain(acls, "B");

        ctx.AppCommands.MoveChainToIndex(a, 0);

        Assert.Equal(new[] { a, b }, acls.AnimationChains.ToArray());
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void MoveChainToIndex_SingleUndo_RestoresOriginalOrder()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var a = TestHelpers.MakeChain(acls, "A");
        var b = TestHelpers.MakeChain(acls, "B");
        var c = TestHelpers.MakeChain(acls, "C");

        ctx.AppCommands.MoveChainToIndex(c, 0);
        ctx.UndoManager.Undo();

        // One undo step fully restores the prior order.
        Assert.Equal(new[] { a, b, c }, acls.AnimationChains.ToArray());
    }

    [Fact]
    public void MoveChainToIndex_Redo_ReappliesMove()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var a = TestHelpers.MakeChain(acls, "A");
        var b = TestHelpers.MakeChain(acls, "B");
        var c = TestHelpers.MakeChain(acls, "C");

        ctx.AppCommands.MoveChainToIndex(c, 0);
        ctx.UndoManager.Undo();
        ctx.UndoManager.Redo();

        Assert.Equal(new[] { c, a, b }, acls.AnimationChains.ToArray());
    }

    // ── HandleReorder — frame selected ───────────────────────────────────────

    [Fact]
    public void HandleReorder_FrameSelected_DeltaPos1_MovesFrameDown()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 3);
        var frameA = chain.Frames[0];
        var frameB = chain.Frames[1];
        ctx.SelectedState.SelectedFrame = frameA;

        ctx.AppCommands.HandleReorder(+1);

        Assert.Equal(frameB, chain.Frames[0]);
        Assert.Equal(frameA, chain.Frames[1]);
    }

    [Fact]
    public void HandleReorder_FrameSelected_DeltaNeg1_MovesFrameUp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 3);
        var frameA = chain.Frames[0];
        var frameB = chain.Frames[1];
        ctx.SelectedState.SelectedFrame = frameB;

        ctx.AppCommands.HandleReorder(-1);

        Assert.Equal(frameB, chain.Frames[0]);
        Assert.Equal(frameA, chain.Frames[1]);
    }

    [Fact]
    public void HandleReorder_NothingSelected_DoesNotThrow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        // SelectedChain and SelectedFrame are both null after SetupFreshAcls

        var ex = Record.Exception(() => ctx.AppCommands.HandleReorder(+1));

        Assert.Null(ex);
    }

    // ── HandleReorder — multiple frames selected ─────────────────────────────

    [Fact]
    public void HandleReorder_MultiFrameContiguous_DeltaPos1_MovesBlockDown()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 4);
        var f = chain.Frames.ToArray();
        ctx.SelectedState.SelectedFrame = f[0];
        ctx.SelectedState.SelectedNodes = new List<object> { f[0], f[1] };

        ctx.AppCommands.HandleReorder(+1);

        // Block {f0,f1} shifts down one; f2 fills the vacated top slot.
        Assert.Equal(new[] { f[2], f[0], f[1], f[3] }, chain.Frames.ToArray());
    }

    [Fact]
    public void HandleReorder_MultiFrameContiguous_DeltaNeg1_MovesBlockUp()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 4);
        var f = chain.Frames.ToArray();
        ctx.SelectedState.SelectedFrame = f[2];
        ctx.SelectedState.SelectedNodes = new List<object> { f[2], f[3] };

        ctx.AppCommands.HandleReorder(-1);

        Assert.Equal(new[] { f[0], f[2], f[3], f[1] }, chain.Frames.ToArray());
    }

    [Fact]
    public void HandleReorder_MultiFrameNonContiguous_DeltaPos1_PreservesGaps()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 6);
        var f = chain.Frames.ToArray();
        ctx.SelectedState.SelectedFrame = f[0];
        ctx.SelectedState.SelectedNodes = new List<object> { f[0], f[2], f[4] };

        ctx.AppCommands.HandleReorder(+1);

        // 0,2,4 → 1,3,5; gaps preserved, non-selected fill remaining slots in order.
        Assert.Equal(new[] { f[1], f[0], f[3], f[2], f[5], f[4] }, chain.Frames.ToArray());
    }

    [Fact]
    public void HandleReorder_MultiFrameNonContiguous_DeltaNeg1_PreservesGaps()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 6);
        var f = chain.Frames.ToArray();
        ctx.SelectedState.SelectedFrame = f[1];
        ctx.SelectedState.SelectedNodes = new List<object> { f[1], f[3], f[5] };

        ctx.AppCommands.HandleReorder(-1);

        // 1,3,5 → 0,2,4; gaps preserved.
        Assert.Equal(new[] { f[1], f[0], f[3], f[2], f[5], f[4] }, chain.Frames.ToArray());
    }

    [Fact]
    public void HandleReorder_MultiFrameAtBottom_DeltaPos1_IsNoOp()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 4);
        var f = chain.Frames.ToArray();
        ctx.SelectedState.SelectedFrame = f[2];
        ctx.SelectedState.SelectedNodes = new List<object> { f[2], f[3] };

        ctx.AppCommands.HandleReorder(+1);

        // Bottommost selected frame is already at the end — whole group is clamped.
        Assert.Equal(f, chain.Frames.ToArray());
    }

    [Fact]
    public void HandleReorder_MultiFrameAtTop_DeltaNeg1_IsNoOp()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 4);
        var f = chain.Frames.ToArray();
        ctx.SelectedState.SelectedFrame = f[0];
        ctx.SelectedState.SelectedNodes = new List<object> { f[0], f[1] };

        ctx.AppCommands.HandleReorder(-1);

        Assert.Equal(f, chain.Frames.ToArray());
    }

    [Fact]
    public void HandleReorder_MultiFrameNonContiguousBlockedEdge_DeltaPos1_IsNoOp()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 5);
        var f = chain.Frames.ToArray();
        ctx.SelectedState.SelectedFrame = f[0];
        ctx.SelectedState.SelectedNodes = new List<object> { f[0], f[2], f[4] };

        ctx.AppCommands.HandleReorder(+1);

        // f4 is already at the end, so the rigid group can't move; nothing shifts.
        Assert.Equal(f, chain.Frames.ToArray());
    }

    [Fact]
    public void HandleReorder_MultiFrame_SingleUndo_RevertsWholeMove()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 6);
        var f = chain.Frames.ToArray();
        ctx.SelectedState.SelectedFrame = f[0];
        ctx.SelectedState.SelectedNodes = new List<object> { f[0], f[2], f[4] };

        ctx.AppCommands.HandleReorder(+1);
        ctx.UndoManager.Undo();

        Assert.Equal(f, chain.Frames.ToArray());
    }

    [Fact]
    public void HandleReorder_MultiFrame_KeepsAllFramesSelected()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 6);
        var f = chain.Frames.ToArray();
        ctx.SelectedState.SelectedFrame = f[0];
        ctx.SelectedState.SelectedNodes = new List<object> { f[0], f[2], f[4] };

        ctx.AppCommands.HandleReorder(+1);

        Assert.Equal(new[] { f[0], f[2], f[4] }, ctx.SelectedState.SelectedFrames.ToArray());
    }

    // ── HandleReorder — rectangle selected ───────────────────────────────────

    [Fact]
    public void HandleReorder_RectangleSelected_DeltaPos1_MovesRectDown()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var acls  = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rectA = new AARectSave { Name = "A" };
        var rectB = new AARectSave { Name = "B" };
        frame.ShapesSave!.Shapes.Add(rectA);
        frame.ShapesSave!.Shapes.Add(rectB);
        ctx.SelectedState.SelectedFrame     = frame;
        ctx.SelectedState.SelectedRectangle = rectA;

        ctx.AppCommands.HandleReorder(+1);

        Assert.Equal(rectB, frame.ShapesSave.Shapes[0]);
        Assert.Equal(rectA, frame.ShapesSave.Shapes[1]);
    }

    [Fact]
    public void HandleReorder_RectangleSelected_DeltaNeg1_MovesRectUp()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var acls  = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rectA = new AARectSave { Name = "A" };
        var rectB = new AARectSave { Name = "B" };
        frame.ShapesSave!.Shapes.Add(rectA);
        frame.ShapesSave!.Shapes.Add(rectB);
        ctx.SelectedState.SelectedFrame     = frame;
        ctx.SelectedState.SelectedRectangle = rectB;

        ctx.AppCommands.HandleReorder(-1);

        Assert.Equal(rectB, frame.ShapesSave.Shapes[0]);
        Assert.Equal(rectA, frame.ShapesSave.Shapes[1]);
    }

    [Fact]
    public void HandleReorder_RectangleSelected_DoesNotReorderChain()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var acls  = ctx.Acls;
        var walk  = TestHelpers.MakeChain(acls, "Walk", 1);
        var run   = TestHelpers.MakeChain(acls, "Run", 1);
        var frame = walk.Frames[0];
        var rectA = new AARectSave { Name = "A" };
        var rectB = new AARectSave { Name = "B" };
        frame.ShapesSave!.Shapes.Add(rectA);
        frame.ShapesSave!.Shapes.Add(rectB);
        ctx.SelectedState.SelectedFrame     = frame;
        ctx.SelectedState.SelectedRectangle = rectA;

        ctx.AppCommands.HandleReorder(+1);

        // Chain order must be untouched
        Assert.Equal(walk, acls.AnimationChains[0]);
        Assert.Equal(run,  acls.AnimationChains[1]);
    }

    // ── HandleReorder — circle selected ──────────────────────────────────────

    [Fact]
    public void HandleReorder_CircleSelected_DeltaPos1_MovesCircleDown()
    {
        var ctx    = TestHelpers.SetupFreshAcls();
        var acls   = ctx.Acls;
        var chain  = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame  = chain.Frames[0];
        var circA  = new CircleSave { Name = "A" };
        var circB  = new CircleSave { Name = "B" };
        frame.ShapesSave!.Shapes.Add(circA);
        frame.ShapesSave!.Shapes.Add(circB);
        ctx.SelectedState.SelectedFrame  = frame;
        ctx.SelectedState.SelectedCircle = circA;

        ctx.AppCommands.HandleReorder(+1);

        Assert.Equal(circB, frame.ShapesSave.Shapes[0]);
        Assert.Equal(circA, frame.ShapesSave.Shapes[1]);
    }

    [Fact]
    public void HandleReorder_CircleSelected_DeltaNeg1_MovesCircleUp()
    {
        var ctx    = TestHelpers.SetupFreshAcls();
        var acls   = ctx.Acls;
        var chain  = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame  = chain.Frames[0];
        var circA  = new CircleSave { Name = "A" };
        var circB  = new CircleSave { Name = "B" };
        frame.ShapesSave!.Shapes.Add(circA);
        frame.ShapesSave!.Shapes.Add(circB);
        ctx.SelectedState.SelectedFrame  = frame;
        ctx.SelectedState.SelectedCircle = circB;

        ctx.AppCommands.HandleReorder(-1);

        Assert.Equal(circB, frame.ShapesSave.Shapes[0]);
        Assert.Equal(circA, frame.ShapesSave.Shapes[1]);
    }

    [Fact]
    public void HandleReorder_CircleSelected_DoesNotReorderChain()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var acls  = ctx.Acls;
        var walk  = TestHelpers.MakeChain(acls, "Walk", 1);
        var run   = TestHelpers.MakeChain(acls, "Run", 1);
        var frame = walk.Frames[0];
        var circA = new CircleSave { Name = "A" };
        var circB = new CircleSave { Name = "B" };
        frame.ShapesSave!.Shapes.Add(circA);
        frame.ShapesSave!.Shapes.Add(circB);
        ctx.SelectedState.SelectedFrame  = frame;
        ctx.SelectedState.SelectedCircle = circA;

        ctx.AppCommands.HandleReorder(+1);

        Assert.Equal(walk, acls.AnimationChains[0]);
        Assert.Equal(run,  acls.AnimationChains[1]);
    }

    // ── MoveShape — rect ──────────────────────────────────────────────────────

    [Fact]
    public void MoveShape_Rect_Delta1_MovesRectDown()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var acls  = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rectA = new AARectSave { Name = "A" };
        var rectB = new AARectSave { Name = "B" };
        frame.ShapesSave!.Shapes.Add(rectA);
        frame.ShapesSave!.Shapes.Add(rectB);

        ctx.AppCommands.MoveShape(rectA, frame, +1);

        Assert.Equal(rectB, frame.ShapesSave.Shapes[0]);
        Assert.Equal(rectA, frame.ShapesSave.Shapes[1]);
    }

    [Fact]
    public void MoveShape_Rect_DeltaNeg1_MovesRectUp()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var acls  = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rectA = new AARectSave { Name = "A" };
        var rectB = new AARectSave { Name = "B" };
        frame.ShapesSave!.Shapes.Add(rectA);
        frame.ShapesSave!.Shapes.Add(rectB);

        ctx.AppCommands.MoveShape(rectB, frame, -1);

        Assert.Equal(rectB, frame.ShapesSave.Shapes[0]);
        Assert.Equal(rectA, frame.ShapesSave.Shapes[1]);
    }

    [Fact]
    public void MoveShape_Rect_AtBottom_DoesNotMoveBelowEnd()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var acls  = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rectA = new AARectSave { Name = "A" };
        var rectB = new AARectSave { Name = "B" };
        frame.ShapesSave!.Shapes.Add(rectA);
        frame.ShapesSave!.Shapes.Add(rectB);

        ctx.AppCommands.MoveShape(rectB, frame, +1);

        Assert.Equal(rectA, frame.ShapesSave.Shapes[0]);
        Assert.Equal(rectB, frame.ShapesSave.Shapes[1]);
    }

    // ── MoveShape — circle ────────────────────────────────────────────────────

    [Fact]
    public void MoveShape_Circle_Delta1_MovesCircleDown()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var acls  = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        var circA = new CircleSave { Name = "A" };
        var circB = new CircleSave { Name = "B" };
        frame.ShapesSave!.Shapes.Add(circA);
        frame.ShapesSave!.Shapes.Add(circB);

        ctx.AppCommands.MoveShape(circA, frame, +1);

        Assert.Equal(circB, frame.ShapesSave.Shapes[0]);
        Assert.Equal(circA, frame.ShapesSave.Shapes[1]);
    }

    [Fact]
    public void MoveShape_Circle_DeltaNeg1_MovesCircleUp()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var acls  = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        var circA = new CircleSave { Name = "A" };
        var circB = new CircleSave { Name = "B" };
        frame.ShapesSave!.Shapes.Add(circA);
        frame.ShapesSave!.Shapes.Add(circB);

        ctx.AppCommands.MoveShape(circB, frame, -1);

        Assert.Equal(circB, frame.ShapesSave.Shapes[0]);
        Assert.Equal(circA, frame.ShapesSave.Shapes[1]);
    }

    [Fact]
    public void MoveShape_Circle_AtBottom_DoesNotMoveBelowEnd()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var acls  = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        var circA = new CircleSave { Name = "A" };
        var circB = new CircleSave { Name = "B" };
        frame.ShapesSave!.Shapes.Add(circA);
        frame.ShapesSave!.Shapes.Add(circB);

        ctx.AppCommands.MoveShape(circB, frame, +1);

        Assert.Equal(circA, frame.ShapesSave.Shapes[0]);
        Assert.Equal(circB, frame.ShapesSave.Shapes[1]);
    }

    // ── MoveShapeToBottom ─────────────────────────────────────────────────────

    [Fact]
    public void MoveShapeToBottom_FromMiddle_MovesShapeToEnd()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rectA = new AARectSave { Name = "A" };
        var rectB = new AARectSave { Name = "B" };
        var rectC = new AARectSave { Name = "C" };
        frame.ShapesSave!.Shapes.Add(rectA);
        frame.ShapesSave!.Shapes.Add(rectB);
        frame.ShapesSave!.Shapes.Add(rectC);

        ctx.AppCommands.MoveShapeToBottom(rectA, frame);

        Assert.Equal(rectB, frame.ShapesSave.Shapes[0]);
        Assert.Equal(rectC, frame.ShapesSave.Shapes[1]);
        Assert.Equal(rectA, frame.ShapesSave.Shapes[2]);
    }

    // ── MoveShapeToTop ────────────────────────────────────────────────────────

    [Fact]
    public void MoveShapeToTop_FromMiddle_MovesShapeToFront()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rectA = new AARectSave { Name = "A" };
        var rectB = new AARectSave { Name = "B" };
        var rectC = new AARectSave { Name = "C" };
        frame.ShapesSave!.Shapes.Add(rectA);
        frame.ShapesSave!.Shapes.Add(rectB);
        frame.ShapesSave!.Shapes.Add(rectC);

        ctx.AppCommands.MoveShapeToTop(rectC, frame);

        Assert.Equal(rectC, frame.ShapesSave.Shapes[0]);
        Assert.Equal(rectA, frame.ShapesSave.Shapes[1]);
        Assert.Equal(rectB, frame.ShapesSave.Shapes[2]);
    }

    [Fact]
    public void MoveShapeToTop_Undo_RestoresOriginalOrder()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rectA = new AARectSave { Name = "A" };
        var rectB = new AARectSave { Name = "B" };
        frame.ShapesSave!.Shapes.Add(rectA);
        frame.ShapesSave!.Shapes.Add(rectB);

        ctx.AppCommands.MoveShapeToTop(rectB, frame);
        ctx.UndoManager.Undo();

        Assert.Equal(rectA, frame.ShapesSave.Shapes[0]);
        Assert.Equal(rectB, frame.ShapesSave.Shapes[1]);
    }

    // ── MoveShape — cross-type ────────────────────────────────────────────────

    [Fact]
    public void MoveShape_RectPastCircle_DeltaPos1_SwapsOrder()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var acls  = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rect  = new AARectSave  { Name = "Rect" };
        var circ  = new CircleSave  { Name = "Circ" };
        frame.ShapesSave!.Shapes.Add(rect);  // index 0
        frame.ShapesSave!.Shapes.Add(circ);  // index 1

        ctx.AppCommands.MoveShape(rect, frame, +1);

        Assert.Equal(circ, frame.ShapesSave.Shapes[0]);
        Assert.Equal(rect, frame.ShapesSave.Shapes[1]);
    }

    [Fact]
    public void MoveShape_CirclePastRect_DeltaNeg1_SwapsOrder()
    {
        var ctx   = TestHelpers.SetupFreshAcls();
        var acls  = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rect  = new AARectSave  { Name = "Rect" };
        var circ  = new CircleSave  { Name = "Circ" };
        frame.ShapesSave!.Shapes.Add(rect);  // index 0
        frame.ShapesSave!.Shapes.Add(circ);  // index 1

        ctx.AppCommands.MoveShape(circ, frame, -1);

        Assert.Equal(circ, frame.ShapesSave.Shapes[0]);
        Assert.Equal(rect, frame.ShapesSave.Shapes[1]);
    }
}

