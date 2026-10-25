using System.Collections.Generic;
using System.Linq;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// Per-frame shape definitions in a .achx file. FRB1 reads them too (<c>Sprite.SyncShapesFromAnimation</c>),
/// so element names must match FRB1's. In FRB2, entries are converted to runtime
/// <c>AnimationShapeFrame</c> instances by <c>AnimationChainListSaveExtensions.ToAnimationChainList</c>
/// (main engine assembly).
/// </summary>
/// <remarks>Serialized as <c>&lt;ShapeCollectionSave&gt;</c> in .achx XML.</remarks>
public class ShapesSave
{
    private readonly List<object> _shapes = new();

    /// <summary>
    /// All shapes in display order. Always grouped by type (rectangles, then polygons, then circles),
    /// which is the order the .achx/.achj writers emit, so the order never changes across a save and
    /// reload. Entries are <see cref="AARectSave"/>, <see cref="CircleSave"/>, or <see cref="PolygonSave"/>.
    /// Mutate through <see cref="Add"/>, <see cref="Insert"/>, <see cref="Remove"/>, <see cref="Move"/>,
    /// <see cref="MoveToEdge"/>, and <see cref="SetOrder"/>.
    /// </summary>
    public IReadOnlyList<object> Shapes => _shapes;

    // Writer order: rects, polygons, circles.
    private static int Rank(object shape) => shape switch
    {
        AARectSave => 0,
        PolygonSave => 1,
        CircleSave => 2,
        _ => 3,
    };

    // [start, end) of the contiguous run holding shapes of the given rank.
    private (int Start, int End) GroupRange(int rank)
    {
        int start = _shapes.FindIndex(s => Rank(s) >= rank);
        if (start < 0) return (_shapes.Count, _shapes.Count);
        int end = start;
        while (end < _shapes.Count && Rank(_shapes[end]) == rank) end++;
        return (start, end);
    }

    /// <summary>Adds <paramref name="shape"/> at the end of its type group, keeping <see cref="Shapes"/> in file order.</summary>
    public void Add(object shape)
    {
        var (_, end) = GroupRange(Rank(shape));
        _shapes.Insert(end, shape);
    }

    /// <summary>
    /// Inserts <paramref name="shape"/> at <paramref name="index"/>, clamped into its type group.
    /// Used by undo to restore a deleted shape to its original slot.
    /// </summary>
    public void Insert(int index, object shape)
    {
        var (start, end) = GroupRange(Rank(shape));
        _shapes.Insert(System.Math.Clamp(index, start, end), shape);
    }

    /// <summary>Index of <paramref name="shape"/> in <see cref="Shapes"/>, or -1.</summary>
    public int IndexOf(object shape) => _shapes.IndexOf(shape);

    /// <summary>Removes <paramref name="shape"/>. Returns <c>false</c> if it was not present.</summary>
    public bool Remove(object shape) => _shapes.Remove(shape);

    /// <summary>
    /// Swaps <paramref name="shape"/> with its neighbor of the same type, <paramref name="delta"/> (-1 or +1)
    /// positions away. Returns <c>false</c> (no change) at the edge of its type group; shapes never cross types.
    /// </summary>
    public bool Move(object shape, int delta)
    {
        int idx = _shapes.IndexOf(shape);
        if (idx < 0) return false;
        var (start, end) = GroupRange(Rank(shape));
        int target = idx + delta;
        if (target < start || target >= end || target == idx) return false;
        _shapes.RemoveAt(idx);
        _shapes.Insert(target, shape);
        return true;
    }

    /// <summary>Moves <paramref name="shape"/> to the start or end of its own type group. Returns <c>false</c> if already there.</summary>
    public bool MoveToEdge(object shape, bool toStart)
    {
        int idx = _shapes.IndexOf(shape);
        if (idx < 0) return false;
        var (start, end) = GroupRange(Rank(shape));
        int target = toStart ? start : end - 1;
        if (target == idx) return false;
        _shapes.RemoveAt(idx);
        _shapes.Insert(target, shape);
        return true;
    }

