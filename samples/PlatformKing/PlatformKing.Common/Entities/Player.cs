using FlatRedBall2;
using FlatRedBall2.Animation.Content;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework.Input;

namespace PlatformKing.Entities;

public class Player : Entity, IPlatformerEntity
{
    private readonly PlatformerBehavior _platformer = new();

    private AARect _body = null!;
    private Sprite _sprite = null!;
    private bool _isSwimming;

    // Two loaded configs; we re-apply one of them to _platformer each frame
    // based on water overlap. ApplyTo is idempotent — cheaper than tracking
    // edge-triggered transitions with a "wasSwimming" field.
    private PlatformerConfig _normalConfig = null!;
    private PlatformerConfig _waterConfig = null!;

    public AARect CollisionBody => _body;

    public PlatformerBehavior Platformer => _platformer;

    public TileShapes? Ladders
    {
        get => _platformer.Ladders;
        set => _platformer.Ladders = value;
    }

    // Ladders are built into PlatformerBehavior; water is a game-defined concept,
    // so we hold the zone ourselves and use it to swap movement values each frame.
    public TileShapes? WaterZones { get; set; }

    public override void CustomInitialize()
    {

        // Sprite is offset above the body so the sprite's feet align with the
        // bottom of the collision rectangle (body Y=10, Height=20 → bottom at Y=0).
        _sprite = new Sprite
        {
            Y = 16f,
        };
        var animations = Engine.Content.LoadAnimationChainList("Content/Animations/PlayerAnimations.achx");
        _sprite.AnimationChains = animations;
        Add(_sprite);
        _body = new AARect
        {
            Width = 12f,
            Height = 20f,
            Y = 10f,
            IsVisible = false,
            IsFilled = false
        };
        Add(_body);

        var keyboard = Engine.Input.Keyboard;
        _platformer.MovementInput =
            new KeyboardInput2D(keyboard, Keys.Left, Keys.Right, Keys.Up, Keys.Down)
            .Or(new KeyboardInput2D(keyboard, Keys.A, Keys.D, Keys.W, Keys.S));
        _platformer.JumpInput =
            new KeyboardPressableInput(keyboard, Keys.Space);

        _normalConfig = PlatformerConfig.FromJson("Content/player.platformer.json");
        _waterConfig = PlatformerConfig.FromJson("Content/player.water.platformer.json");
        _normalConfig.ApplyTo(_platformer);
        _platformer.CollisionShape = _body;
    }

    public override void CustomActivity(FrameTime time)
    {
        _isSwimming = IsInWater();
        if (_isSwimming) _waterConfig.ApplyTo(_platformer);
        else             _normalConfig.ApplyTo(_platformer);

        _platformer.Update(this, time);

        UpdateAnimation();
    }

    private bool IsInWater() => WaterZones != null && _body.CollidesWith(WaterZones);

    private void UpdateAnimation()
    {
        if (_sprite.AnimationChains == null) return;
        string facing = _platformer.DirectionFacing == HorizontalDirection.Left ? "Left" : "Right";

        if (_platformer.IsClimbing)
        {
            if(VelocityY != 0)
            {
                _sprite.PlayAnimation("ClimbMove");
            }
            else
            {
                _sprite.PlayAnimation("ClimbIdle");
            }
            return;
        }

        if (_isSwimming && !_platformer.IsOnGround)
        {
            _sprite.PlayAnimation("Fall" + facing);
            return;
        }

        float inputX = _platformer.MovementInput?.X ?? 0f;
        string chain;
        if (_platformer.IsOnGround)
        {
            chain = MathF.Abs(inputX) > 0.1f ? "Walk" : "Idle";
        }
        else
        {
            chain = VelocityY > 0f ? "Jump" : "Fall";
        }


        _sprite.PlayAnimation(chain + facing);
    }
}
