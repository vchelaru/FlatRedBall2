using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ZombieSample.Entities;

public class Player : Entity
{
    private const float InvincibilitySeconds = 0.5f;
    private const float HitFlashSeconds      = 0.15f;

    private static readonly Color NormalColor = new(50, 150, 255, 255);
    private static readonly Color HitColor    = new(255, 255, 255, 255);

    private readonly TopDownBehavior _topDown = new();

    private Circle _circle = null!;
    private float  _invincibilityTimer;
    private float  _hitFlashTimer;

    public int MaxHealth { get; } = 5;
    public int Health    { get; private set; }

    public bool IsDead        => Health <= 0;
    public bool DiedThisFrame { get; private set; }

    public override void CustomInitialize()
    {
        Health = MaxHealth;

        _circle = new Circle { Radius = 20f, IsVisible = true, Color = NormalColor };
        Add(_circle);

        var values = new TopDownValues
        {
            MaxSpeed        = 250f,
            UsesAcceleration = false,
        };
        _topDown.MovementValues = values;

        var keyboard = Engine.InputManager.Keyboard;
        _topDown.MovementInput = new KeyboardInput2D(
            keyboard, Keys.Left, Keys.Right, Keys.Up, Keys.Down)
            .Or(new KeyboardInput2D(keyboard, Keys.A, Keys.D, Keys.W, Keys.S));
    }

    public override void CustomActivity(FrameTime time)
    {
        if (IsDead) return;

        DiedThisFrame = false;

        _topDown.Update(this, time);

        _invincibilityTimer -= time.DeltaSeconds;
        _hitFlashTimer      -= time.DeltaSeconds;

        _circle.Color = _hitFlashTimer > 0f ? HitColor : NormalColor;
    }

    /// <summary>
    /// Called by the screen when a zombie contacts the player.
    /// Returns false if still invincible; true if damage was applied.
    /// </summary>
    public bool TryTakeDamage()
    {
        if (IsDead || _invincibilityTimer > 0f) return false;

        Health--;
        _invincibilityTimer = InvincibilitySeconds;
        _hitFlashTimer      = HitFlashSeconds;

        if (Health <= 0)
            DiedThisFrame = true;

        return true;
    }

    public override void CustomDestroy()
    {
        _circle.Destroy();
    }
}
