using System;
using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Microsoft.Xna.Framework.Input;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ArcticCrossingSample.Entities;

public class Player : Entity
{
    private readonly PlatformerBehavior _platformer = new();

    // Body parts
    private AxisAlignedRectangle _head = null!;
    private AxisAlignedRectangle _body = null!;
    private AxisAlignedRectangle _leftArm = null!;
    private AxisAlignedRectangle _rightArm = null!;
    private AxisAlignedRectangle _leftLeg = null!;
    private AxisAlignedRectangle _rightLeg = null!;

    public bool IsFemale { get; set; }

    public bool IsOnGround => _platformer.IsOnGround;
    public HorizontalDirection DirectionFacing => _platformer.DirectionFacing;

    public override void CustomInitialize()
    {
        // Body (torso) — the default collision shape. Entity center = body center.
        _body = new AxisAlignedRectangle { Width = 16, Height = 20, IsVisible = true };
        Add(_body);

        // Head — sits on top of body
        _head = new AxisAlignedRectangle { Width = 12, Height = 12, Y = 16, IsVisible = true };
        Add(_head, isDefaultCollision: false);

        // Arms — centered vertically with body, flanking left/right
        _leftArm = new AxisAlignedRectangle { Width = 4, Height = 16, X = -10, IsVisible = true };
        Add(_leftArm, isDefaultCollision: false);

        _rightArm = new AxisAlignedRectangle { Width = 4, Height = 16, X = 10, IsVisible = true };
        Add(_rightArm, isDefaultCollision: false);

        // Legs — hang below body
        _leftLeg = new AxisAlignedRectangle { Width = 6, Height = 14, X = -4, Y = -17, IsVisible = true };
        Add(_leftLeg, isDefaultCollision: false);

        _rightLeg = new AxisAlignedRectangle { Width = 6, Height = 14, X = 4, Y = -17, IsVisible = true };
        Add(_rightLeg, isDefaultCollision: false);

        SetAppearance(IsFemale);

        // Movement
        _platformer.GroundMovement = new PlatformerValues
        {
            MaxSpeedX = 200f,
            AccelerationTimeX = TimeSpan.FromSeconds(0.08),
            DecelerationTimeX = TimeSpan.FromSeconds(0.06),
            Gravity = 800f,
            MaxFallSpeed = 600f,
            JumpVelocity = 420f,
            JumpApplyLength = TimeSpan.FromSeconds(0.2),
            JumpApplyByButtonHold = true,
            UsesAcceleration = true,
        };

        _platformer.AirMovement = new PlatformerValues
        {
            MaxSpeedX = 200f,
            AccelerationTimeX = TimeSpan.FromSeconds(0.14),
            DecelerationTimeX = TimeSpan.FromSeconds(0.25),
            Gravity = 800f,
            MaxFallSpeed = 600f,
            JumpVelocity = 420f,
            JumpApplyLength = TimeSpan.FromSeconds(0.2),
            JumpApplyByButtonHold = true,
            UsesAcceleration = true,
        };

        // Input — WASD + Arrow keys, Space to jump
        var keyboard = Engine.Input.Keyboard;
        _platformer.JumpInput = new KeyboardPressableInput(keyboard, Keys.Space);
        _platformer.MovementInput = new KeyboardInput2D(keyboard, Keys.Left, Keys.Right, Keys.Up, Keys.Down)
            .Or(new KeyboardInput2D(keyboard, Keys.A, Keys.D, Keys.W, Keys.S));
    }

    public override void CustomActivity(FrameTime time)
    {
        _platformer.Update(this, time);
    }

    public void SetAppearance(bool isFemale)
    {
        if (isFemale)
        {
            _head.Color = new XnaColor(255, 180, 200, 255);
            _body.Color = new XnaColor(160, 60, 180, 255);
            _leftArm.Color = new XnaColor(140, 50, 160, 255);
            _rightArm.Color = new XnaColor(140, 50, 160, 255);
            _leftLeg.Color = new XnaColor(40, 130, 130, 255);
            _rightLeg.Color = new XnaColor(40, 130, 130, 255);
        }
        else
        {
            _head.Color = new XnaColor(255, 160, 60, 255);
            _body.Color = new XnaColor(220, 60, 50, 255);
            _leftArm.Color = new XnaColor(200, 50, 40, 255);
            _rightArm.Color = new XnaColor(200, 50, 40, 255);
            _leftLeg.Color = new XnaColor(40, 50, 120, 255);
            _rightLeg.Color = new XnaColor(40, 50, 120, 255);
        }
    }
}
