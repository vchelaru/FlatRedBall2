using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
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
        chain.Frames[0].ShapesSave!.Shapes.Add(rectA);
        chain.Frames[1].ShapesSave!.Shapes.Add(rectB);

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
        chain.Frames[0].ShapesSave!.Shapes.Add(rectA);
        chain.Frames[1].ShapesSave!.Shapes.Add(rectB);

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
        chain.Frames[0].ShapesSave!.Shapes.Add(rect);

        ctx.AppCommands.SetRectPropsBulk(new List<AARectSave> { rect }, null, null, null, 20f, 20f);
        ctx.UndoManager.Undo();

        Assert.Equal(8f, rect.ScaleX);
        Assert.Equal(8f, rect.ScaleY);
    }

    [Fact]
    public void SetCirclePropsBulk_MultipleCircles_AppliesRadiusToAll()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Jump", 2);
        var circleA = new CircleSave { Name = "A", Radius = 8f };
        var circleB = new CircleSave { Name = "B", Radius = 8f };
        chain.Frames[0].ShapesSave!.Shapes.Add(circleA);
        chain.Frames[1].ShapesSave!.Shapes.Add(circleB);

        ctx.AppCommands.SetCirclePropsBulk(
            new List<CircleSave> { circleA, circleB }, null, null, null, 15f);

        Assert.Equal(15f, circleA.Radius);
        Assert.Equal(15f, circleB.Radius);
    }
}
