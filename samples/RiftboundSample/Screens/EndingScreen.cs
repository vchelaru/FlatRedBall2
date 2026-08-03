using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using RiftboundSample.Systems;

namespace RiftboundSample.Screens;

public class EndingScreen : Screen
{
    public EndingType EndingType { get; set; } = EndingType.Good;
    public TimeSpan PlayTime { get; set; }

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(5, 5, 15);

        var panel = new StackPanel();
        panel.Spacing = 16;
        panel.Anchor(Anchor.Center);

        // Ending title
        var titleLabel = new Label();
        titleLabel.Text = EndingSystem.GetEndingTitle(EndingType);
        panel.AddChild(titleLabel);

        // Narration lines
        var narration = EndingSystem.GetEndingNarration(EndingType);
        foreach (var line in narration)
        {
            var lineLabel = new Label();
            lineLabel.Text = line;
            panel.AddChild(lineLabel);
        }

        // Spacer
        var spacer = new Label();
        spacer.Text = "";
        panel.AddChild(spacer);

        // Credits
        var creditsLabel = new Label();
        creditsLabel.Text = "--- RIFTBOUND ---";
        panel.AddChild(creditsLabel);

        var creditLine1 = new Label();
        creditLine1.Text = "A FlatRedBall2 Sample Game";
        panel.AddChild(creditLine1);

        // Play time
        var timeLabel = new Label();
        timeLabel.Text = $"Play Time: {PlayTime.Hours:D2}:{PlayTime.Minutes:D2}:{PlayTime.Seconds:D2}";
        panel.AddChild(timeLabel);

        // Thank you
        var thankYou = new Label();
        thankYou.Text = "Thank you for playing.";
        panel.AddChild(thankYou);

        // Return prompt
        var promptLabel = new Label();
        promptLabel.Text = "Press Enter to return to the title screen.";
        panel.AddChild(promptLabel);

        Add(panel);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (Engine.InputManager.Keyboard.WasKeyPressed(Keys.Enter))
            MoveToScreen<TitleScreen>();
    }
}
