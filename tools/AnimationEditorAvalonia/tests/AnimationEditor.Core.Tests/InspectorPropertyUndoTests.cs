using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class InspectorPropertyUndoTests
{
    // ── SetFrameLength ────────────────────────────────────────────────────────

    [Fact]
    public void SetFrameLength_ChangedValue_CreatesUndoEntry()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        frame.FrameLength = 0.1f;
        chain.Frames.Add(frame);

        ctx.AppCommands.SetFrameLength(frame, 0.5f);

        Assert.True(ctx.UndoManager.CanUndo);
        Assert.Equal(0.5f, frame.FrameLength);
    }

    [Fact]
    public void SetFrameLength_SameValue_DoesNotCreateUndoEntry()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        frame.FrameLength = 0.1f;
        chain.Frames.Add(frame);

        ctx.AppCommands.SetFrameLength(frame, 0.1f);

        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void SetFrameLength_Undo_RestoresOldValue()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        frame.FrameLength = 0.1f;
        chain.Frames.Add(frame);

        ctx.AppCommands.SetFrameLength(frame, 0.5f);
        ctx.UndoManager.Undo();

        Assert.Equal(0.1f, frame.FrameLength);
    }

    // ── SetFrameRelative ──────────────────────────────────────────────────────

    [Fact]
    public void SetFrameRelative_Undo_RestoresBothAxes()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        frame.RelativeX = 10f;
        frame.RelativeY = 20f;
        chain.Frames.Add(frame);

        ctx.AppCommands.SetFrameRelative(frame, 99f, 88f);
        Assert.True(ctx.UndoManager.CanUndo);
        ctx.UndoManager.Undo();

        Assert.Equal(10f, frame.RelativeX);
        Assert.Equal(20f, frame.RelativeY);
    }

    // ── SetFramePixelRegion ───────────────────────────────────────────────────

    [Fact]
    public void SetFramePixelRegion_Undo_RestoresUvCoords()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        // Start: full texture (0..1)
        frame.LeftCoordinate   = 0f;
        frame.RightCoordinate  = 1f;
        frame.TopCoordinate    = 0f;
        frame.BottomCoordinate = 1f;
        chain.Frames.Add(frame);

        // Move/resize to pixel rect (4,8,12,16) on a 64x64 texture
        ctx.AppCommands.SetFramePixelRegion(frame, 4, 8, 12, 16, 64, 64);
        Assert.True(ctx.UndoManager.CanUndo);

        ctx.UndoManager.Undo();

        Assert.Equal(0f,  frame.LeftCoordinate,   precision: 5);
        Assert.Equal(1f,  frame.RightCoordinate,   precision: 5);
        Assert.Equal(0f,  frame.TopCoordinate,     precision: 5);
        Assert.Equal(1f,  frame.BottomCoordinate,  precision: 5);
    }

    // ── SetRectProps ──────────────────────────────────────────────────────────

    [Fact]
    public void SetRectProps_Undo_RestoresAllRectProperties()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        var rect = new AARectSave { Name = "OldName", X = 1f, Y = 2f, ScaleX = 3f, ScaleY = 4f };
        frame.ShapesSave!.Shapes.Add(rect);
        chain.Frames.Add(frame);

        ctx.AppCommands.SetRectProps(frame, rect, "NewName", 10f, 20f, 30f, 40f);
        Assert.True(ctx.UndoManager.CanUndo);

        ctx.UndoManager.Undo();

        Assert.Equal("OldName", rect.Name);
        Assert.Equal(1f, rect.X);
        Assert.Equal(2f, rect.Y);
        Assert.Equal(3f, rect.ScaleX);
        Assert.Equal(4f, rect.ScaleY);
    }

    [Fact]
    public void SetRectProps_IdenticalValues_DoesNotCreateUndoEntry()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        var rect = new AARectSave { Name = "Same", X = 1f, Y = 2f, ScaleX = 3f, ScaleY = 4f };
        frame.ShapesSave!.Shapes.Add(rect);
        chain.Frames.Add(frame);

        ctx.AppCommands.SetRectProps(frame, rect, "Same", 1f, 2f, 3f, 4f);

        Assert.False(ctx.UndoManager.CanUndo);
    }

    // ── SetCircleProps ────────────────────────────────────────────────────────

    [Fact]
    public void SetCircleProps_Undo_RestoresAllCircleProperties()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        var circ = new CircleSave { Name = "OldCircle", X = 5f, Y = 6f, Radius = 7f };
        frame.ShapesSave!.Shapes.Add(circ);
        chain.Frames.Add(frame);

        ctx.AppCommands.SetCircleProps(frame, circ, "NewCircle", 50f, 60f, 70f);
        Assert.True(ctx.UndoManager.CanUndo);

        ctx.UndoManager.Undo();

        Assert.Equal("OldCircle", circ.Name);
        Assert.Equal(5f, circ.X);
        Assert.Equal(6f, circ.Y);
        Assert.Equal(7f, circ.Radius);
    }
}
