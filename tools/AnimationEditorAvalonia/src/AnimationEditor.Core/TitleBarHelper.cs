using AnimationEditor.Core.Paths;

namespace AnimationEditor.Core;

/// <summary>Builds window title strings from an optional open-file path.</summary>
public static class TitleBarHelper
{
    /// <summary>The application name shown in the macOS system menu bar and other OS surfaces.</summary>
    public const string AppName = "Animation Editor";

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
}
