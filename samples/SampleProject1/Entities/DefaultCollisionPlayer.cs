using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Microsoft.Xna.Framework.Input;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace SampleProject1.Entities;

public class DefaultCollisionPlayer : Entity
{
    private readonly TopDownBehavior _topDown = new();
    private Circle _body = null!;
    private Circle _range = null!;
    private bool _collisionEnabled = true;

    public bool IsCollisionEnabled => _collisionEnabled;

    public override void CustomInitialize()
    {
        _body = new Circle
        {
            Radius = 14f,
            IsFilled = true,
            Color = new XnaColor(100, 180, 255, 220),
            IsVisible = true,
        };
        Add(_body); // isDefaultCollision defaults to true

        _range = new Circle
        {
            Radius = 52f,
            IsFilled = false,
            OutlineThickness = 1.5f,
            Color = new XnaColor(255, 255, 255, 80),
            IsVisible = true,
        };
        Add(_range, isDefaultCollision: false); // visual only — does not participate in default collision

        _topDown.MovementValues = new TopDownValues
        {
            MaxSpeed = 180f,
            AccelerationTime = 0.08f,
            DecelerationTime = 0.06f,
            UsesAcceleration = true,
        };

        var keyboard = Engine.InputManager.Keyboard;
        _topDown.MovementInput = new KeyboardInput2D(keyboard, Keys.Left, Keys.Right, Keys.Up, Keys.Down).Or(
            new KeyboardInput2D(keyboard, Keys.A, Keys.D, Keys.W, Keys.S));
    }

    public override void CustomActivity(FrameTime time)
    {
        _topDown.Update(this, time);

        if (Engine.InputManager.Keyboard.WasKeyPressed(Keys.Space))
        {
            _collisionEnabled = !_collisionEnabled;
            SetDefaultCollision(_body, _collisionEnabled);
            _body.Color = _collisionEnabled
                ? new XnaColor(100, 180, 255, 220)
                : new XnaColor(150, 150, 150, 100);
        }
    }
}
