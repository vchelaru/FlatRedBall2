using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AppCommandsShapePropsBulkTests
{
    [Fact]
    public void SetRectPropsBulk_MultipleRectangles_AppliesScaleToAll()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var rectA = new AARectSave { Name = "A", ScaleX = 8f, ScaleY = 8f };
        var rectB = new AARectSave { Name = "B", ScaleX = 8f, ScaleY = 8f };
        chain.Frames[0].ShapesSave!.Add(rectA);
        chain.Frames[1].ShapesSave!.Add(rectB);

        ctx.AppCommands.SetRectPropsBulk(
            new List<AARectSave> { rectA, rectB }, null, null, null, 20f, 20f);

        Assert.Equal(20f, rectA.ScaleX);
        Assert.Equal(20f, rectA.ScaleY);
        Assert.Equal(20f, rectB.ScaleX);
        Assert.Equal(20f, rectB.ScaleY);
    }

    [Fact]
    public void SetRectPropsBulk_NullXY_LeavesXYUnchangedPerRect()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var rectA = new AARectSave { Name = "A", X = 1f, Y = 2f, ScaleX = 8f, ScaleY = 8f };
        var rectB = new AARectSave { Name = "B", X = 3f, Y = 4f, ScaleX = 8f, ScaleY = 8f };
        chain.Frames[0].ShapesSave!.Add(rectA);
        chain.Frames[1].ShapesSave!.Add(rectB);

        ctx.AppCommands.SetRectPropsBulk(
            new List<AARectSave> { rectA, rectB }, null, null, null, 20f, 20f);

        Assert.Equal(1f, rectA.X);
        Assert.Equal(2f, rectA.Y);
        Assert.Equal(3f, rectB.X);
        Assert.Equal(4f, rectB.Y);
    }

    [Fact]
    public void SetRectPropsBulk_Undo_RestoresOriginalScale()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var rect = new AARectSave { Name = "A", ScaleX = 8f, ScaleY = 8f };
        chain.Frames[0].ShapesSave!.Add(rect);

        ctx.AppCommands.SetRectPropsBulk(new List<AARectSave> { rect }, null, null, null, 20f, 20f);
        ctx.UndoManager.Undo();

        Assert.Equal(8f, rect.ScaleX);
        Assert.Equal(8f, rect.ScaleY);
    }

    [Fact]
    public void SetRectPropsBulk_ConsecutiveEdits_CoalesceIntoSingleUndoEntryRestoringOriginalValue()
    {
        // MainWindow's multi-select rect panel commits on every ValueChanged, same as the
        // single-select path (#897) — typing "32" for X must not create two undo entries.
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var rect = new AARectSave { Name = "A", X = 0f, ScaleX = 8f, ScaleY = 8f };
        chain.Frames[0].ShapesSave!.Add(rect);

        ctx.AppCommands.SetRectPropsBulk(new List<AARectSave> { rect }, null, 3f, null, null, null);
        ctx.AppCommands.SetRectPropsBulk(new List<AARectSave> { rect }, null, 32f, null, null, null);

        Assert.Single(ctx.UndoManager.UndoHistory);
        ctx.UndoManager.Undo();
        Assert.Equal(0f, rect.X);
    }

    [Fact]
    public void SetCirclePropsBulk_MultipleCircles_AppliesRadiusToAll()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Jump", 2);
        var circleA = new CircleSave { Name = "A", Radius = 8f };
        var circleB = new CircleSave { Name = "B", Radius = 8f };
        chain.Frames[0].ShapesSave!.Add(circleA);
        chain.Frames[1].ShapesSave!.Add(circleB);

        ctx.AppCommands.SetCirclePropsBulk(
            new List<CircleSave> { circleA, circleB }, null, null, null, 15f);

        Assert.Equal(15f, circleA.Radius);
        Assert.Equal(15f, circleB.Radius);
    }

    [Fact]
    public void SetCirclePropsBulk_ConsecutiveEdits_CoalesceIntoSingleUndoEntryRestoringOriginalValue()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Jump", 1);
        var circle = new CircleSave { Name = "A", X = 0f, Radius = 8f };
        chain.Frames[0].ShapesSave!.Add(circle);

        ctx.AppCommands.SetCirclePropsBulk(new List<CircleSave> { circle }, null, 3f, null, null);
        ctx.AppCommands.SetCirclePropsBulk(new List<CircleSave> { circle }, null, 32f, null, null);

        Assert.Single(ctx.UndoManager.UndoHistory);
        ctx.UndoManager.Undo();
        Assert.Equal(0f, circle.X);
    }

    [Fact]
    public void SetRectPropsBulk_NameGiven_AppliesToEveryRect()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var rectA = new AARectSave { Name = "A" };
        var rectB = new AARectSave { Name = "B" };
        chain.Frames[0].ShapesSave!.Add(rectA);
        chain.Frames[1].ShapesSave!.Add(rectB);

        ctx.AppCommands.SetRectPropsBulk(
            new List<AARectSave> { rectA, rectB }, "Hitbox", null, null, null, null);

        Assert.Equal("Hitbox", rectA.Name);
        Assert.Equal("Hitbox", rectB.Name);
    }

    [Fact]
    public void SetCirclePropsBulk_NameGiven_AppliesToEveryCircle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Jump", 2);
        var circleA = new CircleSave { Name = "A" };
        var circleB = new CircleSave { Name = "B" };
        chain.Frames[0].ShapesSave!.Add(circleA);
        chain.Frames[1].ShapesSave!.Add(circleB);

        ctx.AppCommands.SetCirclePropsBulk(
            new List<CircleSave> { circleA, circleB }, "Hurtbox", null, null, null);

        Assert.Equal("Hurtbox", circleA.Name);
        Assert.Equal("Hurtbox", circleB.Name);
    }

    [Fact]
    public void HasSameFrameNameCollision_RectsAcrossDifferentFrames_ReturnsFalse()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var rectA = new AARectSave { Name = "A" };
        var rectB = new AARectSave { Name = "B" };
        chain.Frames[0].ShapesSave!.Add(rectA);
        chain.Frames[1].ShapesSave!.Add(rectB);

        Assert.False(ctx.AppCommands.HasSameFrameNameCollision(new List<object> { rectA, rectB }));
    }

    [Fact]
    public void HasSameFrameNameCollision_RectsOnSameFrame_ReturnsTrue()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var rectA = new AARectSave { Name = "A" };
        var rectB = new AARectSave { Name = "B" };
        chain.Frames[0].ShapesSave!.Add(rectA);
        chain.Frames[0].ShapesSave!.Add(rectB);

        Assert.True(ctx.AppCommands.HasSameFrameNameCollision(new List<object> { rectA, rectB }));
    }
}

