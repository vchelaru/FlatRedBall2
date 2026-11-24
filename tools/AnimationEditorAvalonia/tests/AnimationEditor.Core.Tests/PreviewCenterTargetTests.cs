using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>The point a double-clicked tree row centers the preview on (#1925).</summary>
public class PreviewCenterTargetTests
{
    private static (AnimationChainSave Chain, FrameLengthUnit Unit) Chain(AnimationFrameSave frame)
    {
        var chain = new AnimationChainSave { Name = "c" };
        chain.Frames.Add(frame);
        return (chain, FrameLengthUnit.Of(null));
    }

    [Fact]
    public void FrameRow_IsTheFramesOffset()
    {
        var frame = new AnimationFrameSave { FrameLength = 0.1f, RelativeX = 12, RelativeY = 7 };
        var (chain, unit) = Chain(frame);
        Assert.Equal((12f, 7f), PreviewCenterTarget.For(chain, unit, 0, frame));
    }

    [Fact]
    public void ShapeRow_IsTheShapesOffset_NotAddedToTheFrameOffset()
    {
        var frame = new AnimationFrameSave { FrameLength = 0.1f, RelativeX = 12, RelativeY = 7, ShapesSave = new ShapesSave() };
        var rect = new AARectSave { Name = "R", X = 30, Y = -10, ScaleX = 4, ScaleY = 4 };
        frame.ShapesSave.Add(rect);
        var (chain, unit) = Chain(frame);
        Assert.Equal((30f, -10f), PreviewCenterTarget.For(chain, unit, 0, rect));
    }

    [Fact]
    public void PolygonRow_IsTheCenterOfItsPoints_NotItsOrigin()
    {
        var frame = new AnimationFrameSave { FrameLength = 0.1f, ShapesSave = new ShapesSave() };
        var poly = new PolygonSave { Name = "P", X = 0, Y = 0 };
        poly.Points.Add(new Vector2Save { X = 40, Y = 20 });
        poly.Points.Add(new Vector2Save { X = 60, Y = 20 });
        poly.Points.Add(new Vector2Save { X = 60, Y = 40 });
        frame.ShapesSave.Add(poly);
        var (chain, unit) = Chain(frame);
        Assert.Equal((50f, 30f), PreviewCenterTarget.For(chain, unit, 0, poly));
    }
}
