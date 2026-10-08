using System.Diagnostics;
using System.Security;
using System.Text.Json;

namespace AnimationEditor.Sandbox.Tests;

/// <summary>One named check a scenario script reported from inside the sandbox.</summary>
public sealed record SandboxCheck(string Name, bool Passed, string Detail);

/// <summary>
/// Runs a PowerShell scenario script inside a fresh Windows Sandbox and returns the checks it
/// wrote. The host folder is mapped read-write at <see cref="GuestFolder"/>; the script runs at
/// logon, writes <c>results.json</c> there, then shuts the sandbox down.
///
/// <para>A sandbox boot costs about a minute, so a scenario script should bundle every check
/// that can share one boot rather than one check per run.</para>
/// </summary>
public static class WindowsSandbox
{
    /// <summary>Where the mapped host folder appears inside the sandbox.</summary>
    public const string GuestFolder = @"C:\AeSandbox";

    private static readonly string SandboxExe =
        Path.Combine(Environment.SystemDirectory, "WindowsSandbox.exe");

    /// <summary>Null when Windows Sandbox can run here; otherwise why not (Home edition, feature
    /// not enabled, non-Windows host), for use as a skip reason.</summary>
    public static string? UnavailableReason()
    {
        if (!OperatingSystem.IsWindows())
            return "Windows Sandbox needs a Windows host.";
        if (!File.Exists(SandboxExe))
            return "Windows Sandbox isn't installed. It needs Windows Pro, Enterprise, or Education with the "
                + "'Windows Sandbox' optional feature enabled.";
        return null;
    }

    /// <summary>
    /// Runs <paramref name="scriptFileName"/> (already copied into <paramref name="hostFolder"/>)
    /// in a new sandbox and waits for its results. Throws if a sandbox is already open (Windows
    /// allows only one) or if no results arrive within <paramref name="timeout"/>.
    /// </summary>
    public static async Task<IReadOnlyList<SandboxCheck>> RunAsync(
        string hostFolder, string scriptFileName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (SandboxSessions().Length > 0)
        {
            throw new InvalidOperationException(
                "A Windows Sandbox is already running. Close it first; Windows allows only one at a time.");
        }

        string resultsPath = Path.Combine(hostFolder, "results.json");
        File.Delete(resultsPath);

        string wsbPath = Path.Combine(hostFolder, "scenario.wsb");
        File.WriteAllText(wsbPath, BuildWsb(hostFolder, scriptFileName));

        Process.Start(new ProcessStartInfo(SandboxExe, $"\"{wsbPath}\"") { UseShellExecute = false });
        try
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (TryReadResults(resultsPath, out var checks))
                    return checks;
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }

            string logPath = Path.Combine(hostFolder, "scenario.log");
            string log = File.Exists(logPath) ? File.ReadAllText(logPath) : "(no scenario.log was written)";
            throw new TimeoutException($"No results.json from the sandbox after {timeout}.\n{log}");
        }
        finally
        {
            // The script shuts the sandbox down itself, but a script that fails before that would
            // leave it open and block the next run. Any session here is ours: we refused to start
            // while one existed.
            foreach (var session in SandboxSessions())
                session.Kill();
        }
    }

    private static Process[] SandboxSessions() =>
        [.. Process.GetProcessesByName("WindowsSandboxRemoteSession"), .. Process.GetProcessesByName("WindowsSandboxClient")];

    /// <summary>The <c>.wsb</c> configuration: no network or GPU (scenarios run offline), one
    /// writable mapped folder, and the scenario script as the logon command.</summary>
    internal static string BuildWsb(string hostFolder, string scriptFileName)
    {
        string command = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File "
            + $"\"{GuestFolder}\\{scriptFileName}\" -Folder \"{GuestFolder}\"";
        return $"""
            <Configuration>
              <Networking>Disable</Networking>
              <VGpu>Disable</VGpu>
              <MappedFolders>
                <MappedFolder>
                  <HostFolder>{SecurityElement.Escape(hostFolder)}</HostFolder>
                  <SandboxFolder>{GuestFolder}</SandboxFolder>
                  <ReadOnly>false</ReadOnly>
                </MappedFolder>
              </MappedFolders>
              <LogonCommand>
                <Command>{SecurityElement.Escape(command)}</Command>
              </LogonCommand>
            </Configuration>
            """;
    }

    // The script writes results.json last, so a parse failure means it's mid-write; retry.
    private static bool TryReadResults(string path, out IReadOnlyList<SandboxCheck> checks)
    {
        checks = [];
        if (!File.Exists(path))
            return false;
        try
        {
            checks = JsonSerializer.Deserialize<List<SandboxCheck>>(
                File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            return true;
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            return false;
        }
    }
}
