using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Packaging;

public class BlazorGLHostScriptTests
{
    private const string EngineScript = "src/FlatRedBall2.BlazorGL/wwwroot/frb-host.js";
    private const string TemplateScript = "templates/frb2-multiplatform/MyGame.BlazorGL/wwwroot/frb-host.js";

    // The template vendors its own copy of frb-host.js (it restores FlatRedBall2.Kni from NuGet and
    // cannot reference the engine's static asset), so the shim version pin and list can drift apart.
    [Fact]
    public void TemplateCopy_LoadsSameShimsAtSameVersionAsEngineScript()
    {
        Shims(EngineScript).ShouldBe(Shims(TemplateScript));
    }

    // KNI's TitleContainer reads every content file through nkast.Wasm.XHR, so dropping the shim
    // breaks content loading; a console 404 that names XHR.<version>.js comes from a failing request
    // the script issued, not from the shim file.
    [Fact]
    public void EngineScript_LoadsTheXhrShimKniContentLoadingNeeds()
    {
        Shims(EngineScript).ShouldContain("nkast.Wasm.XHR/js/XHR");
    }

    private static string[] Shims(string relativePath)
    {
        var js = File.ReadAllText(Path.Combine(TemplatePackageReferenceTests.RepoRootForTests, relativePath));
        var version = Regex.Match(js, @"var v = '([^']+)'").Groups[1].Value;
        version.ShouldNotBeNullOrEmpty($"{relativePath} must pin the shim version in `var v = '...'`.");

        var shims = Regex.Matches(js, @"\['(nkast\.Wasm\.[A-Za-z]+)',\s*'([A-Za-z0-9]+)'\]")
            .Select(m => $"{m.Groups[1].Value}/js/{m.Groups[2].Value}")
            .ToList();
        shims.ShouldNotBeEmpty();
        return shims.Append($"version={version}").ToArray();
    }
}
