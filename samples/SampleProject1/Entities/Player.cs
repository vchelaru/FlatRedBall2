using System;
using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Microsoft.Xna.Framework.Input;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace SampleProject1.Entities;

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
            Color = new XnaColor(100, 180, 255, 255),
            IsVisible = true,
        };
        Add(Rectangle);

        _platformer.GroundMovement = new PlatformerValues
        {
            MaxSpeedX = 220f,
            AccelerationTimeX = TimeSpan.FromSeconds(0.08),
            DecelerationTimeX = TimeSpan.FromSeconds(0.06),
            Gravity = 900f,
            MaxFallSpeed = 700f,
            JumpVelocity = 500f,
            JumpApplyLength = TimeSpan.FromSeconds(0.18),
            JumpApplyByButtonHold = true,
        };

        _platformer.AirMovement = new PlatformerValues
        {
            MaxSpeedX = 220f,
            AccelerationTimeX = TimeSpan.FromSeconds(0.15),
            DecelerationTimeX = TimeSpan.FromSeconds(0.30),
            Gravity = 900f,
            MaxFallSpeed = 700f,
            JumpVelocity = 500f,
            JumpApplyLength = TimeSpan.FromSeconds(0.18),
            JumpApplyByButtonHold = true,
        };

        var keyboard = Engine.Input.Keyboard;
        _platformer.JumpInput = new KeyboardPressableInput(keyboard, Keys.Space);
        _platformer.MovementInput = new KeyboardInput2D(keyboard, Keys.Left, Keys.Right, Keys.Up, Keys.Down).Or(
            new KeyboardInput2D(keyboard, Keys.A, Keys.D, Keys.W, Keys.S));
    }

    private static readonly XnaColor NormalColor = new(100, 180, 255, 255);
    private static readonly XnaColor JumpingColor = new(255, 220, 80, 255);

    public override void CustomActivity(FrameTime time)
    {
        _platformer.Update(this, time);
        Rectangle.Color = _platformer.IsApplyingJump ? JumpingColor : NormalColor;
    }
}
