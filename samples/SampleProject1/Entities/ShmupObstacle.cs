using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace SampleProject1.Entities;

public class ShmupObstacle : Entity
{
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 60,
            Height = 60,
            Color = new Color(100, 100, 100),
            IsVisible = true,
            IsFilled = true,
        };
        Add(Rectangle);
    }
}
