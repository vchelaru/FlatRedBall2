using System.Collections.Generic;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// Remembers which sequence of each <c>.seqj</c> was selected, by name, in the editor settings. A name survives
/// a rename the editor makes (the caller remembers again after it) and a reorder, which an index would not.
/// </summary>
public static class SequenceSelectionMemory
{
    /// <summary>
    /// Records the sequence at <paramref name="selectedIndex"/> of <paramref name="names"/> under <paramref name="fileKey"/>.
    /// The first sequence is what a file opens on anyway, so it clears the entry. Returns true when <paramref name="store"/> changed.
    /// </summary>
    public static bool Remember(Dictionary<string, string> store, string fileKey, IReadOnlyList<string> names, int selectedIndex)
    {
        if (selectedIndex < 0 || selectedIndex >= names.Count) return false;
        if (selectedIndex == 0) return store.Remove(fileKey);
        string name = names[selectedIndex];
        if (store.TryGetValue(fileKey, out var stored) && stored == name) return false;
        store[fileKey] = name;
        return true;
    }

    /// <summary>The index to select when <paramref name="fileKey"/> opens: its remembered sequence, or the first when that no longer exists (-1 for an empty file).</summary>
    public static int IndexToRestore(Dictionary<string, string> store, string fileKey, IReadOnlyList<string> names)
    {
        if (names.Count == 0) return -1;
        if (!store.TryGetValue(fileKey, out var name)) return 0;
        for (int i = 0; i < names.Count; i++)
            if (names[i] == name) return i;
        return 0;
    }
}
