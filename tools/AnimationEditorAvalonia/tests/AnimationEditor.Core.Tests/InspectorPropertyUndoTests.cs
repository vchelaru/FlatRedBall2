using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation;
using FlatRedBall2.Animation.Content;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class InspectorPropertyUndoTests
{
    // ── SetFrameAlpha ─────────────────────────────────────────────────────────

    [Fact]
    public void SetFrameAlpha_Undo_RestoresAlpha()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Fade");
        var frame = TestHelpers.MakeFrame(); // starts with null alpha
        chain.Frames.Add(frame);

        ctx.AppCommands.SetFrameAlpha(new[] { frame }, 128);
        // A single committed edit must record exactly one undo entry (not one per keystroke — #445).
        Assert.Single(ctx.UndoManager.UndoHistory);
        Assert.Equal(128, frame.Alpha);

        ctx.UndoManager.Undo();

        Assert.Null(frame.Alpha);
    }

    [Fact]
    public void SetFrameAlpha_SameValue_DoesNotCreateUndoEntry()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Fade");
        var frame = TestHelpers.MakeFrame();
        frame.Alpha = 128;
        chain.Frames.Add(frame);

        ctx.AppCommands.SetFrameAlpha(new[] { frame }, 128);

        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void SetFrameAlpha_MultipleFrames_AppliesToAllAsOneUndoStep()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Fade", frameCount: 2);
        var frames = chain.Frames.ToList();

        ctx.AppCommands.SetFrameAlpha(frames, 64);

        Assert.All(frames, f => Assert.Equal(64, f.Alpha));
        Assert.Single(ctx.UndoManager.UndoHistory);
    }

    // ── SetFrameColor ─────────────────────────────────────────────────────────

    [Fact]
    public void SetFrameColor_Undo_RestoresChannels()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Flash");
        var frame = TestHelpers.MakeFrame();
        // Start with no authored color (all null).
        chain.Frames.Add(frame);

        ctx.AppCommands.SetFrameColor(new[] { frame }, 255, 200, 128);
        Assert.True(ctx.UndoManager.CanUndo);
        Assert.Equal(255, frame.Red);

        ctx.UndoManager.Undo();

        Assert.Null(frame.Red);
        Assert.Null(frame.Green);
        Assert.Null(frame.Blue);
    }

    [Fact]
    public void SetFrameColor_SameValues_DoesNotCreateUndoEntry()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Flash");
        var frame = TestHelpers.MakeFrame();
        frame.Red = 255;
        frame.Green = 200;
        frame.Blue = 128;
        chain.Frames.Add(frame);

        ctx.AppCommands.SetFrameColor(new[] { frame }, 255, 200, 128);

        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void SetFrameColor_MultipleFrames_AppliesToAllAsOneUndoStep()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Flash", frameCount: 2);
        var frames = chain.Frames.ToList();

        ctx.AppCommands.SetFrameColor(frames, 255, 200, 128);

        Assert.All(frames, f => Assert.Equal(255, f.Red));
        Assert.All(frames, f => Assert.Equal(200, f.Green));
        Assert.All(frames, f => Assert.Equal(128, f.Blue));
        Assert.Single(ctx.UndoManager.UndoHistory);
    }

    // ── SetFrameColorOperation ────────────────────────────────────────────────

    [Fact]
    public void SetFrameColorOperation_Undo_RestoresOperation()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Flash");
        var frame = TestHelpers.MakeFrame(); // starts with null operation
        chain.Frames.Add(frame);

        ctx.AppCommands.SetFrameColorOperation(new[] { frame }, ColorOperation.Add);
        Assert.True(ctx.UndoManager.CanUndo);
        Assert.Equal(ColorOperation.Add, frame.ColorOperation);

        ctx.UndoManager.Undo();

        Assert.Null(frame.ColorOperation);
    }

    [Fact]
    public void SetFrameColorOperation_SameValue_DoesNotCreateUndoEntry()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Flash");
        var frame = TestHelpers.MakeFrame();
        frame.ColorOperation = ColorOperation.Multiply;
        chain.Frames.Add(frame);

        ctx.AppCommands.SetFrameColorOperation(new[] { frame }, ColorOperation.Multiply);

        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void SetFrameColorOperation_MultipleFrames_AppliesToAllAsOneUndoStep()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Flash", frameCount: 2);
        var frames = chain.Frames.ToList();

        ctx.AppCommands.SetFrameColorOperation(frames, ColorOperation.Add);

        Assert.All(frames, f => Assert.Equal(ColorOperation.Add, f.ColorOperation));
        Assert.Single(ctx.UndoManager.UndoHistory);
    }

    // ── SetFrameLength ────────────────────────────────────────────────────────

    [Fact]
    public void SetFrameLength_ChangedValue_CreatesUndoEntry()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var frame = TestHelpers.MakeFrame();
        frame.FrameLength = 0.1f;
        chain.Frames.Add(frame);

        ctx.AppCommands.SetFrameLength(new[] { frame }, 0.5f);

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

        ctx.AppCommands.SetFrameLength(new[] { frame }, 0.1f);

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

        ctx.AppCommands.SetFrameLength(new[] { frame }, 0.5f);
        ctx.UndoManager.Undo();

        Assert.Equal(0.1f, frame.FrameLength);
    }

    [Fact]
    public void SetFrameLength_MultipleFrames_AppliesToAllAsOneUndoStep()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 3);
        var frames = chain.Frames.ToList();

        ctx.AppCommands.SetFrameLength(frames, 0.5f);

        Assert.All(frames, f => Assert.Equal(0.5f, f.FrameLength));
        Assert.Single(ctx.UndoManager.UndoHistory);

        ctx.UndoManager.Undo();

        Assert.All(frames, f => Assert.Equal(0.1f, f.FrameLength));
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

        ctx.AppCommands.SetFrameRelative(new[] { frame }, 99f, 88f);
        Assert.True(ctx.UndoManager.CanUndo);
        ctx.UndoManager.Undo();

        Assert.Equal(10f, frame.RelativeX);
        Assert.Equal(20f, frame.RelativeY);
    }

    [Fact]
    public void SetFrameRelative_MultipleFrames_AppliesToAllAsOneUndoStep()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 2);
        var frames = chain.Frames.ToList();

        ctx.AppCommands.SetFrameRelative(frames, 99f, 88f);

        Assert.All(frames, f => Assert.Equal(99f, f.RelativeX));
        Assert.All(frames, f => Assert.Equal(88f, f.RelativeY));
        Assert.Single(ctx.UndoManager.UndoHistory);
    }

    [Fact]
    public void SetFrameRelative_NullY_LeavesEachFrameOwnRelativeYUntouched()
    {
        // Reproduces issue #571 follow-up: frames disagree on RelativeY (shown "(mixed)" in the
        // inspector). The user only edits RelativeX; RelativeY must be left exactly as each frame
        // already had it, not forced to a shared/null value.
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 2);
        var frames = chain.Frames.ToList();
        frames[0].RelativeX = 1f; frames[0].RelativeY = 10f;
        frames[1].RelativeX = 2f; frames[1].RelativeY = 20f;

        ctx.AppCommands.SetFrameRelative(frames, newRelX: 5f, newRelY: null);

        Assert.All(frames, f => Assert.Equal(5f, f.RelativeX));
        Assert.Equal(10f, frames[0].RelativeY);
        Assert.Equal(20f, frames[1].RelativeY);
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
        ctx.AppCommands.SetFramePixelRegion(new[] { frame }, 4, 8, 12, 16, 64, 64);
        Assert.True(ctx.UndoManager.CanUndo);

        ctx.UndoManager.Undo();

        Assert.Equal(0f,  frame.LeftCoordinate,   precision: 5);
        Assert.Equal(1f,  frame.RightCoordinate,   precision: 5);
        Assert.Equal(0f,  frame.TopCoordinate,     precision: 5);
        Assert.Equal(1f,  frame.BottomCoordinate,  precision: 5);
    }

    [Fact]
    public void SetFramePixelRegion_MultipleFrames_AppliesToAllAsOneUndoStep()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 2);
        var frames = chain.Frames.ToList();
        foreach (var f in frames)
        {
            f.LeftCoordinate = 0f; f.RightCoordinate = 1f;
            f.TopCoordinate = 0f; f.BottomCoordinate = 1f;
        }

        ctx.AppCommands.SetFramePixelRegion(frames, 4, 8, 12, 16, 64, 64);

        Assert.All(frames, f => Assert.Equal(4f / 64f, f.LeftCoordinate, precision: 5));
        Assert.All(frames, f => Assert.Equal(16f / 64f, f.RightCoordinate, precision: 5));
        Assert.Single(ctx.UndoManager.UndoHistory);
    }

    [Fact]
    public void SetFramePixelRegion_OnlyXSpecified_LeavesEachFrameOwnYWidthHeightUntouched()
    {
        // Reproduces issue #571 follow-up: two frames at different positions/sizes on the sheet
        // (so Y/W/H are all "(mixed)" in the inspector). The user types only a new X and presses
        // Enter — that must move both frames' X without collapsing their own differing Y/W/H.
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", frameCount: 2);
        var frames = chain.Frames.ToList();
        frames[0].LeftCoordinate = 0f / 64; frames[0].RightCoordinate = 8f / 64;
        frames[0].TopCoordinate = 0f / 64; frames[0].BottomCoordinate = 8f / 64;
        frames[1].LeftCoordinate = 32f / 64; frames[1].RightCoordinate = 40f / 64;
        frames[1].TopCoordinate = 16f / 64; frames[1].BottomCoordinate = 32f / 64;

        ctx.AppCommands.SetFramePixelRegion(frames, pixelX: 5, pixelY: null, pixelW: null, pixelH: null, bmpW: 64, bmpH: 64);

        Assert.Equal(5f / 64f, frames[0].LeftCoordinate, precision: 5);
        Assert.Equal(5f / 64f, frames[1].LeftCoordinate, precision: 5);
        Assert.Equal(0f / 64f, frames[0].TopCoordinate, precision: 5);      // frame 0's own Y preserved
        Assert.Equal(16f / 64f, frames[1].TopCoordinate, precision: 5);     // frame 1's own Y preserved
        Assert.Equal(8f / 64f, frames[0].BottomCoordinate - frames[0].TopCoordinate, precision: 5); // width/height untouched
        Assert.Equal(16f / 64f, frames[1].BottomCoordinate - frames[1].TopCoordinate, precision: 5);
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
