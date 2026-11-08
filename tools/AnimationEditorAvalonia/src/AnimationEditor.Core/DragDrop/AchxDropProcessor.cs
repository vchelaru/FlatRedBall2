using AnimationEditor.Core.Paths;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.DragDrop;

/// <summary>
/// Classifies an OS file-drop payload for the "open dropped project files as tabs" path --
/// <c>.achx</c>/<c>.achj</c>, native-tsx (<c>.tsx</c>, issue #1140) projects and <c>.seqj</c> sequence files alike, since
/// <c>MainWindow.LoadAnimationFileAsync</c> opens any of them once selected here. The class keeps its old name.
/// UI-independent so it can be unit-tested without Avalonia. Companion to
/// <see cref="TextureDropProcessor"/>, which handles PNG-onto-tree texture drops.
/// </summary>
public static class AchxDropProcessor
{
    /// <summary>
    /// Returns the dropped paths that are <c>.achx</c>, <c>.achj</c>, <c>.tsx</c> or <c>.seqj</c> files
    /// (case-insensitive), preserving their original order and skipping null/blank entries. Other
    /// paths (e.g. PNG textures) are filtered out so a mixed drop opens the project files and
    /// ignores the rest.
    /// </summary>
    public static IReadOnlyList<string> SelectAchxFiles(IEnumerable<string?>? droppedFilePaths) =>
        droppedFilePaths is null
            ? new List<string>()
            : droppedFilePaths
                .Where(p => !string.IsNullOrWhiteSpace(p) &&
                    (new FilePath(p).Extension == "achx" || new FilePath(p).Extension == "achj"
                        || new FilePath(p).Extension == "tsx" || new FilePath(p).Extension == "seqj"))
                .Select(p => p!)
                .ToList();

    /// <summary>True when at least one dropped path is an <c>.achx</c>, <c>.achj</c>, <c>.tsx</c> or <c>.seqj</c>
    /// file.</summary>
    public static bool ContainsAchx(IEnumerable<string?>? droppedFilePaths) =>
        SelectAchxFiles(droppedFilePaths).Count > 0;
}
