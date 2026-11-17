namespace AnimationEditor.Core.Paths;

/// <summary>
/// Display labels for a list of recent files or folders. An entry's label is its name; entries whose names
/// clash (ignoring case) get the fewest nearest parent segments that make every clashing entry unique,
/// for example <c>Content - CrankyChibiCthulu</c>. Plain text, so native menus can show it.
/// </summary>
public static class RecentEntryLabels
{
    private const string Separator = " - ";

    /// <summary>One label per path, in the same order. Backslashes and trailing slashes are normalized.</summary>
    public static List<string> Build(IReadOnlyList<string> paths)
    {
        var segments = paths
            .Select(p => new FilePath(p).FullPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
            .ToList();
        var labels = segments.Select(s => s.Length > 0 ? s[^1] : "").ToList();

        foreach (var group in Enumerable.Range(0, paths.Count)
                     .GroupBy(i => labels[i], StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1))
        {
            var indices = group.ToList();
            foreach (int i in indices)
            {
                // Each entry takes the fewest parents that tell it apart from every other clashing entry.
                int take = 1;
                while (take < segments[i].Length - 1 && indices.Any(j => j != i && Key(segments[j], take) == Key(segments[i], take)))
                    take++;
                var parents = ParentsOf(segments[i], take);
                if (parents.Length > 0)
                    labels[i] += Separator + string.Join("/", parents);
            }
        }
        return labels;
    }

    private static string Key(string[] segments, int take) => string.Join("/", ParentsOf(segments, take)).ToLowerInvariant();

    private static string[] ParentsOf(string[] segments, int take)
    {
        int available = Math.Max(0, segments.Length - 1);
        return segments.Skip(available - Math.Min(take, available)).Take(Math.Min(take, available)).ToArray();
    }
}
