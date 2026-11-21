namespace AnimationEditor.Editing;

/// <summary>
/// The text appended to a "no such name" error so a caller can fix a typo without another round trip: the
/// nearest valid name, or else the list of valid names.
/// </summary>
public static class NameHints
{
    private const int MaxListed = 10;

    /// <summary>
    /// <c> Did you mean 'Idle'?</c> when a candidate is a close match for <paramref name="name"/>, otherwise
    /// <c> Valid chains: Walk, Idle.</c> (<paramref name="plural"/> names the kind), or empty with no candidates.
    /// </summary>
    public static string For(string name, IEnumerable<string> candidates, string plural)
    {
        var list = candidates.Where(c => c.Length > 0).Distinct().ToList();
        if (list.Count == 0) return string.Empty;

        string? best = null;
        int bestDistance = int.MaxValue;
        foreach (var candidate in list)
        {
            int d = Distance(name.ToLowerInvariant(), candidate.ToLowerInvariant());
            if (d < bestDistance) (best, bestDistance) = (candidate, d);
        }

        int limit = Math.Max(2, name.Length / 3);
        if (best is not null && bestDistance <= limit && bestDistance < Math.Min(name.Length, best.Length))
            return $" Did you mean '{best}'?";

        var shown = string.Join(", ", list.Take(MaxListed));
        return list.Count > MaxListed
            ? $" Valid {plural}: {shown}, and {list.Count - MaxListed} more."
            : $" Valid {plural}: {shown}.";
    }

    private static int Distance(string a, string b)
    {
        var row = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) row[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            int diagonal = row[0];
            row[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int above = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                diagonal = above;
            }
        }
        return row[b.Length];
    }
}
