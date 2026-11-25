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
            SkipOffsetKeyframe  = source.SkipOffsetKeyframe,
            SkipColorKeyframe   = source.SkipColorKeyframe,
            OffsetInterpolation = source.OffsetInterpolation,
            OffsetEasing        = source.OffsetEasing,
            ColorInterpolation  = source.ColorInterpolation,
            ColorEasing         = source.ColorEasing,
        };

        if (source.ShapesSave is not null)
        {
            copy.ShapesSave = new ShapesSave();
            foreach (var shape in source.ShapesSave.Shapes)
                if (CloneShape(shape) is { } shapeCopy)
                    copy.ShapesSave.Add(shapeCopy);
        }

        foreach (var frameEvent in source.Events)
            copy.Events.Add(frameEvent.Clone());

        return copy;
    }

    public static AnimationChainSave CloneChain(AnimationChainSave source)
    {
        // The lock goes along to a duplicate or a paste on purpose for now; this may be revisited.
        var copy = new AnimationChainSave
        {
            Name = source.Name, Loop = source.Loop, PingPong = source.PingPong, Reverse = source.Reverse, IsLocked = source.IsLocked,
        };
        foreach (var frame in source.Frames)
            copy.Frames.Add(CloneFrame(frame));
        return copy;
    }

    public static object? CloneShape(object shape) => (shape as ShapeSave)?.Clone();
}
