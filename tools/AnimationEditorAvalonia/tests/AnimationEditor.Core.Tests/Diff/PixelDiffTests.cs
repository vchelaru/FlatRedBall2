using AnimationEditor.Core.Diff;
using Xunit;

namespace AnimationEditor.Core.Tests.Diff;

public class PixelDiffTests
{
    // Builds a width×height image, transparent everywhere except the pixels set via edits.
    private static ImageData Image(int width, int height, params (int x, int y, byte r, byte g, byte b, byte a)[] edits)
    {
        var rgba = new byte[width * height * 4];
        foreach (var (x, y, r, g, b, a) in edits)
        {
            int i = (y * width + x) * 4;
            rgba[i] = r; rgba[i + 1] = g; rgba[i + 2] = b; rgba[i + 3] = a;
        }
        return new ImageData(width, height, rgba);
    }

    [Fact]
    public void Compute_AlphaOnlyChange_Detected()
    {
        // Same RGB, alpha 0 → 255: a new sprite frame drawn on a previously-transparent pixel.
        var before = Image(1, 1, (0, 0, 100, 100, 100, 0));
        var after = Image(1, 1, (0, 0, 100, 100, 100, 255));

        Assert.True(PixelDiff.Compute(before, after, tolerance: 0).IsChanged(0, 0));
    }

    [Fact]
    public void Compute_DeltaWithinTolerance_NotChanged()
    {
        // Per-channel delta of 5, tolerance 10 → absorbed as re-export noise.
        var before = Image(1, 1, (0, 0, 100, 100, 100, 255));
        var after = Image(1, 1, (0, 0, 105, 105, 105, 255));

        Assert.False(PixelDiff.Compute(before, after, tolerance: 10).HasAnyChange);
    }

    [Fact]
    public void Compute_IdenticalImages_NoChanges()
    {
        var before = Image(2, 2, (0, 0, 10, 20, 30, 255), (1, 1, 40, 50, 60, 255));
        var after = Image(2, 2, (0, 0, 10, 20, 30, 255), (1, 1, 40, 50, 60, 255));

        Assert.False(PixelDiff.Compute(before, after, tolerance: 0).HasAnyChange);
    }

    [Fact]
    public void Compute_LargerAfter_NewRowChanged()
    {
        // Sheet grew from 2×1 to 2×2; the new bottom row (opaque) reads as changed, the
        // unchanged top row does not.
        var before = Image(2, 1, (0, 0, 10, 10, 10, 255), (1, 0, 10, 10, 10, 255));
        var after = Image(2, 2, (0, 0, 10, 10, 10, 255), (1, 0, 10, 10, 10, 255),
                                (0, 1, 200, 200, 200, 255), (1, 1, 200, 200, 200, 255));

        var mask = PixelDiff.Compute(before, after, tolerance: 0);

        Assert.False(mask.IsChanged(0, 0));
        Assert.True(mask.IsChanged(0, 1));
        Assert.True(mask.IsChanged(1, 1));
    }

    [Fact]
    public void Compute_NullBefore_OpaqueContentChangedTransparentNot()
    {
        // Initial add (no prior revision): opaque pixels are new content; transparent pixels are not.
        var after = Image(2, 1, (0, 0, 255, 0, 0, 255));   // (1,0) left transparent

        var mask = PixelDiff.Compute(before: null, after, tolerance: 0);

        Assert.True(mask.IsChanged(0, 0));
        Assert.False(mask.IsChanged(1, 0));
    }

    [Fact]
    public void Compute_SinglePixelBeyondTolerance_MarksOnlyThatPixel()
    {
        var before = Image(2, 1, (0, 0, 0, 0, 0, 255), (1, 0, 0, 0, 0, 255));
        var after = Image(2, 1, (0, 0, 0, 0, 0, 255), (1, 0, 255, 255, 255, 255));

        var mask = PixelDiff.Compute(before, after, tolerance: 10);

        Assert.False(mask.IsChanged(0, 0));
        Assert.True(mask.IsChanged(1, 0));
    }
}
