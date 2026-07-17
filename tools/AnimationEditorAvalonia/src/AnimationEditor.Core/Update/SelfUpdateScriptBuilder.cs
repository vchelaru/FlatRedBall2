using System.IO;

namespace AnimationEditor.Core.Update;

/// <summary>
/// Builds the PowerShell script that finishes a Windows self-update after the running app
/// exits — a process can't overwrite its own running exe, so the swap has to happen from a
/// separate process that waits for this one to close first. Kept as a pure string builder
/// (no file/process I/O) so the exact commands are unit-testable.
/// </summary>
public static class SelfUpdateScriptBuilder
{
    public static string Build(int processId, string extractedDir, string installDir, string exeFileName, string workDirToCleanUp) =>
        $@"while (Get-Process -Id {processId} -ErrorAction SilentlyContinue) {{ Start-Sleep -Milliseconds 300 }}
Copy-Item -Path ""{extractedDir}\*"" -Destination ""{installDir}"" -Recurse -Force
Start-Process -FilePath ""{Path.Combine(installDir, exeFileName)}""
Remove-Item -Path ""{workDirToCleanUp}"" -Recurse -Force -ErrorAction SilentlyContinue
";
}
