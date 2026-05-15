using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
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

