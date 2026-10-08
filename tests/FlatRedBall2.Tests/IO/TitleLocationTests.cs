using System.IO;
using FlatRedBall2.IO;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.IO;

public class TitleLocationTests
{
    private const string BaseDirectory = "/Game.app/Contents/MacOS/";

    [Fact]
    public void Resolve_NotMacOS_ReturnsBaseDirectoryEvenWhenResourcesExists()
    {
        TitleLocation.Resolve(BaseDirectory, isMacOS: false, directoryExists: _ => true)
            .ShouldBe(BaseDirectory);
    }

    [Fact]
    public void Resolve_MacOSWithSiblingResources_ReturnsSiblingResources()
    {
        string expected = Path.Combine(BaseDirectory, "..", "Resources");

        TitleLocation.Resolve(BaseDirectory, isMacOS: true, directoryExists: d => d == expected)
            .ShouldBe(expected);
    }

    [Fact]
    public void Resolve_MacOSWithOnlyGrandparentResources_ReturnsGrandparentResources()
    {
        // The apphost can sit one folder deeper than Contents/MacOS, e.g. Contents/MacOS/bin.
        string expected = Path.Combine(BaseDirectory, "..", "..", "Resources");

        TitleLocation.Resolve(BaseDirectory, isMacOS: true, directoryExists: d => d == expected)
            .ShouldBe(expected);
    }

    [Fact]
    public void Resolve_MacOSWithNoResources_ReturnsBaseDirectory()
    {
        TitleLocation.Resolve(BaseDirectory, isMacOS: true, directoryExists: _ => false)
            .ShouldBe(BaseDirectory);
    }
}
