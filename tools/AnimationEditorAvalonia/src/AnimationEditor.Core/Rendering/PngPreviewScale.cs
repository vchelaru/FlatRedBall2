using System;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Pure fit-to-window math for the PNG viewer tab (issue #604). Kept out of the Avalonia control
/// so the zoom rule is unit-testable.
/// </summary>
public static class PngPreviewScale
{
    /// <summary>
    /// The zoom applied when a PNG first opens: shrink a too-large image to fit the viewport,
    /// but never upscale a small image (it shows at 100% centred). Returns <c>1.0</c> for any
    /// non-positive dimension so a not-yet-measured control cannot produce NaN.
    /// </summary>
    public static double ComputeInitialScale(double imageWidth, double imageHeight,
        double viewportWidth, double viewportHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0 || viewportWidth <= 0 || viewportHeight <= 0)
            return 1.0;

        double fit = Math.Min(viewportWidth / imageWidth, viewportHeight / imageHeight);
        return Math.Min(fit, 1.0);
    }
}
