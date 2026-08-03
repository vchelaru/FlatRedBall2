using System;
using FlatRedBall2;
using FlatRedBall2.Collision;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ArcticCrossingSample.Entities;

public class Seal : Entity
{
    public AxisAlignedRectangle Body { get; private set; } = null!;

    public float PopUpInterval { get; set; } = 5f;
    public float SitDuration { get; set; } = 3f;

    private float _originY;
    private float _hideY;
    private float _timer;
    private bool _isUp;
    private bool _isTransitioning;
    private float _transitionProgress;
    private bool _initialized;

    private static readonly XnaColor BodyColor = new(140, 140, 160, 255);
    private static readonly XnaColor NoseColor = new(40, 40, 50, 255);

    public override void CustomInitialize()
    {
        Body = new AxisAlignedRectangle
        {
            Width = 36f, Height = 18f, IsVisible = true, IsFilled = true, Color = BodyColor,
        };
        Add(Body);

        // Head
        var head = new Circle
        {
            Radius = 10f, IsVisible = true, IsFilled = true, Color = BodyColor, X = 16f, Y = 4f,
        };
        Add(head, isDefaultCollision: false);

        // Nose
        var nose = new Circle
        {
            Radius = 2f, IsVisible = true, IsFilled = true, Color = NoseColor, X = 24f, Y = 6f,
        };
        Add(nose, isDefaultCollision: false);

        // Eye
        var eye = new Circle
        {
            Radius = 1.5f, IsVisible = true, IsFilled = true, Color = NoseColor, X = 14f, Y = 8f,
        };
        Add(eye, isDefaultCollision: false);

        // Flippers
        var leftFlipper = new AxisAlignedRectangle
        {
            Width = 10f, Height = 4f, IsVisible = true, IsFilled = true, Color = BodyColor, X = -14f, Y = -6f,
        };
        Add(leftFlipper, isDefaultCollision: false);

        var rightFlipper = new AxisAlignedRectangle
        {
            Width = 10f, Height = 4f, IsVisible = true, IsFilled = true, Color = BodyColor, X = 14f, Y = -6f,
        };
        Add(rightFlipper, isDefaultCollision: false);
    }

    public void InitPosition()
    {
        _originY = Y;
        _hideY = Y - 60f;
        Y = _hideY;
        _initialized = true;
        _timer = Engine.Random.Between(1f, PopUpInterval);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (!_initialized) return;

        _timer -= time.DeltaSeconds;

        if (_isTransitioning)
        {
            _transitionProgress += time.DeltaSeconds * 3f;
            if (_transitionProgress >= 1f)
            {
                _transitionProgress = 1f;
                _isTransitioning = false;
                _timer = _isUp ? SitDuration : PopUpInterval;
            }

            float t = _transitionProgress;
            Y = _isUp
                ? _hideY + (_originY - _hideY) * t
                : _originY + (_hideY - _originY) * t;
            return;
        }

        if (_timer <= 0f)
        {
            _isUp = !_isUp;
            _isTransitioning = true;
            _transitionProgress = 0f;
        }
    }
}
