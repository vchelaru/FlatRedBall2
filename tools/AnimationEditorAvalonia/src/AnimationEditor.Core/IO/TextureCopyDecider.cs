using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Determines whether the user should be prompted to copy a texture file next to the
/// loaded .achx.  Mirrors the "ask to copy" dialog logic from the WinForms
/// AnimationEditor.
/// </summary>
public static class TextureCopyDecider
{
    /// <summary>
    /// Returns <see langword="true"/> when assigning <paramref name="texturePath"/> should show the
    /// "copy file?" dialog, given the editor's current project state. No prompt when the .achx has
    /// never been saved (nowhere to copy to yet), when the texture already sits under the .achx's
    /// own folder, or when the .achx and the texture are both under the open project folder — a
    /// project is shared as a whole, so a path relative to it is already portable.
    /// </summary>
    public static bool ShouldPromptToCopyForProject(IProjectManager projectManager, string? texturePath)
    {
        if (string.IsNullOrEmpty(projectManager.FileName)) return false;

        var achxFolder = new FilePath(projectManager.FileName).GetDirectoryContainingThis().FullPath;
        return ShouldPromptToCopy(texturePath, achxFolder, projectManager.ProjectFolderPath);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the user should be shown a "copy file?"
    /// dialog — i.e., the texture lives outside <paramref name="folder"/>.
    /// </summary>
    /// <param name="texturePath">
    /// Absolute or relative path of the texture the user selected.
    /// If null or empty the texture has not been set, so no copy is needed.
    /// </param>
    /// <param name="folder">
    /// Absolute path of the folder the texture may already live in.  If null or empty,
    /// no folder context is available and the prompt should always be shown.
    /// </param>
    public static bool ShouldPromptToCopy(string? texturePath, string? folder)
    {
        if (string.IsNullOrEmpty(texturePath)) return false;
        if (string.IsNullOrEmpty(folder)) return true;

        return !IsInside(texturePath, folder);
    }

    /// <summary>
    /// Overload that also lets <paramref name="projectFolder"/> suppress the prompt — but only when
    /// <paramref name="achxFolder"/> is inside it too. An .achx outside the project would store a
    /// "../.." climb to reach the project's textures, which is what the prompt exists to avoid.
    /// </summary>
    public static bool ShouldPromptToCopy(string? texturePath, string? achxFolder, string? projectFolder)
    {
        if (!ShouldPromptToCopy(texturePath, achxFolder)) return false;
        if (string.IsNullOrEmpty(achxFolder) || string.IsNullOrEmpty(projectFolder)) return true;

        return !(IsInside(texturePath!, projectFolder) && IsInside(achxFolder, projectFolder));
    }

    /// <summary>
    /// Path-prefix containment, tolerant of mixed separators, casing, and trailing separators.
    /// A path equal to the folder counts as inside so a folder can be tested against itself.
    /// </summary>
    private static bool IsInside(string path, string folder)
    {
        // Normalise separators so forward- and back-slash variants compare equally.
        char sep = Path.DirectorySeparatorChar;
        string normPath   = path.Replace('/', sep).Replace('\\', sep).TrimEnd(sep);
        string normFolder = folder.Replace('/', sep).Replace('\\', sep).TrimEnd(sep);

        return normPath.Equals(normFolder, StringComparison.OrdinalIgnoreCase)
            || normPath.StartsWith(normFolder + sep, StringComparison.OrdinalIgnoreCase);
    }
}
