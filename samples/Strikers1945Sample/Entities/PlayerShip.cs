using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Strikers1945Sample.Screens;

namespace Strikers1945Sample.Entities;

public class PlayerShip : Entity
{
    private Sprite _sprite = null!;
    public AxisAlignedRectangle CollisionRect { get; private set; } = null!;

    private KeyboardInput2D _movement = null!;
    private PlaneData _planeData = null!;

    private float _fireCooldown;
    private const float MoveSpeed = 350f;
    private const float BulletSpeed = 700f;

    // Weapon power level (1-4)
    private int _weaponLevel = 1;
    public int WeaponLevel => _weaponLevel;

    // Charge shot system
    private float _chargeTimer;
    private const float ChargeTime = 1.5f;
    private bool _fireWasHeld;
    public bool IsFullyCharged => _chargeTimer >= ChargeTime;

    // Super attack (bombs)
    private int _bombStock = 2;
    public int BombStock => _bombStock;
    private const int MaxBombs = 3;

    // Muzzle flash
    private float _muzzleFlashTimer;

    // Invincibility after respawn
    private float _invincibilityTimer;
    private const float InvincibilityDuration = 2f;
    public bool IsInvincible => _invincibilityTimer > 0f;

    public event Action? SuperFired;

    public override void CustomInitialize()
    {
        _planeData = PlaneSelectScreen.SelectedPlane;

        var texture = Engine.ContentManager.Load<Texture2D>(_planeData.SpriteName);
        _sprite = new Sprite
        {
            Texture = texture,
            TextureScale = 2.5f,
        };
        Add(_sprite);

        CollisionRect = new AxisAlignedRectangle
        {
            Width = 8,
            Height = 8,
            Visible = false,
        };
        Add(CollisionRect);

        _movement = new KeyboardInput2D(
            Engine.InputManager.Keyboard,
            Keys.Left, Keys.Right, Keys.Up, Keys.Down);
    }

    public override void CustomActivity(FrameTime time)
    {
        HandleMovement();
        HandleFiring(time);
        HandleChargeVisual(time);
        HandleSuper();
        HandleInvincibility(time);
    }

    private void HandleMovement()
    {
        VelocityX = _movement.X * MoveSpeed;
        VelocityY = _movement.Y * MoveSpeed;

        var halfW = Engine.Camera.TargetWidth / 2f;
        var halfH = Engine.Camera.TargetHeight / 2f;
        X = Math.Clamp(X, -halfW + 20, halfW - 20);
        Y = Math.Clamp(Y, -halfH + 20, halfH - 20);
    }

    private void HandleFiring(FrameTime time)
    {
        _fireCooldown -= time.DeltaSeconds;

        var kb = Engine.InputManager.Keyboard;
        bool fireHeld = kb.IsKeyDown(Keys.Z) || kb.IsKeyDown(Keys.Space);

        if (fireHeld)
        {
            if (_fireCooldown <= 0f)
            {
                SpawnBullets();
                _fireCooldown = _planeData.FireRate;
            }
            _chargeTimer = MathF.Min(_chargeTimer + time.DeltaSeconds, ChargeTime);
        }
        else if (_fireWasHeld)
        {
            if (IsFullyCharged)
                FireChargeAttack();
            _chargeTimer = 0f;
        }

        _fireWasHeld = fireHeld;
    }

    private void SpawnBullets()
    {
        _muzzleFlashTimer = 0.05f;
        var factory = Engine.GetFactory<PlayerBullet>();
        var patterns = _planeData.GetShotPattern(_weaponLevel);

        foreach (var gun in patterns)
        {
            float offsetX = gun[0];
            float angleOffset = gun.Length > 1 ? gun[1] : 0f;

            var bullet = factory.Create();
            bullet.X = X + offsetX;
            bullet.Y = Y + 30f;
            bullet.VelocityX = angleOffset * BulletSpeed;
            bullet.VelocityY = BulletSpeed;
        }
    }

