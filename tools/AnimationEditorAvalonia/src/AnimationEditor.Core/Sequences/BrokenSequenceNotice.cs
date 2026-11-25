using AnimationEditor.Core.IO;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// The chain names <paramref name="Document"/> lost to one user action. <paramref name="Verb"/> starts the sentence about it ("Renaming").
/// </summary>
public sealed record RemovedChainNames(AnimationChainListSave Document, string Verb, IReadOnlyCollection<string> Names);

/// <summary>One <c>.seqj</c> and its blocks that a chain rename or delete left without a chain.</summary>
public sealed record BrokenSequenceFile(string Path, IReadOnlyList<SeqjChainMatch> Blocks);

/// <summary>The problem shown when a chain rename or delete leaves sequence blocks playing a chain their source no longer has (#1961).</summary>
public static class BrokenSequenceNotice
{
    private const int MaxPlaces = 3;
    private const int MaxFiles = 5;

    /// <summary>
    /// The <c>.seqj</c> files to look at: those of <paramref name="projectFiles"/>, the open project folder's files, or with
    /// none open (null) those beside <paramref name="animationPath"/>, not in subfolders. A folder that cannot be listed has none.
    /// </summary>
    public static IReadOnlyList<string> FilesToScan(string animationPath, IEnumerable<string>? projectFiles)
    {
        try
        {
            return (projectFiles ?? Directory.GetFiles(Path.GetDirectoryName(animationPath)!))
                .Where(AchxFolderScanner.IsSequencePath).Order(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// The banner text, or null when no file has a broken block: the edit, then each file with the places (sequence/track) of its
    /// blocks while those are few, else with how many it has.
    /// </summary>
    public static string? Message(RemovedChainNames removed, IEnumerable<BrokenSequenceFile> files) =>
        Banner(removed.Verb, removed.Names.Count == 1 ? $"'{removed.Names.First()}'" : Plural.Format(removed.Names.Count, "animation"), files);

    /// <summary>The same banner for a file that is gone: <paramref name="verb"/> is what removed <paramref name="path"/>.</summary>
    public static string? Message(string verb, string path, IEnumerable<BrokenSequenceFile> files) =>
        Banner(verb, Path.GetFileName(path), files);

    private static string? Banner(string verb, string subject, IEnumerable<BrokenSequenceFile> files)
    {
        var broken = files.Where(file => file.Blocks.Count > 0).ToList();
        if (broken.Count == 0) return null;

        bool named = broken.Sum(file => Places(file).Count) <= MaxPlaces;
        var listed = string.Join(", ", broken.Take(MaxFiles)
            .Select(file => $"{Path.GetFileName(file.Path)} ({(named ? string.Join(", ", Places(file)) : file.Blocks.Count)})"));
        int more = broken.Count - MaxFiles;
        return $"{verb} {subject} broke {Plural.Format(broken.Sum(file => file.Blocks.Count), "sequence block")}: {listed}"
            + (more > 0 ? $" and {more} more {Plural.Noun(more, "file")}." : ".");
    }

    private static List<string> Places(BrokenSequenceFile file) =>
        file.Blocks.Select(block => $"{block.Sequence.Name}/{block.Track.Name}").Distinct().ToList();
}
