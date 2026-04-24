using FlatRedBall2;
using FlatRedBall2.Animation.Content;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;

namespace PlatformKing.Entities;

public class Enemy : Entity
{
    // Simple I2DInput that lets us set X programmatically each frame.
    private sealed class PatrolInput : I2DInput
    {
        public float X { get; set; }
        public float Y => 0f;
    }

    private readonly PlatformerBehavior _platformer = new();
    private readonly PatrolInput _patrolInput = new() { X = 1f };

    public AxisAlignedRectangle Body { get; private set; } = null!;
    private Sprite _sprite = null!;

    public override void CustomInitialize()
    {

        _sprite = new Sprite { Y = 16f };
        var animations = AnimationChainListSave
            .FromFile("Content/Animations/EnemyAnimations.achx")
            .ToAnimationChainList(Engine.Content);
        _sprite.AnimationChains = animations;
        Add(_sprite);

        Body = new AxisAlignedRectangle
        {
            Width = 14f,
            Height = 14,
            Y = 7,
            Color = Color.Cyan,
            IsVisible = false,
            IsFilled = false
        };
        Add(Body);

        PlatformerConfig.FromJson("Content/enemy.platformer.json").ApplyTo(_platformer);
        _platformer.MovementInput = _patrolInput;
        // No JumpInput assigned — enemy never jumps.
    }

    public override void CustomActivity(FrameTime time)
    {
        // Flip patrol direction when solid collision pushes us horizontally.
        if (LastReposition.X != 0f)
            _patrolInput.X = -_patrolInput.X;

        _platformer.Update(this, time);

        _sprite.PlayAnimation(_patrolInput.X >= 0f ? "WalkRight" : "WalkLeft");
    }
}
