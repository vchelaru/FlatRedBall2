using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using FlatRedBall2.Animation.Content;
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
        var originalRect = frame.ShapesSave!.AARectSaves[0];
        ctx.UndoManager.Undo();

        ctx.UndoManager.Redo();

        Assert.Single(frame.ShapesSave!.AARectSaves);
        Assert.Same(originalRect, frame.ShapesSave!.AARectSaves[0]);
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
        var rects = frame.ShapesSave!.AARectSaves;
        var first = rects[0];

        ctx.AppCommands.DeleteAxisAlignedRectangle(first, frame);
        Assert.Single(rects);

        ctx.UndoManager.Undo();

        Assert.Equal(2, rects.Count);
        Assert.Same(first, rects[0]);
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
        var rect = frame.ShapesSave!.AARectSaves[0];

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
        var originalCircle = frame.ShapesSave!.CircleSaves[0];
        ctx.UndoManager.Undo();

        ctx.UndoManager.Redo();

        Assert.Single(frame.ShapesSave!.CircleSaves);
        Assert.Same(originalCircle, frame.ShapesSave!.CircleSaves[0]);
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
        var circles = frame.ShapesSave!.CircleSaves;
        var first = circles[0];

        ctx.AppCommands.DeleteCircle(first, frame);
        Assert.Single(circles);

        ctx.UndoManager.Undo();

        Assert.Equal(2, circles.Count);
        Assert.Same(first, circles[0]);
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
        var circle = frame.ShapesSave!.CircleSaves[0];

        ctx.AppCommands.DeleteCircle(circle, frame);
        ctx.UndoManager.Undo();
        Assert.Single(frame.ShapesSave!.CircleSaves);

        ctx.UndoManager.Redo();

        Assert.Empty(frame.ShapesSave!.CircleSaves);
    }
}
