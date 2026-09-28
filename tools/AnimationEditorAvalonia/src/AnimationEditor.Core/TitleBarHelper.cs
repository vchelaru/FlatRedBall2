using AnimationEditor.Core.Paths;

namespace AnimationEditor.Core;

/// <summary>Builds window title strings from an optional open-file path.</summary>
public static class TitleBarHelper
{
    /// <summary>The application name shown in the macOS system menu bar and other OS surfaces.</summary>
    public const string AppName = "AnimationEditor";

    /// <summary>
    /// Returns the window title for the animation editor.
    /// When <paramref name="filePath"/> is null or empty, returns <c>"AnimationEditor"</c>.
    /// Otherwise returns <c>"AnimationEditor - {filename}"</c> where <c>filename</c> is only
    /// the file name portion of the path (not the full path).
    /// Uses <see cref="FilePath.NoPath"/> so both forward and back slash paths work cross-platform.
    /// </summary>
    public static string BuildWindowTitle(string? filePath) =>
        string.IsNullOrEmpty(filePath)
            ? "AnimationEditor"
            : $"AnimationEditor - {new FilePath(filePath).NoPath}";

    /// <summary>
    /// Returns the folder to show as the active project root, or a placeholder when no
    /// folder is known yet. Prefers <paramref name="projectFolderPath"/> (explicitly set via
    /// File → Open Project Folder) and falls back to <paramref name="filesPanelRoot"/> (inferred
    /// from the open .achx, e.g. <c>ProjectManager.ResolveFilesPanelRoot</c>).
    /// </summary>
    public static string BuildActiveFolderDisplay(string? projectFolderPath, string? filesPanelRoot)
    {
        var path = projectFolderPath ?? filesPanelRoot;
        return string.IsNullOrEmpty(path) ? "No folder open" : path;
    }
}
