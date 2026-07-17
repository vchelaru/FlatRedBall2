using AnimationEditor.Core.Update;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="SelfUpdateScriptBuilder"/> — the PowerShell script text is the only
/// pure, assertable part of the Windows self-update flow. Everything downstream (spawning
/// powershell.exe, waiting for this process to exit, swapping files) is OS-process wiring
/// that can't run inside a test without side effects, so this locks down the script's content
/// rather than its execution.
/// </summary>
public class SelfUpdateScriptBuilderTests
{
    [Fact]
    public void Build_WaitsForGivenProcessId()
    {
        var script = SelfUpdateScriptBuilder.Build(1234, @"C:\temp\extracted", @"C:\Apps\AnimationEditor", "AnimationEditor.exe", @"C:\temp\work");

        Assert.Contains("Get-Process -Id 1234", script);
    }

    [Fact]
    public void Build_CopiesExtractedFilesIntoInstallDir()
    {
        var script = SelfUpdateScriptBuilder.Build(1234, @"C:\temp\extracted", @"C:\Apps\AnimationEditor", "AnimationEditor.exe", @"C:\temp\work");

        Assert.Contains(@"C:\temp\extracted", script);
        Assert.Contains(@"C:\Apps\AnimationEditor", script);
        Assert.Contains("Copy-Item", script);
    }

    [Fact]
    public void Build_RelaunchesTheNamedExeFromTheInstallDir()
    {
        var script = SelfUpdateScriptBuilder.Build(1234, @"C:\temp\extracted", @"C:\Apps\AnimationEditor", "AnimationEditor.exe", @"C:\temp\work");

        Assert.Contains("Start-Process", script);
        Assert.Contains(@"C:\Apps\AnimationEditor\AnimationEditor.exe", script);
    }

    [Fact]
    public void Build_CleansUpTheWorkDirectory()
    {
        var script = SelfUpdateScriptBuilder.Build(1234, @"C:\temp\extracted", @"C:\Apps\AnimationEditor", "AnimationEditor.exe", @"C:\temp\work");

        Assert.Contains("Remove-Item", script);
        Assert.Contains(@"C:\temp\work", script);
    }
}
