using Microsoft.Xna.Framework;

namespace FlatRedBall2.Movement;

/// <summary>
/// The direction a top-down entity is currently facing.
/// The active set of directions is controlled by <see cref="DirectionSnap"/>.
/// </summary>
public enum TopDownDirection
{
    /// <summary>Facing right (+X).</summary>
    Right,
    /// <summary>Facing up and right (+X, +Y).</summary>
    UpRight,
    /// <summary>Facing up (+Y).</summary>
    Up,
    /// <summary>Facing up and left (-X, +Y).</summary>
    UpLeft,
    /// <summary>Facing left (-X).</summary>
    Left,
    /// <summary>Facing down and left (-X, -Y).</summary>
    DownLeft,
    /// <summary>Facing down (-Y).</summary>
    Down,
    /// <summary>Facing down and right (+X, -Y).</summary>
    DownRight,
}

/// <summary>
/// Controls how many discrete directions are snapped to when updating
/// <see cref="TopDownBehavior.DirectionFacing"/>.
/// </summary>
public enum DirectionSnap
{
    /// <summary>Snap to the four cardinal directions: Right, Up, Left, Down.</summary>
    FourWay,
    /// <summary>Snap to all eight compass directions, including diagonals.</summary>
    EightWay,
}

/// <summary>
/// Axis that diagonal directions collapse onto when reducing to a cardinal.
/// </summary>
public enum DiagonalCollapse
{
    /// <summary>Diagonals collapse to Left/Right (e.g. UpRight → Right). Best for character art where horizontal silhouettes read most distinctly.</summary>
    Horizontal,
    /// <summary>Diagonals collapse to Up/Down (e.g. UpRight → Up). Best when up/down poses are more distinct than left/right.</summary>
    Vertical,
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

    /// <summary>
    /// Collapses a diagonal <see cref="TopDownDirection"/> to its nearest cardinal
    /// (Right, Up, Left, or Down). Cardinal inputs are returned unchanged.
    /// Useful for selecting animations when input is 8-way but art has only 4 chains.
    /// </summary>
    /// <param name="direction">The direction to collapse.</param>
    /// <param name="axis">Which axis diagonals collapse onto. Defaults to <see cref="DiagonalCollapse.Horizontal"/>.</param>
    public static TopDownDirection ToCardinal(this TopDownDirection direction, DiagonalCollapse axis = DiagonalCollapse.Horizontal)
        => axis == DiagonalCollapse.Horizontal
            ? direction switch
            {
                TopDownDirection.UpRight or TopDownDirection.DownRight => TopDownDirection.Right,
                TopDownDirection.UpLeft  or TopDownDirection.DownLeft  => TopDownDirection.Left,
                _ => direction,
            }
            : direction switch
            {
                TopDownDirection.UpRight or TopDownDirection.UpLeft     => TopDownDirection.Up,
                TopDownDirection.DownRight or TopDownDirection.DownLeft => TopDownDirection.Down,
                _ => direction,
            };
}
