namespace FlatRedBall2;

/// <summary>
/// Shared math for composing a child's world-space position from a parent <see cref="Entity"/>.
/// Every <see cref="ISpatialAttachable"/> implementation (<see cref="Entity"/>, shapes, <c>Sprite</c>)
/// calls this from its <c>AbsoluteX</c>/<c>AbsoluteY</c> getters so the rigid-2D-transform formula
/// exists in exactly one place.
/// </summary>
internal static class AttachmentMath
{
    /// <summary>
    /// Rotates the local offset (<paramref name="localX"/>, <paramref name="localY"/>) by
    /// <paramref name="parent"/>'s <see cref="Entity.AbsoluteRotation"/> and adds it to the
    /// parent's absolute position — the lever-arm transform that makes an attached child orbit
    /// around its parent's origin as the parent rotates, matching the parent's own visible facing.
    /// </summary>
    public static (float X, float Y) ComposeAbsolute(Entity parent, float localX, float localY)
    {
        float angle = parent.AbsoluteRotation.Radians;
        float cos = System.MathF.Cos(angle);
        float sin = System.MathF.Sin(angle);
        float rotatedX = localX * cos - localY * sin;
        float rotatedY = localX * sin + localY * cos;
        return (parent.AbsoluteX + rotatedX, parent.AbsoluteY + rotatedY);
    }
}
