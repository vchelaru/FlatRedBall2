namespace AnimationEditor.Core.IO;

/// <summary>
/// Determines whether the user should be prompted to copy a texture file into
/// the project folder.  Mirrors the "ask to copy" dialog logic from the WinForms
/// AnimationEditor (see <c>AppState.ProjectFolder</c> doc comment in AS05).
/// </summary>
public static class TextureCopyDecider
{
    /// <summary>
    /// Returns <see langword="true"/> when the user should be shown a "copy file?"
    /// dialog — i.e., the texture lives outside the project folder.
    /// </summary>
    /// <param name="texturePath">
    /// Absolute or relative path of the texture the user selected.
    /// If null or empty the texture has not been set, so no copy is needed.
    /// </param>
    /// <param name="projectFolder">
    /// Absolute path of the project folder.  If null or empty, no project context
    /// is available and the prompt should always be shown.
    /// </param>
    public static bool ShouldPromptToCopy(string? texturePath, string? projectFolder)
    {
        if (string.IsNullOrEmpty(texturePath)) return false;
        if (string.IsNullOrEmpty(projectFolder)) return true;

        // Normalise separators so forward- and back-slash variants compare equally.
        char sep = Path.DirectorySeparatorChar;
        string normTexture = texturePath.Replace('/', sep).Replace('\\', sep);
        string normFolder  = projectFolder.TrimEnd('/', '\\').Replace('/', sep).Replace('\\', sep)
                             + sep;

        return !normTexture.StartsWith(normFolder, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Overload for callers with two candidate "already inside" folders — e.g. the .achx
    /// folder and the broader project folder. Prompts only when the texture is outside both.
    /// </summary>
    public static bool ShouldPromptToCopy(string? texturePath, string? primaryFolder, string? secondaryFolder)
        => ShouldPromptToCopy(texturePath, primaryFolder) && ShouldPromptToCopy(texturePath, secondaryFolder);
}
