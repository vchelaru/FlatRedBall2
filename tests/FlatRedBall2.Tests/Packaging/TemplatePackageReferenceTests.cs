using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Packaging;

public class TemplatePackageReferenceTests
{
    [Theory]
    [InlineData("templates/frb2-desktop/MyGame.Desktop/MyGame.Desktop.csproj")]
    [InlineData("templates/frb2-multiplatform/MyGame.Desktop/MyGame.Desktop.csproj")]
    public void DesktopTemplate_DoesNotPinAposShapes(string relativeCsprojPath)
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot, relativeCsprojPath));

        Regex.IsMatch(csproj, @"<PackageReference\s+Include=""Apos\.Shapes""")
            .ShouldBeFalse(
                "Desktop templates must not pin Apos.Shapes; version flows transitively from FlatRedBall2.MonoGame.");
    }

    [Fact]
    public void BlazorGLTemplate_DoesNotPinAposShapesKni()
    {
        var csproj = File.ReadAllText(
            Path.Combine(RepoRoot, "templates/frb2-multiplatform/MyGame.BlazorGL/MyGame.BlazorGL.csproj"));

        Regex.IsMatch(csproj, @"<PackageReference\s+Include=""Apos\.Shapes\.KNI""")
            .ShouldBeFalse(
                "BlazorGL templates must not pin Apos.Shapes.KNI; version flows transitively from FlatRedBall2.Kni.");
    }

    // Templates resolve FlatRedBall2.MonoGame/FlatRedBall2.Kni as a floating "*-*" PackageVersion
    // from nuget.org, not from this repo's source (see templates/*/Directory.Packages.props). The
    // latest published release still depends on a pre-0.7.2 Apos.Shapes that compiles its .fx via
    // the content pipeline (Wine on macOS/Linux), so every template project needs the precompiled
    // shader workaround until a release depending on Apos.Shapes >=0.7.2 ships. Dropping this
    // import without also dropping the workaround file is what broke CI in PR #783.
    [Theory]
    [InlineData("templates/frb2-desktop/MyGame.Desktop/MyGame.Desktop.csproj")]
    [InlineData("templates/frb2-multiplatform/MyGame.Desktop/MyGame.Desktop.csproj")]
    [InlineData("templates/frb2-multiplatform/MyGame.BlazorGL/MyGame.BlazorGL.csproj")]
    public void Template_ImportsPrecompiledAposShapesWorkaround(string relativeCsprojPath)
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot, relativeCsprojPath));

        csproj.ShouldContain("AposShapesPrecompiled.props");
    }

    // Visual Studio picks the first project in the .slnx as the default startup project, so a
    // freshly created project that lists the class library first cannot be run without the user
    // changing that first.
    [Theory]
    [InlineData("templates/frb2-desktop/MyGame.slnx")]
    [InlineData("templates/frb2-multiplatform/MyGame.slnx")]
    public void TemplateSolution_ListsARunnableProjectFirstAndCommonLast(string relativeSlnxPath)
    {
        var projects = Regex.Matches(
                File.ReadAllText(Path.Combine(RepoRoot, relativeSlnxPath)),
                @"<Project\s+Path=""([^""]+)""")
            .Select(match => match.Groups[1].Value)
            .ToList();

        projects.First().ShouldContain("MyGame.Desktop");
        projects.Last().ShouldContain("MyGame.Common");
    }

    private static string RepoRoot => RepoRootForTests;

    internal static string RepoRootForTests
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "src", "FlatRedBall2.csproj")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException("Could not locate repository root.");
        }
    }
}
