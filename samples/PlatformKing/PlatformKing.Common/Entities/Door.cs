using FlatRedBall2;
using FlatRedBall2.Collision;

namespace PlatformKing.Entities;

// Doors are placed at the edge of each map and are intentionally invisible —
// the player walks off the map edge into the door's trigger volume, which
// transitions to the next level.
public class Door : Entity
{
    public AARect Body { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Body = new AARect
        {
            Width = 16f,
            Height = 16f,
            Y = 8f,
            IsVisible = false,
        };
        Add(Body);
    }
}
