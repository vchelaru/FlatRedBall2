using System;

namespace FlatRedBall2.Collision;

/// <summary>
/// Controls which directions an <see cref="AxisAlignedRectangle"/> may reposition overlapping objects.
/// Used to eliminate "snagging" — the unintended velocity deflection that occurs when a moving
/// object grazes the shared edge between two adjacent rectangles.
/// </summary>
/// <remarks>
/// <para>Set to <see cref="All"/> by default, meaning repositioning is allowed in every direction.</para>
/// <para>
/// For a horizontal row of floor tiles, remove <see cref="Left"/> and <see cref="Right"/> from
/// each interior tile so that only the top and bottom surfaces produce push-back. Objects then
/// glide smoothly along the surface instead of catching on seam corners.
/// </para>
/// <para>
/// When flags are restricted, the engine picks the smallest valid displacement along the remaining
/// allowed axes. A collision that would naturally push an object left against a Down-only rect
/// will instead push it downward — the collision is never silently suppressed.
/// </para>
/// </remarks>
[Flags]
public enum RepositionDirections
{
    /// <summary>No sides are solid — everything passes through.</summary>
    None  = 0,
    /// <summary>The top surface is solid (pushes colliding objects upward, +Y).</summary>
    Up    = 1 << 0,
    /// <summary>The bottom surface is solid (pushes colliding objects downward, −Y).</summary>
    Down  = 1 << 1,
    /// <summary>The left surface is solid (pushes colliding objects leftward, −X).</summary>
    Left  = 1 << 2,
    /// <summary>The right surface is solid (pushes colliding objects rightward, +X).</summary>
    Right = 1 << 3,
    /// <summary>All four sides are solid. This is the default.</summary>
    All   = Up | Down | Left | Right,
}
