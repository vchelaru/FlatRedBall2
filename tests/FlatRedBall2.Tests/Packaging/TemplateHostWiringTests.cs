using System.IO;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Packaging;

public class TemplateHostWiringTests
{
    private static string BlazorGLDir => Path.Combine(
        TemplatePackageReferenceTests.RepoRootForTests, "templates", "frb2-multiplatform", "MyGame.BlazorGL");

    [Fact]
    public void Template_IndexPage_InstallsBrowserStorageBackend()
    {
        // Without the factory, Engine.Storage silently stays in memory in a template web build (#1314).
        var index = File.ReadAllText(Path.Combine(BlazorGLDir, "Pages", "Index.razor.cs"));

        index.ShouldContain("StorageBackendFactory");
        index.ShouldContain("new BrowserStorageBackend(name, new LocalStorageApi())");
    }

    [Fact]
    public void Template_IndexPage_DrivesPerfProbeAndReportsBootFailure()
    {
        // The template vendors the host page, so it must carry the engine page's ?diag=perf probe
        // (#1318) and the boot-failure report (#1319); otherwise both silently do nothing there.
        var index = File.ReadAllText(Path.Combine(BlazorGLDir, "Pages", "Index.razor.cs"));

        index.ShouldContain("PerfDiagnosticsProbe.TryCreateFromQuery(");
        index.ShouldContain("_perfProbe?.OnTick();");
        index.ShouldContain("InvokeBootScreen(\"frbBootFailed\"");
    }

    [Theory]
    [InlineData("frb2-desktop")]
    [InlineData("frb2-multiplatform")]
    public void Template_DesktopProgram_AppliesWaylandFallbackBeforeConstructingGame(string template)
    {
        // SDL picks its video driver inside the Game constructor, so the call must come first.
        var program = File.ReadAllText(Path.Combine(
            TemplatePackageReferenceTests.RepoRootForTests, "templates", template, "MyGame.Desktop", "Program.cs"));

        var fallback = program.IndexOf("LinuxVideoDriver.ApplyWaylandFallback();");
        var construct = program.IndexOf("new GameNamespace.Game1()");

        fallback.ShouldBeGreaterThanOrEqualTo(0);
        construct.ShouldBeGreaterThan(fallback);
    }

    [Fact]
    public void Template_VendoredLocalStorageApi_MatchesPackageCopy()
    {
        // FlatRedBall2.BlazorGL is not a NuGet package, so the template vendors LocalStorageApi;
        // this keeps the copy from drifting. Only the namespace may differ.
        var packageCopy = File.ReadAllText(Path.Combine(
            TemplatePackageReferenceTests.RepoRootForTests, "src", "FlatRedBall2.BlazorGL", "LocalStorageApi.cs"));
        var templateCopy = File.ReadAllText(Path.Combine(BlazorGLDir, "LocalStorageApi.cs"));

        templateCopy.Replace("namespace GameNamespace.BlazorGL;", "namespace FlatRedBall2.BlazorGL;")
            .ReplaceLineEndings().ShouldBe(packageCopy.ReplaceLineEndings());
    }

    [Fact]
    public void Template_BlazorGLProject_AllowsUnsafeBlocksForJsImportGenerator()
    {
        var csproj = File.ReadAllText(Path.Combine(BlazorGLDir, "MyGame.BlazorGL.csproj"));

        csproj.ShouldContain("<AllowUnsafeBlocks>true</AllowUnsafeBlocks>");
    }
}
