using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace RiftboundSample.Entities;

public class PlayerEntity : Entity
{
    private readonly TopDownBehavior _topDown = new();

    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 16,
            Height = 16,
            Color = new Color(60, 180, 80),
            IsFilled = true,
            OutlineThickness = 0f,
            IsVisible = true,
        };
        Add(Rectangle);

        _topDown.MovementValues = new TopDownValues
        {
            MaxSpeed = 150f,
            UsesAcceleration = true,
            AccelerationTime = 0.1f,
            DecelerationTime = 0.1f,
        };

        var keyboard = Engine.InputManager.Keyboard;
        _topDown.MovementInput = new KeyboardInput2D(
            keyboard, Keys.A, Keys.D, Keys.W, Keys.S);
    }

    public override void CustomActivity(FrameTime time)
    {
        _topDown.Update(this, time);
    }
}
