using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ZombieSample.Screens;

/// <summary>
/// Displayed when the player reaches the goal. Press Enter/Space/R to play again.
/// </summary>
public class WinScreen : Screen
{
    private AxisAlignedRectangle _panel = null!;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(10, 60, 20);

        // A large gold rectangle in the center signals victory.
        _panel = new AxisAlignedRectangle
        {
            X        = 0f,
            Y        = 0f,
            Width    = 400f,
            Height   = 200f,
            Color    = new Color(200, 170, 0, 220),
            IsFilled = true,
            IsVisible  = true,
        };
        Add(_panel);
    }

    public override void CustomActivity(FrameTime time)
    {
        var kb = Engine.Input.Keyboard;
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
