namespace FlatRedBall2;

/// <summary>
/// Controls whether an <see cref="Entity"/>'s per-frame physics and <see cref="Entity.CustomActivity"/>
/// are suppressed by <see cref="Screen.IsPaused"/>.
/// </summary>
public enum PauseMode
{
    /// <summary>Default. The entity is frozen while the owning screen is paused.</summary>
    Pausable,

    /// <summary>
    /// The entity continues to run physics and <see cref="Entity.CustomActivity"/> while the screen
    /// is paused. Use for cursors, parallax backgrounds, menu spinners, and other UI that must keep
    /// ticking through pause. Collision processing remains gated by <see cref="Screen.IsPaused"/>.
    /// </summary>
    Always,
}
