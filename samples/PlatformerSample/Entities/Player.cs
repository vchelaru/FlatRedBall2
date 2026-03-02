using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Microsoft.Xna.Framework.Input;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace PlatformerSample.Entities;

public class Player : Entity
{
    private readonly PlatformerBehavior _platformer = new();

    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 32f,
            Height = 48f,
            Visible = true,
            Color = new XnaColor(100, 180, 255, 255),
        };
        AddChild(Rectangle);

        _platformer.GroundMovement = new PlatformerValues
        {
            MaxSpeedX = 220f,
            AccelerationTimeX = 0.08f,
            DecelerationTimeX = 0.06f,
            Gravity = 900f,
            MaxFallSpeed = 700f,
            JumpVelocity = 500f,
            JumpApplyLength = 0.18f,
            JumpApplyByButtonHold = true,
            UsesAcceleration = true,
        };

        _platformer.AirMovement = new PlatformerValues
        {
            MaxSpeedX = 220f,
            AccelerationTimeX = 0.15f,
            DecelerationTimeX = 0.30f,
            Gravity = 900f,
            MaxFallSpeed = 700f,
            JumpVelocity = 500f,
            JumpApplyLength = 0.18f,
            JumpApplyByButtonHold = true,
            UsesAcceleration = true,
        };

        var keyboard = Engine.InputManager.Keyboard;
        _platformer.JumpInput = new KeyboardPressableInput(keyboard, Keys.Space);
        _platformer.MovementInput = new KeyboardInput2D(keyboard, Keys.Left, Keys.Right, Keys.Up, Keys.Down);
    }

    public override void CustomActivity(FrameTime time)
    {
        _platformer.Update(this, time);
    }
}
