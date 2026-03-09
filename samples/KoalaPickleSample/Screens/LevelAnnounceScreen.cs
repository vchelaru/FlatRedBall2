using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace KoalaPickleSample.Screens;

/// <summary>
/// Briefly displays the upcoming level number before transferring to <see cref="GameScreen"/>.
/// Set <see cref="LevelIndex"/> via the <c>MoveToScreen</c> configure callback before
/// <see cref="Screen.CustomInitialize"/> runs.
/// </summary>
public class LevelAnnounceScreen : Screen
{
    /// <summary>0-based index of the level to announce and then start.</summary>
    public int LevelIndex { get; set; } = 0;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(10, 20, 40);

        var panel = new StackPanel();
        panel.Spacing = 20;
        panel.Anchor(Anchor.Center);

        var levelLabel = new Label { Text = $"Level {LevelIndex + 1}" };
        panel.AddChild(levelLabel);

        var hint = new Label { Text = "Get ready..." };
        panel.AddChild(hint);

        Add(panel);

        AutoAdvance();
    }

    private async void AutoAdvance()
    {
        await Engine.Time.DelaySeconds(2, Token);
        MoveToScreen<GameScreen>(s => s.LevelIndex = LevelIndex);
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
            MoveToScreen<GameScreen>(s => s.LevelIndex = LevelIndex);
    }
}
