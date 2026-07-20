using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Filters discovered <c>.achx</c> entries for the Project tab's search box (#770 follow-up).
/// Matches anywhere in <see cref="AchxFileEntry.RelativePath"/> (not just the file name) so
/// typing a folder name narrows to that folder too.
/// </summary>
public static class AchxSearchFilter
{
    public static IReadOnlyList<AchxFileEntry> Filter(IReadOnlyList<AchxFileEntry> entries, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return entries;

        return entries
            .Where(e => e.RelativePath.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
