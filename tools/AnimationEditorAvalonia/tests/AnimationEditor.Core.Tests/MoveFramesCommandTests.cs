using AnimationEditor.Core;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Drag-and-drop frame move command: within-chain reorder, cross-chain move, gap squash,
/// and single-undo semantics (issue #500).
/// </summary>
[Collection("SequentialSingletons")]
public class MoveFramesCommandTests
{
    [Fact]
    public void MoveFrames_WithinChain_MultiSelectSquashesGapsAndPreservesOrder()
    {
        // Chain frames 0..5; move {1,3,4} to the end → 0,2,5,1,3,4 (block squashed, order kept).
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 6);
        var f = chain.Frames.ToList();
        var selected = new[] { f[1], f[3], f[4] };

        ctx.AppCommands.MoveFrames(selected, chain, chain, insertIndex: 6);

        Assert.Equal(new[] { f[0], f[2], f[5], f[1], f[3], f[4] }, chain.Frames.ToArray());
    }

    [Fact]
    public void MoveFrames_WithinChain_OneUndoRestoresOriginalOrder()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 4);
        var original = chain.Frames.ToArray();

        // Move frame 0 to the end.
        ctx.AppCommands.MoveFrames(new[] { original[0] }, chain, chain, insertIndex: 4);
        Assert.Equal(new[] { original[1], original[2], original[3], original[0] }, chain.Frames.ToArray());

        ctx.UndoManager.Undo();
        Assert.Equal(original, chain.Frames.ToArray());
    }

    [Fact]
    public void MoveFrames_CrossChain_RemovesFromSourceInsertsIntoTargetOneUndoRestoresBoth()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var walk = TestHelpers.MakeChain(ctx.Acls, "Walk", 3);
        var run = TestHelpers.MakeChain(ctx.Acls, "Run", 2);
        var walkOriginal = walk.Frames.ToArray();
        var runOriginal = run.Frames.ToArray();
        var moving = new[] { walkOriginal[0], walkOriginal[2] };

        // Insert the two moved frames at the front of Run.
        ctx.AppCommands.MoveFrames(moving, walk, run, insertIndex: 0);

        Assert.Equal(new[] { walkOriginal[1] }, walk.Frames.ToArray());
        Assert.Equal(new[] { walkOriginal[0], walkOriginal[2], runOriginal[0], runOriginal[1] }, run.Frames.ToArray());

        ctx.UndoManager.Undo();
        Assert.Equal(walkOriginal, walk.Frames.ToArray());
        Assert.Equal(runOriginal, run.Frames.ToArray());
    }

    [Fact]
    public void MoveFrames_CrossChain_SelectsMovedFramesAfterMove()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var walk = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var run = TestHelpers.MakeChain(ctx.Acls, "Run", 1);
        var moving = walk.Frames.ToArray();

        ctx.AppCommands.MoveFrames(moving, walk, run, insertIndex: 1);

        Assert.Equal(moving.Cast<object>().ToList(), ctx.SelectedState.SelectedNodes);
    }

    [Fact]
    public void MoveFrames_WithinChain_DropAtSamePosition_NoUndoEntry()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 3);
        var f = chain.Frames.ToArray();

        // Upper half of frame 1, moving frame 1 — lands in its own slot → no change.
        ctx.AppCommands.MoveFrames(new[] { f[1] }, chain, chain, insertIndex: 1);

        Assert.Equal(f, chain.Frames.ToArray());
        Assert.False(ctx.UndoManager.CanUndo);
    }
}
