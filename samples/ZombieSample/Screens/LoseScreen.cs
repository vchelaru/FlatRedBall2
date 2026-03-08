using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ZombieSample.Screens;

/// <summary>
/// Displayed when the player's health reaches zero. Press Enter/Space/R to try again.
/// </summary>
public class LoseScreen : Screen
{
    private AxisAlignedRectangle _panel = null!;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(50, 5, 5);

        // A dark red rectangle in the center signals defeat.
        _panel = new AxisAlignedRectangle
        {
            X        = 0f,
            Y        = 0f,
            Width    = 400f,
            Height   = 200f,
            Color    = new Color(160, 20, 20, 220),
            IsFilled = true,
            IsVisible  = true,
        };
        Add(_panel);
    }

    public override void CustomActivity(FrameTime time)
    {
        var kb = Engine.InputManager.Keyboard;
        if (kb.WasKeyPressed(Keys.Enter) ||
            kb.WasKeyPressed(Keys.Space)  ||
            kb.WasKeyPressed(Keys.R))
        {
            MoveToScreen<Level1Screen>();
        }
    }

    public override void CustomDestroy()
    {
        Remove(_panel);
        _panel.Destroy();
    }
}
