using System;
using System.IO;

namespace AnimationEditor.Core.Logging;

/// <summary>Keeps <c>log.txt</c> from growing without bound: one previous generation, <c>log.old.txt</c>.</summary>
public static class LogRotation
{
    public const string OldFileName = "log.old.txt";

    /// <summary>
    /// Moves <paramref name="logPath"/> to <c>log.old.txt</c> beside it (replacing an earlier one) when it is larger than
    /// <paramref name="maxBytes"/>. Never throws: a log that cannot be rotated is just a log that keeps growing.
    /// </summary>
    public static bool RotateIfLarge(string logPath, long maxBytes)
    {
        try
        {
            var info = new FileInfo(logPath);
            if (!info.Exists || info.Length <= maxBytes)
                return false;

            var oldPath = Path.Combine(info.DirectoryName ?? "", OldFileName);
            File.Move(logPath, oldPath, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
