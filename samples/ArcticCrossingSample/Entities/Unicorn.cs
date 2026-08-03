using System;
using FlatRedBall2;
using FlatRedBall2.Collision;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ArcticCrossingSample.Entities;

public class Unicorn : Entity
{
    public AxisAlignedRectangle Body { get; private set; } = null!;
    public bool IsCollected { get; private set; }

    private float _floatPhase;
    private float _originY;
    private bool _initialized;
    private Circle _horn = null!;

    private static readonly XnaColor BodyColor = new(180, 140, 220, 255);
    private static readonly XnaColor HornColor = new(255, 215, 0, 255);
    private static readonly XnaColor ManeColor = new(255, 150, 200, 255);
    private static readonly XnaColor LegColor = new(160, 120, 200, 255);

    public override void CustomInitialize()
    {
        Body = new AxisAlignedRectangle
        {
            Width = 30f, Height = 24f, IsVisible = true, IsFilled = true, Color = BodyColor,
        };
        Add(Body);

        // Head
        var head = new AxisAlignedRectangle
        {
            Width = 16f, Height = 18f, IsVisible = true, IsFilled = true, Color = BodyColor, X = 16f, Y = 6f,
        };
        Add(head, isDefaultCollision: false);

        // Horn
        _horn = new Circle
        {
            Radius = 4f, IsVisible = true, IsFilled = true, Color = HornColor, X = 22f, Y = 20f,
        };
        Add(_horn, isDefaultCollision: false);

        // Mane
        var mane = new AxisAlignedRectangle
        {
            Width = 8f, Height = 16f, IsVisible = true, IsFilled = true, Color = ManeColor, X = -4f, Y = 10f,
        };
        Add(mane, isDefaultCollision: false);

        // Legs
        float[] legXs = [-10f, -4f, 4f, 10f];
        foreach (var lx in legXs)
        {
            var leg = new AxisAlignedRectangle
            {
                Width = 5f, Height = 10f, IsVisible = true, IsFilled = true, Color = LegColor, X = lx, Y = -17f,
            };
            Add(leg, isDefaultCollision: false);
        }

        // Eye
        var eye = new Circle
        {
            Radius = 2f, IsVisible = true, IsFilled = true, Color = new XnaColor(40, 20, 60, 255), X = 20f, Y = 10f,
        };
        Add(eye, isDefaultCollision: false);
    }

    public void InitPosition()
    {
        _originY = Y;
        _initialized = true;
        _floatPhase = Engine.Random.Between(0f, 360f);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (!_initialized || IsCollected) return;

        // Float up and down
        _floatPhase += 90f * time.DeltaSeconds;
        Y = _originY + MathF.Sin(_floatPhase * MathF.PI / 180f) * 6f;

        // Horn sparkle (color pulse)
        float pulse = (MathF.Sin(_floatPhase * 3f * MathF.PI / 180f) + 1f) / 2f;
        byte g = (byte)(180 + (int)(75 * pulse));
        _horn.Color = new XnaColor((byte)255, g, (byte)0, (byte)255);
    }

    public void Collect()
    {
        IsCollected = true;
        Body.IsVisible = false;
        Y = -9999f;
    }
}
