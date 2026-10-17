using System.IO;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Packaging;

public class TemplateBrowserStorageTests
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
