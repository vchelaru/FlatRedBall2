using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Microsoft.Xna.Framework.Input;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace SampleProject1.Entities;

public class TopDownPlayer : Entity
{
    private readonly TopDownBehavior _topDown = new();

    public Circle Circle { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Circle = new Circle
        {
            Radius = 14f,
            Color = new XnaColor(100, 220, 130, 255),
            Visible = true,
        };
        Add(Circle);

        _topDown.MovementValues = new TopDownValues
        {
            MaxSpeed = 200f,
            AccelerationTime = 0.08f,
            DecelerationTime = 0.06f,
            UsesAcceleration = true,
            UpdateDirectionFromInput = true,
        };

        var keyboard = Engine.InputManager.Keyboard;
        _topDown.MovementInput = new KeyboardInput2D(keyboard, Keys.Left, Keys.Right, Keys.Up, Keys.Down).Or(
            new KeyboardInput2D(keyboard, Keys.A, Keys.D, Keys.W, Keys.S));
    }

    public override void CustomActivity(FrameTime time)
    {
        _topDown.Update(this, time);
    }
}
