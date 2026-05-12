using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace PlatformKing.Entities;

public class Box : Entity
{
    public AARect Body { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Body = new AARect
        {
            Width = 14f,
            Height = 14f,
            Y = 7f,
            Color = Color.Yellow,
            IsVisible = true,
        };
        Add(Body);
    }
}
