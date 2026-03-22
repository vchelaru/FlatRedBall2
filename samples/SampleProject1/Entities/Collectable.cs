using FlatRedBall2;
using FlatRedBall2.Collision;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace SampleProject1.Entities;

public class Collectable : Entity
{
    public Circle Circle { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Circle = new Circle
        {
            Radius = 10f,
            IsVisible = true,
            Color = new XnaColor(255, 215, 0, 255),
        };
        Add(Circle);
    }
}
