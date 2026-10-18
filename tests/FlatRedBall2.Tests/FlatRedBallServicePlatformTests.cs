using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

// Platform decisions are made from what the host is (browser or not), never from which graphics
// backend the engine was compiled against.
public class FlatRedBallServicePlatformTests
{
    [Theory]
    [InlineData("/Content/a.gumx", "Content/a.gumx")]
    [InlineData("//Content\\a.gumx", "Content\\a.gumx")]
    [InlineData("Content/a.gumx", "Content/a.gumx")]
    public void ToTitleContainerPath_LeadingSeparators_AreStripped(string path, string expected)
    {
        FlatRedBallService.ToTitleContainerPath(path).ShouldBe(expected);
    }

    [Fact]
    public void ConfigureStorage_BrowserHostWithoutFactory_StaysInMemoryAndNotPersistent()
    {
        var engine = new FlatRedBallService { IsBrowserHost = true };

        engine.ConfigureStorage(new EngineInitSettings { StorageName = "my-game" });

        engine.Storage.IsPersistent.ShouldBeFalse();
    }
}
