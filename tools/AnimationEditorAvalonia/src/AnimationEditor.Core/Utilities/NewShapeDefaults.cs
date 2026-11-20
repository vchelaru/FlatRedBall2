using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Utilities;

/// <summary>
/// The shapes the editor's Add Rectangle / Add Circle / Add Polygon create: default size, centred on the
/// frame's offset. Shared so the editor and headless callers (aecli) add identical shapes.
/// </summary>
public static class NewShapeDefaults
{
    public const string RectName = "AxisAlignedRectangleInstance";
    public const string CircleName = "CircleInstance";
    public const string PolygonName = "PolygonInstance";

    /// <summary>An 8-half-extent rectangle at the frame's offset.</summary>
    public static AARectSave Rect(AnimationFrameSave frame, string name) =>
        new() { Name = name, ScaleX = 8, ScaleY = 8, X = frame.RelativeX, Y = frame.RelativeY };

    /// <summary>A radius-8 circle at the frame's offset.</summary>
    public static CircleSave Circle(AnimationFrameSave frame, string name) =>
        new() { Name = name, Radius = 8, X = frame.RelativeX, Y = frame.RelativeY };

    /// <summary>A 16x16 square polygon at the frame's offset, stored closed (first point repeated at the end).</summary>
    public static PolygonSave Polygon(AnimationFrameSave frame, string name)
    {
        var polygon = new PolygonSave { Name = name, X = frame.RelativeX, Y = frame.RelativeY };
        foreach (var (x, y) in new[] { (-8f, -8f), (8f, -8f), (8f, 8f), (-8f, 8f), (-8f, -8f) })
            polygon.Points.Add(new Vector2Save { X = x, Y = y });
        return polygon;
    }
}
