using FlatRedBall2.Animation;
using SkiaSharp;

namespace AnimationEditor.App;

/// <summary>
/// Maps a frame's <see cref="ColorOperation"/> + per-channel R/G/B to the SkiaSharp blend the preview
/// applies. This is the editor's <em>reference</em> interpretation of the operation — runtimes that
/// consume the same <c>.achx</c> render it however they choose (see the animation-editor skill).
/// </summary>
public static class FrameColorFilter
{
    /// <summary>
    /// Returns the (blend mode, color) to build an <c>SKColorFilter</c> from, or <c>null</c> when
    /// <paramref name="operation"/> is <c>null</c> (no color effect). Unset channels default to each
    /// operation's identity — 255 for Multiply (×1), 0 for Add (+0) — so a partially-authored color
    /// only affects the channels the artist set. Add uses alpha 0 so a flash never forces opacity.
    /// </summary>
    public static (SKBlendMode Mode, SKColor Color)? Resolve(ColorOperation? operation, int? r, int? g, int? b)
    {
        switch (operation)
        {
            case ColorOperation.Multiply:
                return (SKBlendMode.Modulate, new SKColor((byte)(r ?? 255), (byte)(g ?? 255), (byte)(b ?? 255), 255));
            case ColorOperation.Add:
                return (SKBlendMode.Plus, new SKColor((byte)(r ?? 0), (byte)(g ?? 0), (byte)(b ?? 0), 0));
            default:
                return null;
        }
    }
}