[Collection("SequentialSingletons")]
public class AppCommandsPolygonPropsBulkTests
{
    private static PolygonSave Poly(string name, float x = 0f, float y = 0f) => new() { Name = name, X = x, Y = y };

    [Fact]
    public void SetPolygonPropsBulk_AppliesNameAndXToAll_LeavingNullYPerPolygon()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var a = Poly("A", y: 2f);
        var b = Poly("B", y: 4f);
        chain.Frames[0].ShapesSave!.Add(a);
        chain.Frames[1].ShapesSave!.Add(b);

        ctx.AppCommands.SetPolygonPropsBulk(new List<PolygonSave> { a, b }, "Blade", 9f, null);

        Assert.Equal("Blade", a.Name);
        Assert.Equal("Blade", b.Name);
        Assert.Equal(9f, a.X);
        Assert.Equal(9f, b.X);
        Assert.Equal(2f, a.Y);
        Assert.Equal(4f, b.Y);
    }

    [Fact]
    public void SetPolygonPropsBulk_NullName_KeepsEachName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var a = Poly("A");
        var b = Poly("B");
        chain.Frames[0].ShapesSave!.Add(a);
        chain.Frames[1].ShapesSave!.Add(b);

        ctx.AppCommands.SetPolygonPropsBulk(new List<PolygonSave> { a, b }, null, 5f, 5f);

        Assert.Equal("A", a.Name);
        Assert.Equal("B", b.Name);
    }

    [Fact]
    public void SetPolygonPropsBulk_Undo_RestoresEveryPolygonInOneStep()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var a = Poly("A", x: 1f);
        var b = Poly("B", x: 3f);
        chain.Frames[0].ShapesSave!.Add(a);
        chain.Frames[1].ShapesSave!.Add(b);

        ctx.AppCommands.SetPolygonPropsBulk(new List<PolygonSave> { a, b }, null, 50f, null);
        ctx.UndoManager.Undo();

        Assert.Equal(1f, a.X);
        Assert.Equal(3f, b.X);
    }

    [Fact]
    public void SetPolygonPropsBulk_SkipsPolygonsInLockedChains()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var locked = TestHelpers.MakeChain(ctx.Acls, "Locked", 1);
        var open = TestHelpers.MakeChain(ctx.Acls, "Open", 1);
        locked.IsLocked = true;
        var a = Poly("A", x: 1f);
        var b = Poly("B", x: 3f);
        locked.Frames[0].ShapesSave!.Add(a);
        open.Frames[0].ShapesSave!.Add(b);

        ctx.AppCommands.SetPolygonPropsBulk(new List<PolygonSave> { a, b }, null, 50f, null);

        Assert.Equal(1f, a.X);
        Assert.Equal(50f, b.X);
    }
}