    private void FireChargeAttack()
    {
        var factory = Engine.GetFactory<ChargeProjectile>();

        switch (_planeData.Name)
        {
            case "P-38 Lightning": // Fork Lightning: converging streams
                for (int i = 0; i < 6; i++)
                {
                    var proj = factory.Create();
                    proj.X = X - 30f;
                    proj.Y = Y + 20f + i * 8f;
                    proj.VelocityX = 120f - i * 40f;
                    proj.VelocityY = 600f;

                    var proj2 = factory.Create();
                    proj2.X = X + 30f;
                    proj2.Y = Y + 20f + i * 8f;
                    proj2.VelocityX = -120f + i * 40f;
                    proj2.VelocityY = 600f;
                }
                break;

            case "Spitfire": // Piercing Lance: single powerful beam column
                for (int i = 0; i < 8; i++)
                {
                    var proj = factory.Create();
                    proj.X = X;
                    proj.Y = Y + 30f + i * 12f;
                    proj.VelocityY = 800f;
                }
                break;

            case "Mosquito": // Homing Salvo: spread of projectiles (no homing, but wide fan)
                for (int i = 0; i < 8; i++)
                {
                    var proj = factory.Create();
                    proj.X = X;
                    proj.Y = Y + 20f;
                    float angle = MathF.PI / 2f + (i - 3.5f) * 0.2f;
                    proj.VelocityX = MathF.Cos(angle) * 500f;
                    proj.VelocityY = MathF.Sin(angle) * 500f;
                }
                break;

            case "Zero": // Blade Wave: wide crescent
                for (int i = 0; i < 12; i++)
                {
                    var proj = factory.Create();
                    proj.X = X + (i - 5.5f) * 20f;
                    proj.Y = Y + 20f;
                    proj.VelocityX = (i - 5.5f) * 30f;
                    proj.VelocityY = 650f;
                }
                break;
        }
    }

    private void HandleChargeVisual(FrameTime time)
    {
        if (_muzzleFlashTimer > 0f)
        {
            _sprite.Color = new Color(1f, 1f, 0.7f);
            _muzzleFlashTimer -= time.DeltaSeconds;
            return;
        }

        if (_chargeTimer > 0f && _chargeTimer < ChargeTime)
        {
            float chargePercent = _chargeTimer / ChargeTime;
            float pulse = 0.7f + 0.3f * MathF.Sin(chargePercent * 20f);
            _sprite.Color = new Color(pulse, pulse, 1f);
        }
        else if (IsFullyCharged)
        {
            float flash = 0.8f + 0.2f * MathF.Sin((float)Environment.TickCount / 50f);
            _sprite.Color = new Color(flash, flash, 1f);
        }
        else
        {
            _sprite.Color = Color.White;
        }
    }

    private void HandleSuper()
    {
        var kb = Engine.InputManager.Keyboard;
        if (kb.WasKeyPressed(Keys.X) && _bombStock > 0)
        {
            _bombStock--;
            MakeInvincible();
            SuperFired?.Invoke();
        }
    }

    private void HandleInvincibility(FrameTime time)
    {
        if (_invincibilityTimer > 0f)
        {
            _invincibilityTimer -= time.DeltaSeconds;
            bool flashOn = ((int)(_invincibilityTimer / 0.08f) & 1) == 0;
            _sprite.Alpha = flashOn ? 1f : 0.3f;
        }
        else
        {
            _sprite.Alpha = 1f;
        }
    }

    public void MakeInvincible() => _invincibilityTimer = InvincibilityDuration;
    public void PowerUp() { if (_weaponLevel < 4) _weaponLevel++; }
    public void AddBomb() { if (_bombStock < MaxBombs) _bombStock++; }

    public void ResetPower()
    {
        _weaponLevel = 1;
        if (_bombStock > 0) _bombStock--;
    }

    public override void CustomDestroy()
    {
        _sprite.Destroy();
        CollisionRect.Destroy();
    }
}
