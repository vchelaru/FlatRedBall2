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

    private AxisAlignedRectangle _body = null!;
    private Sprite _sprite = null!;
    private bool _isSwimming;

    private PlatformerValues? _normalGroundMovement;
    private PlatformerValues _normalAirMovement = null!;
    private PlatformerValues? _normalAfterDoubleJump;
    private PlatformerValues? _waterGroundMovement;
    private PlatformerValues _waterAirMovement = null!;

    // Track velocity before the platformer update so box-break detection can see it.
    public float VelocityYBeforeCollision { get; private set; }

    public AxisAlignedRectangle CollisionBody => _body;

    public PlatformerBehavior Platformer => _platformer;

    public TileShapeCollection? Ladders
    {
        get => _platformer.Ladders;
        set => _platformer.Ladders = value;
    }

    public TileShapeCollection? WaterZones { get; set; }

    public override void CustomInitialize()
    {

        _sprite = new Sprite
        {
            Y = 16f,
        };
        var animations = AnimationChainListSave
            .FromFile("Content/Animations/PlayerAnimations.achx")
            .ToAnimationChainList(Engine.Content);
        _sprite.AnimationChains = animations;
        Add(_sprite);
        _body = new AxisAlignedRectangle
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
            new KeyboardPressableInput(keyboard, Keys.Space)
            .Or(new KeyboardPressableInput(keyboard, Keys.Up));

        PlatformerConfig.FromJson("Content/player.platformer.json").ApplyTo(_platformer);
        _platformer.CollisionShape = _body;

        _normalGroundMovement = _platformer.GroundMovement;
        _normalAirMovement = _platformer.AirMovement;
        _normalAfterDoubleJump = _platformer.AfterDoubleJump;

        var waterBehavior = new PlatformerBehavior();
        PlatformerConfig.FromJson("Content/player.water.platformer.json").ApplyTo(waterBehavior);
        _waterGroundMovement = waterBehavior.GroundMovement;
        _waterAirMovement = waterBehavior.AirMovement;
    }

    public override void CustomActivity(FrameTime time)
    {
        VelocityYBeforeCollision = VelocityY;

        _isSwimming = IsInWater();
        _platformer.GroundMovement = _isSwimming ? _waterGroundMovement : _normalGroundMovement;
        _platformer.AirMovement = _isSwimming ? _waterAirMovement : _normalAirMovement;
        _platformer.AfterDoubleJump = _isSwimming ? null : _normalAfterDoubleJump;

        _platformer.Update(this, time);

        UpdateAnimation();
    }

    private bool IsInWater() => WaterZones != null && _body.CollidesWith(WaterZones);

    private void UpdateAnimation()
    {
        if (_sprite.AnimationChains == null) return;
        _sprite.AnimationSpeed = 1;
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
