using System;
using System.Diagnostics;
using System.IO;

namespace AnimationEditor.App.Services;

/// <summary>
/// Moves a file to the OS trash/recycle bin instead of permanently deleting it (issue #919).
/// </summary>
public static class RecycleBin
{
    /// <summary>
    /// Moves <paramref name="absolutePath"/> to the system trash. Returns <c>null</c> on
    /// success, or an error message when the file is missing or the platform call fails.
    /// </summary>
    public static string? Delete(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath))
            return "No file path was provided.";

        if (!File.Exists(absolutePath))
            return $"File not found: {absolutePath}";

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    absolutePath,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
            else if (OperatingSystem.IsMacOS())
            {
                // Finder both moves the file into Trash and de-duplicates the destination name
                // itself, so there's no manual collision handling needed here (unlike Linux).
                var script = $"tell application \"Finder\" to delete POSIX file \"{absolutePath}\"";
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "osascript",
                    ArgumentList = { "-e", script },
                    UseShellExecute = false,
                });
                process?.WaitForExit();
            }
            else
            {
                MoveToLinuxTrash(absolutePath);
            }

            // A platform call that reports no failure (osascript's exit code) may still have moved nothing.
            return File.Exists(absolutePath) ? $"Could not move to recycle bin: {absolutePath} is still there." : null;
        }
        catch (Exception ex)
        {
            return $"Could not move to recycle bin: {ex.Message}";
        }
    }

    /// <summary>
    /// Implements the freedesktop.org home-Trash spec directly instead of shelling out to
    /// <c>gio trash</c>/<c>kioclient</c> -- neither is guaranteed present on every distro or
    /// window manager, while <c>$XDG_DATA_HOME/Trash/files</c> plus a sibling <c>.trashinfo</c>
    /// is the same on-disk layout GNOME/KDE file managers themselves read to offer "Restore".
    /// </summary>
    private static void MoveToLinuxTrash(string absolutePath)
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrEmpty(dataHome))
            dataHome = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");

        var trashFiles = Path.Combine(dataHome, "Trash", "files");
        var trashInfo = Path.Combine(dataHome, "Trash", "info");
        Directory.CreateDirectory(trashFiles);
        Directory.CreateDirectory(trashInfo);

        var name = Path.GetFileName(absolutePath);
        var destName = name;
        var suffix = 1;
        while (File.Exists(Path.Combine(trashFiles, destName)) ||
               File.Exists(Path.Combine(trashInfo, destName + ".trashinfo")))
        {
            destName = $"{Path.GetFileNameWithoutExtension(name)}.{suffix++}{Path.GetExtension(name)}";
        }

        File.WriteAllText(Path.Combine(trashInfo, destName + ".trashinfo"),
            "[Trash Info]\n" +
            $"Path={Uri.EscapeDataString(absolutePath)}\n" +
            $"DeletionDate={DateTime.Now:yyyy-MM-ddTHH:mm:ss}\n");
        File.Move(absolutePath, Path.Combine(trashFiles, destName));
    }
}
