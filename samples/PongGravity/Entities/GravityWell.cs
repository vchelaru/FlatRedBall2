#nullable enable
using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace PongGravity.Entities;

public class GravityWell : Entity
{
    public Circle Circle { get; private set; } = null!;
    public Circle InfluenceCircle { get; private set; } = null!;
    public bool IsBlackHole { get; private set; }
    public GravityWell? Partner { get; set; }

    public const float VisualRadius = 30f;
    public const float InfluenceRadius = 345f; // matches MaxGravityDist in GameScreen

    private float _pulseTime;
    private byte _influenceR, _influenceG, _influenceB;
    private const float PeakAlpha = 0.15f; // low enough not to obscure gameplay

    public override void CustomInitialize()
    {
        Circle = new Circle { Radius = VisualRadius, IsVisible = true };
        Add(Circle, isDefaultCollision: false);

        InfluenceCircle = new Circle
        {
            Radius = InfluenceRadius,
            IsVisible = false,
        };
        Add(InfluenceCircle, isDefaultCollision: false);
    }

    public void SetupAsBlackHole()
    {
        IsBlackHole = true;
        Circle.Color = new Color((byte)60, (byte)0, (byte)80, (byte)230);
        SetDefaultCollision(Circle, true);
        _influenceR = 120; _influenceG = 0; _influenceB = 200;
    }

    public void SetupAsWhiteHole()
    {
        IsBlackHole = false;
        Circle.Color = new Color((byte)180, (byte)210, (byte)255, (byte)200);
        _influenceR = 100; _influenceG = 180; _influenceB = 255;
    }

    /// <summary>Called each frame by GameScreen to drive the pulse animation.</summary>
    public void SetInfluencing(bool active, float deltaSeconds = 0f)
    {
        if (!active)
        {
            InfluenceCircle.IsVisible = false;
            return;
        }

        _pulseTime += deltaSeconds * 3f;
        float alpha = MathF.Abs(MathF.Sin(_pulseTime)) * PeakAlpha;
        InfluenceCircle.Color = new Color(_influenceR, _influenceG, _influenceB, (byte)(alpha * 255f));
        InfluenceCircle.IsVisible = true;
    }
}
