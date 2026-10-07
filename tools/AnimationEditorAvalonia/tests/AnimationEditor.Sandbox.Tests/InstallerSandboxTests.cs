using Xunit;

namespace AnimationEditor.Sandbox.Tests;

public class InstallerSandboxTests
{
    /// <summary>
    /// #493: a Setup.exe install registers .achx/.achj so Windows lists the editor in Default
    /// apps and opens .achx with it; uninstall removes only our entries. One sandbox boot covers
    /// install, every registry/association check, and uninstall (see the scenario script).
    /// The Settings "Set as default" click can't be automated: Windows hash-protects UserChoice.
    /// </summary>
    [Fact(Timeout = 30 * 60 * 1000)]
    public async Task Install_ThenUninstall_RegistersAndRemovesAchxAssociation()
    {
        Assert.SkipWhen(WindowsSandbox.UnavailableReason() is not null, WindowsSandbox.UnavailableReason() ?? "");
        var cancel = TestContext.Current.CancellationToken;

        string setupExe = await InstallerBuilder.GetSetupExeAsync(cancel);
        string hostFolder = Path.Combine(Path.GetTempPath(), "AnimationEditorSandbox", "install-uninstall");
        if (Directory.Exists(hostFolder))
            Directory.Delete(hostFolder, recursive: true);
        Directory.CreateDirectory(hostFolder);
        File.Copy(setupExe, Path.Combine(hostFolder, "Setup.exe"));
        const string script = "install-uninstall-achx.ps1";
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Scenarios", script), Path.Combine(hostFolder, script));

        var checks = await WindowsSandbox.RunAsync(hostFolder, script, TimeSpan.FromMinutes(10), cancel);

        // Report every failed check at once; a rerun costs minutes.
        var failures = checks.Where(c => !c.Passed).Select(c => $"{c.Name}: {c.Detail}").ToList();
        Assert.NotEmpty(checks);
        Assert.True(failures.Count == 0, "Sandbox checks failed:\n" + string.Join("\n", failures));
    }
}
