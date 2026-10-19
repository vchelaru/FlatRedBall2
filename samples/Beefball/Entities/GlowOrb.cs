using FlatRedBall2;
using Microsoft.Xna.Framework;

namespace Beefball.Entities;

/// <summary>A standalone radial glow (title-screen decoration).</summary>
public class GlowOrb : Entity
{
    public Color GlowColor { get; set; }
    public float BodyRadius { get; set; }
    public RadialGlowConfig Config { get; set; } = null!;
    public float Strength { get; set; } = 1f;

    public override void CustomInitialize()
    {
        Z = -10f;
        Visuals.AddRadialGlow(this, Config, Strength, GlowColor, BodyRadius);
    }
}
