using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Filters scanned file entries for a folder-tree search box (the Project tab's <c>.achx</c> tree
/// and the Images tab's PNG tree). Matches anywhere in the entry's relative path (not just the
/// file name) so typing a folder name narrows to that folder too.
/// </summary>
public static class RelativePathSearchFilter
{
    public static IReadOnlyList<T> Filter<T>(IReadOnlyList<T> entries, Func<T, string> relativePath, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return entries;

        return entries
            .Where(e => relativePath(e).Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
