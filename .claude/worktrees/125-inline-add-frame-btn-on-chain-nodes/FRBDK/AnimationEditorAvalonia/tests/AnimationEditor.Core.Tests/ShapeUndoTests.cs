using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using FlatRedBall.Content.Math.Geometry;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class ShapeUndoTests
{
    // ── AddAxisAlignedRectangle + Undo ────────────────────────────────────────

    [Fact]
    public void AddAxisAlignedRectangle_Undo_RemovesRect()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);

        AppCommands.Self.AddAxisAlignedRectangle(frame);
        Assert.Single(frame.ShapeCollectionSave.AxisAlignedRectangleSaves);

        UndoManager.Self.Undo();

        Assert.Empty(frame.ShapeCollectionSave.AxisAlignedRectangleSaves);
    }

    [Fact]
    public void AddAxisAlignedRectangle_UndoThenRedo_ReAddsRect()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);

        AppCommands.Self.AddAxisAlignedRectangle(frame);
        var originalRect = frame.ShapeCollectionSave.AxisAlignedRectangleSaves[0];
        UndoManager.Self.Undo();

        UndoManager.Self.Redo();

        Assert.Single(frame.ShapeCollectionSave.AxisAlignedRectangleSaves);
        Assert.Same(originalRect, frame.ShapeCollectionSave.AxisAlignedRectangleSaves[0]);
    }

    // ── DeleteAxisAlignedRectangle + Undo ─────────────────────────────────────

    [Fact]
    public void DeleteAxisAlignedRectangle_Undo_RestoresRectAtOriginalIndex()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);
        AppCommands.Self.AddAxisAlignedRectangle(frame);
        AppCommands.Self.AddAxisAlignedRectangle(frame);
        UndoManager.Self.Clear(); // clear add history — we're testing delete
        var rects = frame.ShapeCollectionSave.AxisAlignedRectangleSaves;
        var first = rects[0];

        AppCommands.Self.DeleteAxisAlignedRectangle(first, frame);
        Assert.Single(rects);

        UndoManager.Self.Undo();

        Assert.Equal(2, rects.Count);
        Assert.Same(first, rects[0]);
    }

    [Fact]
    public void DeleteAxisAlignedRectangle_UndoThenRedo_RemovesAgain()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);
        AppCommands.Self.AddAxisAlignedRectangle(frame);
        UndoManager.Self.Clear();
        var rect = frame.ShapeCollectionSave.AxisAlignedRectangleSaves[0];

        AppCommands.Self.DeleteAxisAlignedRectangle(rect, frame);
        UndoManager.Self.Undo();
        Assert.Single(frame.ShapeCollectionSave.AxisAlignedRectangleSaves);

        UndoManager.Self.Redo();

        Assert.Empty(frame.ShapeCollectionSave.AxisAlignedRectangleSaves);
    }

    // ── AddCircle + Undo ──────────────────────────────────────────────────────

    [Fact]
    public void AddCircle_Undo_RemovesCircle()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);

        AppCommands.Self.AddCircle(frame);
        Assert.Single(frame.ShapeCollectionSave.CircleSaves);

        UndoManager.Self.Undo();

        Assert.Empty(frame.ShapeCollectionSave.CircleSaves);
    }

    [Fact]
    public void AddCircle_UndoThenRedo_ReAddsCircle()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);

        AppCommands.Self.AddCircle(frame);
        var originalCircle = frame.ShapeCollectionSave.CircleSaves[0];
        UndoManager.Self.Undo();

        UndoManager.Self.Redo();

        Assert.Single(frame.ShapeCollectionSave.CircleSaves);
        Assert.Same(originalCircle, frame.ShapeCollectionSave.CircleSaves[0]);
    }

    // ── DeleteCircle + Undo ───────────────────────────────────────────────────

    [Fact]
    public void DeleteCircle_Undo_RestoresCircleAtOriginalIndex()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);
        AppCommands.Self.AddCircle(frame);
        AppCommands.Self.AddCircle(frame);
        UndoManager.Self.Clear();
        var circles = frame.ShapeCollectionSave.CircleSaves;
        var first = circles[0];

        AppCommands.Self.DeleteCircle(first, frame);
        Assert.Single(circles);

        UndoManager.Self.Undo();

        Assert.Equal(2, circles.Count);
        Assert.Same(first, circles[0]);
    }

    [Fact]
    public void DeleteCircle_UndoThenRedo_RemovesAgain()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        chain.Frames.Add(frame);
        AppCommands.Self.AddCircle(frame);
        UndoManager.Self.Clear();
        var circle = frame.ShapeCollectionSave.CircleSaves[0];

        AppCommands.Self.DeleteCircle(circle, frame);
        UndoManager.Self.Undo();
        Assert.Single(frame.ShapeCollectionSave.CircleSaves);

        UndoManager.Self.Redo();

        Assert.Empty(frame.ShapeCollectionSave.CircleSaves);
    }
}
