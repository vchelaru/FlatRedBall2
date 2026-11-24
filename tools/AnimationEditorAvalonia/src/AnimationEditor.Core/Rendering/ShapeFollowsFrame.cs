using System.Linq;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Rendering;

/// <summary>The rule that keeps a selected shape selected when the frame changes underneath it.</summary>
public static class ShapeFollowsFrame
{
    /// <summary>
    /// The shape on <paramref name="frame"/> with the same name as <paramref name="selected"/>, or null when there is
    /// none, nothing is selected or the name is blank. Names are unique per frame across rectangles, circles and
    /// polygons, so there is at most one match.
    /// </summary>
    public static ShapeSave? Find(object? selected, AnimationFrameSave frame)
    {
        if (selected is not ShapeSave { Name: { Length: > 0 } name }) return null;
        return frame.ShapesSave?.Shapes.OfType<ShapeSave>().FirstOrDefault(s => s.Name == name);
    }
}
