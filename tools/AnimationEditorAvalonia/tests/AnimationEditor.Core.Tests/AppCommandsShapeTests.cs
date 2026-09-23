using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AppCommandsShapeTests
{
    // ── AddAxisAlignedRectangle ───────────────────────────────────────────────

    [Fact]
    public void AddAxisAlignedRectangle_AddsRectangleToFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        ctx.SelectedState.SelectedFrame = frame;

        ctx.AppCommands.AddAxisAlignedRectangle(frame);

        Assert.Single(frame.ShapesSave!.AARectSaves);
    }

    [Fact]
    public void AddAxisAlignedRectangle_ThenUndo_OnAFrameWithNoShapes_LeavesShapesSaveNull()
    {
        // An empty ShapesSave still serializes as a <ShapeCollectionSave> block, so a frame that
        // never had shapes must go back to having none, or "undo everything" changes the file.
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        frame.ShapesSave = null;
        ctx.SelectedState.SelectedFrame = frame;
        ctx.AppCommands.AddAxisAlignedRectangle(frame);

        ctx.UndoManager.Undo();

        frame.ShapesSave.ShouldBeNull();
    }

    [Fact]
    public void AddAxisAlignedRectangle_ThenUndo_OnAFrameThatHadShapes_KeepsTheOthers()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        ctx.SelectedState.SelectedFrame = frame;
        ctx.AppCommands.AddCircle(frame);
        ctx.AppCommands.AddAxisAlignedRectangle(frame);

        ctx.UndoManager.Undo();

        frame.ShapesSave!.Shapes.Count.ShouldBe(1);
        frame.ShapesSave.CircleSaves.Count().ShouldBe(1);
    }

    [Fact]
    public void AddAxisAlignedRectangle_SetsDefaultScale8()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        ctx.SelectedState.SelectedFrame = frame;

        ctx.AppCommands.AddAxisAlignedRectangle(frame);

        var rect = frame.ShapesSave!.AARectSaves.First();
        Assert.Equal(8, rect.ScaleX);
        Assert.Equal(8, rect.ScaleY);
    }

    [Fact]
    public void AddAxisAlignedRectangle_GeneratesUniqueNamesForMultipleRects()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        ctx.SelectedState.SelectedFrame = frame;

        ctx.AppCommands.AddAxisAlignedRectangle(frame);
        ctx.AppCommands.AddAxisAlignedRectangle(frame);

        var names = frame.ShapesSave!.AARectSaves.Select(r => r.Name).ToList();
        Assert.Equal(2, names.Distinct().Count());
    }

    [Fact]
    public void AddAxisAlignedRectangle_SetsSelectedRectangle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        ctx.SelectedState.SelectedFrame = frame;

        ctx.AppCommands.AddAxisAlignedRectangle(frame);

        Assert.NotNull(ctx.SelectedState.SelectedRectangle);
        Assert.Same(
            frame.ShapesSave!.AARectSaves.First(),
            ctx.SelectedState.SelectedRectangle);
    }

    [Fact]
    public void AddAxisAlignedRectangle_PositionMatchesFrameRelativeXY()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        frame.RelativeX = 10f;
        frame.RelativeY = -5f;
        ctx.SelectedState.SelectedFrame = frame;

        ctx.AppCommands.AddAxisAlignedRectangle(frame);

        var rect = frame.ShapesSave!.AARectSaves.First();
        Assert.Equal(10f, rect.X);
        Assert.Equal(-5f, rect.Y);
    }

    // ── AddCircle ────────────────────────────────────────────────────────────

    [Fact]
    public void AddCircle_AddsCircleToFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Jump", 1);
        var frame = chain.Frames[0];
        ctx.SelectedState.SelectedFrame = frame;

        ctx.AppCommands.AddCircle(frame);

        Assert.Single(frame.ShapesSave!.CircleSaves);
    }

    [Fact]
    public void AddCircle_ThenUndo_OnAFrameWithNoShapes_LeavesShapesSaveNull()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        frame.ShapesSave = null;
        ctx.SelectedState.SelectedFrame = frame;
        ctx.AppCommands.AddCircle(frame);

        ctx.UndoManager.Undo();

        frame.ShapesSave.ShouldBeNull();
    }

    [Fact]
    public void AddCircle_SetsDefaultRadius8()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Jump", 1);
        var frame = chain.Frames[0];
        ctx.SelectedState.SelectedFrame = frame;

        ctx.AppCommands.AddCircle(frame);

        Assert.Equal(8, frame.ShapesSave!.CircleSaves.First().Radius);
    }

    [Fact]
    public void AddCircle_GeneratesUniqueNamesForMultipleCircles()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Jump", 1);
        var frame = chain.Frames[0];
        ctx.SelectedState.SelectedFrame = frame;

        ctx.AppCommands.AddCircle(frame);
        ctx.AppCommands.AddCircle(frame);

        var names = frame.ShapesSave!.CircleSaves.Select(c => c.Name).ToList();
        Assert.Equal(2, names.Distinct().Count());
    }

    [Fact]
    public void AddCircle_SetsSelectedCircle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Jump", 1);
        var frame = chain.Frames[0];
        ctx.SelectedState.SelectedFrame = frame;

        ctx.AppCommands.AddCircle(frame);

        Assert.NotNull(ctx.SelectedState.SelectedCircle);
        Assert.Same(frame.ShapesSave!.CircleSaves.First(), ctx.SelectedState.SelectedCircle);
    }

    [Fact]
    public void AddCircle_PositionMatchesFrameRelativeXY()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Jump", 1);
        var frame = chain.Frames[0];
        frame.RelativeX = 3f;
        frame.RelativeY = -7f;
        ctx.SelectedState.SelectedFrame = frame;

        ctx.AppCommands.AddCircle(frame);

        var circle = frame.ShapesSave!.CircleSaves.First();
        Assert.Equal(3f, circle.X);
        Assert.Equal(-7f, circle.Y);
    }

    [Fact]
    public void AddCircle_UniqueNameNotConflictWithExistingRectangleName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Attack", 1);
        var frame = chain.Frames[0];
        ctx.SelectedState.SelectedFrame = frame;

        // Add a rect with the default circle name to force a conflict
        frame.ShapesSave!.Shapes.Add(
            new AARectSave { Name = "CircleInstance" });

        ctx.AppCommands.AddCircle(frame);

        Assert.NotEqual("CircleInstance", frame.ShapesSave!.CircleSaves.First().Name);
    }

    // ── DeleteAxisAlignedRectangle ───────────────────────────────────────────

    [Fact]
    public void DeleteAxisAlignedRectangle_RemovesFromOwnerFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "Box" };
        frame.ShapesSave!.Shapes.Add(rect);

        ctx.AppCommands.DeleteAxisAlignedRectangle(rect, frame);

        Assert.Empty(frame.ShapesSave!.AARectSaves);
    }

    [Fact]
    public void DeleteAxisAlignedRectangle_WhenNotOwnedByFrame_DoesNotRemoveFromOtherFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 2);
        var frame1 = chain.Frames[0];
        var frame2 = chain.Frames[1];
        var rect = new AARectSave { Name = "Box" };
        frame1.ShapesSave!.Shapes.Add(rect);

        // Call with wrong owner (frame2 doesn't own rect)
        ctx.AppCommands.DeleteAxisAlignedRectangle(rect, frame2);

        Assert.Single(frame1.ShapesSave!.AARectSaves);
    }

    [Fact]
    public void DeleteAxisAlignedRectangle_FiresAnimationChainsChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "Box" };
        frame.ShapesSave!.Shapes.Add(rect);

        bool fired = false;
        void Handler() => fired = true;
        ctx.ApplicationEvents.AnimationChainsChanged += Handler;
        try
        {
            ctx.AppCommands.DeleteAxisAlignedRectangle(rect, frame);
            Assert.True(fired);
        }
        finally
        {
            ctx.ApplicationEvents.AnimationChainsChanged -= Handler;
        }
    }

    // ── DeleteCircle ─────────────────────────────────────────────────────────

    [Fact]
    public void DeleteCircle_RemovesCircleFromOwnerFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Jump", 1);
        var frame = chain.Frames[0];
        var circle = new CircleSave { Name = "Ring", Radius = 5 };
        frame.ShapesSave!.Shapes.Add(circle);

        ctx.AppCommands.DeleteCircle(circle, frame);

        Assert.Empty(frame.ShapesSave!.CircleSaves);
    }

    [Fact]
    public void DeleteCircle_WhenNotOwnedByFrame_DoesNotThrow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Jump", 2);
        var frame1 = chain.Frames[0];
        var frame2 = chain.Frames[1];
        var circle = new CircleSave { Name = "Ring", Radius = 5 };
        frame1.ShapesSave!.Shapes.Add(circle);

        // Call with wrong owner - should not remove and should not throw
        ctx.AppCommands.DeleteCircle(circle, frame2);

        Assert.Single(frame1.ShapesSave!.CircleSaves);
    }

    [Fact]
    public void DeleteCircle_FiresAnimationChainsChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Jump", 1);
        var frame = chain.Frames[0];
        var circle = new CircleSave { Name = "Ring", Radius = 5 };
        frame.ShapesSave!.Shapes.Add(circle);

        bool fired = false;
        void Handler() => fired = true;
        ctx.ApplicationEvents.AnimationChainsChanged += Handler;
        try
        {
            ctx.AppCommands.DeleteCircle(circle, frame);
            Assert.True(fired);
        }
        finally
        {
            ctx.ApplicationEvents.AnimationChainsChanged -= Handler;
        }
    }

    // ── MatchRectangleToFrame / MatchCircleToFrame ───────────────────────────

    /// <summary>A project whose Tex.png is <paramref name="width"/> x <paramref name="height"/>, so frame sizes resolve to pixels.</summary>
    private static string SetupProjectWithTexture(TestServices ctx, int width, int height)
    {
        string dir = Path.Combine(Path.GetTempPath(), "AnimationEditorCoreTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        byte[] header = new byte[24];
        header[0] = 0x89; header[1] = 0x50; header[2] = 0x4E; header[3] = 0x47;
        header[4] = 0x0D; header[5] = 0x0A; header[6] = 0x1A; header[7] = 0x0A;
        header[11] = 13; header[12] = 0x49; header[13] = 0x48; header[14] = 0x44; header[15] = 0x52;
        header[16] = (byte)(width >> 24); header[17] = (byte)(width >> 16); header[18] = (byte)(width >> 8); header[19] = (byte)width;
        header[20] = (byte)(height >> 24); header[21] = (byte)(height >> 16); header[22] = (byte)(height >> 8); header[23] = (byte)height;
        File.WriteAllBytes(Path.Combine(dir, "Tex.png"), header);
        ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");
        return dir;
    }

    [Fact]
    public void MatchRectangleToFrame_SetsScaleToHalfTheFramePixelSize()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        SetupProjectWithTexture(ctx, 64, 64);
        var frame = TestHelpers.MakeFrame();
        frame.LeftCoordinate = 0f; frame.RightCoordinate = 0.5f;   // 32 px wide
        frame.TopCoordinate = 0f; frame.BottomCoordinate = 0.25f;  // 16 px tall
        var rect = new AARectSave { ScaleX = 3f, ScaleY = 3f };

        ctx.AppCommands.MatchRectangleToFrame(rect, frame);

        rect.ScaleX.ShouldBe(16f);
        rect.ScaleY.ShouldBe(8f);
    }

    [Fact]
    public void MatchRectangleToFrame_WithAnUnknownTextureSize_KeepsTheScale()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = TestHelpers.MakeFrame("missing.png");
        var rect = new AARectSave { ScaleX = 3f, ScaleY = 5f };

        ctx.AppCommands.MatchRectangleToFrame(rect, frame);

        (rect.ScaleX, rect.ScaleY).ShouldBe((3f, 5f));
    }

    [Fact]
    public void MatchRectanglesToFrames_SizesEachRectangleFromItsOwnFrame_AsOneUndoStep()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        SetupProjectWithTexture(ctx, 64, 64);
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        chain.Frames[0].TextureName = "Tex.png";
        chain.Frames[1].TextureName = "Tex.png";
        chain.Frames[0].LeftCoordinate = 0f; chain.Frames[0].RightCoordinate = 0.5f;
        chain.Frames[0].TopCoordinate = 0f; chain.Frames[0].BottomCoordinate = 0.5f;
        chain.Frames[1].LeftCoordinate = 0f; chain.Frames[1].RightCoordinate = 0.25f;
        chain.Frames[1].TopCoordinate = 0f; chain.Frames[1].BottomCoordinate = 1f;
        var first = new AARectSave { Name = "A", ScaleX = 1f, ScaleY = 1f };
        var second = new AARectSave { Name = "B", ScaleX = 1f, ScaleY = 1f };
        chain.Frames[0].ShapesSave = new ShapesSave { Shapes = { first } };
        chain.Frames[1].ShapesSave = new ShapesSave { Shapes = { second } };

        ctx.AppCommands.MatchRectanglesToFrames(new List<AARectSave> { first, second });

        (first.ScaleX, first.ScaleY).ShouldBe((16f, 16f));
        (second.ScaleX, second.ScaleY).ShouldBe((8f, 32f));
        ctx.UndoManager.Undo();
        (first.ScaleX, second.ScaleY).ShouldBe((1f, 1f));
    }

    [Fact]
    public void MatchRectangleToFrame_SetsRectangleXFromFrameRelativeX()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = TestHelpers.MakeFrame();
        frame.RelativeX = 12f;
        frame.RelativeY = -4f;
        var rect = new AARectSave();

        ctx.AppCommands.MatchRectangleToFrame(rect, frame);

        Assert.Equal(12f, rect.X);
    }

    [Fact]
    public void MatchRectangleToFrame_SetsRectangleYFromFrameRelativeY()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = TestHelpers.MakeFrame();
        frame.RelativeX = 3f;
        frame.RelativeY = -9f;
        var rect = new AARectSave();

        ctx.AppCommands.MatchRectangleToFrame(rect, frame);

        Assert.Equal(-9f, rect.Y);
    }

    // ── MatchRectanglesToFrames (issue #567: multi-selection) ────────────────

    [Fact]
    public void MatchRectanglesToFrames_MatchesEveryRectangleInTheBatch()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        frame.RelativeX = 12f;
        frame.RelativeY = -4f;
        var r0 = new AARectSave { Name = "R0" };
        var r1 = new AARectSave { Name = "R1" };
        frame.ShapesSave!.Shapes.Add(r0);
        frame.ShapesSave!.Shapes.Add(r1);

        ctx.AppCommands.MatchRectanglesToFrames(new List<AARectSave> { r0, r1 });

        Assert.Equal(12f, r0.X);
        Assert.Equal(-4f, r0.Y);
        Assert.Equal(12f, r1.X);
        Assert.Equal(-4f, r1.Y);
    }

    [Fact]
    public void MatchRectanglesToFrames_UsesEachRectanglesOwnFrame_NotASharedFrame()
    {
        // Root cause from #567: a naive fix would resize every selected rectangle to the
        // single "current" frame. A multi-selection spanning two frames must match each
        // rectangle to its *own* containing frame instead.
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var frameA = chain.Frames[0];
        var frameB = chain.Frames[1];
        frameA.RelativeX = 5f;  frameA.RelativeY = 6f;
        frameB.RelativeX = 50f; frameB.RelativeY = 60f;
        var rectInA = new AARectSave { Name = "InA" };
        var rectInB = new AARectSave { Name = "InB" };
        frameA.ShapesSave!.Shapes.Add(rectInA);
        frameB.ShapesSave!.Shapes.Add(rectInB);

        ctx.AppCommands.MatchRectanglesToFrames(new List<AARectSave> { rectInA, rectInB });

        Assert.Equal(5f, rectInA.X);
        Assert.Equal(6f, rectInA.Y);
        Assert.Equal(50f, rectInB.X);
        Assert.Equal(60f, rectInB.Y);
    }

    [Fact]
    public void MatchRectanglesToFrames_RecordsOneUndoStepForTheWholeBatch()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var frameA = chain.Frames[0];
        var frameB = chain.Frames[1];
        frameA.RelativeX = 5f;  frameA.RelativeY = 6f;
        frameB.RelativeX = 50f; frameB.RelativeY = 60f;
        var rectInA = new AARectSave { Name = "InA", X = 1f, Y = 1f };
        var rectInB = new AARectSave { Name = "InB", X = 2f, Y = 2f };
        frameA.ShapesSave!.Shapes.Add(rectInA);
        frameB.ShapesSave!.Shapes.Add(rectInB);

        ctx.AppCommands.MatchRectanglesToFrames(new List<AARectSave> { rectInA, rectInB });
        Assert.True(ctx.UndoManager.CanUndo);

        ctx.UndoManager.Undo();

        Assert.Equal(1f, rectInA.X);
        Assert.Equal(1f, rectInA.Y);
        Assert.Equal(2f, rectInB.X);
        Assert.Equal(2f, rectInB.Y);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void MatchCircleToFrame_SetsCircleXFromFrameRelativeX()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = TestHelpers.MakeFrame();
        frame.RelativeX = 7f;
        frame.RelativeY = 0f;
        var circle = new CircleSave();

        ctx.AppCommands.MatchCircleToFrame(circle, frame);

        Assert.Equal(7f, circle.X);
    }

    [Fact]
    public void MatchCircleToFrame_SetsCircleYFromFrameRelativeY()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = TestHelpers.MakeFrame();
        frame.RelativeX = 0f;
        frame.RelativeY = 15f;
        var circle = new CircleSave();

        ctx.AppCommands.MatchCircleToFrame(circle, frame);

        Assert.Equal(15f, circle.Y);
    }
}
