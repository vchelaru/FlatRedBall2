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
    /// Index immediately after the last anchor chain in project order.
    /// </summary>
    public static int ResolveBlockInsertIndexAfterAnchors(
        AnimationChainListSave acls,
        IReadOnlyList<AnimationChainSave> anchors)
    {
        int insertIndex = -1;
        foreach (var anchor in anchors)
        {
            int idx = acls.AnimationChains.IndexOf(anchor);
            if (idx >= 0)
                insertIndex = Math.Max(insertIndex, idx + 1);
        }
        return insertIndex >= 0 ? insertIndex : acls.AnimationChains.Count;
    }

    /// <summary>
    /// Index immediately after the last chain whose name appears in
    /// <paramref name="anchorNames"/>, scanning in project order.
    /// </summary>
    public static int ResolveBlockInsertIndexAfterAnchorNames(
        AnimationChainListSave acls,
        IReadOnlyCollection<string> anchorNames)
    {
        int insertIndex = acls.AnimationChains.Count;
        for (int i = 0; i < acls.AnimationChains.Count; i++)
        {
            if (anchorNames.Contains(acls.AnimationChains[i].Name))
                insertIndex = i + 1;
        }
        return insertIndex;
    }

    /// <summary>Inserts <paramref name="chains"/> contiguously at <paramref name="insertIndex"/>.</summary>
    public static void InsertChainBlockAt(
        AnimationChainListSave acls,
        IReadOnlyList<AnimationChainSave> chains,
        int insertIndex)
    {
        for (int i = 0; i < chains.Count; i++)
            acls.AnimationChains.Insert(Math.Min(insertIndex + i, acls.AnimationChains.Count), chains[i]);
    }

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
        IReadOnlyList<AnimationChainSave> pastedChains,
        IReadOnlyList<AnimationChainSave>? anchorChains = null)
    {
        if (pastedChains.Count == 0) return;

        var sourceNames = pastedChains.Select(c => c.Name).ToHashSet();
        int insertIndex = anchorChains is { Count: > 0 }
            ? ResolveBlockInsertIndexAfterAnchors(acls, anchorChains)
            : ResolveBlockInsertIndexAfterAnchorNames(acls, sourceNames);

        var existingNames = acls.AnimationChains.Select(c => c.Name).ToList();
        foreach (var chain in pastedChains)
        {
            chain.Name = StringFunctions.MakeStringUnique(chain.Name, existingNames, 2);
            existingNames.Add(chain.Name);
        }

        InsertChainBlockAt(acls, pastedChains, insertIndex);
    }
}
