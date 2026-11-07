using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Decides whether a folder row's "Convert Animations to .achj" item has anything to act on. Works
/// on the relative paths the Project tree already holds from its scan, so opening the menu touches
/// no disk. Mirrors what the conversion itself does: it takes every <c>.achx</c> under the folder
/// except under <c>bin</c>/<c>obj</c>, and <see cref="AchxToAchjConverter"/> skips a source whose
/// <c>.achj</c> already exists.
/// </summary>
public static class ProjectFolderConvertRule
{
    /// <param name="projectRelativePaths">Every scanned file's forward-slash path from the project root.</param>
    /// <param name="folderRelativePath">The right-clicked folder's path from the project root; empty for the root.</param>
    /// <returns>True when at least one <c>.achx</c> under the folder would actually be converted.</returns>
    public static bool HasConvertibleAchx(IEnumerable<string> projectRelativePaths, string folderRelativePath)
    {
        var prefix = folderRelativePath.Length == 0 ? "" : folderRelativePath + "/";
        var inFolder = projectRelativePaths
            .Where(p => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var achj = inFolder
            .Where(p => p.EndsWith(".achj", StringComparison.OrdinalIgnoreCase))
            .Select(p => p[..^".achj".Length])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return inFolder.Any(p =>
            p.EndsWith(".achx", StringComparison.OrdinalIgnoreCase)
            && !BinObjPathFilter.IsExcluded(p[prefix.Length..])
            && !achj.Contains(p[..^".achx".Length]));
    }
}
