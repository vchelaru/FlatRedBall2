using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.UI;

// Gum bleeds neighbor color into transparent texels on every texture it loads, to stop dark fringes under
// Linear filtering. Under Point filtering it changes nothing and costs a full GetData/SetData per texture.
public class GumTextureBleedTests
{
    [Theory]
    [InlineData("Linear", true)]
    [InlineData("linear", true)]
    [InlineData("Point", false)]
    [InlineData(null, false)]
    public void ShouldBleedTransparentTextureEdges_ByProjectTextureFilter(string? filter, bool expected)
    {
        FlatRedBallService.ShouldBleedTransparentTextureEdges(filter).ShouldBe(expected);
    }
}
