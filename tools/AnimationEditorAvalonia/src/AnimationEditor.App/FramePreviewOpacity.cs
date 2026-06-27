using System;

namespace AnimationEditor.App;

/// <summary>
/// The editor preview's <em>reference</em> interpretation of a frame's per-frame <c>Alpha</c> as
/// opacity: the drawn sprite's alpha is the layer alpha (<c>1.0</c> for the live frame, <c>0.4</c>
/// for onion skin) scaled by <c>Alpha/255</c>. A <c>null</c> frame alpha means fully opaque.
/// Runtimes consuming the <c>.achx</c> apply alpha however they choose; this is only what the
/// preview shows. Independent of <c>ColorOperation</c> — straight transparency, not a tint.
/// </summary>
public static class FramePreviewOpacity
{
    /// <param name="frameAlpha">Per-frame alpha 0–255, or <c>null</c> for fully opaque.</param>
    /// <param name="layerAlpha">Base opacity of the layer being drawn (1.0 live frame, 0.4 onion).</param>
    public static byte Resolve(int? frameAlpha, float layerAlpha)
    {
        float a = (frameAlpha ?? 255) / 255f;
        return (byte)Math.Clamp((int)MathF.Round(255f * layerAlpha * a), 0, 255);
    }
}
