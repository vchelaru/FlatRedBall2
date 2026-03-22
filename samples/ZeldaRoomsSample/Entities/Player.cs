using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ZeldaRoomsSample.Entities;

public class Player : Entity
{
    private const float AttackDuration = 0.25f;
    private const float InvincibilityDuration = 1.0f;
    private const float KnockbackSpeed = 300f;
    private const float KnockbackDuration = 0.2f;

    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    public int Hearts { get; private set; } = 3;
    public bool IsDead => Hearts <= 0;

    // Direction used to place sword hitbox
    public TopDownDirection FacingDirection => _topDown.DirectionFacing;

    private readonly TopDownBehavior _topDown = new();
    private bool _isAttacking = false;
    private float _invincibilityTimer = 0f;
    private float _knockbackTimer = 0f;

    // Flash during invincibility
    private readonly Color _normalColor = new Color(80, 140, 255);
    private readonly Color _flashColor = new Color(255, 255, 100);

    // Callback: screen needs to know when attack key is pressed
    public event Action? AttackPressed;
    public event Action? Died;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 40f,
            Height = 40f,
            Color = _normalColor,
            IsVisible = true,
        };
        Add(Rectangle);

        var kb = Engine.Input.Keyboard;
        _topDown.MovementValues = new TopDownValues
        {
            MaxSpeed = 180f,
            UsesAcceleration = false,
        };
        _topDown.MovementInput = new KeyboardInput2D(kb, Keys.Left, Keys.Right, Keys.Up, Keys.Down)
            .Or(new KeyboardInput2D(kb, Keys.A, Keys.D, Keys.W, Keys.S));
        _topDown.PossibleDirections = PossibleDirections.EightWay;
    }

    public void TakeHit(Vector2 knockbackDirection)
    {
        if (_invincibilityTimer > 0f) return;

        Hearts--;
        _invincibilityTimer = InvincibilityDuration;
        _knockbackTimer = KnockbackDuration;

        VelocityX = knockbackDirection.X * KnockbackSpeed;
        VelocityY = knockbackDirection.Y * KnockbackSpeed;

        if (Hearts <= 0)
            Died?.Invoke();
    }

    public override void CustomActivity(FrameTime time)
    {
        bool isKnockedBack = _knockbackTimer > 0f;
        bool isInvincible = _invincibilityTimer > 0f;

        // Invincibility countdown
        if (isInvincible)
        {
            _invincibilityTimer -= time.DeltaSeconds;
            // Flash: alternate color every 0.1s
            Rectangle.Color = (int)(_invincibilityTimer / 0.1f) % 2 == 0 ? _flashColor : _normalColor;
        }
        else
        {
            Rectangle.Color = _normalColor;
        }

        // Knockback countdown
        if (isKnockedBack)
        {
            _knockbackTimer -= time.DeltaSeconds;
            if (_knockbackTimer <= 0f)
            {
                VelocityX = 0f;
                VelocityY = 0f;
            }
            return; // no movement input during knockback
        }

        // Attack lock
        if (_isAttacking) return;

        // Attack input
        if (Engine.Input.Keyboard.WasKeyPressed(Keys.Space))
        {
            AttackPressed?.Invoke();
            return;
        }

        _topDown.Update(this, time);
    }

    public void BeginAttack()
    {
        _isAttacking = true;
        VelocityX = 0f;
        VelocityY = 0f;
    }

    public void EndAttack()
    {
        _isAttacking = false;
    }
}
