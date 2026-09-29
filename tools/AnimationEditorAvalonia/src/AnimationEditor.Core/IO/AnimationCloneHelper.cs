using FlatRedBall2.AnimationEditorCommon;

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
            FlipDiagonal     = source.FlipDiagonal,
            RelativeX        = source.RelativeX,
            RelativeY        = source.RelativeY,
            Red              = source.Red,
            Green            = source.Green,
            Blue             = source.Blue,
            Alpha            = source.Alpha,
            ColorOperation   = source.ColorOperation,
        };

        if (source.ShapesSave is not null)
        {
            copy.ShapesSave = new ShapesSave();
            foreach (var shape in source.ShapesSave.Shapes)
                if (CloneShape(shape) is { } shapeCopy)
                    copy.ShapesSave.Shapes.Add(shapeCopy);
        }

        foreach (var frameEvent in source.Events)
            copy.Events.Add(frameEvent.Clone());

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
        PolygonSave p => new PolygonSave { Name = p.Name, X = p.X, Y = p.Y, Points = Utilities.PolygonVertices.CopyPoints(p) },
        _ => null,
    };
}
