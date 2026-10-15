using System.IO;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Packaging;

public class BootScreenPackagingTests
{
    private static string RepoRoot => TemplatePackageReferenceTests.RepoRootForTests;

    private const string PackageScript = "_content/FlatRedBall2.BlazorGL/boot-screen.js";

    [Theory]
    [InlineData("samples/PlatformKing/PlatformKing.BlazorGL/wwwroot/index.html", PackageScript)]
    [InlineData("samples/ShmupSpace/ShmupSpace.BlazorGL/wwwroot/index.html", PackageScript)]
    [InlineData("samples/Solitaire/Solitaire.BlazorGL/wwwroot/index.html", PackageScript)]
    [InlineData("templates/frb2-multiplatform/MyGame.BlazorGL/wwwroot/index.html", "boot-screen.js")]
    public void IndexHtml_LoadsBootScreenBeforeBlazorRuntime(string relativeIndexPath, string expectedSrc)
    {
        // The script wraps fetch to count the runtime download, so it must be installed before
        // blazor.webassembly.js starts that download.
        var html = File.ReadAllText(Path.Combine(RepoRoot, relativeIndexPath));

        var bootScreen = html.IndexOf($"<script src=\"{expectedSrc}\"></script>");
        var runtime = html.IndexOf("<script src=\"_framework/blazor.webassembly.js\" autostart=\"false\"></script>");

        bootScreen.ShouldBeGreaterThanOrEqualTo(0);
        runtime.ShouldBeGreaterThan(bootScreen);
    }

    [Theory]
    [InlineData("samples/PlatformKing/PlatformKing.BlazorGL/wwwroot/index.html")]
    [InlineData("samples/ShmupSpace/ShmupSpace.BlazorGL/wwwroot/index.html")]
    [InlineData("samples/Solitaire/Solitaire.BlazorGL/wwwroot/index.html")]
    [InlineData("templates/frb2-multiplatform/MyGame.BlazorGL/wwwroot/index.html")]
    public void IndexHtml_AppRootStaysPlainText(string relativeIndexPath)
    {
        // index.html is the one un-hashed file in the boot path, so the screen injects its own DOM
        // and the file carries no markup for it. Blazor also replaces #app's children on first
        // render, so markup nested there would be destroyed anyway.
        var html = File.ReadAllText(Path.Combine(RepoRoot, relativeIndexPath));

        Regex.IsMatch(html, @"<div id=""app"">[^<]*</div>").ShouldBeTrue();
        html.ShouldNotContain("frb-boot-screen");
    }

    [Theory]
    [InlineData("diagnostics/MusicPitchWebSpike/MusicPitchWebSpike.BlazorGL/wwwroot/index.html")]
    [InlineData("samples/PlatformKing/PlatformKing.BlazorGL/wwwroot/index.html")]
    [InlineData("samples/ShmupSpace/ShmupSpace.BlazorGL/wwwroot/index.html")]
    [InlineData("samples/Solitaire/Solitaire.BlazorGL/wwwroot/index.html")]
    [InlineData("templates/frb2-multiplatform/MyGame.BlazorGL/wwwroot/index.html")]
    public void IndexHtml_DisablesBlazorAutostartSoFrbHostCanCapDownloads(string relativeIndexPath)
    {
        // frb-host.js calls Blazor.start itself (it is the only place that can set
        // maxParallelDownloads, #1467); a page that leaves autostart on silently loses the cap.
        var html = File.ReadAllText(Path.Combine(RepoRoot, relativeIndexPath));

        html.ShouldContain("<script src=\"_framework/blazor.webassembly.js\" autostart=\"false\"></script>");
    }

    [Fact]
    public void Template_VendoredBootScreen_MatchesPackageCopy()
    {
        // The template cannot use the _content/ path (it references only FlatRedBall2.Kni), so it
        // vendors the file; this keeps the vendored copy from drifting like frb-host.js did.
        var packageCopy = File.ReadAllText(Path.Combine(
            RepoRoot, "src", "FlatRedBall2.BlazorGL", "wwwroot", "boot-screen.js"));
        var templateCopy = File.ReadAllText(Path.Combine(
            RepoRoot, "templates", "frb2-multiplatform", "MyGame.BlazorGL", "wwwroot", "boot-screen.js"));

        templateCopy.ShouldBe(packageCopy);
    }
}