    /// <summary>
    /// Replaces the order wholesale (undo/redo of a reorder). Throws <see cref="System.ArgumentException"/>
    /// if <paramref name="order"/> is not the same set of shapes or is not grouped by type.
    /// </summary>
    public void SetOrder(IReadOnlyList<object> order)
    {
        if (order.Count != _shapes.Count || order.Except(_shapes).Any())
            throw new System.ArgumentException("Order must contain exactly the current shapes.", nameof(order));
        for (int i = 1; i < order.Count; i++)
            if (Rank(order[i]) < Rank(order[i - 1]))
                throw new System.ArgumentException("Order must be grouped by type: rectangles, polygons, circles.", nameof(order));
        _shapes.Clear();
        _shapes.AddRange(order);
    }

    /// <summary>All rectangles, projected from <see cref="Shapes"/> in their stored order.</summary>
    public IEnumerable<AARectSave> AARectSaves => _shapes.OfType<AARectSave>();

    /// <summary>All circles, projected from <see cref="Shapes"/> in their stored order.</summary>
    public IEnumerable<CircleSave> CircleSaves => _shapes.OfType<CircleSave>();

    /// <summary>All polygons, projected from <see cref="Shapes"/> in their stored order.</summary>
    public IEnumerable<PolygonSave> PolygonSaves => _shapes.OfType<PolygonSave>();
}

/// <summary>
/// FRB1-only shape fields that FRB2 does not model but preserves verbatim so opening and
/// re-saving an existing .achx round-trips byte-identical. Inert at runtime.
/// </summary>
public class Frb1ShapeData
{
    /// <summary>Z depth. FRB1 default 0; FRB2 is 2D so this is never read.</summary>
    public float Z;
    /// <summary>Tint alpha. FRB1 default 1.</summary>
    public float Alpha = 1f;
    /// <summary>Tint red. FRB1 default 1.</summary>
    public float Red = 1f;
    /// <summary>Tint green. FRB1 default 1.</summary>
    public float Green = 1f;
    /// <summary>Tint blue. FRB1 default 1.</summary>
    public float Blue = 1f;
}

/// <summary>Fields every per-frame shape has: <see cref="AARectSave"/>, <see cref="CircleSave"/>, <see cref="PolygonSave"/>.</summary>
public abstract class ShapeSave : Frb1ShapeData
{
    /// <summary>Shape name; matched by name against entity-attached shapes.</summary>
    public string Name = string.Empty;
    /// <summary>X relative to the entity: the center of a rectangle or circle, the origin of a polygon.</summary>
    public float X;
    /// <summary>Y relative to the entity (Y+ up): the center of a rectangle or circle, the origin of a polygon.</summary>
    public float Y;

    /// <summary>
    /// When <c>true</c>, this shape is not an <b>offset keyframe</b> in its frame: its own <see cref="X"/>/<see cref="Y"/>
    /// and <see cref="OffsetInterpolation"/> are ignored, and the previous offset keyframe of the same-named shape
    /// glides across it. The first frame of a run of frames that hold the name is always a keyframe. Written only
    /// when <c>true</c>. See <see cref="KeyframeEvaluator.EvaluateShape(AnimationChainSave, int, double, ShapeSave)"/>.
    /// </summary>
    public bool SkipOffsetKeyframe;

    /// <summary>
    /// How <see cref="X"/>/<see cref="Y"/> get from this keyframe to the next one of the same-named shape: an
    /// <c>InterpolationType</c> name, same rules as <see cref="AnimationFrameSave.OffsetInterpolation"/>.
    /// <c>null</c> or <c>Instant</c> is snap.
    /// </summary>
    public string? OffsetInterpolation;

    /// <summary><c>In</c>, <c>Out</c> or <c>InOut</c> for <see cref="OffsetInterpolation"/>; <c>null</c> means <c>Out</c>.</summary>
    public string? OffsetEasing;
}

