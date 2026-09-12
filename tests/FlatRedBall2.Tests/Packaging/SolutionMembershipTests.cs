using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Packaging;

// Visual Studio's IDE-hosted NuGet restore only walks ProjectReferences among projects that are
// themselves listed as <Project Path="..."> members of the .slnx being restored. dotnet/msbuild
// CLI restore walks the full reference closure regardless of solution membership, so a dangling
// reference (a project references a .csproj that isn't listed in the same .slnx) builds fine on
// the command line and in CI, but fails with NU1105 the moment a human opens the .slnx in Visual
// Studio. This test catches the general condition (any dangling reference in any .slnx in the
// repo), not one specific missing project.
public class SolutionMembershipTests
{
    [Fact]
    public void EverySlnxListsAllProjectReferencesOfItsMemberProjects()
    {
        var repoRoot = TemplatePackageReferenceTests.RepoRootForTests;
        var slnxFiles = Directory.EnumerateFiles(repoRoot, "*.slnx", SearchOption.AllDirectories)
            .Where(path => !ContainsExcludedSegment(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        slnxFiles.ShouldNotBeEmpty();

        var violations = new List<string>();

        foreach (var slnxPath in slnxFiles)
        {
            var slnxDir = Path.GetDirectoryName(slnxPath)!;
            var listedProjectPaths = Regex.Matches(File.ReadAllText(slnxPath), @"<Project\s+Path=""([^""]+)""")
                .Select(match => match.Groups[1].Value)
                .ToList();

            var listedAbsolutePaths = listedProjectPaths
                .Select(relative => NormalizePath(Path.Combine(slnxDir, relative)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var relativeProjectPath in listedProjectPaths)
            {
                var csprojPath = Path.Combine(slnxDir, relativeProjectPath);
                if (!File.Exists(csprojPath))
                {
                    continue;
                }

                var csprojDir = Path.GetDirectoryName(csprojPath)!;
                var referencedPaths = Regex.Matches(
                        File.ReadAllText(csprojPath), @"<ProjectReference\s+Include=""([^""]+)""")
                    .Select(match => match.Groups[1].Value);

                foreach (var referencedPath in referencedPaths)
                {
                    var referencedAbsolute = NormalizePath(Path.Combine(csprojDir, referencedPath));
                    if (!listedAbsolutePaths.Contains(referencedAbsolute))
                    {
                        violations.Add(
                            $"{Path.GetRelativePath(repoRoot, slnxPath)}: " +
                            $"{relativeProjectPath} references {referencedPath}, " +
                            "which is not listed as a <Project Path> member of the same .slnx " +
                            "(builds fine via dotnet/msbuild CLI, fails NU1105 in Visual Studio).");
                    }
                }
            }
        }

        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }

    private static bool ContainsExcludedSegment(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "obj" or "bin" or ".vs" or ".git");

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
}
