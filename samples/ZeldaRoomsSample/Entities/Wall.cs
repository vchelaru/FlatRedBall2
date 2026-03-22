using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace ZeldaRoomsSample.Entities;

public class Wall : Entity
{
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 64f,
            Height = 64f,
            Color = new Color(80, 80, 100),
            IsVisible = true,
        };
        Add(Rectangle);
    }
}
