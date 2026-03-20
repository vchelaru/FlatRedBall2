using FlatRedBall2;
using FlatRedBall2.Collision;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace SampleProject1.Entities;

public class BouncingBall : Entity
{
    public Circle Circle { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Circle = new Circle
        {
            Radius = 12f,
            Color = new XnaColor(255, 120, 40, 255),
            IsVisible = true,
        };
        Add(Circle);

        AccelerationY = -600f;
    }
}
