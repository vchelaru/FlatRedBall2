using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

public class FlatRedBallServiceFontOversamplingTests
{
    [Fact]
    public void ResolveUseFontOversampling_NullSettings_ReturnsFalse()
    {
        FlatRedBallService.ResolveUseFontOversampling(null).ShouldBeFalse();
    }

    [Fact]
    public void ResolveUseFontOversampling_DefaultSettings_ReturnsFalse()
    {
        FlatRedBallService.ResolveUseFontOversampling(new EngineInitSettings()).ShouldBeFalse();
    }

    [Fact]
    public void ResolveUseFontOversampling_OptedIn_ReturnsTrue()
    {
        var settings = new EngineInitSettings { UseFontOversampling = true };

        FlatRedBallService.ResolveUseFontOversampling(settings).ShouldBeTrue();
    }
}
