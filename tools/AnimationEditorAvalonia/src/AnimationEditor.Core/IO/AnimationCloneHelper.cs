using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Deep-clones animation content for copy, paste, and duplicate operations.
/// </summary>
public static class AnimationCloneHelper
{
    public static AnimationFrameSave CloneFrame(AnimationFrameSave source)
    {
        var copy = new AnimationFrameSave
        {
            TextureName      = source.TextureName,
            LeftCoordinate   = source.LeftCoordinate,
            RightCoordinate  = source.RightCoordinate,
            TopCoordinate    = source.TopCoordinate,
            BottomCoordinate = source.BottomCoordinate,
            FrameLength      = source.FrameLength,
            FlipHorizontal   = source.FlipHorizontal,
            FlipVertical     = source.FlipVertical,
            RelativeX        = source.RelativeX,
            RelativeY        = source.RelativeY,
            Red              = source.Red,
            Green            = source.Green,
            Blue             = source.Blue,
            ColorOperation   = source.ColorOperation,
            ShapesSave       = new ShapesSave(),
        };

        if (source.ShapesSave is not null)
        {
            foreach (var shape in source.ShapesSave.Shapes)
                if (CloneShape(shape) is { } shapeCopy)
                    copy.ShapesSave!.Shapes.Add(shapeCopy);
        }

        return copy;
    }

    public static AnimationChainSave CloneChain(AnimationChainSave source)
    {
        var copy = new AnimationChainSave { Name = source.Name };
        foreach (var frame in source.Frames)
            copy.Frames.Add(CloneFrame(frame));
        return copy;
    }

    public static object? CloneShape(object shape) => shape switch
    {
        AARectSave r => new AARectSave { Name = r.Name, X = r.X, Y = r.Y, ScaleX = r.ScaleX, ScaleY = r.ScaleY },
        CircleSave c => new CircleSave { Name = c.Name, X = c.X, Y = c.Y, Radius = c.Radius },
        _ => null,
    };
}
