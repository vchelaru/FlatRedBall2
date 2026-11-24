using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.IO;

/// <summary>One block matched by <see cref="SeqjChainUsageScanner"/>; <paramref name="Block"/> is its index in the track.</summary>
public sealed record SeqjChainMatch(SequenceSave Sequence, SequenceTrackSave Track, int Block)
{
    /// <summary>The name of the chain the block plays.</summary>
    public string Chain => Track.Chains[Block].Chain;

    /// <summary>The block as <c>Sequence/Track/index</c>.</summary>
    public string Address => $"{Sequence.Name}/{Track.Name}/{Block}";
}

/// <summary>Finds the blocks of a <c>.seqj</c> that play given chains of one animation file (#1949).</summary>
public static class SeqjChainUsageScanner
{
    /// <summary>
    /// <see cref="FindMatches(SequenceListSave, string, string, IReadOnlyCollection{string})"/> on the file at
    /// <paramref name="seqjPath"/>. Returns empty (never throws) for a missing, unreadable or malformed file.
    /// </summary>
    public static IReadOnlyList<SeqjChainMatch> FindMatches(
        string seqjPath, string animationPath, IReadOnlyCollection<string> chains)
    {
        try { return FindMatches(SequenceListSave.FromFile(seqjPath), seqjPath, animationPath, chains); }
        catch { return []; }
    }

    /// <summary>
    /// The blocks of <paramref name="sequences"/>, stored at <paramref name="seqjPath"/>, that play one of
    /// <paramref name="chains"/> from the file at <paramref name="animationPath"/>. Sources match by resolved path, so a
    /// same-named chain of another file does not count, and neither does a block whose source is not in the library.
    /// </summary>
    public static IReadOnlyList<SeqjChainMatch> FindMatches(
        SequenceListSave sequences, string seqjPath, string animationPath, IReadOnlyCollection<string> chains)
    {
        var target = new FilePath(animationPath).FullPath;
        var folder = new FilePath(seqjPath).GetDirectoryContainingThis().FullPath;
        var matches = new List<SeqjChainMatch>();
        foreach (var sequence in sequences.Sequences)
            foreach (var track in sequence.Tracks)
                for (int i = 0; i < track.Chains.Count; i++)
                {
                    var block = track.Chains[i];
                    if (chains.Contains(block.Chain)
                        && sequences.Sources.Contains(block.Source, StringComparer.OrdinalIgnoreCase)
                        && IsTarget(block.Source))
                        matches.Add(new SeqjChainMatch(sequence, track, i));
                }
        return matches;

        bool IsTarget(string source)
        {
            try { return string.Equals(SequenceClipboard.AbsolutePath(folder, source), target, PathComparison); }
            catch (InvalidOperationException) { return false; } // more ../ than folders
        }
    }

    // FilePath compares ignoring case everywhere; here two files differing only by case must stay apart where they can both exist.
    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
