using System;
using FlatRedBall2;
using FlatRedBall2.Collision;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ArcticCrossingSample.Entities;

public class Penguin : Entity
{
    public AxisAlignedRectangle Body { get; private set; } = null!;

    public float WaddleRange { get; set; } = 60f;
    public bool CanBellySlide { get; set; }

    private float _originX;
    private float _waddleDir = 1f;
    private float _waddleSpeed = 30f;
    private float _bellySlideTimer;
    private float _bellySlideInterval = 8f;
    private bool _isBellySliding;
    private float _bellySlideSpeed = 250f;
    private float _bellySlideRemaining;
    private bool _initialized;

    private static readonly XnaColor BodyColor = new(30, 30, 40, 255);
    private static readonly XnaColor BellyColor = new(240, 240, 250, 255);
    private static readonly XnaColor BeakColor = new(255, 160, 40, 255);
    private static readonly XnaColor EyeColor = new(255, 255, 255, 255);
    private static readonly XnaColor FeetColor = new(255, 160, 40, 255);

    public override void CustomInitialize()
    {
        Body = new AxisAlignedRectangle
        {
            Width = 20f, Height = 28f, IsVisible = true, IsFilled = true, Color = BodyColor,
        };
        Add(Body);

        // Belly
        var belly = new AxisAlignedRectangle
        {
            Width = 14f, Height = 20f, IsVisible = true, IsFilled = true, Color = BellyColor, Y = -2f,
        };
        Add(belly, isDefaultCollision: false);

        // Beak (circle approximation)
        var beak = new Circle
        {
            Radius = 3f, IsVisible = true, IsFilled = true, Color = BeakColor, X = 6f, Y = 10f,
        };
        Add(beak, isDefaultCollision: false);

        // Eye
        var eye = new Circle
        {
            Radius = 2f, IsVisible = true, IsFilled = true, Color = EyeColor, X = -2f, Y = 12f,
        };
        Add(eye, isDefaultCollision: false);

        // Feet
        var leftFoot = new AxisAlignedRectangle
        {
            Width = 6f, Height = 4f, IsVisible = true, IsFilled = true, Color = FeetColor, X = -5f, Y = -16f,
        };
        Add(leftFoot, isDefaultCollision: false);

        var rightFoot = new AxisAlignedRectangle
        {
            Width = 6f, Height = 4f, IsVisible = true, IsFilled = true, Color = FeetColor, X = 5f, Y = -16f,
        };
        Add(rightFoot, isDefaultCollision: false);
    }

    public void InitPosition()
    {
        _originX = X;
        _initialized = true;
        _bellySlideTimer = Engine.Random.Between(3f, _bellySlideInterval);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (!_initialized) return;

        if (_isBellySliding)
        {
            _bellySlideRemaining -= time.DeltaSeconds;
            VelocityX = _bellySlideSpeed * _waddleDir;
            if (_bellySlideRemaining <= 0f)
            {
                _isBellySliding = false;
                VelocityX = 0f;
                X = _originX;
            }
            return;
        }

        // Waddle back and forth
        X += _waddleDir * _waddleSpeed * time.DeltaSeconds;
        if (X > _originX + WaddleRange / 2f)
            _waddleDir = -1f;
        else if (X < _originX - WaddleRange / 2f)
            _waddleDir = 1f;

        // Belly slide timer
        if (CanBellySlide)
        {
            _bellySlideTimer -= time.DeltaSeconds;
            if (_bellySlideTimer <= 0f)
            {
                _isBellySliding = true;
                _bellySlideRemaining = 0.6f;
                _bellySlideTimer = _bellySlideInterval;
            }
        }
    }
}
