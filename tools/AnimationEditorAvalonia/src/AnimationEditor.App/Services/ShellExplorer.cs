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
                    Arguments = $"/select,\"{absolutePath}\"",
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
}
