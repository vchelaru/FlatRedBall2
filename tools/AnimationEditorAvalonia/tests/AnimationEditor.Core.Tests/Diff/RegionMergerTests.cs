using AnimationEditor.Core.Diff;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Diff;

public class RegionMergerTests
{
    // Builds a mask over a width×height grid with the given changed pixels.
    private static ChangeMask Mask(int width, int height, params (int x, int y)[] changed)
    {
        var flags = new bool[width * height];
        foreach (var (x, y) in changed)
            flags[y * width + x] = true;
        return new ChangeMask(width, height, flags);
    }

    [Fact]
    public void Merge_NoChanges_ReturnsEmpty()
    {
        Assert.Empty(RegionMerger.Merge(Mask(4, 4), distanceThreshold: 8));
    }

    [Fact]
    public void Merge_TwoFarClusters_ProducesTwoRegions()
    {
        // Two changed pixels 20 apart on a wide sheet, threshold 4 → they stay separate.
        var mask = Mask(30, 1, (2, 0), (22, 0));

        var regions = RegionMerger.Merge(mask, distanceThreshold: 4);

        Assert.Equal(2, regions.Count);
    }

    [Fact]
    public void Merge_TwoNearPixels_MergeIntoOneTightBox()
    {
        // Two changed pixels 2 apart, threshold 8 → one region whose box hugs both pixels.
        var mask = Mask(10, 10, (3, 3), (5, 3));

        var regions = RegionMerger.Merge(mask, distanceThreshold: 8);

        Assert.Single(regions);
        var r = regions[0];
        Assert.Equal(3, r.MinX);
        Assert.Equal(5, r.MaxX);
        Assert.Equal(3, r.MinY);
        Assert.Equal(3, r.MaxY);
        Assert.Equal(2, r.ChangedPixelCount);
    }

    [Fact]
    public void Merge_TwoClusters_LargestReturnedFirst()
    {
        // Left cluster has 3 changed pixels, right cluster has 1; largest comes first.
        var mask = Mask(40, 1, (0, 0), (1, 0), (2, 0), (30, 0));

        var regions = RegionMerger.Merge(mask, distanceThreshold: 4);

        Assert.Equal(2, regions.Count);
        Assert.Equal(3, regions[0].ChangedPixelCount);
        Assert.Equal(1, regions[1].ChangedPixelCount);
    }
}
