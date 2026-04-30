using System;
using System.Numerics;

namespace FlatRedBall2.Input;

/// <summary>
/// Adapts an <see cref="ICursor"/> to <see cref="I2DInput"/> by computing a direction vector from
/// a supplied position toward the cursor's world position. Only produces input while the primary
/// button is held (touch active or left mouse down). Useful for touch-to-move gameplay where an
/// entity should move toward the player's finger.
/// </summary>
/// <remarks>
/// <para>
/// <c>positionProvider</c> is called each frame to get the "from" position (typically
/// the entity's current world position). This avoids a hard dependency on <see cref="Entity"/> so
/// the input can be used with any positional source.
/// </para>
/// <para>
/// When the distance between the position and the cursor is less than <see cref="DeadZone"/>,
/// the output magnitude scales linearly from 0 to 1. Beyond the dead zone, the output is a
/// unit-length direction vector. This gives a natural "snap to finger" feel without jittering
/// when the entity is close to the touch point.
/// </para>
/// </remarks>
public class CursorTrackingInput2D : I2DInput
{
    private readonly ICursor _cursor;
    private readonly Func<Vector2> _positionProvider;

    /// <summary>
    /// Distance (in world units) within which the output magnitude scales linearly toward zero.
    /// Default is 8 — a reasonable value for pixel-art games where entities are 16–32 px.
    /// </summary>
    public float DeadZone { get; set; } = 8f;

    /// <summary>
    /// Creates a cursor-tracking 2D input.
    /// </summary>
    /// <param name="cursor">The cursor to read. Obtain via <c>Engine.Input.Cursor</c>.</param>
    /// <param name="positionProvider">Returns the current world position to track from (e.g. <c>() => new Vector2(entity.X, entity.Y)</c>).</param>
    public CursorTrackingInput2D(ICursor cursor, Func<Vector2> positionProvider)
    {
        _cursor = cursor;
        _positionProvider = positionProvider;
    }

    /// <inheritdoc/>
    public float X
    {
        get
        {
            if (!_cursor.PrimaryDown) return 0f;
            var (x, _) = ComputeDirection();
            return x;
        }
    }

    /// <inheritdoc/>
    public float Y
    {
        get
        {
            if (!_cursor.PrimaryDown) return 0f;
            var (_, y) = ComputeDirection();
            return y;
        }
    }

    private (float x, float y) ComputeDirection()
    {
        var pos = _positionProvider();
        var target = _cursor.WorldPosition;
        var dx = target.X - pos.X;
        var dy = target.Y - pos.Y;
        var dist = MathF.Sqrt(dx * dx + dy * dy);

        if (dist < 0.001f) return (0f, 0f);

        float magnitude = DeadZone > 0f ? MathF.Min(dist / DeadZone, 1f) : 1f;
        float invDist = 1f / dist;
        return (dx * invDist * magnitude, dy * invDist * magnitude);
    }
}
