using Gum.GueDeriving;
using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;

namespace Beefball;

/// <summary>Code-only Gum text helpers. Coordinates are canvas pixels from the top-left (Y down).</summary>
public static class UiText
{
    public static TextRuntime Make(string text, int fontSize, Color color, float x, float y,
        HorizontalAlignment xOrigin = HorizontalAlignment.Center, bool bold = false)
    {
        var t = new TextRuntime
        {
            Text = text,
            IsBold = bold,
            FontSize = fontSize,
            Color = color,
            XOrigin = xOrigin,
            YOrigin = VerticalAlignment.Center,
            X = x,
            Y = y,
        };
        return t;
    }

    /// <summary>"TEAM" becomes "T E A M": a cheap letterspaced look without font tooling.</summary>
    public static string Spaced(string text) => string.Join(' ', text.ToCharArray());
}
