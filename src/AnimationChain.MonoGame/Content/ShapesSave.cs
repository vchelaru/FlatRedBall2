namespace FlatRedBall.AnimationChain.Content;

/// <summary>
/// Per-frame shape definitions stored in a .achx file. Shapes are exposed as data only —
/// the caller is responsible for applying them to collision geometry.
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

/// <summary>Serialized axis-aligned rectangle entry within a <see cref="ShapesSave"/>.</summary>
public class AARectSave
{
    /// <summary>Shape name; used for per-frame reconciliation.</summary>
    public string Name = string.Empty;
    /// <summary>Center X relative to the entity.</summary>
    public float X;
    /// <summary>Center Y relative to the entity.</summary>
    public float Y;
    /// <summary>Half-width (FRB1 convention). Width = ScaleX * 2.</summary>
    public float ScaleX = 16f;
    /// <summary>Half-height (FRB1 convention). Height = ScaleY * 2.</summary>
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
