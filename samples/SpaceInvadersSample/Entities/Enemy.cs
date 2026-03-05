using FlatRedBall2;
using FlatRedBall2.Collision;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace SpaceInvadersSample.Entities;

public class Enemy : Entity
{
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    public float BaseX { get; set; }
    public float BaseY { get; set; }

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 24f,
            Height = 18f,
            Visible = true,
            Color = new XnaColor(255, 255, 255, 255),
        };
        Add(Rectangle);
    }

    public override void CustomDestroy()
    {
        Rectangle.Destroy();
    }
}
