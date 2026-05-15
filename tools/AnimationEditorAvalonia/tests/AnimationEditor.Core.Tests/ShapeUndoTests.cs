using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using FlatRedBall2.Animation.Content;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class ShapeUndoTests
{
    // ── AddAxisAlignedRectangle + Undo ────────────────────────────────────────

    [Fact]
    public void AddAxisAlignedRectangle_Undo_RemovesRect()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);

        ctx.AppCommands.AddAxisAlignedRectangle(frame);
        Assert.Single(frame.ShapesSave!.AARectSaves);

        ctx.UndoManager.Undo();

        Assert.Empty(frame.ShapesSave!.AARectSaves);
    }

    [Fact]
    public void AddAxisAlignedRectangle_UndoThenRedo_ReAddsRect()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);

        ctx.AppCommands.AddAxisAlignedRectangle(frame);
        var originalRect = frame.ShapesSave!.AARectSaves.First();
        ctx.UndoManager.Undo();

        ctx.UndoManager.Redo();

        Assert.Single(frame.ShapesSave!.AARectSaves);
        Assert.Same(originalRect, frame.ShapesSave!.AARectSaves.First());
    }

    // ── DeleteAxisAlignedRectangle + Undo ─────────────────────────────────────

    [Fact]
    public void DeleteAxisAlignedRectangle_Undo_RestoresRectAtOriginalIndex()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);
        ctx.AppCommands.AddAxisAlignedRectangle(frame);
        ctx.AppCommands.AddAxisAlignedRectangle(frame);
        ctx.UndoManager.Clear(); // clear add history — we're testing delete
        var first = frame.ShapesSave!.AARectSaves.First();

        ctx.AppCommands.DeleteAxisAlignedRectangle(first, frame);
        Assert.Single(frame.ShapesSave!.AARectSaves);

        ctx.UndoManager.Undo();

        Assert.Equal(2, frame.ShapesSave!.AARectSaves.Count());
        Assert.Same(first, frame.ShapesSave!.AARectSaves.First());
    }

    [Fact]
    public void DeleteAxisAlignedRectangle_UndoThenRedo_RemovesAgain()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);
        ctx.AppCommands.AddAxisAlignedRectangle(frame);
        ctx.UndoManager.Clear();
        var rect = frame.ShapesSave!.AARectSaves.First();

        ctx.AppCommands.DeleteAxisAlignedRectangle(rect, frame);
        ctx.UndoManager.Undo();
        Assert.Single(frame.ShapesSave!.AARectSaves);

        ctx.UndoManager.Redo();

        Assert.Empty(frame.ShapesSave!.AARectSaves);
    }

    // ── AddCircle + Undo ──────────────────────────────────────────────────────

    [Fact]
    public void AddCircle_Undo_RemovesCircle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);

        ctx.AppCommands.AddCircle(frame);
        Assert.Single(frame.ShapesSave!.CircleSaves);

        ctx.UndoManager.Undo();

        Assert.Empty(frame.ShapesSave!.CircleSaves);
    }

    [Fact]
    public void AddCircle_UndoThenRedo_ReAddsCircle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);

        ctx.AppCommands.AddCircle(frame);
        var originalCircle = frame.ShapesSave!.CircleSaves.First();
        ctx.UndoManager.Undo();

        ctx.UndoManager.Redo();

        Assert.Single(frame.ShapesSave!.CircleSaves);
        Assert.Same(originalCircle, frame.ShapesSave!.CircleSaves.First());
    }

    // ── DeleteCircle + Undo ───────────────────────────────────────────────────

    [Fact]
    public void DeleteCircle_Undo_RestoresCircleAtOriginalIndex()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);
        ctx.AppCommands.AddCircle(frame);
        ctx.AppCommands.AddCircle(frame);
        ctx.UndoManager.Clear();
        var first = frame.ShapesSave!.CircleSaves.First();

        ctx.AppCommands.DeleteCircle(first, frame);
        Assert.Single(frame.ShapesSave!.CircleSaves);

        ctx.UndoManager.Undo();

        Assert.Equal(2, frame.ShapesSave!.CircleSaves.Count());
        Assert.Same(first, frame.ShapesSave!.CircleSaves.First());
    }

    [Fact]
    public void DeleteCircle_UndoThenRedo_RemovesAgain()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);
        ctx.AppCommands.AddCircle(frame);
        ctx.UndoManager.Clear();
        var circle = frame.ShapesSave!.CircleSaves.First();

        ctx.AppCommands.DeleteCircle(circle, frame);
        ctx.UndoManager.Undo();
        Assert.Single(frame.ShapesSave!.CircleSaves);

        ctx.UndoManager.Redo();

        Assert.Empty(frame.ShapesSave!.CircleSaves);
    }
}
