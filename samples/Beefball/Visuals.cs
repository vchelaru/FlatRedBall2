using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace Beefball;

/// <summary>Shape helpers shared by the entities and the arena: alpha tints and stacked-circle glow.</summary>
public static class Visuals
{
    /// <summary>Same RGB, alpha 0..1.</summary>
    public static Color WithAlpha(Color c, float alpha)
        => new(c.R, c.G, c.B, (int)Math.Clamp(alpha * 255f, 0f, 255f));

    /// <summary>
    /// Attaches the stacked translucent filled circles behind a glowing object. Not part of
    /// collision. Returns the circles (innermost first) so callers can fade or pulse them.
    /// </summary>
    public static List<Circle> AddGlow(Entity entity, BeefballConfig config, Color color, float radius, float z)
    {
        var circles = new List<Circle>();
        foreach (var layer in config.Glow.Layers)
        {
            var circle = new Circle
            {
                Radius = radius * layer.Scale,
                IsFilled = true,
                IsVisible = true,
                Color = WithAlpha(color, layer.Alpha * config.Glow.Strength),
                Z = z - circles.Count * 0.01f,
            };
            entity.Add(circle, isDefaultCollision: false);
            circles.Add(circle);
        }
        return circles;
    }
}
