namespace FlatRedBall2.Movement;

/// <summary>
/// Side-scrolling facing for a platformer entity. Reported by
/// <see cref="PlatformerBehavior.DirectionFacing"/> based on the most recent non-zero
/// horizontal input — useful for selecting left/right-facing animations.
/// </summary>
public enum HorizontalDirection { Left, Right }
