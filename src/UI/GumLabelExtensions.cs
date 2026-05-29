using System;
using Gum.Forms.Controls;
using MonoGameGum.GueDeriving;

namespace FlatRedBall2.UI;

/// <summary>
/// Runtime text styling helpers for Gum <see cref="Label"/> controls.
/// </summary>
public static class GumLabelExtensions
{
    /// <summary>
    /// Sets the backing <see cref="TextRuntime.FontSize"/> for a Forms
    /// <see cref="Label"/> in pixels.
    /// </summary>
    public static void SetFontSize(this Label label, int fontSize)
    {
        if (fontSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(fontSize), "Font size must be greater than zero.");

        GetTextRuntime(label).FontSize = fontSize;
    }

    /// <summary>
    /// Sets label opacity in the unit range [0..1], mapped to
    /// <see cref="TextRuntime.Alpha"/> [0..255].
    /// </summary>
    public static void SetOpacity(this Label label, float opacity)
    {
        if (float.IsNaN(opacity) || float.IsInfinity(opacity) || opacity < 0f || opacity > 1f)
            throw new ArgumentOutOfRangeException(nameof(opacity), "Opacity must be in the range [0..1].");

        GetTextRuntime(label).Alpha = (int)MathF.Round(opacity * 255f);
    }

    private static TextRuntime GetTextRuntime(Label label)
    {
        ArgumentNullException.ThrowIfNull(label);

        if (label.Visual is TextRuntime text)
            return text;

        throw new InvalidOperationException(
            $"Label visual is {label.Visual?.GetType().Name ?? "null"}; expected {nameof(TextRuntime)}.");
    }
}
