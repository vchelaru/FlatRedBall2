using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace AnimationEditor.Core.Update;

/// <summary>
/// Downloads the win-x64 release zip, extracts it, and spawns a PowerShell helper
/// (<see cref="SelfUpdateScriptBuilder"/>) that waits for this process to exit, copies the
/// extracted files over the install directory, and relaunches the exe — then exits this
/// process so the swap can happen. Untested: this method's own body is OS-process wiring
/// (real network download, real zip extraction, spawning a real detached process, and an
/// unconditional <see cref="Environment.Exit(int)"/> on success that would kill the test
/// runner itself) with no way to exercise it in-process without side effects. The two pieces
/// that decide *what* it does — <see cref="WindowsReleaseAsset"/> asset selection and
/// <see cref="SelfUpdateScriptBuilder"/>'s script text — are extracted and fully unit tested;
/// this class only orchestrates them against the real filesystem/network/process APIs.
/// </summary>
public sealed class WindowsAppUpdateInstaller : IAppUpdateInstaller
{
    public bool IsSupported => OperatingSystem.IsWindows();

    public async Task InstallAndRestartAsync(string downloadUrl, CancellationToken cancellationToken = default)
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not resolve the running executable's path.");
        var installDir = Path.GetDirectoryName(exePath)
            ?? throw new InvalidOperationException($"Could not resolve the install directory for '{exePath}'.");

        var workDir = Path.Combine(Path.GetTempPath(), "AnimationEditorUpdate", Guid.NewGuid().ToString("N"));
        var extractDir = Path.Combine(workDir, "extracted");
        var zipPath = Path.Combine(workDir, "update.zip");
        Directory.CreateDirectory(workDir);

        using (var httpClient = new HttpClient())
        {
            await using var responseStream = await httpClient.GetStreamAsync(downloadUrl, cancellationToken).ConfigureAwait(false);
            await using var fileStream = File.Create(zipPath);
            await responseStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        }

        ZipFile.ExtractToDirectory(zipPath, extractDir);

        var scriptPath = Path.Combine(workDir, "apply-update.ps1");
        var script = SelfUpdateScriptBuilder.Build(
            Environment.ProcessId, extractDir, installDir, Path.GetFileName(exePath), workDir);
        await File.WriteAllTextAsync(scriptPath, script, cancellationToken).ConfigureAwait(false);

        Process.Start(new ProcessStartInfo("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });

        Environment.Exit(0);
    }
}
