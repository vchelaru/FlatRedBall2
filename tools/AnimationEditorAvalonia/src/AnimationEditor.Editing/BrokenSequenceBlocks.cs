using AnimationEditor.Core.IO;

namespace AnimationEditor.Editing;

/// <summary>Warnings for the sequence blocks a chain rename or delete left without a chain (#1949). Reads only.</summary>
internal static class BrokenSequenceBlocks
{
    private const int MaxListed = 10;

    /// <summary>
    /// One warning per .seqj beside <paramref name="animationPath"/> (not in subfolders) with blocks playing one of
    /// <paramref name="missingChains"/>, names the animation file no longer has. An unreadable .seqj is skipped.
    /// </summary>
    public static IEnumerable<string> Warnings(string animationPath, IReadOnlyCollection<string> missingChains)
    {
        if (missingChains.Count == 0) yield break;

        foreach (var seqj in SequenceFilesIn(Path.GetDirectoryName(animationPath)!))
        {
            var blocks = SeqjChainUsageScanner.FindMatches(seqj, animationPath, missingChains);
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
