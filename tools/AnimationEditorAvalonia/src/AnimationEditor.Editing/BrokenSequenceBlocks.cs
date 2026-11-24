using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>Warnings for the sequence blocks a chain rename or delete left without a chain (#1949). Reads only.</summary>
internal static class BrokenSequenceBlocks
{
    private const int MaxListed = 10;

    /// <summary>The chain names of <paramref name="document"/> before <paramref name="ops"/> run, or null when no op renames or deletes a chain.</summary>
    public static IReadOnlyList<string>? ChainsAtRisk(AnimationChainListSave document, IReadOnlyList<EditOp> ops)
    {
        foreach (var op in ops)
            if (op.Spec.Name is "rename-chain" or "delete-chain")
                return document.AnimationChains.Select(c => c.Name).ToList();
        return null;
    }

    /// <summary>
    /// One warning per .seqj beside <paramref name="animationPath"/> (not in subfolders) with blocks playing one of
    /// <paramref name="chainsBefore"/> that <paramref name="document"/> no longer has. An unreadable .seqj is skipped.
    /// </summary>
    public static IEnumerable<string> Warnings(AnimationChainListSave document, string animationPath, IReadOnlyList<string> chainsBefore)
    {
        var missing = chainsBefore.Except(document.AnimationChains.Select(c => c.Name)).ToHashSet();
        if (missing.Count == 0) yield break;

        foreach (var seqj in SequenceFilesIn(Path.GetDirectoryName(animationPath)!))
        {
            var blocks = SeqjChainUsageScanner.FindMatches(seqj, animationPath, missing);
            if (blocks.Count == 0) continue;
            var listed = string.Join(", ", blocks.Take(MaxListed).Select(b => $"{b.Address} '{b.Chain}'"))
                + (blocks.Count > MaxListed ? $" and {blocks.Count - MaxListed} more" : "");
            yield return $"{Path.GetFileName(seqj)} now has {blocks.Count} block(s) playing a chain that {Path.GetFileName(animationPath)} no longer has: {listed}.";
        }
    }

    private static IEnumerable<string> SequenceFilesIn(string folder)
    {
        try
        {
            return Directory.GetFiles(folder).Where(SequenceFileReader.IsSequenceFile).Order(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return []; // the edit is already saved, so a folder that cannot be listed must not fail it
        }
    }
}
