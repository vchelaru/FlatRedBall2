using FlatRedBall2.Animation.Content;
using System.Collections.Generic;

namespace AnimationEditor.Core.Utilities;

/// <summary>
/// Finds which <see cref="AnimationFrameSave"/> owns a given shape (AARectSave/CircleSave/
/// PolygonSave), by reference, across every chain in a project.
/// </summary>
public static class ShapeFrameLookup
{
    public static AnimationFrameSave? FindFrameForShape(AnimationChainListSave? acls, object shape)
    {
        if (acls is null) return null;
        foreach (var chain in acls.AnimationChains)
        {
            foreach (var frame in chain.Frames)
            {
                if (frame.ShapesSave?.Shapes.Contains(shape) == true)
                    return frame;
            }
        }
        return null;
    }

    /// <summary>
    /// True when two or more of <paramref name="shapes"/> are owned by the same frame. Shape
    /// names only need to be unique within a single frame, not across frames (see
    /// <see cref="AARectSave.Name"/>), so batch-renaming shapes that share a frame to one literal
    /// name would collide, while batch-renaming shapes spread across different frames is safe.
    /// </summary>
    public static bool HasSameFrameCollision(AnimationChainListSave? acls, IReadOnlyList<object> shapes)
    {
        if (shapes.Count < 2) return false;
        var framesSeen = new HashSet<AnimationFrameSave>();
        foreach (var shape in shapes)
        {
            var frame = FindFrameForShape(acls, shape);
            if (frame != null && !framesSeen.Add(frame))
                return true;
        }
        return false;
    }
}
