using AnimationEditor.Core.DragDrop;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>Pure drop-target logic for dragging a shape to reorder it within its frame (issue #1285).</summary>
public class ShapeDropResolverTests
{
    private static AnimationFrameSave FrameWithShapes(int count, out object[] shapes)
    {
        var frame = new AnimationFrameSave { ShapesSave = new ShapesSave() };
        for (int i = 0; i < count; i++)
            frame.ShapesSave.Shapes.Add(new AARectSave { Name = $"S{i}" });
        shapes = frame.ShapesSave.Shapes.ToArray();
        return frame;
    }

    private static AnimationFrameSave? Owner(AnimationFrameSave frame, object shape)
        => frame.ShapesSave!.Shapes.Contains(shape) ? frame : null;

    // Dragging shape 1 of 4 (S0 S1 S2 S3): slots 1 and 2 leave the order unchanged.
    [Theory]
    [InlineData(0, ShapeRowHalf.Upper, 0, true)]
    [InlineData(0, ShapeRowHalf.Lower, 1, false)]
    [InlineData(1, ShapeRowHalf.Upper, 1, false)]
    [InlineData(1, ShapeRowHalf.Lower, 2, false)]
    [InlineData(2, ShapeRowHalf.Upper, 2, false)]
    [InlineData(2, ShapeRowHalf.Lower, 3, true)]
    [InlineData(3, ShapeRowHalf.Upper, 3, true)]
    [InlineData(3, ShapeRowHalf.Lower, 4, true)]
    public void Resolve_OverSiblingShape_PicksInsertSlot(int targetIdx, ShapeRowHalf half, int expectedInsert, bool expectedValid)
    {
        var frame = FrameWithShapes(4, out var shapes);

        var result = ShapeDropResolver.Resolve(shapes[targetIdx], half, shapes[1], frame, s => Owner(frame, s));

        Assert.Equal(expectedValid, result.IsValid);
        Assert.Equal(expectedInsert, result.InsertIndex);
    }

    [Fact]
    public void Resolve_OverFrameRow_AppendsToEnd()
    {
        var frame = FrameWithShapes(3, out var shapes);

        var result = ShapeDropResolver.Resolve(frame, ShapeRowHalf.Upper, shapes[0], frame, s => Owner(frame, s));

        Assert.True(result.IsValid);
        Assert.Equal(3, result.InsertIndex);
    }

    [Fact]
    public void Resolve_OverFrameRow_WhenDraggedIsAlreadyLast_IsInvalid()
    {
        var frame = FrameWithShapes(3, out var shapes);

        var result = ShapeDropResolver.Resolve(frame, ShapeRowHalf.Upper, shapes[2], frame, s => Owner(frame, s));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Resolve_ShapeInOtherFrame_IsInvalid()
    {
        var frame = FrameWithShapes(3, out var shapes);
        var other = FrameWithShapes(2, out var otherShapes);

        var result = ShapeDropResolver.Resolve(otherShapes[0], ShapeRowHalf.Upper, shapes[0], frame,
            s => Owner(other, s) ?? Owner(frame, s));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Resolve_OtherFrameRow_IsInvalid()
    {
        var frame = FrameWithShapes(3, out var shapes);
        var other = FrameWithShapes(2, out _);

        var result = ShapeDropResolver.Resolve(other, ShapeRowHalf.Upper, shapes[0], frame, s => Owner(frame, s));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Resolve_OverChainOrNothing_IsInvalid()
    {
        var frame = FrameWithShapes(3, out var shapes);

        Assert.False(ShapeDropResolver.Resolve(new AnimationChainSave(), ShapeRowHalf.Upper, shapes[0], frame, s => Owner(frame, s)).IsValid);
        Assert.False(ShapeDropResolver.Resolve(null, ShapeRowHalf.Upper, shapes[0], frame, s => Owner(frame, s)).IsValid);
    }
}
