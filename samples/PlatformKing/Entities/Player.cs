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
    // Swimming constants
    private const float SwimGravity = -200f;   // gentle upward buoyancy
    private const float SwimMaxFallSpeed = 80f; // max downward speed in water
    private const float SwimHorizontalMax = 100f;
    private const float SwimJumpBoost = 200f;

    private readonly PlatformerBehavior _platformer = new();

    private AxisAlignedRectangle _body = null!;
    private Sprite _sprite = null!;
    private bool _isSwimming;

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
            IsVisible = true,
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

    }

    public override void CustomActivity(FrameTime time)
    {
        VelocityYBeforeCollision = VelocityY;

        // Determine swimming state (check before platformer update).
        _isSwimming = WaterZones != null && _body.CollidesWith(WaterZones);

        if (_isSwimming)
        {
            HandleSwimming(time);
        }
        else
        {
            _platformer.Update(this, time);
        }

        UpdateAnimation();
    }

    private void HandleSwimming(FrameTime time)
    {
        float dt = time.DeltaSeconds;

        // Horizontal movement — capped.
        float inputX = _platformer.MovementInput?.X ?? 0f;
        VelocityX = MathF.Max(-SwimHorizontalMax, MathF.Min(SwimHorizontalMax, inputX * SwimHorizontalMax));

        // Vertical physics — gentle upward buoyancy + input.
        float inputY = _platformer.MovementInput?.Y ?? 0f;
        VelocityY += SwimGravity * dt;

        if (inputY > 0.1f)
            VelocityY += 200f * dt;
        else if (inputY < -0.1f)
            VelocityY -= 200f * dt;

        // Jump while swimming = upward boost.
        if (_platformer.JumpInput?.WasJustPressed == true)
            VelocityY = SwimJumpBoost;

        // Clamp fall speed in water.
        VelocityY = MathF.Max(-SwimMaxFallSpeed, VelocityY);
    }

    private void UpdateAnimation()
    {
        if (_sprite.AnimationChains == null) return;

        string facing = _platformer.DirectionFacing == HorizontalDirection.Left ? "Left" : "Right";

        if (_platformer.IsClimbing)
        {
            _sprite.PlayAnimation("Idle" + facing);
            return;
        }

        if (_isSwimming)
        {
            _sprite.PlayAnimation("Fall" + facing);
            return;
        }

        float inputX = _platformer.MovementInput?.X ?? 0f;
        string chain;
        if (_platformer.IsOnGround)
            chain = MathF.Abs(inputX) > 0.1f ? "Walk" : "Idle";
        else
            chain = VelocityY > 0f ? "Jump" : "Fall";

        _sprite.PlayAnimation(chain + facing);
    }
}
