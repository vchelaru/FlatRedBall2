using FlatRedBall2;
using Gum.GueDeriving;
using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;

namespace Beefball;

/// <summary>
/// Color helpers and the Gum gradient glows. Every glow is one smooth gradient shape; stacking
/// translucent engine shapes shows concentric rings.
/// </summary>
public static class Visuals
{
    /// <summary>Same RGB, alpha 0..1.</summary>
    public static Color WithAlpha(Color c, float alpha)
        => new(c.R, c.G, c.B, (int)Math.Clamp(alpha * 255f, 0f, 255f));

    /// <summary>Wall color along the table: team 1 on the left blending to team 2 on the right.</summary>
    public static Color WallColorAt(PaletteConfig palette, float x)
    {
        float t = Math.Clamp((x + Arena.HalfWidth) / (2f * Arena.HalfWidth), 0f, 1f);
        return Color.Lerp(palette.Team1, palette.Team2, t);
    }

    /// <summary>
    /// A soft round glow: alpha at innerRadius easing to nothing at outerRadius. Centered on its
    /// origin; the caller adds it to an entity.
    /// </summary>
    public static CircleRuntime CreateRadialGlow(Color color, float alpha, float innerRadius, float outerRadius)
    {
        var circle = new CircleRuntime
        {
            Width = outerRadius * 2f,
            Height = outerRadius * 2f,
            XOrigin = HorizontalAlignment.Center,
            YOrigin = VerticalAlignment.Center,
            IsFilled = true,
            StrokeWidth = 0f,
            UseGradient = true,
            GradientType = GradientType.Radial,
            // Radial gradients start at the shape top-left unless told otherwise.
            GradientX1 = outerRadius,
            GradientY1 = outerRadius,
            GradientInnerRadius = innerRadius,
            GradientOuterRadius = outerRadius,
        };
        SetGlowColor(circle, color, alpha);
        return circle;
    }

    public static void SetGlowColor(CircleRuntime glow, Color color, float alpha)
    {
        glow.Visible = alpha > 0.001f;
        glow.FillColor = WithAlpha(color, alpha);
        glow.Color2 = WithAlpha(color, 0f);
    }

    /// <summary>
    /// A strip that is alpha along one long edge and fades to nothing across depth. Horizontal strips
    /// run along X. side is +1 when the bright edge is the world-space bottom (horizontal) or left
    /// (vertical) edge, so the strip lies above or right of its wall; -1 for the opposite side.
    /// Centered on its origin.
    /// </summary>
    public static RectangleRuntime CreateLinearGlow(Color color, float alpha, float length, float depth, bool horizontal, int side)
    {
        float width = horizontal ? length : depth;
        float height = horizontal ? depth : length;
        var rect = new RectangleRuntime
        {
            Width = width,
            Height = height,
            XOrigin = HorizontalAlignment.Center,
            YOrigin = VerticalAlignment.Center,
            IsFilled = true,
            StrokeWidth = 0f,
            UseGradient = true,
            GradientType = GradientType.Linear,
        };
        // Gradient coordinates are pixels from the strip top-left, Y down. Start = bright edge.
        if (horizontal)
        {
            rect.GradientX1 = 0f; rect.GradientX2 = 0f;
            rect.GradientY1 = side > 0 ? depth : 0f;
            rect.GradientY2 = side > 0 ? 0f : depth;
        }
        else
        {
            rect.GradientY1 = 0f; rect.GradientY2 = 0f;
            rect.GradientX1 = side > 0 ? 0f : depth;
            rect.GradientX2 = side > 0 ? depth : 0f;
        }
        SetGlowColor(rect, color, alpha);
        return rect;
    }

    /// <summary>
    /// A depth-by-depth square that is alpha at one corner and fades radially to nothing at distance
    /// depth: the quarter-disc that fills the empty quadrant at a wall corner. quadrantX and quadrantY
    /// (+1 or -1, world space, Y up) say which way the square extends from the corner point.
    /// </summary>
    public static RectangleRuntime CreateCornerGlow(Color color, float alpha, float depth, int quadrantX, int quadrantY)
    {
        var rect = new RectangleRuntime
        {
            Width = depth,
            Height = depth,
            XOrigin = HorizontalAlignment.Center,
            YOrigin = VerticalAlignment.Center,
            IsFilled = true,
            StrokeWidth = 0f,
            UseGradient = true,
            GradientType = GradientType.Radial,
            // The corner point is the square corner farthest from where it extends (Gum Y is down).
            GradientX1 = quadrantX > 0 ? 0f : depth,
            GradientY1 = quadrantY > 0 ? depth : 0f,
            GradientInnerRadius = 0f,
            GradientOuterRadius = depth,
        };
        SetGlowColor(rect, color, alpha);
        return rect;
    }

    public static void SetGlowColor(RectangleRuntime glow, Color color, float alpha)
    {
        glow.Visible = alpha > 0.001f;
        glow.FillColor = WithAlpha(color, alpha);
        glow.Color2 = WithAlpha(color, 0f);
    }

    /// <summary>Adds a radial glow to an entity. Call before adding the entity shapes so the glow draws first.</summary>
    public static CircleRuntime AddRadialGlow(Entity entity, RadialGlowConfig config, float strength, Color color, float bodyRadius)
    {
        var glow = CreateRadialGlow(color, config.Alpha * strength, bodyRadius, bodyRadius * config.RadiusScale);
        entity.Add(glow);
        return glow;
    }
}
