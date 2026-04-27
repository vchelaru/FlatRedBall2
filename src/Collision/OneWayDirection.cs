namespace FlatRedBall2.Collision;

/// <summary>
/// Restricts a <see cref="CollisionRelationship{A,B}"/> to only resolve overlap when the entity
/// is being pushed in the configured direction — i.e., it approached from the opposite side.
/// Used for jump-through platforms (<see cref="Up"/>, aka cloud collision), ceiling-only barriers
/// (<see cref="Down"/>), and Yoshi's-Island-style one-way doors (<see cref="Left"/>/<see cref="Right"/>).
/// Three gates must pass for separation to fire: (1) the computed separation pushes the entity in
/// the configured direction, (2) the entity is moving in that direction (or stationary) along the
/// gated axis — so an entity passing through from the wrong side isn't popped out, and (3) the
/// entity's <c>LastPosition</c> was at or beyond where separation would place it along the gated
/// axis — confirming it was already on the correct side before sinking in. The off-axis component
/// of the separation is zeroed so an entity clipping an edge is pushed in the gated direction
/// only, never sideways.
/// </summary>
/// <remarks>
/// Slope-aware <c>LastPosition</c> handling (using polygon tile heightmaps) only applies to
/// <see cref="Up"/>, since sloped floors are the common case. <see cref="Down"/>/<see cref="Left"/>/<see cref="Right"/>
/// use a flat positional gate.
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
