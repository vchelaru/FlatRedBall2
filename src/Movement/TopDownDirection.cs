using Microsoft.Xna.Framework;

namespace FlatRedBall2.Movement;

/// <summary>
/// The direction a top-down entity is currently facing.
/// The active set of directions is controlled by <see cref="PossibleDirections"/>.
/// </summary>
public enum TopDownDirection
{
    Right,
    UpRight,
    Up,
    UpLeft,
    Left,
    DownLeft,
    Down,
    DownRight,
}

/// <summary>
/// Controls how many discrete directions are snapped to when updating
/// <see cref="TopDownBehavior.DirectionFacing"/>.
/// </summary>
public enum PossibleDirections
{
    /// <summary>Snap to the four cardinal directions: Right, Up, Left, Down.</summary>
    FourWay,
    /// <summary>Snap to all eight compass directions, including diagonals.</summary>
    EightWay,
}

/// <summary>
/// Extension methods for <see cref="TopDownDirection"/>.
/// </summary>
public static class TopDownDirectionExtensions
{
    private const float D = 0.7071067811865476f; // 1/√2

    /// <summary>
    /// Returns a normalized <see cref="Vector2"/> pointing in the given direction.
    /// Y+ is up, matching world-space coordinates.
    /// </summary>
    public static Vector2 ToVector2(this TopDownDirection direction) => direction switch
    {
        TopDownDirection.Right     => new Vector2( 1,  0),
        TopDownDirection.UpRight   => new Vector2( D,  D),
        TopDownDirection.Up        => new Vector2( 0,  1),
        TopDownDirection.UpLeft    => new Vector2(-D,  D),
        TopDownDirection.Left      => new Vector2(-1,  0),
        TopDownDirection.DownLeft  => new Vector2(-D, -D),
        TopDownDirection.Down      => new Vector2( 0, -1),
        TopDownDirection.DownRight => new Vector2( D, -D),
        _                          => Vector2.Zero,
    };
}
