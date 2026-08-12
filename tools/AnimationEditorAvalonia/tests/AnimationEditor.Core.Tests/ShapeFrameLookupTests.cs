using AnimationEditor.Core.Utilities;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ShapeFrameLookupTests
{
    [Fact]
    public void FindFrameForShape_ShapeInFrame_ReturnsOwningFrame()
    {
        var acls = new AnimationChainListSave();
        var chain = TestHelpers.MakeChain(acls, "Walk", 2);
        var rect = new AARectSave { Name = "A" };
        chain.Frames[0].ShapesSave!.Shapes.Add(rect);

        var found = ShapeFrameLookup.FindFrameForShape(acls, rect);

        Assert.Same(chain.Frames[0], found);
    }

    [Fact]
    public void FindFrameForShape_ShapeNotInAnyFrame_ReturnsNull()
    {
        var acls = new AnimationChainListSave();
        TestHelpers.MakeChain(acls, "Walk", 1);
        var rect = new AARectSave { Name = "Orphan" };

        var found = ShapeFrameLookup.FindFrameForShape(acls, rect);

        Assert.Null(found);
    }

    [Fact]
    public void HasSameFrameCollision_ShapesOnDifferentFrames_ReturnsFalse()
    {
        var acls = new AnimationChainListSave();
        var chain = TestHelpers.MakeChain(acls, "Walk", 2);
        var rectA = new AARectSave { Name = "A" };
        var rectB = new AARectSave { Name = "B" };
        chain.Frames[0].ShapesSave!.Shapes.Add(rectA);
        chain.Frames[1].ShapesSave!.Shapes.Add(rectB);

        var collision = ShapeFrameLookup.HasSameFrameCollision(acls, new List<object> { rectA, rectB });

        Assert.False(collision);
    }

    [Fact]
    public void HasSameFrameCollision_TwoShapesOnSameFrame_ReturnsTrue()
    {
        var acls = new AnimationChainListSave();
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var rectA = new AARectSave { Name = "A" };
        var rectB = new AARectSave { Name = "B" };
        chain.Frames[0].ShapesSave!.Shapes.Add(rectA);
        chain.Frames[0].ShapesSave!.Shapes.Add(rectB);

        var collision = ShapeFrameLookup.HasSameFrameCollision(acls, new List<object> { rectA, rectB });

        Assert.True(collision);
    }

    [Fact]
    public void HasSameFrameCollision_SingleShape_ReturnsFalse()
    {
        var acls = new AnimationChainListSave();
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var rectA = new AARectSave { Name = "A" };
        chain.Frames[0].ShapesSave!.Shapes.Add(rectA);

        var collision = ShapeFrameLookup.HasSameFrameCollision(acls, new List<object> { rectA });

        Assert.False(collision);
    }
}
