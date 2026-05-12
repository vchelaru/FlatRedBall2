using System;
using Microsoft.Xna.Framework;

namespace FlatRedBall2.Rendering;

/// <summary>
/// Color construction helpers that aren't on MonoGame's <see cref="Color"/> directly.
/// </summary>
public static class Colors
{
    /// <summary>
    /// Builds a <see cref="Color"/> from HSV components. Hue is in degrees and wraps
    /// (negative and &gt;= 360 values are normalized into [0, 360)); saturation and value
    /// are in [0, 1] and are clamped. Alpha is 255.
    /// </summary>
    /// <remarks>
    /// Useful for picking vivid varied colors — sweep <paramref name="hue"/> with
    /// <c>s = 1, v = 1</c> for fully saturated rainbow output, drop <paramref name="value"/>
    /// for darker tones, drop <paramref name="saturation"/> for pastels.
    /// </remarks>
    public static Color FromHsv(float hue, float saturation, float value)
    {
        hue = ((hue % 360f) + 360f) % 360f;
        saturation = System.Math.Clamp(saturation, 0f, 1f);
        value = System.Math.Clamp(value, 0f, 1f);

        float c = value * saturation;
        float h = hue / 60f;
        float x = c * (1f - System.Math.Abs((h % 2f) - 1f));
        float m = value - c;

        float r, g, b;
        if (h < 1f)      { r = c; g = x; b = 0f; }
        else if (h < 2f) { r = x; g = c; b = 0f; }
        else if (h < 3f) { r = 0f; g = c; b = x; }
        else if (h < 4f) { r = 0f; g = x; b = c; }
        else if (h < 5f) { r = x; g = 0f; b = c; }
        else             { r = c; g = 0f; b = x; }

        return new Color(
            (byte)System.Math.Round((r + m) * 255f),
            (byte)System.Math.Round((g + m) * 255f),
            (byte)System.Math.Round((b + m) * 255f),
            (byte)255);
    }

    /// <summary>
    /// Converts a <see cref="Color"/> to its HSV representation. Hue is in degrees [0, 360);
    /// saturation and value are in [0, 1]. Alpha is not represented in HSV — read it from
    /// <see cref="Color.A"/> separately if needed.
    /// </summary>
    /// <remarks>
    /// Inverse of <see cref="FromHsv"/> up to byte rounding (round-tripping a color may shift
    /// each channel by at most one byte). Achromatic colors (R == G == B) report a hue of 0.
    /// </remarks>
    public static (float hue, float saturation, float value) ToHsv(this Color color)
    {
        float r = color.R / 255f;
        float g = color.G / 255f;
        float b = color.B / 255f;

        float max = System.Math.Max(r, System.Math.Max(g, b));
        float min = System.Math.Min(r, System.Math.Min(g, b));
        float delta = max - min;

        float hue;
        if (delta == 0f)        hue = 0f;
        else if (max == r)      hue = 60f * (((g - b) / delta) % 6f);
        else if (max == g)      hue = 60f * (((b - r) / delta) + 2f);
        else                    hue = 60f * (((r - g) / delta) + 4f);
        if (hue < 0f) hue += 360f;

        float saturation = max == 0f ? 0f : delta / max;
        return (hue, saturation, max);
    }
}
