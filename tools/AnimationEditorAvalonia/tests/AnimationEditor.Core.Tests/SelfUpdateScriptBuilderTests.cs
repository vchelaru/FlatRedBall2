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

    // ── Status window (so the user isn't staring at nothing between the app closing and the
    // updated one appearing) ───────────────────────────────────────────────────────────────

    [Fact]
    public void Build_ShowsAWinFormsStatusWindow()
    {
        var script = SelfUpdateScriptBuilder.Build(1234, @"C:\temp\extracted", @"C:\Apps\AnimationEditor", "AnimationEditor.exe", @"C:\temp\work");

        Assert.Contains("System.Windows.Forms.Form", script);
        Assert.Contains("$form.Show()", script);
    }

    [Fact]
    public void Build_ReportsEachPhaseOfTheUpdate()
    {
        var script = SelfUpdateScriptBuilder.Build(1234, @"C:\temp\extracted", @"C:\Apps\AnimationEditor", "AnimationEditor.exe", @"C:\temp\work");

        Assert.Contains("Waiting for Animation Editor to close", script);
        Assert.Contains("Installing update", script);
        Assert.Contains("Launching Animation Editor", script);
    }

    [Fact]
    public void Build_PumpsTheMessageLoopWhileWaiting()
    {
        // Without periodic DoEvents() calls the form would never paint or respond while this
        // script's own while-loop/Copy-Item are running on the same thread.
        var script = SelfUpdateScriptBuilder.Build(1234, @"C:\temp\extracted", @"C:\Apps\AnimationEditor", "AnimationEditor.exe", @"C:\temp\work");

        Assert.Contains("[System.Windows.Forms.Application]::DoEvents()", script);
    }

    [Fact]
    public void Build_ClosesTheFormBeforeCleanup()
    {
        var script = SelfUpdateScriptBuilder.Build(1234, @"C:\temp\extracted", @"C:\Apps\AnimationEditor", "AnimationEditor.exe", @"C:\temp\work");

        Assert.True(script.IndexOf("$form.Close()") < script.IndexOf("Remove-Item"));
    }
}
