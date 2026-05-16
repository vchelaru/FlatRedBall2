using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using System.Linq;
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
