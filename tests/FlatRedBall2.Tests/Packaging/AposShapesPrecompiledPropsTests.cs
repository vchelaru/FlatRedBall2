using System;
using System.Diagnostics;
using System.IO;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Packaging;

// Issue #666. AposShapesPrecompiled.props is imported inside the project body, but MonoGame sets
// $(MonoGamePlatform) in a *.targets file that imports AFTER the body. A <PropertyGroup> gate on
// that property therefore evaluates against an empty value and silently no-ops, so the precompiled
// xnb never gets copied and Apos.Shapes falls back to a Wine-only .fx compile on macOS/Linux.
// These tests reproduce that evaluation-order shape and lock in the item/target-condition fix.
public class AposShapesPrecompiledPropsTests
{
    [Fact]
    public void PrecompiledXnb_DesktopGLPlatformSetAfterImport_IsCopiedToContent()
    {
        // "DesktopGL" is set AFTER the props import, exactly as MonoGame's .targets do. The copy
        // must still resolve because the Content item condition reads the final property value.
        var contentJson = EvaluateContentItems(monoGamePlatformSetAfterImport: "DesktopGL");

        contentJson.ShouldContain("apos-shapes.xnb");
        contentJson.ShouldContain("PrecompiledShaders");
    }

    [Fact]
    public void PrecompiledXnb_UnknownPlatform_IsNotCopiedToContent()
    {
        // No supported platform resolves, so nothing is copied and Apos.Shapes' own compilation runs.
        var contentJson = EvaluateContentItems(monoGamePlatformSetAfterImport: null);

        contentJson.ShouldNotContain("apos-shapes.xnb");
    }

    // Evaluates a synthetic project that imports the real AposShapesPrecompiled.props at the top
    // (as sample .csproj files do), then optionally sets MonoGamePlatform AFTER the import (as
    // MonoGame's .targets do). Returns the JSON that `dotnet msbuild -getItem:Content` emits;
    // -getItem performs a full evaluation, so item conditions see the late property value.
    private static string EvaluateContentItems(string? monoGamePlatformSetAfterImport)
    {
        var repoRoot = TemplatePackageReferenceTests.RepoRootForTests;
        var propsPath = Path.Combine(repoRoot, "src", "PrecompiledShaders", "AposShapesPrecompiled.props");

        var latePlatform = monoGamePlatformSetAfterImport is null
            ? string.Empty
            : $"  <PropertyGroup>{Environment.NewLine}" +
              $"    <MonoGamePlatform>{monoGamePlatformSetAfterImport}</MonoGamePlatform>{Environment.NewLine}" +
              $"  </PropertyGroup>{Environment.NewLine}";

        var project =
            $"<Project>{Environment.NewLine}" +
            $"  <Import Project=\"{propsPath}\" />{Environment.NewLine}" +
            latePlatform +
            $"</Project>{Environment.NewLine}";

        var tempDir = Path.Combine(Path.GetTempPath(), "frb2-apos-props-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var projectPath = Path.Combine(tempDir, "SyntheticSample.proj");
            File.WriteAllText(projectPath, project);

            var startInfo = new ProcessStartInfo("dotnet", $"msbuild \"{projectPath}\" -getItem:Content")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var process = Process.Start(startInfo)!;
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            process.ExitCode.ShouldBe(
                0,
                $"dotnet msbuild -getItem failed:{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");

            return stdout;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); }
            catch { /* best-effort temp cleanup */ }
        }
    }
}
