using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary><see cref="ShapeFollowsFrame.Find"/>: the shape to select on a new frame, by name.</summary>
public class ShapeFollowsFrameTests
{
    private static AnimationFrameSave FrameWith(params ShapeSave[] shapes)
    {
        var frame = new AnimationFrameSave { ShapesSave = new ShapesSave() };
        foreach (var shape in shapes) frame.ShapesSave.Add(shape);
        return frame;
    }

    [Fact]
    public void SameNamedShapeOnTheNewFrame_IsReturned()
    {
        var target = new AARectSave { Name = "Hit" };
        var frame = FrameWith(new AARectSave { Name = "Other" }, target);

        Assert.Same(target, ShapeFollowsFrame.Find(new AARectSave { Name = "Hit" }, frame));
    }

    [Fact]
    public void MatchIsByNameAlone_SoAnotherShapeTypeStillMatches()
    {
        var circle = new CircleSave { Name = "Hit" };

        Assert.Same(circle, ShapeFollowsFrame.Find(new AARectSave { Name = "Hit" }, FrameWith(circle)));
    }

    [Fact]
    public void NoShapeWithThatName_ReturnsNull() =>
        Assert.Null(ShapeFollowsFrame.Find(new AARectSave { Name = "Hit" }, FrameWith(new AARectSave { Name = "Other" })));

    [Fact]
    public void FrameWithNoShapes_ReturnsNull() =>
        Assert.Null(ShapeFollowsFrame.Find(new AARectSave { Name = "Hit" }, new AnimationFrameSave()));

    [Fact]
    public void NothingSelected_ReturnsNull() =>
        Assert.Null(ShapeFollowsFrame.Find(null, FrameWith(new AARectSave { Name = "Hit" })));

    [Fact]
    public void BlankName_ReturnsNull() =>
        Assert.Null(ShapeFollowsFrame.Find(new AARectSave { Name = "" }, FrameWith(new AARectSave { Name = "" })));
}
