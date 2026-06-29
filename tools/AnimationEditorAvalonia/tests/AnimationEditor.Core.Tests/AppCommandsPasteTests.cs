using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// The Paste* commands take already-built (clipboard-deserialized) chains, frames,
/// or shapes and add them to the project through the undo stack — paste must be
/// undoable like every other mutation.
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsPasteTests
{
    [Fact]
    public void PasteChains_InsertsChainsAndIsUndoable()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        TestHelpers.MakeChain(ctx.Acls, "Existing");
        var pasted = new AnimationChainSave { Name = "Existing" }; // collides → renamed on paste

        ctx.AppCommands.PasteChains(new List<AnimationChainSave> { pasted });

        Assert.Equal(2, ctx.Acls.AnimationChains.Count);
        Assert.Contains(pasted, ctx.Acls.AnimationChains);

        ctx.UndoManager.Undo();
        Assert.Single(ctx.Acls.AnimationChains);
        Assert.DoesNotContain(pasted, ctx.Acls.AnimationChains);

        ctx.UndoManager.Redo();
        Assert.Contains(pasted, ctx.Acls.AnimationChains);
    }

    [Fact]
    public void PasteFrames_WithoutInsertIndex_AppendsToTargetChainAndIsUndoable()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var f1 = TestHelpers.MakeFrame("a.png");
        var f2 = TestHelpers.MakeFrame("b.png");

        ctx.AppCommands.PasteFrames(chain, new List<AnimationFrameSave> { f1, f2 });

        Assert.Equal(new[] { "frame0.png", "a.png", "b.png" },
            chain.Frames.Select(f => f.TextureName));
        Assert.DoesNotContain(f1, chain.Frames);
        Assert.DoesNotContain(f2, chain.Frames);

        ctx.UndoManager.Undo();
        Assert.Equal(new[] { "frame0.png" }, chain.Frames.Select(f => f.TextureName));
    }

    [Fact]
    public void PasteFrames_WithInsertIndex_InsertsAfterSelectionInOrderAndIsUndoable()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 3);
        var p1 = TestHelpers.MakeFrame("a.png");
        var p2 = TestHelpers.MakeFrame("b.png");

        // Selected frame is f1 (index 1) → pasted frames land at indices 2,3, keeping order.
        ctx.AppCommands.PasteFrames(chain, new List<AnimationFrameSave> { p1, p2 }, insertIndex: 2);

        Assert.Equal(new[] { "frame0.png", "frame1.png", "a.png", "b.png", "frame2.png" },
            chain.Frames.Select(f => f.TextureName));

        ctx.UndoManager.Undo();
        Assert.Equal(new[] { "frame0.png", "frame1.png", "frame2.png" },
            chain.Frames.Select(f => f.TextureName));

        ctx.UndoManager.Redo();
        Assert.Equal(new[] { "frame0.png", "frame1.png", "a.png", "b.png", "frame2.png" },
            chain.Frames.Select(f => f.TextureName));
    }

    [Fact]
    public void PasteRectangle_AddsRectangleToFrameAndIsUndoable()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "Pasted" };

        ctx.AppCommands.PasteRectangle(frame, rect);

        Assert.Equal("Pasted", frame.ShapesSave!.AARectSaves.First().Name);

        ctx.UndoManager.Undo();
        Assert.Empty(frame.ShapesSave!.AARectSaves);
    }

    [Fact]
    public void PasteCircle_AddsCircleToFrameAndIsUndoable()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        var circle = new CircleSave { Name = "Pasted", Radius = 3 };

        ctx.AppCommands.PasteCircle(frame, circle);

        Assert.Equal("Pasted", frame.ShapesSave!.CircleSaves.First().Name);

        ctx.UndoManager.Undo();
        Assert.Empty(frame.ShapesSave!.CircleSaves);
    }
}
