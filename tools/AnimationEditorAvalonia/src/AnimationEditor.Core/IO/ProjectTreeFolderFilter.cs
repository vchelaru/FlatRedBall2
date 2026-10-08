using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Picks which scanned folders the Project tab shows on top of the folders that hold animation
/// files (#1332). Off by default, so the tree lists only folders with animation files.
/// </summary>
public static class ProjectTreeFolderFilter
{
    /// <summary>
    /// Returns the folder paths to pass to <see cref="AchxFolderTreeBuilder.Build"/>. Empty while
    /// <paramref name="searchQuery"/> is non-blank: a search lists matching files only, and
    /// every empty folder would bury them.
    /// </summary>
    public static IReadOnlyList<string> Select(
        IEnumerable<string> folderPaths, bool showAllFolders, bool excludeBinObj, string searchQuery)
    {
        if (!showAllFolders || !string.IsNullOrWhiteSpace(searchQuery))
            return Array.Empty<string>();

        return folderPaths.Where(path => !excludeBinObj || !BinObjPathFilter.IsExcluded(path)).ToList();
    }
}
