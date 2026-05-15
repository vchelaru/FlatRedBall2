using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;

namespace MyGame.Screens;

public class GameScreen : Screen
{
    public override void CustomInitialize()
    {
        var label = new Label();
        label.Text = "Hello from FlatRedBall 2";
        label.Anchor(Anchor.Center);
        Add(label);
    }

    public override void CustomActivity(FrameTime time)
    {
    }

    public override void CustomDestroy()
    {
    }
}
