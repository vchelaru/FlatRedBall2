using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
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

    // ── AskToDeleteFrames ─────────────────────────────────────────────────────

    [Fact]
    public async Task AskToDeleteFrames_WhenConfirmed_DeletesFramesFromSelectedChain()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        ctx.AppCommands.ConfirmAsync = (_, __) => Task.FromResult(true);
        var chain = TestHelpers.MakeChain(acls, "Run", 3);
        ctx.SelectedState.SelectedChain = chain;
        var frameToDelete = chain.Frames[1];

        await ctx.AppCommands.AskToDeleteFrames(
            new List<AnimationFrameSave> { frameToDelete });

        Assert.Equal(2, chain.Frames.Count);
        Assert.DoesNotContain(frameToDelete, chain.Frames);
    }

    [Fact]
    public async Task AskToDeleteFrames_WhenCancelled_DoesNotDeleteAnyFrames()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        ctx.AppCommands.ConfirmAsync = (_, __) => Task.FromResult(false);
        var chain = TestHelpers.MakeChain(acls, "Run", 3);
        ctx.SelectedState.SelectedChain = chain;

        await ctx.AppCommands.AskToDeleteFrames(
            new List<AnimationFrameSave> { chain.Frames[0] });

        Assert.Equal(3, chain.Frames.Count);
    }

    [Fact]
    public async Task AskToDeleteFrames_WhenConfirmed_DeletesMultipleFrames()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        ctx.AppCommands.ConfirmAsync = (_, __) => Task.FromResult(true);
        var chain = TestHelpers.MakeChain(acls, "Run", 4);
        ctx.SelectedState.SelectedChain = chain;
        var toDelete = new List<AnimationFrameSave> { chain.Frames[0], chain.Frames[2] };

        await ctx.AppCommands.AskToDeleteFrames(toDelete);

        Assert.Equal(2, chain.Frames.Count);
    }

    [Fact]
    public async Task AskToDeleteFrames_WhenConfirmed_FiresAnimationChainsChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        ctx.AppCommands.ConfirmAsync = (_, __) => Task.FromResult(true);
        var chain = TestHelpers.MakeChain(acls, "Run", 2);
        ctx.SelectedState.SelectedChain = chain;
        bool fired = false;
        void Handler() => fired = true;
        ctx.ApplicationEvents.AnimationChainsChanged += Handler;
        try
        {
            await ctx.AppCommands.AskToDeleteFrames(
                new List<AnimationFrameSave> { chain.Frames[0] });
            Assert.True(fired);
        }
        finally
        {
            ctx.ApplicationEvents.AnimationChainsChanged -= Handler;
        }
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
        var rect = new AxisAlignedRectangleSave { Name = "HitBox" };
        frame.ShapeCollectionSave!.AxisAlignedRectangleSaves.Add(rect);

        await ctx.AppCommands.AskToDeleteRectangles(
            new List<AxisAlignedRectangleSave> { rect });

        Assert.Empty(frame.ShapeCollectionSave!.AxisAlignedRectangleSaves);
    }

    [Fact]
    public async Task AskToDeleteRectangles_WhenCancelled_DoesNotRemoveRectangle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        ctx.AppCommands.ConfirmAsync = (_, __) => Task.FromResult(false);
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rect = new AxisAlignedRectangleSave { Name = "HitBox" };
        frame.ShapeCollectionSave!.AxisAlignedRectangleSaves.Add(rect);

        await ctx.AppCommands.AskToDeleteRectangles(
            new List<AxisAlignedRectangleSave> { rect });

        Assert.Single(frame.ShapeCollectionSave!.AxisAlignedRectangleSaves);
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
        var rect = new AxisAlignedRectangleSave { Name = "BodyCollision" };
        frame.ShapeCollectionSave!.AxisAlignedRectangleSaves.Add(rect);

        await ctx.AppCommands.AskToDeleteRectangles(
            new List<AxisAlignedRectangleSave> { rect });

        Assert.NotNull(capturedMessage);
        Assert.Contains("BodyCollision", capturedMessage);
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
        frame.ShapeCollectionSave!.CircleSaves.Add(circle);

        await ctx.AppCommands.AskToDeleteCircles(
            new List<CircleSave> { circle });

        Assert.Empty(frame.ShapeCollectionSave!.CircleSaves);
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
        frame.ShapeCollectionSave!.CircleSaves.Add(circle);

        await ctx.AppCommands.AskToDeleteCircles(
            new List<CircleSave> { circle });

        Assert.Single(frame.ShapeCollectionSave!.CircleSaves);
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
        frame.ShapeCollectionSave!.CircleSaves.Add(circle);

        await ctx.AppCommands.AskToDeleteCircles(
            new List<CircleSave> { circle });

        Assert.NotNull(capturedMessage);
        Assert.Contains("DetectionArea", capturedMessage);
    }
}
