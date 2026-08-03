using System;
using FlatRedBall2;
using FlatRedBall2.Collision;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ArcticCrossingSample.Entities;

public enum PlatformType
{
    Static,
    Moving,
    Crumbling,
    Tilting,
    OneShot,
}

public class IcePlatform : Entity
{
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    // Configuration — set after Create(), before first frame
    public PlatformType Type { get; set; } = PlatformType.Static;

    // Moving platform config
    public float MoveRangeX { get; set; }
    public float MoveRangeY { get; set; }
    public float MoveSpeed { get; set; } = 60f;
    private float _movePhase;
    private float _originX;
    private float _originY;

    // Crumbling platform state
    public float CrumbleDelay { get; set; } = 2f;
    private float _crumbleTimer;
    private bool _playerOnThis;
    private bool _crumbling;
    private bool _destroyed;

    // Tilting platform state
    public float TiltSpeed { get; set; } = 1.5f;
    public float TiltRange { get; set; } = 15f;

    // OneShot — falls after player steps on it
    public float OneShotDelay { get; set; } = 0.5f;
    private float _oneShotTimer;
    private bool _oneShotTriggered;

    // Colors
    private static readonly XnaColor StaticColor = new(200, 220, 240, 255);
    private static readonly XnaColor MovingColor = new(150, 200, 240, 255);
    private static readonly XnaColor CrumbleColor = new(180, 210, 230, 255);
    private static readonly XnaColor CrumbleWarningColor = new(255, 180, 150, 255);
    private static readonly XnaColor TiltColor = new(170, 195, 235, 255);
    private static readonly XnaColor OneShotColor = new(220, 200, 180, 255);

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 100f,
            Height = 20f,
            IsVisible = true,
            Color = StaticColor,
        };
        Add(Rectangle);
    }

    /// <summary>
    /// Call after setting <see cref="Type"/> and position to finalize setup.
    /// Captures origin for movement/reset and applies the type-appropriate color.
    /// </summary>
    public void Initialize()
    {
        _originX = X;
        _originY = Y;
        Rectangle.Color = Type switch
        {
            PlatformType.Moving => MovingColor,
            PlatformType.Crumbling => CrumbleColor,
            PlatformType.Tilting => TiltColor,
            PlatformType.OneShot => OneShotColor,
            _ => StaticColor,
        };
    }

    /// <summary>
    /// Called by the gameplay screen when collision detects the player standing on this platform.
    /// </summary>
    public void NotifyPlayerOn()
    {
        _playerOnThis = true;
    }

    public override void CustomActivity(FrameTime time)
    {
        if (_destroyed) return;

        switch (Type)
        {
            case PlatformType.Moving:
                UpdateMoving(time);
                break;
            case PlatformType.Crumbling:
                UpdateCrumbling(time);
                break;
            case PlatformType.OneShot:
                UpdateOneShot(time);
                break;
        }

        // Reset per-frame flag
        _playerOnThis = false;
    }

    private void UpdateMoving(FrameTime time)
    {
        _movePhase += MoveSpeed * time.DeltaSeconds;
        float t = MathF.Sin(_movePhase * MathF.PI * 2f / 360f);
        X = _originX + MoveRangeX * t;
        Y = _originY + MoveRangeY * t;
    }

    private void UpdateCrumbling(FrameTime time)
    {
        if (_playerOnThis && !_crumbling)
        {
            _crumbleTimer += time.DeltaSeconds;

            // Visual warning — flash between normal and warning color
            float warningProgress = _crumbleTimer / CrumbleDelay;
            if (warningProgress > 0.5f)
            {
                bool flash = ((int)(_crumbleTimer * 8)) % 2 == 0;
                Rectangle.Color = flash ? CrumbleWarningColor : CrumbleColor;
            }

            if (_crumbleTimer >= CrumbleDelay)
            {
                _crumbling = true;
                _destroyed = true;
                Rectangle.IsVisible = false;
                // Move far offscreen so collision doesn't trigger
                Y = -9999f;
            }
        }
        else if (!_crumbling)
        {
            // Slowly reset timer when player is not on it
            _crumbleTimer = MathF.Max(0f, _crumbleTimer - time.DeltaSeconds * 0.5f);
            Rectangle.Color = CrumbleColor;
        }
    }

    private void UpdateOneShot(FrameTime time)
    {
        if (_playerOnThis && !_oneShotTriggered)
        {
            _oneShotTriggered = true;
            _oneShotTimer = OneShotDelay;
        }

        if (_oneShotTriggered)
        {
            _oneShotTimer -= time.DeltaSeconds;
            // Shake effect
            Rectangle.Color = ((int)(_oneShotTimer * 12)) % 2 == 0 ? OneShotColor : CrumbleWarningColor;

            if (_oneShotTimer <= 0f)
            {
                // Fall down
                VelocityY = -400f;
                AccelerationY = -600f;

                // Destroy when off screen
                if (Y < _originY - 1000f)
                {
                    _destroyed = true;
                    Rectangle.IsVisible = false;
                    Y = -9999f;
                    VelocityY = 0f;
                    AccelerationY = 0f;
                }
            }
        }
    }

    /// <summary>
    /// Resets the platform to its original state (for phase restart).
    /// </summary>
    public void Reset()
    {
        _destroyed = false;
        _crumbling = false;
        _crumbleTimer = 0f;
        _oneShotTriggered = false;
        _playerOnThis = false;
        X = _originX;
        Y = _originY;
        VelocityY = 0f;
        AccelerationY = 0f;
        Rectangle.IsVisible = true;
        Initialize();
    }
}
