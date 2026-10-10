using AnimationEditor.Core.Utilities;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class PluralTests
{
    [Theory]
    [InlineData(0, "0 frames")]
    [InlineData(1, "1 frame")]
    [InlineData(2, "2 frames")]
    public void Format_PluralizesByCount(int count, string expected) =>
        Assert.Equal(expected, Plural.Format(count, "frame"));

    [Fact]
    public void Noun_UsesExplicitPlural_WhenGiven()
    {
        Assert.Equal("Properties", Plural.Noun(2, "Property", "Properties"));
        Assert.Equal("Property", Plural.Noun(1, "Property", "Properties"));
    }
}
