namespace FlatRedBall2.Collision;

/// <summary>
/// Restricts a <see cref="CollisionRelationship{A,B}"/> to only resolve overlap when the entity
/// is being pushed in the configured direction — i.e., it approached from the opposite side.
/// Used for jump-through platforms (<see cref="Up"/>, aka cloud collision) and Yoshi's-Island
/// style one-way doors. For <see cref="Up"/>, separation fires only when all three gates pass:
/// (1) the computed separation vector pushes the entity upward (<c>sep.Y &gt; 0</c>),
/// (2) the entity is moving downward or stationary (<c>VelocityY &lt;= 0</c>) — so an upward-
/// moving entity that overlaps deeply enough for SAT to pick the upward exit isn't popped onto
/// the top, and (3) the entity's <c>LastPosition.Y</c> was at or above where separation would
/// place it — confirming the entity was actually on top of the platform before sinking in,
/// rather than starting to fall back from inside after a partial jump-up. Separation X is also
/// zeroed so a player clipping a platform's side edge is nudged up, never sideways.
/// </summary>
/// <remarks>
/// MVP only implements <see cref="None"/> and <see cref="Up"/>. Setting <see cref="Down"/>,
/// <see cref="Left"/>, or <see cref="Right"/> is allowed but throws
/// <see cref="System.NotImplementedException"/> on the next collision pass.
/// </remarks>
public enum OneWayDirection
{
    /// <summary>Standard collision; blocks from all directions.</summary>
    None,
    /// <summary>Jump-through platform; blocks entities moving down (approaching from above).</summary>
    Up,
    /// <summary>Blocks entities moving up (approaching from below).</summary>
    Down,
    /// <summary>Blocks entities moving right (approaching from the left).</summary>
    Left,
    /// <summary>Blocks entities moving left (approaching from the right).</summary>
    Right,
}
