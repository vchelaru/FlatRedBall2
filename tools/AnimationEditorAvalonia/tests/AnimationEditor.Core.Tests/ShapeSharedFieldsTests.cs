using AnimationEditor.Core.ViewModels;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ShapeSharedFieldsTests
{
    private static AARectSave Rect(string name, float x, float y) => new() { Name = name, X = x, Y = y, ScaleX = 8, ScaleY = 8 };
    private static CircleSave Circle(string name, float x, float y) => new() { Name = name, X = x, Y = y, Radius = 4 };
    private static PolygonSave Poly(string name, float x, float y) => new() { Name = name, X = x, Y = y };

    private static ShapeSharedFields For(params ShapeSave[] shapes) =>
        ShapeSharedFields.For(shapes, _ => null, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

    [Fact]
    public void For_RectangleAndCircle_IsMixedType()
    {
        Assert.True(For(Rect("a", 0, 0), Circle("b", 0, 0)).IsMixedType);
    }

    [Fact]
    public void For_RectangleAndPolygon_IsMixedType()
    {
        Assert.True(For(Rect("a", 0, 0), Poly("b", 0, 0)).IsMixedType);
    }

    [Fact]
    public void For_TwoCircles_IsNotMixedType()
    {
        Assert.False(For(Circle("a", 0, 0), Circle("b", 0, 0)).IsMixedType);
    }

    [Fact]
    public void For_DifferentOffsets_ShowNullForTheDifferingAxis()
    {
        var state = For(Rect("a", -20, 5), Circle("b", 20, 5));

        Assert.Null(state.X);
        Assert.Equal(5f, state.Y);
    }

    [Fact]
    public void For_SameOffsets_ShowTheCommonValue()
    {
        var state = For(Rect("a", 3, 4), Circle("b", 3, 4));

        Assert.Equal(3f, state.X);
        Assert.Equal(4f, state.Y);
    }

    [Fact]
    public void For_DifferentNames_ShowNullName_AndSameNameShowsIt()
    {
        Assert.Null(For(Rect("a", 0, 0), Circle("b", 0, 0)).Name);
        Assert.Equal("hit", For(Rect("hit", 0, 0), Circle("hit", 0, 0)).Name);
    }

    [Fact]
    public void For_NoShapes_IsEmpty()
    {
        var state = ShapeSharedFields.For(new List<ShapeSave>(), _ => null, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.False(state.IsMixedType);
        Assert.Null(state.X);
        Assert.Null(state.Name);
    }
}
