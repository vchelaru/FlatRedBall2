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
}
