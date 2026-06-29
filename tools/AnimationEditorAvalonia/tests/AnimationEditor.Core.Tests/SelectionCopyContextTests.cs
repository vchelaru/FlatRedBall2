using AnimationEditor.Core.IO;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class SelectionCopyContextTests
{
    private static TestServices Ctx() => TestHelpers.SetupFreshAcls();

    [Fact]
    public void TryGet_EmptySelection_Fails()
    {
        var ctx = Ctx();
        bool ok = SelectionCopyContext.TryGet(
            ctx.SelectedState, ctx.ObjectFinder, ctx.Acls,
            out _, out var message);
        Assert.False(ok);
        Assert.NotNull(message);
    }

    [Fact]
    public void TryGet_SingleChain_Succeeds()
    {
        var ctx = Ctx();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedNodes = new List<object> { chain };

        Assert.True(SelectionCopyContext.TryGet(
            ctx.SelectedState, ctx.ObjectFinder, ctx.Acls,
            out var payload, out _));
        Assert.Equal(CopySelectionKind.Chain, payload.Kind);
        Assert.Single(payload.Chains);
        Assert.Same(chain, payload.Chains[0]);
    }

    [Fact]
    public void TryGet_MultipleChains_SortedByProjectIndex()
    {
        var ctx = Ctx();
        var walk = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var run  = TestHelpers.MakeChain(ctx.Acls, "Run");
        var idle = TestHelpers.MakeChain(ctx.Acls, "Idle");
        ctx.SelectedState.SelectedChain = idle;
        ctx.SelectedState.SelectedNodes = new List<object> { idle, walk, run };

        Assert.True(SelectionCopyContext.TryGet(
            ctx.SelectedState, ctx.ObjectFinder, ctx.Acls,
            out var payload, out _));

        Assert.Equal(3, payload.Chains.Count);
        Assert.Same(walk, payload.Chains[0]);
        Assert.Same(run,  payload.Chains[1]);
        Assert.Same(idle, payload.Chains[2]);
    }

    [Fact]
    public void TryGet_FramesSameChain_SortedByFrameIndex()
    {
        var ctx = Ctx();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 4);
        var f0 = chain.Frames[0];
        var f2 = chain.Frames[2];
        var f3 = chain.Frames[3];
        ctx.SelectedState.SelectedFrame = f3;
        ctx.SelectedState.SelectedNodes = new List<object> { f3, f0, f2 };

        Assert.True(SelectionCopyContext.TryGet(
            ctx.SelectedState, ctx.ObjectFinder, ctx.Acls,
            out var payload, out _));

        Assert.Equal(CopySelectionKind.Frame, payload.Kind);
        Assert.Equal(new[] { f0, f2, f3 }, payload.Frames);
    }

    [Fact]
    public void TryGet_FramesCrossChain_SortedByChainThenFrameIndex()
    {
        var ctx = Ctx();
        var walk = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var run  = TestHelpers.MakeChain(ctx.Acls, "Run", 2);
        var walkF1 = walk.Frames[1];
        var runF0  = run.Frames[0];
        ctx.SelectedState.SelectedFrame = runF0;
        ctx.SelectedState.SelectedNodes = new List<object> { runF0, walkF1 };

        Assert.True(SelectionCopyContext.TryGet(
            ctx.SelectedState, ctx.ObjectFinder, ctx.Acls,
            out var payload, out _));

        Assert.Equal(new[] { walkF1, runF0 }, payload.Frames);
    }

    [Fact]
    public void TryGet_SelectedNodesEmpty_FallsBackToSelectedFrame()
    {
        var ctx = Ctx();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        ctx.SelectedState.SelectedFrame = frame;
        ctx.SelectedState.SelectedNodes = new List<object>();

        Assert.True(SelectionCopyContext.TryGet(
            ctx.SelectedState, ctx.ObjectFinder, ctx.Acls,
            out var payload, out _));
        Assert.Single(payload.Frames);
        Assert.Same(frame, payload.Frames[0]);
    }

    [Fact]
    public void TryGet_MixedRectsAndCirclesSameFrame_Succeeds()
    {
        var ctx = Ctx();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rect   = new AARectSave { Name = "R" };
        var circle = new CircleSave { Name = "C" };
        frame.ShapesSave!.Shapes.Add(rect);
        frame.ShapesSave.Shapes.Add(circle);
        ctx.SelectedState.SelectedRectangle = rect;
        ctx.SelectedState.SelectedNodes = new List<object> { circle, rect };

        Assert.True(SelectionCopyContext.TryGet(
            ctx.SelectedState, ctx.ObjectFinder, ctx.Acls,
            out var payload, out _));

        Assert.Equal(CopySelectionKind.Shape, payload.Kind);
        Assert.Equal(2, payload.Shapes.Count);
    }

    [Fact]
    public void TryGet_ChainAndFrame_FailsWithMixedMessage()
    {
        var ctx = Ctx();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        ctx.SelectedState.SelectedFrame = frame;
        ctx.SelectedState.SelectedNodes = new List<object> { chain, frame };

        Assert.False(SelectionCopyContext.TryGet(
            ctx.SelectedState, ctx.ObjectFinder, ctx.Acls,
            out _, out var message));
        Assert.Equal(SelectionCopyContext.MixedSelectionMessage, message);
    }

    [Fact]
    public void TryGet_FrameAndShape_FailsWithMixedMessage()
    {
        var ctx = Ctx();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "R" };
        frame.ShapesSave!.Shapes.Add(rect);
        ctx.SelectedState.SelectedRectangle = rect;
        ctx.SelectedState.SelectedNodes = new List<object> { frame, rect };

        Assert.False(SelectionCopyContext.TryGet(
            ctx.SelectedState, ctx.ObjectFinder, ctx.Acls,
            out _, out var message));
        Assert.Equal(SelectionCopyContext.MixedSelectionMessage, message);
    }

    [Fact]
    public void TryGet_ShapesOnDifferentFrames_FailsWithMixedMessage()
    {
        var ctx = Ctx();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var rect0 = new AARectSave { Name = "R0" };
        var rect1 = new AARectSave { Name = "R1" };
        chain.Frames[0].ShapesSave!.Shapes.Add(rect0);
        chain.Frames[1].ShapesSave!.Shapes.Add(rect1);
        ctx.SelectedState.SelectedRectangle = rect0;
        ctx.SelectedState.SelectedNodes = new List<object> { rect0, rect1 };

        Assert.False(SelectionCopyContext.TryGet(
            ctx.SelectedState, ctx.ObjectFinder, ctx.Acls,
            out _, out var message));
        Assert.Equal(SelectionCopyContext.MixedSelectionMessage, message);
    }
}
