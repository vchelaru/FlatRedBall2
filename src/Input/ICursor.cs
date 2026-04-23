using System.Numerics;

namespace FlatRedBall2.Input;

/// <summary>
/// Per-frame mouse/touch state. Accessible via <c>Engine.Input.Cursor</c>.
/// <see cref="WorldPosition"/> is in Y+ up world space and matches entity coordinates directly.
/// </summary>
public interface ICursor
{
    /// <summary>
    /// Cursor position in world space (Y+ up). Coordinates match entity X/Y directly — use this
    /// for click-to-move, entity targeting, and world-space hit detection.
    /// </summary>
    Vector2 WorldPosition { get; }

    /// <summary>Cursor position in screen pixels, top-left origin. Use for HUD hit-testing.</summary>
    Vector2 ScreenPosition { get; }

    /// <summary>True every frame the primary button (left mouse / first touch) is held down.</summary>
    bool PrimaryDown { get; }

    /// <summary>True only on the first frame the primary button transitions from up to down.</summary>
    bool PrimaryPressed { get; }

    /// <summary>True only on the first frame the primary button transitions from down to up (the "click" — fires on release).</summary>
    bool PrimaryClick { get; }

    /// <summary>
    /// True on the frame of a press transition that occurred within <see cref="DoubleClickThreshold"/>
    /// seconds of the previous press transition. Press-edge double — fires immediately on the
    /// second press, not on its release.
    /// </summary>
    bool PrimaryDoublePressed { get; }

    /// <summary>
    /// True on the frame of a release transition that occurred within <see cref="DoubleClickThreshold"/>
    /// seconds of the previous release transition. Release-edge double — fires when the user
    /// finishes the second click, matching the standard "double-click" gesture.
    /// </summary>
    bool PrimaryDoubleClick { get; }

    /// <summary>
    /// True every frame the secondary button (right mouse) is held down. Touch input does not
    /// activate this — touch devices have no equivalent of a right-click, so secondary input
    /// must come from a real mouse.
    /// </summary>
    bool SecondaryDown { get; }

    /// <summary>True only on the first frame the secondary button (right mouse) transitions from up to down.</summary>
    bool SecondaryPressed { get; }

    /// <summary>True only on the first frame the secondary button transitions from down to up.</summary>
    bool SecondaryClick { get; }

    /// <summary>
    /// True on the frame of a secondary press transition that occurred within <see cref="DoubleClickThreshold"/>
    /// seconds of the previous secondary press transition.
    /// </summary>
    bool SecondaryDoublePressed { get; }

    /// <summary>
    /// True on the frame of a secondary release transition that occurred within <see cref="DoubleClickThreshold"/>
    /// seconds of the previous secondary release transition.
    /// </summary>
    bool SecondaryDoubleClick { get; }

    /// <summary>
    /// Maximum gap between two press (or two release) transitions for them to register as a
    /// double-press / double-click. Default 250 ms, matching the historical FlatRedBall convention.
    /// Measured against wall-clock time (<see cref="TimeManager.RealTimeSinceStart"/>), so game
    /// pause / time-scaling do not affect detection.
    /// </summary>
    System.TimeSpan DoubleClickThreshold { get; set; }
}
