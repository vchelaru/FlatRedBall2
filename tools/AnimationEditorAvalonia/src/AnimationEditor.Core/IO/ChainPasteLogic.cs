using AnimationEditor.Core.Utilities;
using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Places pasted animation chains into an <see cref="AnimationChainListSave"/>.
/// Kept separate from clipboard (de)serialization so the placement rule is
/// unit-testable without touching the app layer or the system clipboard.
/// </summary>
public static class ChainPasteLogic
{
    /// <summary>
    /// Renames each pasted chain to be unique within <paramref name="acls"/>, then inserts
    /// the block directly below the lowest (last) source chain the copy was made from —
    /// matched by the names the pasted chains still carry from the clipboard. When no
    /// source chain is present (e.g. pasting into a different project, or the source was
    /// deleted), the block is appended at the end. The pasted block's relative order is
    /// preserved.
    /// </summary>
    public static void InsertPastedChains(
        AnimationChainListSave acls,
        IReadOnlyList<AnimationChainSave> pastedChains)
    {
        if (pastedChains.Count == 0) return;

        // The pasted chains still carry their source names at this point (renaming
        // happens below), so they double as the lookup key for the source rows.
        var sourceNames = pastedChains.Select(c => c.Name).ToHashSet();
        int insertIndex = acls.AnimationChains.Count;
        for (int i = 0; i < acls.AnimationChains.Count; i++)
        {
            if (sourceNames.Contains(acls.AnimationChains[i].Name))
                insertIndex = i + 1;
        }

        var existingNames = acls.AnimationChains.Select(c => c.Name).ToList();
        foreach (var chain in pastedChains)
        {
            chain.Name = StringFunctions.MakeStringUnique(chain.Name, existingNames, 2);
            existingNames.Add(chain.Name);
            acls.AnimationChains.Insert(insertIndex, chain);
            insertIndex++;
        }
    }
}
