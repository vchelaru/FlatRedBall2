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
