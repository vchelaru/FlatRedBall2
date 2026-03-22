using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace ZeldaRoomsSample.Entities;

public class Enemy : Entity
{
    private const float MoveSpeed = 80f;
    private const float MoveDistance = 96f;   // units to travel before picking new direction
    private const float PauseDuration = 0.6f;

    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    public int HitsRemaining { get; private set; } = 3;

    private float _travelRemaining = 0f;
    private float _pauseTimer = 0f;
    private bool _isPaused = false;

    // Flash feedback on hit
    private float _flashTimer = 0f;
    private readonly Color _normalColor = new Color(220, 60, 60);
    private readonly Color _hitColor = Color.White;

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

        PickNewDirection();
    }

    public void Hit()
    {
        HitsRemaining--;
        _flashTimer = 0.15f;
        Rectangle.Color = _hitColor;

        if (HitsRemaining <= 0)
            Destroy();
    }

    public void SetMovementEnabled(bool enabled)
    {
        if (!enabled)
        {
            VelocityX = 0f;
            VelocityY = 0f;
        }
        else if (!_isPaused)
        {
            ApplyCurrentVelocity();
        }
    }

    public override void CustomActivity(FrameTime time)
    {
        // Flash feedback
        if (_flashTimer > 0f)
        {
            _flashTimer -= time.DeltaSeconds;
            if (_flashTimer <= 0f)
                Rectangle.Color = _normalColor;
        }

        // Pause between moves
        if (_isPaused)
        {
            _pauseTimer -= time.DeltaSeconds;
            if (_pauseTimer <= 0f)
            {
                _isPaused = false;
                PickNewDirection();
            }
            return;
        }

        // Travel
        float step = MoveSpeed * time.DeltaSeconds;
        _travelRemaining -= step;
        if (_travelRemaining <= 0f)
        {
            VelocityX = 0f;
            VelocityY = 0f;
            _isPaused = true;
            _pauseTimer = PauseDuration;
        }
    }

    private void PickNewDirection()
    {
        _travelRemaining = MoveDistance;
        int dir = Engine.Random.Next(4);
        VelocityX = 0f;
        VelocityY = 0f;
        switch (dir)
        {
            case 0: VelocityX =  MoveSpeed; break;
            case 1: VelocityX = -MoveSpeed; break;
            case 2: VelocityY =  MoveSpeed; break;
            case 3: VelocityY = -MoveSpeed; break;
        }
    }

    private void ApplyCurrentVelocity()
    {
        // Re-apply whatever direction we were last moving
        PickNewDirection();
    }
}
