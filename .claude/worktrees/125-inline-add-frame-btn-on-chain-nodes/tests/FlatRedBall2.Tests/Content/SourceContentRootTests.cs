using System.IO;
using System.Linq;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Content;

public class SourceContentRootTests
{
    [Fact]
    public void SourceContentRoots_IsMutableForOverride()
    {
        var engine = new FlatRedBallService();
        engine.SourceContentRoots.Clear();
        engine.SourceContentRoots.Add("C:/some/path");
        engine.SourceContentRoots.ShouldBe(new[] { "C:/some/path" });
    }

    [Fact]
    public void SourceContentRoots_AutoDetect_FindsCsprojWalkingUpFromBaseDirectory()
    {
        // Arrange a fake project layout under the temp dir and walk up from bin/Debug/net10.0.
        var root = Path.Combine(Path.GetTempPath(), "frb2-srcroot-" + System.Guid.NewGuid().ToString("N"));
        var bin = Path.Combine(root, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(root, "FakeGame.csproj"), "<Project />");

        try
        {
            var detected = FlatRedBallService.DetectSourceContentRoots(bin);
            detected.ShouldBe(new[] { root });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SourceContentRoots_AutoDetect_ReturnsEmptyWhenNothingFound()
    {
        var root = Path.Combine(Path.GetTempPath(), "frb2-srcroot-" + System.Guid.NewGuid().ToString("N"));
        var bin = Path.Combine(root, "d1", "d2", "d3", "d4", "d5", "d6", "d7", "d8", "d9", "d10");
        Directory.CreateDirectory(bin);

        try
        {
            FlatRedBallService.DetectSourceContentRoots(bin).ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SourceContentRoots_AutoDetect_WithSlnNearby_IncludesAllSiblingProjectsWithContent()
    {
        // Layout:
        //   <root>/MySolution.sln          — references Common.csproj and Desktop.csproj
        //   <root>/Common/Common.csproj
        //   <root>/Common/Content/         — has content, should be picked up
        //   <root>/Desktop/Desktop.csproj
        //   <root>/Desktop/Content/        — has content, should also be picked up
        //   <root>/Desktop/bin/Debug/net10.0/   — BaseDirectory
        var root = Path.Combine(Path.GetTempPath(), "frb2-sln-" + System.Guid.NewGuid().ToString("N"));
        var commonDir = Path.Combine(root, "Common");
        var desktopDir = Path.Combine(root, "Desktop");
        var bin = Path.Combine(desktopDir, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(Path.Combine(commonDir, "Content"));
        Directory.CreateDirectory(Path.Combine(desktopDir, "Content"));
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(commonDir, "Common.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(desktopDir, "Desktop.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(root, "MySolution.sln"),
            "Microsoft Visual Studio Solution File, Format Version 12.00\r\n" +
            "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Common\", \"Common\\Common.csproj\", \"{11111111-1111-1111-1111-111111111111}\"\r\n" +
            "EndProject\r\n" +
            "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Desktop\", \"Desktop\\Desktop.csproj\", \"{22222222-2222-2222-2222-222222222222}\"\r\n" +
            "EndProject\r\n");

        try
        {
            var detected = FlatRedBallService.DetectSourceContentRoots(bin).ToArray();
            detected.ShouldContain(commonDir);
            detected.ShouldContain(desktopDir);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SourceContentRoots_AutoDetect_WithSlnxNearby_IncludesAllSiblingProjectsWithContent()
    {
        var root = Path.Combine(Path.GetTempPath(), "frb2-slnx-" + System.Guid.NewGuid().ToString("N"));
        var commonDir = Path.Combine(root, "Common");
        var desktopDir = Path.Combine(root, "Desktop");
        var bin = Path.Combine(desktopDir, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(Path.Combine(commonDir, "Content"));
        Directory.CreateDirectory(Path.Combine(desktopDir, "Content"));
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(commonDir, "Common.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(desktopDir, "Desktop.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(root, "MySolution.slnx"),
            "<Solution>\n" +
            "  <Project Path=\"Common/Common.csproj\" />\n" +
            "  <Project Path=\"Desktop/Desktop.csproj\" />\n" +
            "</Solution>\n");

        try
        {
            var detected = FlatRedBallService.DetectSourceContentRoots(bin).ToArray();
            detected.ShouldContain(commonDir);
            detected.ShouldContain(desktopDir);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SourceContentRoots_AutoDetect_WithSlnButNoProjectHasContent_FallsBackToCsproj()
    {
        // Solution exists but no referenced project has a Content/ folder. The detector should
        // fall through to the .csproj walk-up so it still returns *something* useful.
        var root = Path.Combine(Path.GetTempPath(), "frb2-slnfb-" + System.Guid.NewGuid().ToString("N"));
        var desktopDir = Path.Combine(root, "Desktop");
        var bin = Path.Combine(desktopDir, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(desktopDir, "Desktop.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(root, "MySolution.sln"),
            "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Desktop\", \"Desktop\\Desktop.csproj\", \"{22222222-2222-2222-2222-222222222222}\"\r\n" +
            "EndProject\r\n");

        try
        {
            var detected = FlatRedBallService.DetectSourceContentRoots(bin).ToArray();
            detected.ShouldBe(new[] { desktopDir });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
