using System;
using FlatRedBall2;
using FlatRedBall2.Collision;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ArcticCrossingSample.Entities;

public class PolarBear : Entity
{
    public AxisAlignedRectangle Body { get; private set; } = null!;
    public string HintText { get; set; } = "";

    private float _bobPhase;
    private float _originY;
    private bool _initialized;

    private static readonly XnaColor FurColor = new(240, 240, 245, 255);
    private static readonly XnaColor SnoutColor = new(200, 200, 210, 255);
    private static readonly XnaColor EyeColor = new(20, 20, 20, 255);

    public override void CustomInitialize()
    {
        // Body (collision)
        Body = new AxisAlignedRectangle
        {
            Width = 50f, Height = 40f, IsVisible = true, IsFilled = true, Color = FurColor,
        };
        Add(Body);

        // Head
        var head = new AxisAlignedRectangle
        {
            Width = 30f, Height = 25f, IsVisible = true, IsFilled = true, Color = FurColor, Y = 32f,
        };
        Add(head, isDefaultCollision: false);

        // Ears
        var leftEar = new AxisAlignedRectangle
        {
            Width = 8f, Height = 8f, IsVisible = true, IsFilled = true, Color = FurColor, X = -12f, Y = 46f,
        };
        Add(leftEar, isDefaultCollision: false);

        var rightEar = new AxisAlignedRectangle
        {
            Width = 8f, Height = 8f, IsVisible = true, IsFilled = true, Color = FurColor, X = 12f, Y = 46f,
        };
        Add(rightEar, isDefaultCollision: false);

        // Snout
        var snout = new Circle
        {
            Radius = 6f, IsVisible = true, IsFilled = true, Color = SnoutColor, Y = 26f,
        };
        Add(snout, isDefaultCollision: false);

        // Eyes
        var leftEye = new Circle
        {
            Radius = 2f, IsVisible = true, IsFilled = true, Color = EyeColor, X = -8f, Y = 34f,
        };
        Add(leftEye, isDefaultCollision: false);

        var rightEye = new Circle
        {
            Radius = 2f, IsVisible = true, IsFilled = true, Color = EyeColor, X = 8f, Y = 34f,
        };
        Add(rightEye, isDefaultCollision: false);
    }

    public void InitPosition()
    {
        _originY = Y;
        _initialized = true;
    }

    public override void CustomActivity(FrameTime time)
    {
        if (!_initialized) return;
        _bobPhase += 60f * time.DeltaSeconds;
        Y = _originY + MathF.Sin(_bobPhase * MathF.PI / 180f) * 1.5f;
    }
}
