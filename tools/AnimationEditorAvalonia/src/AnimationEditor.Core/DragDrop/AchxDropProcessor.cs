using AnimationEditor.Core.Paths;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.DragDrop;

/// <summary>
/// Classifies an OS file-drop payload for the "open dropped .achx files as tabs" path.
/// UI-independent so it can be unit-tested without Avalonia. Companion to
/// <see cref="TextureDropProcessor"/>, which handles PNG-onto-tree texture drops.
/// </summary>
public static class AchxDropProcessor
{
    /// <summary>
    /// Returns the dropped paths that are <c>.achx</c> files (case-insensitive), preserving
    /// their original order and skipping null/blank entries. Non-.achx paths (e.g. PNG
    /// textures) are filtered out so a mixed drop opens the .achx files and ignores the rest.
    /// </summary>
    public static IReadOnlyList<string> SelectAchxFiles(IEnumerable<string?>? droppedFilePaths) =>
        droppedFilePaths is null
            ? new List<string>()
            : droppedFilePaths
                .Where(p => !string.IsNullOrWhiteSpace(p) && new FilePath(p).Extension == "achx")
                .Select(p => p!)
                .ToList();

    /// <summary>True when at least one dropped path is an <c>.achx</c> file.</summary>
    public static bool ContainsAchx(IEnumerable<string?>? droppedFilePaths) =>
        SelectAchxFiles(droppedFilePaths).Count > 0;
}
