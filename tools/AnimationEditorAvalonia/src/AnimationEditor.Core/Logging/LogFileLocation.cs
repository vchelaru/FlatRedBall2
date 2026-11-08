using System;
using System.IO;
using AnimationEditor.Core.IO;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Logging;

/// <summary>
/// Resolves where the editor writes its <c>log.txt</c>. The default is next to the running
/// executable so the user can find and share it without hunting through <c>%APPDATA%</c>;
/// if that directory isn't writable (e.g. the editor is installed under <c>Program Files</c>)
/// the location falls back to the per-user config dir that <see cref="AppSettingsLocation"/> uses.
/// </summary>
public static class LogFileLocation
{
    public const string FileName = "log.txt";

    /// <param name="baseDirectory">
    /// The directory containing the running executable — i.e. <c>AppContext.BaseDirectory</c>.
    /// </param>
    public static FilePath NextToExecutable(string baseDirectory) =>
        new FilePath(Path.Combine(baseDirectory, FileName));

    /// <param name="applicationDataRoot">
    /// The platform's per-user application-data root — i.e.
    /// <c>Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)</c>.
    /// </param>
    public static FilePath ForApplicationDataRoot(string applicationDataRoot) =>
        new FilePath(Path.Combine(applicationDataRoot, AppSettingsLocation.FolderName, FileName));

    /// <summary>
    /// Picks <see cref="NextToExecutable"/> when its directory is writable and is not a Velopack
    /// <c>current</c> folder, otherwise <see cref="ForApplicationDataRoot"/>. The probes are injected so callers
    /// (and tests) control the side effects; <paramref name="fileExists"/> defaults to <see cref="File.Exists"/>.
    /// </summary>
    public static FilePath Resolve(string baseDirectory, string applicationDataRoot,
        Func<string, bool> isDirectoryWritable, Func<string, bool>? fileExists = null) =>
        !IsVelopackCurrentFolder(baseDirectory, fileExists ?? File.Exists) && isDirectoryWritable(baseDirectory)
            ? NextToExecutable(baseDirectory)
            : ForApplicationDataRoot(applicationDataRoot);

    /// <summary>
    /// Whether <paramref name="baseDirectory"/> is the <c>current</c> folder of a Velopack install (an <c>Update.exe</c>
    /// sits beside it). Velopack replaces that folder wholesale on each update, taking a log kept there with it.
    /// </summary>
    public static bool IsVelopackCurrentFolder(string baseDirectory, Func<string, bool> fileExists)
    {
        var folder = new FilePath(baseDirectory.TrimEnd('\\', '/'));
        if (!string.Equals(folder.NoPath, "current", StringComparison.OrdinalIgnoreCase))
            return false;

        return fileExists(folder.GetDirectoryContainingThis().FullPath.TrimEnd('/') + "/Update.exe");
    }
}
