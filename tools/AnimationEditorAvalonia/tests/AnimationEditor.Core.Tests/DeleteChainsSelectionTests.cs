using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Deleting a chain must drop that chain from the selection. A stale
/// <see cref="ISelectedState.SelectedChain"/> pointing at an orphaned chain keeps
/// the preview rendering its frames. Same selection-clearing gap as issue #284.
/// </summary>
[Collection("SequentialSingletons")]
public class DeleteChainsSelectionTests
{
    [Fact]
    public void DeleteChains_ClearsSelectedChain_WhenSelectedChainIsDeleted()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 2);
        ctx.SelectedState.SelectedChain = chain;

        ctx.AppCommands.DeleteAnimationChains(new List<AnimationChainSave> { chain });

        Assert.Null(ctx.SelectedState.SelectedChain);
    }

    [Fact]
    public void DeleteChains_KeepsSelectedChain_WhenADifferentChainIsDeleted()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var first = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var keep  = TestHelpers.MakeChain(ctx.Acls, "Run");
        ctx.SelectedState.SelectedChain = keep;

        ctx.AppCommands.DeleteAnimationChains(new List<AnimationChainSave> { first });

        Assert.Same(keep, ctx.SelectedState.SelectedChain);
    }

    [Fact]
    public void DeleteChains_RemovesDeletedChainsFromMultiSelection()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var a = TestHelpers.MakeChain(ctx.Acls, "A");
        var b = TestHelpers.MakeChain(ctx.Acls, "B");
        ctx.SelectedState.SelectedNodes = new List<object> { a, b };

        ctx.AppCommands.DeleteAnimationChains(new List<AnimationChainSave> { a, b });

        Assert.Empty(ctx.SelectedState.SelectedNodes);
    }

    [Fact]
    public void DeleteChains_Redo_ClearsSelectionAgain()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        ctx.SelectedState.SelectedChain = chain;

        ctx.AppCommands.DeleteAnimationChains(new List<AnimationChainSave> { chain });
        ctx.UndoManager.Undo();

        // User re-selects the restored chain, then redoes the delete.
        ctx.SelectedState.SelectedChain = chain;
        ctx.UndoManager.Redo();

        Assert.Null(ctx.SelectedState.SelectedChain);
    }
}
