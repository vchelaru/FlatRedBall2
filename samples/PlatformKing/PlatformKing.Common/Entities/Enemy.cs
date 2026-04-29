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
    // PlatformerBehavior.MovementInput expects an I2DInput. For AI-driven
    // movement we supply this stand-in and mutate X from patrol logic
    // instead of reading from a keyboard/gamepad.
    private sealed class PatrolInput : I2DInput
    {
        public float X { get; set; }
        public float Y => 0f;
    }

    private readonly PlatformerBehavior _platformer = new();
    private readonly PatrolInput _patrolInput = new() { X = 1f };

    public AARect Body { get; private set; } = null!;
    private AARect _leftFoot = null!;
    private AARect _rightFoot = null!;
    private Sprite _sprite = null!;

    public TileShapes? SolidCollision { get; set; }
    public TileShapes? JumpThroughCollision { get; set; }

    public override void CustomInitialize()
    {

        _sprite = new Sprite { Y = 16f };
        var animations = Engine.Content.LoadAnimationChainList("Content/Animations/EnemyAnimations.achx");
        _sprite.AnimationChains = animations;
        Add(_sprite);

        Body = new AARect
        {
            Width = 14f,
            Height = 14f,
            Y = 7f,
            Color = Color.Cyan,
            IsVisible = false,
            IsFilled = false
        };
        Add(Body);

        // Foot probes sit just outside the body's left/right edges and just below
        // its bottom. Each frame we check whether the probe overlaps any ground;
        // if the one ahead of travel does not, we've reached a ledge and flip.
        _leftFoot = new AARect
        {
            Width = 2f,
            Height = 2f,
            X = -8f,
            Y = -1f,
            Color = Color.Yellow,
            IsVisible = false,
            IsFilled = false
        };
        Add(_leftFoot, isDefaultCollision: false);

        _rightFoot = new AARect
        {
            Width = 2f,
            Height = 2f,
            X = 8f,
            Y = -1f,
            Color = Color.Yellow,
            IsVisible = false,
            IsFilled = false
        };
        Add(_rightFoot, isDefaultCollision: false);

        PlatformerConfig.FromJson("Content/enemy.platformer.json").ApplyTo(_platformer);
        _platformer.MovementInput = _patrolInput;
        // No JumpInput assigned — enemy never jumps.
    }

    public override void CustomActivity(FrameTime time)
    {
        // Flip patrol direction when solid collision pushes us horizontally,
        // or — only while on the ground — when the foot probe in the direction
        // of travel is over empty space. Skipping the ledge check while airborne
        // prevents mid-jump/fall jitter when both feet are temporarily off ground.
        if (LastReposition.X != 0f)
        {
            _patrolInput.X = -_patrolInput.X;
        }
        else if (_platformer.IsOnGround)
        {
            if (_patrolInput.X > 0f && !HasGround(_rightFoot))
                _patrolInput.X = -1f;
            else if (_patrolInput.X < 0f && !HasGround(_leftFoot))
                _patrolInput.X = 1f;
        }

        _platformer.Update(this, time);

        _sprite.PlayAnimation(_patrolInput.X >= 0f ? "WalkRight" : "WalkLeft");
    }

    private bool HasGround(AARect foot)
    {
        if (SolidCollision != null && foot.CollidesWith(SolidCollision))
            return true;
        if (JumpThroughCollision != null && foot.CollidesWith(JumpThroughCollision))
            return true;
        return false;
    }
}
