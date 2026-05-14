using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
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
    public void PasteFrames_AppendsFramesToTargetChainAndIsUndoable()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var f1 = TestHelpers.MakeFrame("a.png");
        var f2 = TestHelpers.MakeFrame("b.png");

        ctx.AppCommands.PasteFrames(chain, new List<AnimationFrameSave> { f1, f2 });

        Assert.Equal(new[] { chain.Frames[0], f1, f2 }, chain.Frames);

        ctx.UndoManager.Undo();
        Assert.Single(chain.Frames);
    }

    [Fact]
    public void PasteRectangle_AddsRectangleToFrameAndIsUndoable()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "Pasted" };

        ctx.AppCommands.PasteRectangle(frame, rect);

        Assert.Same(rect, frame.ShapesSave!.AARectSaves[0]);

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

        Assert.Same(circle, frame.ShapesSave!.CircleSaves[0]);

        ctx.UndoManager.Undo();
        Assert.Empty(frame.ShapesSave!.CircleSaves);
    }
}
