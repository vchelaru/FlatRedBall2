using System.Numerics;

namespace FlatRedBall2.Animation;

/// <summary>
/// A per-frame shape definition carried by <see cref="AnimationFrame.Shapes"/>. Reconciled
/// against the parent entity's shapes by name when the frame becomes current — see
/// <see cref="AnimationChainList"/> for the ownership rule that decides which entity shapes
/// the animation system is allowed to touch.
/// </summary>
public abstract class AnimationShapeFrame
{
    /// <summary>
    /// Identifier matched against <c>Name</c> on entity-attached shapes. Required and non-empty —
    /// unnamed entries are rejected at apply time.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Local X offset from the owning entity. Applied each frame switch.</summary>
    public float RelativeX { get; set; }

    /// <summary>Local Y offset from the owning entity. Applied each frame switch.</summary>
    public float RelativeY { get; set; }
}

/// <summary>Per-frame definition for an <see cref="FlatRedBall2.Collision.AARect"/>.</summary>
public class AnimationAARectFrame : AnimationShapeFrame
{
    /// <summary>Width in world units.</summary>
    public float Width { get; set; } = 32f;
    /// <summary>Height in world units.</summary>
    public float Height { get; set; } = 32f;
}

/// <summary>Per-frame definition for a <see cref="FlatRedBall2.Collision.Circle"/>.</summary>
public class AnimationCircleFrame : AnimationShapeFrame
{
    /// <summary>Circle radius in world units.</summary>
    public float Radius { get; set; } = 16f;
}

/// <summary>Per-frame definition for a <see cref="FlatRedBall2.Collision.Polygon"/>.</summary>
public class AnimationPolygonFrame : AnimationShapeFrame
{
    /// <summary>Polygon vertices in the shape's local space (relative to <see cref="AnimationShapeFrame.RelativeX"/>/<see cref="AnimationShapeFrame.RelativeY"/>).</summary>
    public Vector2[] Points { get; set; } = System.Array.Empty<Vector2>();
}
