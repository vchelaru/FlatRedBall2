using System.Numerics;

namespace FlatRedBall.AnimationChain;

/// <summary>
/// A per-frame shape definition carried by <see cref="AnimationFrame.Shapes"/>. Exposed as
/// plain data — the caller is responsible for applying these values to collision shapes.
/// </summary>
public abstract class AnimationShapeFrame
{
    /// <summary>
    /// Identifier matched against named shapes on the entity. Required and non-empty —
    /// unnamed entries are rejected when the file is loaded.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Local X offset from the owning entity. Applied each frame switch.</summary>
    public float RelativeX { get; set; }

    /// <summary>Local Y offset from the owning entity. Applied each frame switch.</summary>
    public float RelativeY { get; set; }
}

/// <summary>Per-frame definition for an axis-aligned rectangle shape.</summary>
public class AnimationAARectFrame : AnimationShapeFrame
{
    /// <summary>Width in world units.</summary>
    public float Width { get; set; } = 32f;

    /// <summary>Height in world units.</summary>
    public float Height { get; set; } = 32f;
}

/// <summary>Per-frame definition for a circle shape.</summary>
public class AnimationCircleFrame : AnimationShapeFrame
{
    /// <summary>Circle radius in world units.</summary>
    public float Radius { get; set; } = 16f;
}

/// <summary>Per-frame definition for a polygon shape.</summary>
public class AnimationPolygonFrame : AnimationShapeFrame
{
    /// <summary>Polygon vertices in the shape's local space.</summary>
    public Vector2[] Points { get; set; } = Array.Empty<Vector2>();
}
