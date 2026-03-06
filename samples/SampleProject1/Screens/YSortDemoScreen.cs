using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using SampleProject1.Entities;

namespace SampleProject1.Screens;

public class YSortDemoScreen : Screen
{
    public override void CustomInitialize()
    {
        SortMode = FlatRedBall2.Rendering.SortMode.ZSecondaryParentY;
        Camera.BackgroundColor = new Color(30, 30, 40);

        var shipFactory = new Factory<ShipEntity>(this);
        var otherFactory = new Factory<OtherEntity>(this);

        var ship = shipFactory.Create();
        ship.X = 0f;
        ship.Y = 0f;

        // Scatter OtherEntities at several positions so the ship can move in front of and behind them
        float[] xs = { -240f, -120f,  0f, 120f, 240f };
        float[] ys = { -150f,   75f, -50f,  100f, -200f };
        for (int i = 0; i < xs.Length; i++)
        {
            var other = otherFactory.Create();
            other.X = xs[i];
            other.Y = ys[i];
        }

        var panel = new Panel();
        panel.Dock(Dock.Fill);
        var hint = new Label { Text = "Arrow Keys / WASD to move  |  Y-sort: lower = in front" };
        hint.Anchor(Anchor.TopLeft);
        hint.X = 10;
        hint.Y = 10;
        panel.AddChild(hint);
        Add(panel);
    }
}
