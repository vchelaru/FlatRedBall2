using System;
using FlatRedBall2;
using FlatRedBall2.Collision;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ArcticCrossingSample.Entities;

public class Collectible : Entity
{
    public Circle Circle { get; private set; } = null!;

    public int PointValue { get; set; } = 100;
    private float _bobPhase;
    private float _originY;
    private bool _initialized;

    private static readonly XnaColor DiamondColor = new(255, 255, 100, 255);

    public override void CustomInitialize()
    {
        Circle = new Circle
        {
            Radius = 8f,
            IsVisible = true,
            Color = DiamondColor,
        };
        Add(Circle);
    }

    /// <summary>
    /// Captures the current Y as the bob origin. Call after positioning the entity.
    /// </summary>
    public void InitPosition()
    {
        _originY = Y;
        _initialized = true;
        _bobPhase = Engine.Random.Between(0f, 360f);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (!_initialized) return;
        // Gentle bobbing animation
        _bobPhase += 120f * time.DeltaSeconds;
        Y = _originY + MathF.Sin(_bobPhase * MathF.PI / 180f) * 4f;
    }

    public void Collect()
    {
        Circle.IsVisible = false;
        Y = -9999f;
    }
}
