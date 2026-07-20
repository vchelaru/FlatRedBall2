using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class BinObjPathFilterTests
{
    [Fact]
    public void IsExcluded_BinSegmentAnyCase_ReturnsTrue()
    {
        Assert.True(BinObjPathFilter.IsExcluded("Sprites/BIN/hero.achx"));
    }

    [Fact]
    public void IsExcluded_NoBinOrObjSegment_ReturnsFalse()
    {
        Assert.False(BinObjPathFilter.IsExcluded("Sprites/Enemies/goblin.achx"));
    }

    [Fact]
    public void IsExcluded_ObjSegment_ReturnsTrue()
    {
        Assert.True(BinObjPathFilter.IsExcluded("obj/Debug/hero.achx"));
    }

    [Fact]
    public void IsExcluded_SegmentContainingButNotEqualToBin_ReturnsFalse()
    {
        // "Cabin" contains "bin" as a substring but is not itself a "bin" segment.
        Assert.False(BinObjPathFilter.IsExcluded("Cabin/hero.achx"));
    }
}
