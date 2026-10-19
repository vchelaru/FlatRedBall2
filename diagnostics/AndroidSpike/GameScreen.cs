using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;

namespace AndroidSpike;

// Bisecting what renders on Android: background + Gum label only (no shapes, which crash on Android).
public class GameScreen : Screen
{
    public override void CustomInitialize()
    {
        Camera.BackgroundColor = Color.CornflowerBlue;

        var label = new Label { Text = "FlatRedBall2 on Android" };
        label.Anchor(Anchor.Center);
        Add(label);
    }
}
