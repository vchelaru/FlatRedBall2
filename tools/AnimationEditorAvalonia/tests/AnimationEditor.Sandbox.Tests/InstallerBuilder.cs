using System.Diagnostics;

namespace AnimationEditor.Sandbox.Tests;

/// <summary>
/// Produces the Velopack <c>Setup.exe</c> a scenario installs: the same <c>dotnet publish</c> +
/// <c>vpk pack</c> steps as <c>.github/workflows/animation-editor.yml</c>. Set
/// <c>AE_SANDBOX_SETUP_EXE</c> to a prebuilt Setup.exe (e.g. a CI artifact) to skip the build.
/// </summary>
public static class InstallerBuilder
{
    public const string PackId = "FlatRedBall2.AnimationEditor";
    private const string VpkVersion = "1.2.0";

    public static async Task<string> GetSetupExeAsync(CancellationToken cancellationToken)
    {
        string? prebuilt = Environment.GetEnvironmentVariable("AE_SANDBOX_SETUP_EXE");
        if (!string.IsNullOrEmpty(prebuilt))
        {
            if (!File.Exists(prebuilt))
                throw new FileNotFoundException($"AE_SANDBOX_SETUP_EXE points at a missing file: {prebuilt}");
            return prebuilt;
        }

        string toolRoot = FindToolRoot();
        string work = Path.Combine(Path.GetTempPath(), "AnimationEditorSandbox", "installer");
        if (Directory.Exists(work))
            Directory.Delete(work, recursive: true);
        string publishDir = Path.Combine(work, "publish");
        string releaseDir = Path.Combine(work, "release");

        await RunAsync("dotnet",
            $"publish \"{Path.Combine(toolRoot, "src", "AnimationEditor.App", "AnimationEditor.App.csproj")}\" "
            + $"-c Release -r win-x64 --self-contained -p:PublishSingleFile=false -o \"{publishDir}\"",
            cancellationToken);

        string vpk = await EnsureVpkAsync(cancellationToken);
        await RunAsync(vpk,
            $"pack --packId {PackId} --packVersion 0.0.1 --packDir \"{publishDir}\" "
            + "--mainExe AnimationEditor.exe --packTitle AnimationEditor "
            + $"--icon \"{Path.Combine(toolRoot, "src", "AnimationEditor.App", "Assets", "icons", "AppIcon.ico")}\" "
            + $"--outputDir \"{releaseDir}\"",
            cancellationToken);

        return Directory.GetFiles(releaseDir, "*Setup.exe").Single();
    }

    // vpk is cached per version outside the repo so repeat runs skip the tool install.
    private static async Task<string> EnsureVpkAsync(CancellationToken cancellationToken)
    {
        string toolPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AnimationEditorSandbox", $"vpk-{VpkVersion}");
        string vpk = Path.Combine(toolPath, "vpk.exe");
        if (!File.Exists(vpk))
            await RunAsync("dotnet", $"tool install --tool-path \"{toolPath}\" vpk --version {VpkVersion}", cancellationToken);
        return vpk;
    }

    private static string FindToolRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AnimationEditorAvalonia.slnx")))
                return dir.FullName;
        }
        throw new DirectoryNotFoundException("Couldn't find AnimationEditorAvalonia.slnx above the test output folder.");
    }

    private static async Task RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"{fileName} {arguments} exited {process.ExitCode}\n{await stdout}\n{await stderr}");
    }
}
