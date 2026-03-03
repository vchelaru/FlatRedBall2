using FlatRedBall2;
using FlatRedBall2.Collision;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace KoalaPickleSample.Entities;

public class Platform : Entity
{
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 200f,
            Height = 24f,
            Visible = true,
            Color = new XnaColor(160, 160, 170, 255),
        };
        AddChild(Rectangle);
    }

    public override void CustomDestroy()
    {
        Rectangle.Destroy();
    }
}
