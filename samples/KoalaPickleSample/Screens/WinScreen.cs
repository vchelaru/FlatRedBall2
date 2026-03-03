using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace KoalaPickleSample.Screens;

/// <summary>
/// Shown after the player clears all levels. Displays a victory message and
/// returns to level 1 when any key or button is pressed.
/// </summary>
public class WinScreen : Screen
{
    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(10, 20, 40);

        var stack = new StackPanel();
        stack.Spacing = 24;
        stack.Anchor(Anchor.Center);

        var title = new Label();
        title.Text = "YOU WIN!";
        stack.AddChild(title);

        var subtitle = new Label();
        subtitle.Text = "Press any key to play again";
        stack.AddChild(subtitle);

        AddGum(stack);
    }

    public override void CustomActivity(FrameTime time)
    {
        var keyboard = Engine.InputManager.Keyboard;
        var gamepad  = Engine.InputManager.GetGamepad(0);

        bool anyKey = keyboard.WasKeyPressed(Keys.Space)
                   || keyboard.WasKeyPressed(Keys.Enter)
                   || keyboard.WasKeyPressed(Keys.Z)
                   || gamepad.WasButtonJustPressed(Buttons.A)
                   || gamepad.WasButtonJustPressed(Buttons.Start);

        if (anyKey)
        {
            MoveToScreen<GameScreen>();
        }
    }
}
