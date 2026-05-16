namespace FlatRedBall2;

/// <summary>
/// Implemented by objects that have an opinion about whether they should advance while a Screen is paused.
/// Screen.Update() checks this before advancing the object each frame.
/// </summary>
public interface IPauseAware
{
    /// <summary>
    /// When false (default), the object freezes while the Screen is paused.
    /// Set to true for objects that should keep running (e.g., pause-menu tweens, UI animations).
    /// </summary>
    bool ShouldAdvanceOnPause { get; }
}
