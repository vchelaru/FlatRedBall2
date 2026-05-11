using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using FlatRedBall.Content.AnimationChain;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class DeleteFramesUndoTests
{
    // ── AskToDeleteFrames + Undo ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteFrames_Undo_RestoresFrameAtOriginalIndex()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", frameCount: 3);
        ctx.SelectedState.SelectedChain = chain;
        var middle = chain.Frames[1];

        await ctx.AppCommands.AskToDeleteFrames(new List<AnimationFrameSave> { middle });
        Assert.Equal(2, chain.Frames.Count);

        ctx.UndoManager.Undo();

        Assert.Equal(3, chain.Frames.Count);
        Assert.Same(middle, chain.Frames[1]);
    }

    [Fact]
    public async Task DeleteFrames_UndoThenRedo_RemovesFrameAgain()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", frameCount: 2);
        ctx.SelectedState.SelectedChain = chain;
        var first = chain.Frames[0];

        await ctx.AppCommands.AskToDeleteFrames(new List<AnimationFrameSave> { first });
        ctx.UndoManager.Undo();
        Assert.Equal(2, chain.Frames.Count);

        ctx.UndoManager.Redo();

        Assert.Single(chain.Frames);
        Assert.DoesNotContain(first, chain.Frames);
    }

    [Fact]
    public async Task DeleteMultipleFrames_Undo_RestoresAllAtOriginalPositions()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", frameCount: 4);
        ctx.SelectedState.SelectedChain = chain;
        var f0 = chain.Frames[0];
        var f2 = chain.Frames[2];

        await ctx.AppCommands.AskToDeleteFrames(new List<AnimationFrameSave> { f0, f2 });
        Assert.Equal(2, chain.Frames.Count);

        ctx.UndoManager.Undo();

        Assert.Equal(4, chain.Frames.Count);
        Assert.Same(f0, chain.Frames[0]);
        Assert.Same(f2, chain.Frames[2]);
    }
}
