namespace FlatRedBall2.Math;

/// <summary>
/// An axis-aligned rectangle defined by its center and size. A lightweight value type
/// with no collision, rendering, or attachment behavior — use it anywhere you need
/// a rectangular region (map bounds, spawn areas, trigger zones, viewport regions).
/// </summary>
/// <remarks>
/// Unlike <see cref="Collision.AxisAlignedRectangle"/>, this is a plain data struct
/// with no entity or physics overhead. Edge properties assume Y+ up (Top &gt; Bottom).
/// </remarks>
public readonly record struct BoundsRectangle(float CenterX, float CenterY, float Width, float Height)
{
    /// <summary>X coordinate of the left edge (<see cref="CenterX"/> − <see cref="Width"/> / 2).</summary>
    public float Left => CenterX - Width / 2f;

    /// <summary>X coordinate of the right edge (<see cref="CenterX"/> + <see cref="Width"/> / 2).</summary>
    public float Right => CenterX + Width / 2f;

    /// <summary>Y coordinate of the top edge (<see cref="CenterY"/> + <see cref="Height"/> / 2). Y+ is up.</summary>
    public float Top => CenterY + Height / 2f;

    /// <summary>Y coordinate of the bottom edge (<see cref="CenterY"/> − <see cref="Height"/> / 2). Y+ is up.</summary>
    public float Bottom => CenterY - Height / 2f;

    /// <summary>
    /// Creates a <see cref="BoundsRectangle"/> centered at the origin with the given dimensions.
    /// </summary>
    public BoundsRectangle(float width, float height) : this(0f, 0f, width, height) { }
}
