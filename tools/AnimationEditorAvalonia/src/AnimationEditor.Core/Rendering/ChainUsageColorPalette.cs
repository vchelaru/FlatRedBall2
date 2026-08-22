using System;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Deterministic, visually-distinct color per index — used by the PNG usage-overlay (issue #953)
/// to give each matching animation chain its own outline/fill color. Steps hue by the golden angle
/// (~137.5°) so consecutive indices land far apart on the color wheel regardless of how many
/// chains are found, avoiding the visual clustering a plain <c>360/N</c> even split would produce
/// for small N.
/// </summary>
public static class ChainUsageColorPalette
{
    private const float GoldenAngleDegrees = 137.508f;

    /// <summary>Saturation/value are fixed (vivid, not too dark/light) — only hue varies by index.</summary>
    public static (byte R, byte G, byte B) GetColor(int index)
    {
        float hue = (index * GoldenAngleDegrees) % 360f;
        return HsvToRgb(hue, saturation: 0.65f, value: 0.95f);
    }

    private static (byte R, byte G, byte B) HsvToRgb(float hue, float saturation, float value)
    {
        float c = value * saturation;
        float x = c * (1 - MathF.Abs(hue / 60f % 2 - 1));
        float m = value - c;

        (float r, float g, float b) = hue switch
        {
            < 60f => (c, x, 0f),
            < 120f => (x, c, 0f),
            < 180f => (0f, c, x),
            < 240f => (0f, x, c),
            < 300f => (x, 0f, c),
            _ => (c, 0f, x),
        };

        return ((byte)((r + m) * 255f), (byte)((g + m) * 255f), (byte)((b + m) * 255f));
    }
}
