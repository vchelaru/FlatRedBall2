using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Packaging;

// Runs the real `dotnet new` template engine (against an isolated custom hive, so the developer's
// installed templates are untouched) because the behavior under test is the template engine's
// conditional processing of the .csproj/.slnx files, which can't be checked by reading the raw
// template text.
public class TemplateEnginePathParameterTests : IClassFixture<TemplateEnginePathParameterTests.TemplateHiveFixture>
{
    private const string EnginePath = "../FlatRedBall2";

    private readonly TemplateHiveFixture _fixture;

    public TemplateEnginePathParameterTests(TemplateHiveFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("frb2-desktop")]
    [InlineData("frb2-multiplatform")]
    public void EnginePathUnset_KeepsPackageReferencesAndLeavesNoTemplateMarkers(string template)
    {
        var output = _fixture.Generate(template, enginePath: null);

        foreach (var file in TextFiles(output))
        {
            var text = File.ReadAllText(file);
            text.ShouldNotContain("EnginePath", Case.Sensitive, $"{file} leaked a template token");
            text.ShouldNotContain("#if", Case.Sensitive, $"{file} leaked a conditional marker");
            text.ShouldNotContain("#endif", Case.Sensitive, $"{file} leaked a conditional marker");
        }

        var common = File.ReadAllText(Path.Combine(output, "Game.Common", "Game.Common.csproj"));
        common.ShouldContain(@"<PackageReference Include=""FlatRedBall2.Kni"" />");

        foreach (var csproj in Directory.GetFiles(output, "*.csproj", SearchOption.AllDirectories))
        {
            // Every ProjectReference in a default-generated project stays inside the generated solution.
            foreach (var reference in ProjectReferences(csproj))
            {
                reference.ShouldNotContain("src/", Case.Insensitive, $"{csproj} references the engine by default");
            }
        }

        var slnxPaths = SlnxPaths(output);
        slnxPaths.ShouldAllBe(path => path.StartsWith("Game."), "default solution lists only the game's own projects");
    }

    [Theory]
    [InlineData("frb2-desktop")]
    [InlineData("frb2-multiplatform")]
    public void EnginePathSet_ReplacesEnginePackageReferencesWithProjectReferences(string template)
    {
        var output = _fixture.Generate(template, EnginePath);

        var common = File.ReadAllText(Path.Combine(output, "Game.Common", "Game.Common.csproj"));
        common.ShouldNotContain("FlatRedBall2.Kni\"");
        ProjectReferences(Path.Combine(output, "Game.Common", "Game.Common.csproj"))
            .ShouldContain("../../FlatRedBall2/src/Kni/FlatRedBall2.Kni.csproj");

        foreach (var file in TextFiles(output))
        {
            var text = File.ReadAllText(file);
            text.ShouldNotContain("__EnginePath", Case.Sensitive, $"{file} leaked a template token");
            text.ShouldNotContain("#if", Case.Sensitive, $"{file} leaked a conditional marker");
            text.ShouldNotContain("#endif", Case.Sensitive, $"{file} leaked a conditional marker");
        }
    }

    // Visual Studio's restore only walks ProjectReferences to projects listed in the same .slnx
    // (see SolutionMembershipTests), so the generated solution must list the engine's whole
    // ProjectReference closure. Computed from the real engine csproj files so this fails if the
    // engine gains a project the template would then dangle on.
    [Theory]
    [InlineData("frb2-desktop")]
    [InlineData("frb2-multiplatform")]
    public void EnginePathSet_SolutionListsEveryEngineProjectInTheReferenceClosure(string template)
    {
        var output = _fixture.Generate(template, EnginePath);
        var repoRoot = TemplatePackageReferenceTests.RepoRootForTests;

        var closure = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>(new[] { "src/Kni/FlatRedBall2.Kni.csproj" });
        while (pending.Count > 0)
        {
            var relative = pending.Pop();
            if (!closure.Add(relative))
            {
                continue;
            }

            var absolute = Path.Combine(repoRoot, relative);
            File.Exists(absolute).ShouldBeTrue($"engine project {relative} should exist");
            foreach (var reference in ProjectReferences(absolute))
            {
                var resolved = Path.GetRelativePath(
                    repoRoot,
                    Path.GetFullPath(Path.Combine(Path.GetDirectoryName(absolute)!, reference.Replace('\\', '/'))));
                pending.Push(resolved.Replace('\\', '/'));
            }
        }

        var listedEngineProjects = SlnxPaths(output)
            .Where(path => path.StartsWith(EnginePath + "/"))
            .Select(path => path.Substring(EnginePath.Length + 1))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        listedEngineProjects.OrderBy(p => p).ShouldBe(closure.OrderBy(p => p));
    }

    // Project-level files only: generated .cs legitimately contains C# "#if KNI" directives.
    private static IEnumerable<string> TextFiles(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".csproj") || f.EndsWith(".slnx") || f.EndsWith(".props"));

    private static List<string> ProjectReferences(string csprojPath) =>
        Regex.Matches(File.ReadAllText(csprojPath), @"<ProjectReference\s+Include=""([^""]+)""")
            .Select(m => m.Groups[1].Value.Replace('\\', '/'))
            .ToList();

    private static List<string> SlnxPaths(string outputDir) =>
        Regex.Matches(File.ReadAllText(Path.Combine(outputDir, "Game.slnx")), @"<Project\s+Path=""([^""]+)""")
            .Select(m => m.Groups[1].Value.Replace('\\', '/'))
            .ToList();

    public sealed class TemplateHiveFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "frb2-template-tests-" + Guid.NewGuid().ToString("N"));
        private readonly string _hive;
        private readonly HashSet<string> _installed = new();
        private readonly Dictionary<string, string> _generated = new();

        public TemplateHiveFixture()
        {
            _hive = Path.Combine(_root, "hive");
            Directory.CreateDirectory(_hive);
        }

        // Returns the generated project folder (named "Game"). Results are cached per
        // (template, enginePath) so each combination runs `dotnet new` once.
        public string Generate(string template, string? enginePath)
        {
            var key = template + "|" + enginePath;
            if (_generated.TryGetValue(key, out var existing))
            {
                return existing;
            }

            if (_installed.Add(template))
            {
                var templateDir = Path.Combine(TemplatePackageReferenceTests.RepoRootForTests, "templates", template);
                Run(_root, "new", "install", templateDir, "--debug:custom-hive", _hive);
            }

            var workDir = Path.Combine(_root, "out-" + _generated.Count);
            Directory.CreateDirectory(workDir);
            var args = new List<string> { "new", template, "-n", "Game", "--debug:custom-hive", _hive };
            if (enginePath != null)
            {
                args.Add("--EnginePath");
                args.Add(enginePath);
            }

            Run(workDir, args.ToArray());
            var output = Path.Combine(workDir, "Game");
            _generated[key] = output;
            return output;
        }

        private static void Run(string workingDirectory, params string[] args)
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = Process.Start(startInfo)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(120_000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"dotnet {string.Join(' ', args)} timed out");
            }

            process.ExitCode.ShouldBe(0, $"dotnet {string.Join(' ', args)} failed:\n{stdout.Result}\n{stderr.Result}");
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
