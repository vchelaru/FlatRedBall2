using System.IO;

namespace AnimationEditor.Core;

/// <summary>Builds window title strings from an optional open-file path.</summary>
public static class TitleBarHelper
{
    /// <summary>
    /// Returns the window title for the animation editor.
    /// When <paramref name="filePath"/> is null or empty, returns <c>"AnimationEditor"</c>.
    /// Otherwise returns <c>"AnimationEditor - {filename}"</c> where <c>filename</c> is only
    /// the file name portion of the path (not the full path).
    /// </summary>
    public static string BuildWindowTitle(string? filePath) =>
        string.IsNullOrEmpty(filePath)
            ? "AnimationEditor"
            : $"AnimationEditor - {Path.GetFileName(filePath)}";
}
