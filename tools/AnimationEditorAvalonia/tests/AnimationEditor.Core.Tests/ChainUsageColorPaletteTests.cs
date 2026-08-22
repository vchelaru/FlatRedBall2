using AnimationEditor.Core.Rendering;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ChainUsageColorPaletteTests
{
    [Fact]
    public void GetColor_EightConsecutiveIndices_AreAllDistinct()
    {
        var colors = Enumerable.Range(0, 8).Select(ChainUsageColorPalette.GetColor).ToList();

        Assert.Equal(colors.Count, colors.Distinct().Count());
    }

    [Fact]
    public void GetColor_SameIndex_IsDeterministic()
    {
        var first = ChainUsageColorPalette.GetColor(3);
        var second = ChainUsageColorPalette.GetColor(3);

        Assert.Equal(first, second);
    }
}
