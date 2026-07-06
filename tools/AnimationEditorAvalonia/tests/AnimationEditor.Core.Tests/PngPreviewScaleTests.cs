using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="PngPreviewScale"/> — the initial fit-to-window zoom for the PNG viewer tab (issue #604).
/// </summary>
public class PngPreviewScaleTests
{
    [Fact]
    public void ComputeInitialScale_ImageLargerThanViewport_ShrinksToFit()
    {
        // 1000×1000 image in a 500×500 viewport must scale to 0.5 so it fits.
        var scale = PngPreviewScale.ComputeInitialScale(1000, 1000, 500, 500);

        Assert.Equal(0.5, scale);
    }

    [Fact]
    public void ComputeInitialScale_ImageSmallerThanViewport_DoesNotUpscale()
    {
        // A 100×100 image in a 500×500 viewport shows at 100% (1.0), not blown up to fill.
        var scale = PngPreviewScale.ComputeInitialScale(100, 100, 500, 500);

        Assert.Equal(1.0, scale);
    }

    [Fact]
    public void ComputeInitialScale_WideImage_UsesLimitingDimension()
    {
        // 1000×250 in 500×500: width is the constraint (500/1000 = 0.5), not height.
        var scale = PngPreviewScale.ComputeInitialScale(1000, 250, 500, 500);

        Assert.Equal(0.5, scale);
    }

    [Fact]
    public void ComputeInitialScale_ZeroViewport_ReturnsOne()
    {
        // Guard against a divide-by-zero / NaN before the control has been measured.
        var scale = PngPreviewScale.ComputeInitialScale(1000, 1000, 0, 0);

        Assert.Equal(1.0, scale);
    }
}
