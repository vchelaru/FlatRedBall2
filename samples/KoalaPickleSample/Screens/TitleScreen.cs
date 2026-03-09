using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace KoalaPickleSample.Screens;

public class TitleScreen : Screen
{
    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(10, 20, 40);

        var panel = new StackPanel();
        panel.Spacing = 20;
        panel.Anchor(Anchor.Center);

        var title = new Label { Text = "The Great Koala vs Pickle Journey" };
        panel.AddChild(title);

        var hint = new Label { Text = "Press SPACE to start" };
        panel.AddChild(hint);

        Add(panel);

        AutoAdvance();
    }

    private async void AutoAdvance()
    {
        await Engine.Time.DelaySeconds(3, Token);
        MoveToScreen<LevelAnnounceScreen>();
    }

    public override void CustomActivity(FrameTime time)
    {
        var keyboard = Engine.Input.Keyboard;
        var gamepad  = Engine.Input.GetGamepad(0);

        bool pressed = keyboard.WasKeyPressed(Keys.Space)
                    || keyboard.WasKeyPressed(Keys.Enter)
                    || gamepad.WasButtonJustPressed(Buttons.Start)
                    || gamepad.WasButtonJustPressed(Buttons.A);

        if (pressed)
            MoveToScreen<LevelAnnounceScreen>();
    }
}
