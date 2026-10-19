using FlatRedBall2;
using FlatRedBall2.Collision;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;

namespace AndroidSpike;

public class GameScreen : Screen
{
    private Circle _circle = null!;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = Color.CornflowerBlue;

        var label = new Label { Text = "FlatRedBall2 on Android" };
        label.Anchor(Anchor.Top);
        Add(label);

        Add(new Circle { X = 0, Y = 0, Radius = 80, Color = Color.Red, IsFilled = true, IsVisible = true });

        _circle = new Circle { Radius = 60, Color = Color.Orange, IsFilled = true, IsVisible = true };
        Add(_circle);
    }

    public override void CustomActivity(FrameTime time)
    {
        var cursor = Engine.Input.Cursor;
        _circle.X = cursor.WorldPosition.X;
        _circle.Y = cursor.WorldPosition.Y;
    }
}
