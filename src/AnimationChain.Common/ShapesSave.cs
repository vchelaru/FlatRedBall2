using System.Collections.Generic;
using System.Linq;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// Per-frame shape definitions in a .achx file. Inert in FRB1; live in FRB2 — entries are
/// converted to runtime <c>AnimationShapeFrame</c> instances by
/// <c>AnimationChainListSaveExtensions.ToAnimationChainList</c> (main engine assembly).
/// </summary>
/// <remarks>Serialized as <c>&lt;ShapeCollectionSave&gt;</c> in .achx XML.</remarks>
public class ShapesSave
{
    /// <summary>All shapes in user-defined order. Entries are <see cref="AARectSave"/>, <see cref="CircleSave"/>, or <see cref="PolygonSave"/>.</summary>
    public List<object> Shapes = new();

    /// <summary>All rectangles, projected from <see cref="Shapes"/> in their stored order.</summary>
    public IEnumerable<AARectSave> AARectSaves => Shapes.OfType<AARectSave>();

    /// <summary>All circles, projected from <see cref="Shapes"/> in their stored order.</summary>
    public IEnumerable<CircleSave> CircleSaves => Shapes.OfType<CircleSave>();

    /// <summary>All polygons, projected from <see cref="Shapes"/> in their stored order.</summary>
    public IEnumerable<PolygonSave> PolygonSaves => Shapes.OfType<PolygonSave>();
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
}

/// <summary>Serialized rectangle entry within a <see cref="ShapesSave"/>.</summary>
/// <remarks>Serialized as <c>&lt;AxisAlignedRectangleSave&gt;</c> inside <c>&lt;AxisAlignedRectangleSaves&gt;</c>.</remarks>
public class AARectSave : ShapeSave
{
    /// <summary>Half-width (FRB1 convention). Loaded as <c>Width = ScaleX * 2</c>.</summary>
    public float ScaleX = 16f;
    /// <summary>Half-height (FRB1 convention). Loaded as <c>Height = ScaleY * 2</c>.</summary>
    public float ScaleY = 16f;
}

/// <summary>Serialized circle entry within a <see cref="ShapesSave"/>.</summary>
public class CircleSave : ShapeSave
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
}

/// <summary>Serialized 2D point used by <see cref="PolygonSave"/>.</summary>
public class Vector2Save
{
    /// <summary>X coordinate.</summary>
    public float X;
    /// <summary>Y coordinate.</summary>
    public float Y;
}
