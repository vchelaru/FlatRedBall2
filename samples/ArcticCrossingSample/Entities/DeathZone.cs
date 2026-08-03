using FlatRedBall2;
using FlatRedBall2.Collision;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ArcticCrossingSample.Entities;

public class DeathZone : Entity
{
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 50000f,
            Height = 40f,
            IsVisible = true,
            IsFilled = true,
            Color = new XnaColor(20, 60, 140, 200),
        };
        Add(Rectangle);
    }
}
