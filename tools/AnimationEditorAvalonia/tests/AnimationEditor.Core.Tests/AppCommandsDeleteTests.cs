using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Frame, shape, and animation-chain deletes all run immediately (no confirmation
/// dialog) because every one is fully undoable; the UI surfaces an undo toast via the
/// <see cref="AppCommands.ItemsDeleted"/> event instead.
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsDeleteTests
{
    // ── DeleteAnimationChains ─────────────────────────────────────────────────

    [Fact]
    public void DeleteAnimationChains_DeletesImmediately()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chainA = TestHelpers.MakeChain(acls, "A");
        TestHelpers.MakeChain(acls, "B");
        var chainC = TestHelpers.MakeChain(acls, "C");

        ctx.AppCommands.DeleteAnimationChains(new List<AnimationChainSave> { chainA, chainC });

        Assert.Single(acls.AnimationChains);
        Assert.Equal("B", acls.AnimationChains[0].Name);
    }

    [Fact]
    public void DeleteAnimationChains_DeletingMultiple_RecordsSingleUndoEntry()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var a = TestHelpers.MakeChain(ctx.Acls, "A");
        var b = TestHelpers.MakeChain(ctx.Acls, "B");

        ctx.AppCommands.DeleteAnimationChains(new List<AnimationChainSave> { a, b });
        Assert.Empty(ctx.Acls.AnimationChains);

        // One user action is one undo step, and the single undo restores both chains.
        ctx.UndoManager.Undo();
        Assert.Equal(new[] { a, b }, ctx.Acls.AnimationChains);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void DeleteAnimationChains_FiresItemsDeleted_MultipleLabel()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var a = TestHelpers.MakeChain(ctx.Acls, "A");
        var b = TestHelpers.MakeChain(ctx.Acls, "B");
        string? label = null;
        ctx.AppCommands.ItemsDeleted += l => label = l;

        ctx.AppCommands.DeleteAnimationChains(new List<AnimationChainSave> { a, b });

        Assert.Equal("2 animations", label);
    }

    [Fact]
    public void DeleteAnimationChains_FiresItemsDeleted_SingleLabelIsChainName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        string? label = null;
        ctx.AppCommands.ItemsDeleted += l => label = l;

        ctx.AppCommands.DeleteAnimationChains(new List<AnimationChainSave> { chain });

        Assert.Equal("Walk", label);
    }

    // ── DeleteFrames ──────────────────────────────────────────────────────────

    [Fact]
    public void DeleteFrames_DeletesFrameFromSelectedChain()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", 3);
        ctx.SelectedState.SelectedChain = chain;
        var frameToDelete = chain.Frames[1];

        ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { frameToDelete });

        Assert.Equal(2, chain.Frames.Count);
        Assert.DoesNotContain(frameToDelete, chain.Frames);
    }

    [Fact]
    public void DeleteFrames_FiresItemsDeleted_MultiFrameLabel()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", 4);
        ctx.SelectedState.SelectedChain = chain;
        string? capturedLabel = null;
        ctx.AppCommands.ItemsDeleted += label => capturedLabel = label;
        var toDelete = new List<AnimationFrameSave> { chain.Frames[0], chain.Frames[2] };

        ctx.AppCommands.DeleteFrames(toDelete);

        Assert.Equal("2 frames", capturedLabel);
    }

    [Fact]
    public void DeleteFrames_FiresItemsDeleted_SingleFrameLabel()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", 3);
        ctx.SelectedState.SelectedChain = chain;
        string? capturedLabel = null;
        ctx.AppCommands.ItemsDeleted += label => capturedLabel = label;

        ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { chain.Frames[0] });

        Assert.Equal("Frame 1", capturedLabel);
    }

    [Fact]
    public void DeleteFrames_RecordsUndoCommand()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", 2);
        ctx.SelectedState.SelectedChain = chain;

        ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { chain.Frames[0] });

        Assert.True(ctx.UndoManager.CanUndo);
    }

    // ── DeleteShapes ──────────────────────────────────────────────────────────

    [Fact]
    public void DeleteShapes_DeletesImmediately_AndIsUndoable()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "Hitbox" };
        var circle = new CircleSave { Name = "Hurtbox", Radius = 10 };
        frame.ShapesSave!.Shapes.Add(rect);
        frame.ShapesSave!.Shapes.Add(circle);

        ctx.AppCommands.DeleteShapes(frame,
            new List<AARectSave> { rect }, new List<CircleSave> { circle });

        Assert.Empty(frame.ShapesSave!.AARectSaves);
        Assert.Empty(frame.ShapesSave!.CircleSaves);

        ctx.UndoManager.Undo();
        Assert.Equal(new[] { rect }, frame.ShapesSave!.AARectSaves);
        Assert.Equal(new[] { circle }, frame.ShapesSave!.CircleSaves);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void DeleteShapes_FiresItemsDeleted_MultipleLabel()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "Hitbox" };
        var circle = new CircleSave { Name = "Hurtbox", Radius = 10 };
        frame.ShapesSave!.Shapes.Add(rect);
        frame.ShapesSave!.Shapes.Add(circle);
        string? label = null;
        ctx.AppCommands.ItemsDeleted += l => label = l;

        ctx.AppCommands.DeleteShapes(frame,
            new List<AARectSave> { rect }, new List<CircleSave> { circle });

        Assert.Equal("2 shapes", label);
    }

    [Fact]
    public void DeleteShapes_FiresItemsDeleted_SingleLabelIsShapeName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "BodyCollision" };
        frame.ShapesSave!.Shapes.Add(rect);
        string? label = null;
        ctx.AppCommands.ItemsDeleted += l => label = l;

        ctx.AppCommands.DeleteShapes(frame,
            new List<AARectSave> { rect }, new List<CircleSave>());

        Assert.Equal("BodyCollision", label);
    }
}
