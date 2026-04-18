using System;
using FlatRedBall2;
using FlatRedBall2.Animation.Content;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework.Input;

namespace PlatformerSample.Entities;

/// <summary>
/// A platformer player character that plays animations from PlatformerAnimations.achx.
/// Animations switch automatically each frame based on movement state (idle, walk, jump, fall)
/// and facing direction.
/// </summary>
public class AnimatedPlayer : Entity
{
    private readonly PlatformerBehavior _platformer = new();
    private Sprite _sprite = null!;

    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    public override void CustomInitialize()
    {
        // Collision rectangle — kept invisible since the sprite is the visual
        Rectangle = new AxisAlignedRectangle
        {
            Width = 14,
            Height = 28,
            Y = 14,
            IsVisible = false,
        };
        Add(Rectangle);

        _sprite = new Sprite
        {
            TextureScale = 1f,
        };
        Add(_sprite);

        var animations = AnimationChainListSave
            .FromFile("Content/PlatformerAnimations.achx")
            .ToAnimationChainList(Engine.Content);

        _sprite.AnimationChains = animations;
        _sprite.IsLooping = true;
        _sprite.PlayAnimation("CharacterIdleRight");

        _platformer.GroundMovement = new PlatformerValues
        {
            MaxSpeedX = 220f,
            AccelerationTimeX = TimeSpan.FromSeconds(0.08),
            DecelerationTimeX = TimeSpan.FromSeconds(0.06),
            Gravity = 900f,
            MaxFallSpeed = 700f,
            JumpVelocity = 500f,
            JumpApplyLength = TimeSpan.FromSeconds(0.68),
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

    public override void CustomActivity(FrameTime time)
    {
        _platformer.Update(this, time);
        UpdateAnimation();

        Engine.Overlay.Line(X - 10, Y, X + 10, Y);

        this.Rectangle.IsVisible = true;
        this.Rectangle.IsFilled = false;
    }

    private void UpdateAnimation()
    {
        string animName = ChooseAnimationName();
        if (_sprite.CurrentAnimation?.Name != animName)
            _sprite.PlayAnimation(animName);
    }

    private string ChooseAnimationName()
    {
        string dir = _platformer.DirectionFacing == HorizontalDirection.Right ? "Right" : "Left";

        if (!_platformer.IsOnGround)
            return VelocityY > 0f ? $"CharacterJump{dir}" : $"CharacterFall{dir}";

        if (MathF.Abs(VelocityX) > 10f)
            return $"CharacterWalk{dir}";

        return $"CharacterIdle{dir}";
    }
}
