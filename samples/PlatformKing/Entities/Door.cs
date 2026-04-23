using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace PlatformKing.Entities;

public class Door : Entity
{
    public AxisAlignedRectangle Body { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Body = new AxisAlignedRectangle
        {
            Width = 16f,
            Height = 16f,
            Y = 8f,
            Color = new Color(255, 215, 0, 80),
            IsVisible = true,
        };
        Add(Body);
    }
}