/// <summary>A shape with a size that can glide between keyframes: <see cref="AARectSave"/> and <see cref="CircleSave"/>.</summary>
public abstract class SizedShapeSave : ShapeSave
{
    /// <summary>
    /// When <c>true</c>, this shape is not a <b>size keyframe</b> in its frame: its own size and
    /// <see cref="SizeInterpolation"/> are ignored. Independent of <see cref="ShapeSave.SkipOffsetKeyframe"/>.
    /// Written only when <c>true</c>.
    /// </summary>
    public bool SkipSizeKeyframe;

    /// <summary>
    /// How the size (<c>ScaleX</c>/<c>ScaleY</c> for a rectangle, <c>Radius</c> for a circle) gets from this keyframe
    /// to the next one of the same-named shape. Same rules as <see cref="ShapeSave.OffsetInterpolation"/>.
    /// </summary>
    public string? SizeInterpolation;

    /// <summary><c>In</c>, <c>Out</c> or <c>InOut</c> for <see cref="SizeInterpolation"/>; <c>null</c> means <c>Out</c>.</summary>
    public string? SizeEasing;
}

/// <summary>Serialized rectangle entry within a <see cref="ShapesSave"/>.</summary>
/// <remarks>Serialized as <c>&lt;AxisAlignedRectangleSave&gt;</c> inside <c>&lt;AxisAlignedRectangleSaves&gt;</c>.</remarks>
public class AARectSave : SizedShapeSave
{
    /// <summary>Half-width (FRB1 convention). Loaded as <c>Width = ScaleX * 2</c>.</summary>
    public float ScaleX = 16f;
    /// <summary>Half-height (FRB1 convention). Loaded as <c>Height = ScaleY * 2</c>.</summary>
    public float ScaleY = 16f;
}

/// <summary>Serialized circle entry within a <see cref="ShapesSave"/>.</summary>
public class CircleSave : SizedShapeSave
{
    /// <summary>Circle radius.</summary>
    public float Radius = 16f;
}

/// <summary>Serialized polygon entry within a <see cref="ShapesSave"/>.</summary>
public class PolygonSave : ShapeSave
{
    /// <summary>
    /// Vertices relative to the origin (<see cref="ShapeSave.X"/>/<see cref="ShapeSave.Y"/>), Y+ up.
    /// FRB1 closes an outline by repeating the first point at the end; that repeated point is stored here as-is.
    /// </summary>
    public List<Vector2Save> Points = new();

    /// <summary>
    /// When <c>true</c>, this polygon is not a <b>points keyframe</b> in its frame: its own <see cref="Points"/> and
    /// <see cref="PointsInterpolation"/> are ignored, and the previous points keyframe of the same-named polygon glides
    /// across it. The first frame of a run of frames that hold the name is always a keyframe. Independent of
    /// <see cref="ShapeSave.SkipOffsetKeyframe"/>. Written only when <c>true</c>.
    /// </summary>
    public bool SkipPointsKeyframe;

    /// <summary>
    /// How <see cref="Points"/> get from this keyframe to the next points keyframe of the same-named polygon: point i
    /// glides to point i, and a changed point count snaps. Same rules as <see cref="ShapeSave.OffsetInterpolation"/>.
    /// </summary>
    public string? PointsInterpolation;

    /// <summary><c>In</c>, <c>Out</c> or <c>InOut</c> for <see cref="PointsInterpolation"/>; <c>null</c> means <c>Out</c>.</summary>
    public string? PointsEasing;
}

/// <summary>Serialized 2D point used by <see cref="PolygonSave"/>.</summary>
/// <remarks>Written as <c>&lt;Point&gt;</c> in .achx XML to match FRB1's <c>FlatRedBall.Math.Geometry.Point</c>.</remarks>
public class Vector2Save
{
    /// <summary>X coordinate.</summary>
    public float X;
    /// <summary>Y coordinate.</summary>
    public float Y;
}
