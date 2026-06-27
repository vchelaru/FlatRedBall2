using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class DeleteChainsUndoTests
{
    // ── DeleteAnimationChains + Undo ──────────────────────────────────────────

    [Fact]
    public void DeleteAnimationChains_Undo_RestoresChainsAtOriginalPositions()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chainA = TestHelpers.MakeChain(acls, "A");
        var chainB = TestHelpers.MakeChain(acls, "B");
        var chainC = TestHelpers.MakeChain(acls, "C");
        Assert.Equal(3, acls.AnimationChains.Count);

        // Delete B (index 1)
        ctx.AppCommands.DeleteAnimationChains(new List<AnimationChainSave> { chainB });
        Assert.Equal(2, acls.AnimationChains.Count);

        ctx.UndoManager.Undo();

        Assert.Equal(3, acls.AnimationChains.Count);
        Assert.Same(chainB, acls.AnimationChains[1]); // restored at original index 1
    }

    [Fact]
    public void DeleteAnimationChains_UndoThenRedo_RemovesChainAgain()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chainA = TestHelpers.MakeChain(acls, "A");
        var chainB = TestHelpers.MakeChain(acls, "B");

        ctx.AppCommands.DeleteAnimationChains(new List<AnimationChainSave> { chainA });
        ctx.UndoManager.Undo();
        Assert.Equal(2, acls.AnimationChains.Count);

        ctx.UndoManager.Redo();

        Assert.Single(acls.AnimationChains);
        Assert.DoesNotContain(chainA, acls.AnimationChains);
    }
}
