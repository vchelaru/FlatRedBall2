using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Alt+Up/Down moves every selected item: whole animations as a block in the list, and each
/// animation's selected frames as a block inside it, as one undo step. Frames of a locked
/// animation stay put (a modification); a locked animation itself still moves.
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsMixedReorderTests
{
    private static string[] Names(TestServices ctx) => ctx.Acls.AnimationChains.Select(c => c.Name).ToArray();

    private static void Select(TestServices ctx, params object[] nodes) =>
        ctx.SelectedState.SelectedNodes = new List<object>(nodes);

    [Fact]
    public void AnimationPlusFrameOfAnother_BothMoveUp_AsOneUndoStep()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        TestHelpers.MakeChain(ctx.Acls, "X", frameCount: 1);
        var a = TestHelpers.MakeChain(ctx.Acls, "A", frameCount: 2);
        var b = TestHelpers.MakeChain(ctx.Acls, "B", frameCount: 1);
        var a0 = a.Frames[0];
        var a1 = a.Frames[1];
        Select(ctx, b, a1);

        ctx.AppCommands.HandleReorder(-1);

        Assert.Equal(new[] { "X", "B", "A" }, Names(ctx));
        Assert.Equal(new[] { a1, a0 }, a.Frames);

        ctx.UndoManager.Undo();

        Assert.Equal(new[] { "X", "A", "B" }, Names(ctx));
        Assert.Equal(new[] { a0, a1 }, a.Frames);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void FrameInLockedAnimation_StaysPut_WhileTheSelectedAnimationStillMoves()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        TestHelpers.MakeChain(ctx.Acls, "X", frameCount: 1);
        var a = TestHelpers.MakeChain(ctx.Acls, "A", frameCount: 2);
        a.IsLocked = true;
        var b = TestHelpers.MakeChain(ctx.Acls, "B", frameCount: 1);
        var a0 = a.Frames[0];
        var a1 = a.Frames[1];
        Select(ctx, b, a1);

        ctx.AppCommands.HandleReorder(-1);

        Assert.Equal(new[] { "X", "B", "A" }, Names(ctx));
        Assert.Equal(new[] { a0, a1 }, a.Frames);
    }

    [Fact]
    public void LockedAnimation_StillMoves_WithAnUnlockedFramesMove()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        TestHelpers.MakeChain(ctx.Acls, "X", frameCount: 1);
        var a = TestHelpers.MakeChain(ctx.Acls, "A", frameCount: 1);
        a.IsLocked = true;
        var b = TestHelpers.MakeChain(ctx.Acls, "B", frameCount: 2);
        var b0 = b.Frames[0];
        var b1 = b.Frames[1];
        Select(ctx, a, b1);

        ctx.AppCommands.HandleReorder(-1);

        Assert.Equal(new[] { "A", "X", "B" }, Names(ctx));
        Assert.Equal(new[] { b1, b0 }, b.Frames);
    }

    [Fact]
    public void FramesInSeveralAnimations_EachMoveWithinTheirOwn()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var a = TestHelpers.MakeChain(ctx.Acls, "A", frameCount: 2);
        var b = TestHelpers.MakeChain(ctx.Acls, "B", frameCount: 2);
        var a0 = a.Frames[0];
        var a1 = a.Frames[1];
        var b0 = b.Frames[0];
        var b1 = b.Frames[1];
        Select(ctx, a1, b1);

        ctx.AppCommands.HandleReorder(-1);

        Assert.Equal(new[] { a1, a0 }, a.Frames);
        Assert.Equal(new[] { b1, b0 }, b.Frames);
        ctx.UndoManager.Undo();
        Assert.Equal(new[] { a0, a1 }, a.Frames);
        Assert.Equal(new[] { b0, b1 }, b.Frames);
    }
}
