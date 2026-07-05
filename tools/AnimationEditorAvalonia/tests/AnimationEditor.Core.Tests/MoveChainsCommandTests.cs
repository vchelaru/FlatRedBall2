using AnimationEditor.Core;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Drag-and-drop multi-chain move command: gap squash and single-undo semantics
/// (issue #566), parallel to <see cref="MoveFramesCommandTests"/> for frames.
/// </summary>
[Collection("SequentialSingletons")]
public class MoveChainsCommandTests
{
    [Fact]
    public void MoveChainsToIndex_MultiSelectSquashesGapsAndPreservesOrder()
    {
        // Chains 0..5; move {1,3,4} to the end → 0,2,5,1,3,4 (block squashed, order kept).
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        for (int i = 0; i < 6; i++)
            TestHelpers.MakeChain(acls, $"Chain{i}");
        var chains = acls.AnimationChains.ToArray();
        var selected = new[] { chains[1], chains[3], chains[4] };

        ctx.AppCommands.MoveChainsToIndex(selected, insertIndex: 6);

        Assert.Equal(
            new[] { chains[0], chains[2], chains[5], chains[1], chains[3], chains[4] },
            acls.AnimationChains.ToArray());
    }

    [Fact]
    public void MoveChainsToIndex_OneUndoRestoresOriginalOrder()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        for (int i = 0; i < 4; i++)
            TestHelpers.MakeChain(acls, $"Chain{i}");
        var original = acls.AnimationChains.ToArray();

        // Move chain 0 to the end.
        ctx.AppCommands.MoveChainsToIndex(new[] { original[0] }, insertIndex: 4);
        Assert.Equal(new[] { original[1], original[2], original[3], original[0] }, acls.AnimationChains.ToArray());

        ctx.UndoManager.Undo();
        Assert.Equal(original, acls.AnimationChains.ToArray());
    }

    [Fact]
    public void MoveChainsToIndex_Redo_ReappliesMove()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        for (int i = 0; i < 4; i++)
            TestHelpers.MakeChain(acls, $"Chain{i}");
        var original = acls.AnimationChains.ToArray();

        ctx.AppCommands.MoveChainsToIndex(new[] { original[0] }, insertIndex: 4);
        ctx.UndoManager.Undo();
        ctx.UndoManager.Redo();

        Assert.Equal(new[] { original[1], original[2], original[3], original[0] }, acls.AnimationChains.ToArray());
    }

    [Fact]
    public void MoveChainsToIndex_DropAtSamePosition_NoUndoEntry()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        for (int i = 0; i < 3; i++)
            TestHelpers.MakeChain(acls, $"Chain{i}");
        var chains = acls.AnimationChains.ToArray();

        // Upper half of chain 1, moving chain 1 — lands in its own slot → no change.
        ctx.AppCommands.MoveChainsToIndex(new[] { chains[1] }, insertIndex: 1);

        Assert.Equal(chains, acls.AnimationChains.ToArray());
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void MoveChainsToIndex_NonContiguousSelection_LaterPosition_AdjustsForRemoval()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        for (int i = 0; i < 5; i++)
            TestHelpers.MakeChain(acls, $"Chain{i}");
        var chains = acls.AnimationChains.ToArray();

        // Move {0,2} to insertIndex 5 (end, pre-removal) → lands after the rest, in order.
        ctx.AppCommands.MoveChainsToIndex(new[] { chains[0], chains[2] }, insertIndex: 5);

        Assert.Equal(
            new[] { chains[1], chains[3], chains[4], chains[0], chains[2] },
            acls.AnimationChains.ToArray());
    }
}
