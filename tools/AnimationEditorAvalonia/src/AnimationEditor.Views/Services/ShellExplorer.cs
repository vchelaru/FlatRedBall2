using System;
using System.Diagnostics;
using System.IO;

namespace AnimationEditor.App.Services;

/// <summary>
/// Opens the host file manager with a file selected (or its folder revealed).
/// </summary>
public static class ShellExplorer
{
    /// <summary>
    /// Reveals <paramref name="absolutePath"/> in the system file manager.
    /// Returns <c>null</c> on success, or an error message when the file is missing
    /// or the shell command fails.
    /// </summary>
    public static string? RevealFile(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath))
            return "No file path was provided.";

        if (!File.Exists(absolutePath))
            return $"File not found: {absolutePath}";

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{ToWindowsSelectPath(absolutePath)}\"",
                    UseShellExecute = true,
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"-R \"{absolutePath}\"",
                    UseShellExecute = false,
                });
            }
            else
            {
                var folder = Path.GetDirectoryName(absolutePath);
                if (string.IsNullOrEmpty(folder))
                    return $"Could not determine folder for: {absolutePath}";

                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{folder}\"",
                    UseShellExecute = false,
                });
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"Could not open file manager: {ex.Message}";
        }
    }

    /// <summary>
    /// explorer.exe's <c>/select,</c> switch mis-parses forward slashes as extra switch
    /// separators, silently ignoring the target and opening a default folder instead of
    /// selecting the file. Callers may pass either separator (e.g. a path built from
    /// <c>AnimationEditor.Core.Paths.FilePath</c>, which always normalizes to forward slashes)
    /// so this converts to backslashes right before building the Windows-only argument string.
    /// </summary>
    internal static string ToWindowsSelectPath(string absolutePath) => absolutePath.Replace('/', '\\');
}
