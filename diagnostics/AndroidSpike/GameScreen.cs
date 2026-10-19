using FlatRedBall2;
using FlatRedBall2.Collision;
using Gum.Forms.Controls;
using Gum.GueDeriving;
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

        // A Gum shape (draws through Gum's own ShapeRenderer), distinct from the engine Circles below.
        var gumRect = new RectangleRuntime { Width = 500, Height = 160, CornerRadius = 50, IsFilled = true, Color = Color.Purple };
        gumRect.Anchor(Anchor.Bottom);
        Add(gumRect);

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
