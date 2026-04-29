using System.IO;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Content;

public class SourceContentRootTests
{
    [Fact]
    public void SourceContentRoot_IsSettableForOverride()
    {
        var engine = new FlatRedBallService { SourceContentRoot = "C:/some/path" };
        engine.SourceContentRoot.ShouldBe("C:/some/path");
    }

    [Fact]
    public void SourceContentRoot_AutoDetect_FindsCsprojWalkingUpFromBaseDirectory()
    {
        // Arrange a fake project layout under the temp dir and walk up from bin/Debug/net10.0.
        var root = Path.Combine(Path.GetTempPath(), "frb2-srcroot-" + System.Guid.NewGuid().ToString("N"));
        var bin = Path.Combine(root, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(root, "FakeGame.csproj"), "<Project />");

        try
        {
            var detected = FlatRedBallService.DetectSourceContentRoot(bin);
            detected.ShouldBe(root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SourceContentRoot_AutoDetect_ReturnsNullWhenNoCsprojFound()
    {
        var root = Path.Combine(Path.GetTempPath(), "frb2-srcroot-" + System.Guid.NewGuid().ToString("N"));
        var bin = Path.Combine(root, "d1", "d2", "d3", "d4", "d5", "d6", "d7", "d8", "d9", "d10");
        Directory.CreateDirectory(bin);

        try
        {
            FlatRedBallService.DetectSourceContentRoot(bin).ShouldBeNull();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
