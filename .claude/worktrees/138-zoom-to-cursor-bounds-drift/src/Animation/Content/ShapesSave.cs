using System.Collections.Generic;

namespace FlatRedBall2.Animation.Content;

/// <summary>
/// Per-frame shape definitions in a .achx file. Inert in FRB1; live in FRB2 — entries are
/// converted to runtime <see cref="AnimationShapeFrame"/> instances by
/// <see cref="AnimationChainListSave.ToAnimationChainList"/>.
/// </summary>
public class ShapesSave
{
    /// <summary>Rectangles defined for this frame.</summary>
    public List<AARectSave> AARectSaves = new();

    /// <summary>Circles defined for this frame.</summary>
    public List<CircleSave> CircleSaves = new();

    /// <summary>Polygons defined for this frame.</summary>
    public List<PolygonSave> PolygonSaves = new();
}

/// <summary>Serialized rectangle entry within a <see cref="ShapesSave"/>.</summary>
public class AARectSave
{
    /// <summary>Shape name; matched by name against entity-attached shapes.</summary>
    public string Name = string.Empty;
    /// <summary>Center X relative to the entity.</summary>
    public float X;
    /// <summary>Center Y relative to the entity.</summary>
    public float Y;
    /// <summary>Half-width (FRB1 convention). Loaded as <c>Width = ScaleX * 2</c>.</summary>
    public float ScaleX = 16f;
    /// <summary>Half-height (FRB1 convention). Loaded as <c>Height = ScaleY * 2</c>.</summary>
    public float ScaleY = 16f;
}

/// <summary>Serialized circle entry within a <see cref="ShapesSave"/>.</summary>
public class CircleSave
{
    /// <summary>Shape name.</summary>
    public string Name = string.Empty;
    /// <summary>Center X relative to the entity.</summary>
    public float X;
    /// <summary>Center Y relative to the entity.</summary>
    public float Y;
    /// <summary>Circle radius.</summary>
    public float Radius = 16f;
}

/// <summary>Serialized polygon entry within a <see cref="ShapesSave"/>.</summary>
public class PolygonSave
{
    /// <summary>Shape name.</summary>
    public string Name = string.Empty;
    /// <summary>Origin X relative to the entity.</summary>
    public float X;
    /// <summary>Origin Y relative to the entity.</summary>
    public float Y;
    /// <summary>Polygon vertices in local space.</summary>
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
