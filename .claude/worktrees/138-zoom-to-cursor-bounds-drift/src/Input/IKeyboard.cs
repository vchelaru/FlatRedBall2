using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

/// <summary>
/// Per-frame keyboard state. Accessible from any entity or screen via <c>Engine.Input.Keyboard</c>.
/// Prefer <see cref="WasKeyPressed"/> for one-shot actions (jump, menu confirm) and <see cref="IsKeyDown"/>
/// for continuous input (movement). State is captured once per frame before entity logic runs.
/// </summary>
public interface IKeyboard
{
    /// <summary>True every frame the key is held down.</summary>
    bool IsKeyDown(Keys key);

    /// <summary>True only on the first frame the key transitions from up to down.</summary>
    bool WasKeyPressed(Keys key);

    /// <summary>True only on the first frame the key transitions from down to up.</summary>
    bool WasKeyJustReleased(Keys key);
}
