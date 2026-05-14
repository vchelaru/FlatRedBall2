using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for the async delete-with-confirmation operations in AppCommands.
/// The <see cref="AppCommands.ConfirmAsync"/> delegate is overridden per-test
/// to control whether the user "confirms" or "cancels" the dialog.
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsDeleteAsyncTests
{
    // ── AskToDeleteAnimationChains ────────────────────────────────────────────

    [Fact]
    public async Task AskToDeleteAnimationChains_WhenConfirmed_DeletesChains()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        ctx.AppCommands.ConfirmAsync = (_, __) => Task.FromResult(true);
        var chainA = TestHelpers.MakeChain(acls, "A");
        var chainB = TestHelpers.MakeChain(acls, "B");
        var chainC = TestHelpers.MakeChain(acls, "C");

        await ctx.AppCommands.AskToDeleteAnimationChains(
            new List<AnimationChainSave> { chainA, chainC });

        Assert.Single(acls.AnimationChains);
        Assert.Equal(chainB, acls.AnimationChains[0]);
    }

    [Fact]
    public async Task AskToDeleteAnimationChains_WhenCancelled_DoesNotDeleteAnyChains()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        ctx.AppCommands.ConfirmAsync = (_, __) => Task.FromResult(false);
        TestHelpers.MakeChain(acls, "A");
        TestHelpers.MakeChain(acls, "B");

        await ctx.AppCommands.AskToDeleteAnimationChains(
            new List<AnimationChainSave> { acls.AnimationChains[0] });

        Assert.Equal(2, acls.AnimationChains.Count);
    }

    [Fact]
    public async Task AskToDeleteAnimationChains_WhenConfirmed_FiresAnimationChainsChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        ctx.AppCommands.ConfirmAsync = (_, __) => Task.FromResult(true);
        var chain = TestHelpers.MakeChain(acls, "X");
        bool fired = false;
        void Handler() => fired = true;
        ctx.ApplicationEvents.AnimationChainsChanged += Handler;
        try
        {
            await ctx.AppCommands.AskToDeleteAnimationChains(
                new List<AnimationChainSave> { chain });
            Assert.True(fired);
        }
        finally
        {
            ctx.ApplicationEvents.AnimationChainsChanged -= Handler;
        }
    }

    [Fact]
    public async Task AskToDeleteAnimationChains_DeletingMultiple_RecordsSingleUndoEntry()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.AppCommands.ConfirmAsync = (_, __) => Task.FromResult(true);
        var a = TestHelpers.MakeChain(ctx.Acls, "A");
        var b = TestHelpers.MakeChain(ctx.Acls, "B");

        await ctx.AppCommands.AskToDeleteAnimationChains(
            new List<AnimationChainSave> { a, b });
        Assert.Empty(ctx.Acls.AnimationChains);

        // One user action is one undo step, and the single undo restores both
        // chains to their original positions.
        ctx.UndoManager.Undo();
        Assert.Equal(new[] { a, b }, ctx.Acls.AnimationChains);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    // ── DeleteFrames ──────────────────────────────────────────────────────────

    [Fact]
    public void DeleteFrames_DeletesFrameFromSelectedChain()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 3);
        ctx.SelectedState.SelectedChain = chain;
        var frameToDelete = chain.Frames[1];

        ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { frameToDelete });

        Assert.Equal(2, chain.Frames.Count);
        Assert.DoesNotContain(frameToDelete, chain.Frames);
    }

    [Fact]
    public void DeleteFrames_DeletesMultipleFrames()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 4);
        ctx.SelectedState.SelectedChain = chain;
        var toDelete = new List<AnimationFrameSave> { chain.Frames[0], chain.Frames[2] };

        ctx.AppCommands.DeleteFrames(toDelete);

        Assert.Equal(2, chain.Frames.Count);
    }

    [Fact]
    public void DeleteFrames_FiresAnimationChainsChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 2);
        ctx.SelectedState.SelectedChain = chain;
        bool fired = false;
        void Handler() => fired = true;
        ctx.ApplicationEvents.AnimationChainsChanged += Handler;
        try
        {
            ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { chain.Frames[0] });
            Assert.True(fired);
        }
        finally { ctx.ApplicationEvents.AnimationChainsChanged -= Handler; }
    }

    [Fact]
    public void DeleteFrames_FiresFramesDeleted_MultiFrameLabel()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 4);
        ctx.SelectedState.SelectedChain = chain;
        string? capturedLabel = null;
        ctx.AppCommands.FramesDeleted += label => capturedLabel = label;
        var toDelete = new List<AnimationFrameSave> { chain.Frames[0], chain.Frames[2] };

        ctx.AppCommands.DeleteFrames(toDelete);

        Assert.Equal("2 frames", capturedLabel);
    }

    [Fact]
    public void DeleteFrames_FiresFramesDeleted_SingleFrameLabel()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 3);
        ctx.SelectedState.SelectedChain = chain;
        string? capturedLabel = null;
        ctx.AppCommands.FramesDeleted += label => capturedLabel = label;

        ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { chain.Frames[0] });

        Assert.NotNull(capturedLabel);
        Assert.Contains("1", capturedLabel);
    }

    [Fact]
    public void DeleteFrames_RecordsUndoCommand()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 2);
        ctx.SelectedState.SelectedChain = chain;

        ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { chain.Frames[0] });

        Assert.True(ctx.UndoManager.CanUndo);
    }

    // ── AskToDeleteRectangles ─────────────────────────────────────────────────

    [Fact]
    public async Task AskToDeleteRectangles_WhenConfirmed_RemovesRectangleFromFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        ctx.AppCommands.ConfirmAsync = (_, __) => Task.FromResult(true);
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "HitBox" };
        frame.ShapesSave!.AARectSaves.Add(rect);

        await ctx.AppCommands.AskToDeleteRectangles(
            new List<AARectSave> { rect });

        Assert.Empty(frame.ShapesSave!.AARectSaves);
    }

    [Fact]
    public async Task AskToDeleteRectangles_WhenCancelled_DoesNotRemoveRectangle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        ctx.AppCommands.ConfirmAsync = (_, __) => Task.FromResult(false);
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "HitBox" };
        frame.ShapesSave!.AARectSaves.Add(rect);

        await ctx.AppCommands.AskToDeleteRectangles(
            new List<AARectSave> { rect });

        Assert.Single(frame.ShapesSave!.AARectSaves);
    }

    [Fact]
    public async Task AskToDeleteRectangles_ConfirmMessageContainsRectangleName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        string? capturedMessage = null;
        ctx.AppCommands.ConfirmAsync = (msg, _) =>
        {
            capturedMessage = msg;
            return Task.FromResult(false);
        };
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "BodyCollision" };
        frame.ShapesSave!.AARectSaves.Add(rect);

        await ctx.AppCommands.AskToDeleteRectangles(
            new List<AARectSave> { rect });

        Assert.NotNull(capturedMessage);
        Assert.Contains("BodyCollision", capturedMessage);
    }

    [Fact]
    public async Task AskToDeleteRectangles_DeletingMultiple_RecordsSingleUndoEntry()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.AppCommands.ConfirmAsync = (_, __) => Task.FromResult(true);
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        var r1 = new AARectSave { Name = "R1" };
        var r2 = new AARectSave { Name = "R2" };
        frame.ShapesSave!.AARectSaves.Add(r1);
        frame.ShapesSave!.AARectSaves.Add(r2);

        await ctx.AppCommands.AskToDeleteRectangles(
            new List<AARectSave> { r1, r2 });
        Assert.Empty(frame.ShapesSave!.AARectSaves);

        // One user action is one undo step, and the single undo restores both
        // rectangles to their original positions (verifies the composite undoes
        // its sub-commands in reverse so indices stay correct).
        ctx.UndoManager.Undo();
        Assert.Equal(new[] { r1, r2 }, frame.ShapesSave!.AARectSaves);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    // ── AskToDeleteCircles ────────────────────────────────────────────────────

    [Fact]
    public async Task AskToDeleteCircles_WhenConfirmed_RemovesCircleFromFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        ctx.AppCommands.ConfirmAsync = (_, __) => Task.FromResult(true);
        var chain = TestHelpers.MakeChain(acls, "Jump", 1);
        var frame = chain.Frames[0];
        var circle = new CircleSave { Name = "AttackRadius", Radius = 10 };
        frame.ShapesSave!.CircleSaves.Add(circle);

        await ctx.AppCommands.AskToDeleteCircles(
            new List<CircleSave> { circle });

        Assert.Empty(frame.ShapesSave!.CircleSaves);
    }

    [Fact]
    public async Task AskToDeleteCircles_WhenCancelled_DoesNotRemoveCircle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        ctx.AppCommands.ConfirmAsync = (_, __) => Task.FromResult(false);
        var chain = TestHelpers.MakeChain(acls, "Jump", 1);
        var frame = chain.Frames[0];
        var circle = new CircleSave { Name = "AttackRadius", Radius = 10 };
        frame.ShapesSave!.CircleSaves.Add(circle);

        await ctx.AppCommands.AskToDeleteCircles(
            new List<CircleSave> { circle });

        Assert.Single(frame.ShapesSave!.CircleSaves);
    }

    [Fact]
    public async Task AskToDeleteCircles_ConfirmMessageContainsCircleName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        string? capturedMessage = null;
        ctx.AppCommands.ConfirmAsync = (msg, _) =>
        {
            capturedMessage = msg;
            return Task.FromResult(false);
        };
        var chain = TestHelpers.MakeChain(acls, "Jump", 1);
        var frame = chain.Frames[0];
        var circle = new CircleSave { Name = "DetectionArea", Radius = 20 };
        frame.ShapesSave!.CircleSaves.Add(circle);

        await ctx.AppCommands.AskToDeleteCircles(
            new List<CircleSave> { circle });

        Assert.NotNull(capturedMessage);
        Assert.Contains("DetectionArea", capturedMessage);
    }

    [Fact]
    public async Task AskToDeleteCircles_DeletingMultiple_RecordsSingleUndoEntry()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.AppCommands.ConfirmAsync = (_, __) => Task.FromResult(true);
        var chain = TestHelpers.MakeChain(ctx.Acls, "Jump", 1);
        var frame = chain.Frames[0];
        var c1 = new CircleSave { Name = "C1", Radius = 5 };
        var c2 = new CircleSave { Name = "C2", Radius = 5 };
        frame.ShapesSave!.CircleSaves.Add(c1);
        frame.ShapesSave!.CircleSaves.Add(c2);

        await ctx.AppCommands.AskToDeleteCircles(
            new List<CircleSave> { c1, c2 });
        Assert.Empty(frame.ShapesSave!.CircleSaves);

        // One user action is one undo step, and the single undo restores both
        // circles to their original positions (verifies the composite undoes
        // its sub-commands in reverse so indices stay correct).
        ctx.UndoManager.Undo();
        Assert.Equal(new[] { c1, c2 }, frame.ShapesSave!.CircleSaves);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    // ── AskToDeleteShapes ─────────────────────────────────────────────────────

    [Fact]
    public async Task AskToDeleteShapes_WhenConfirmed_DeletesBothRectanglesAndCircles()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.AppCommands.ConfirmAsync = (_, __) => Task.FromResult(true);
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "Hitbox" };
        var circle = new CircleSave { Name = "Hurtbox", Radius = 10 };
        frame.ShapesSave!.AARectSaves.Add(rect);
        frame.ShapesSave!.CircleSaves.Add(circle);

        await ctx.AppCommands.AskToDeleteShapes(
            new List<AARectSave> { rect },
            new List<CircleSave> { circle });

        Assert.Empty(frame.ShapesSave!.AARectSaves);
        Assert.Empty(frame.ShapesSave!.CircleSaves);
    }

    [Fact]
    public async Task AskToDeleteShapes_WhenCancelled_DeletesNothing()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.AppCommands.ConfirmAsync = (_, __) => Task.FromResult(false);
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "Hitbox" };
        var circle = new CircleSave { Name = "Hurtbox", Radius = 10 };
        frame.ShapesSave!.AARectSaves.Add(rect);
        frame.ShapesSave!.CircleSaves.Add(circle);

        await ctx.AppCommands.AskToDeleteShapes(
            new List<AARectSave> { rect },
            new List<CircleSave> { circle });

        Assert.Single(frame.ShapesSave!.AARectSaves);
        Assert.Single(frame.ShapesSave!.CircleSaves);
    }

    [Fact]
    public async Task AskToDeleteShapes_RecordsSingleUndoEntry()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.AppCommands.ConfirmAsync = (_, __) => Task.FromResult(true);
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "Hitbox" };
        var circle = new CircleSave { Name = "Hurtbox", Radius = 10 };
        frame.ShapesSave!.AARectSaves.Add(rect);
        frame.ShapesSave!.CircleSaves.Add(circle);

        await ctx.AppCommands.AskToDeleteShapes(
            new List<AARectSave> { rect },
            new List<CircleSave> { circle });

        Assert.Empty(frame.ShapesSave!.AARectSaves);
        Assert.Empty(frame.ShapesSave!.CircleSaves);

        ctx.UndoManager.Undo();

        Assert.Equal(new[] { rect }, frame.ShapesSave!.AARectSaves);
        Assert.Equal(new[] { circle }, frame.ShapesSave!.CircleSaves);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public async Task AskToDeleteShapes_ConfirmMessageContainsAllShapeNames()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        string? capturedMessage = null;
        ctx.AppCommands.ConfirmAsync = (msg, _) =>
        {
            capturedMessage = msg;
            return Task.FromResult(false);
        };
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "BodyHitbox" };
        var circle = new CircleSave { Name = "AttackRadius", Radius = 10 };
        frame.ShapesSave!.AARectSaves.Add(rect);
        frame.ShapesSave!.CircleSaves.Add(circle);

        await ctx.AppCommands.AskToDeleteShapes(
            new List<AARectSave> { rect },
            new List<CircleSave> { circle });

        Assert.NotNull(capturedMessage);
        Assert.Contains("BodyHitbox", capturedMessage);
        Assert.Contains("AttackRadius", capturedMessage);
    }
}
