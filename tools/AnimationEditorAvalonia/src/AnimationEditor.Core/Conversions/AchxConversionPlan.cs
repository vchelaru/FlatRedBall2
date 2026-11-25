using AnimationEditor.Core.IO;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Conversions;

/// <summary>What the editor has open when a conversion is planned, which the files on disk do not say.</summary>
/// <param name="Animations">Each open <c>.achx</c>: whether its tab holds edits a save could not write, and why they still cannot be saved (null when they may be).</param>
/// <param name="Sequences">Each open <c>.seqj</c>, with a copy of what its tab holds when its file could not be given that, else null.</param>
public sealed record OpenDocuments(
    IReadOnlyDictionary<FilePath, (bool UnsavedEdits, string? CannotSave)> Animations,
    IReadOnlyDictionary<FilePath, SequenceListSave?> Sequences)
{
    public static OpenDocuments None { get; } = new(
        new Dictionary<FilePath, (bool, string?)>(), new Dictionary<FilePath, SequenceListSave?>());
}

/// <param name="Why">The reason in a few words.</param>
/// <param name="Detail">The whole reason; empty when <paramref name="Why"/> says it all.</param>
public sealed record ConversionProblem(string Why, string Detail = "");

/// <summary>
/// One <c>.achx</c> of a plan. One that converts was converted in memory and read back, and every animation, frame, shape
/// and event compared equal; one that did not is skipped.
/// </summary>
/// <param name="Problem">Why the file is left alone, or null when it is converted.</param>
/// <param name="IsOpen">Its tab closes.</param>
/// <param name="SavesEdits">Its tab holds edits its file lacks, which are saved first.</param>
public sealed record PlannedFile(
    string Source, string Target, ConversionProblem? Problem = null, int Animations = 0, int Frames = 0, bool IsOpen = false, bool SavesEdits = false)
{
    public bool Converts => Problem is null;
}

/// <summary>One <c>.seqj</c> that uses a file of a plan, or that could not be read to tell.</summary>
/// <param name="Plays">The converted files it uses, as full paths; empty when it could not be read.</param>
/// <param name="Blocks">How many of its blocks play one of them.</param>
/// <param name="Problem">Why it cannot be updated, or null when it is.</param>
public sealed record PlannedSequence(
    string Path, IReadOnlyCollection<string> Plays, int Blocks, bool IsOpen = false, ConversionProblem? Problem = null)
{
    public bool Updates => Problem is null;
}

/// <summary>
/// Everything Convert to .achj will do, built without writing anything (#2135). <see cref="AchxConversion.Run"/> runs it
/// and compares what happened with it.
/// </summary>
/// <param name="SearchedFolder">The folder searched, subfolders included, for the sequences that use the files.</param>
public sealed record AchxConversionPlan(
    string SearchedFolder, IReadOnlyList<PlannedFile> Files, IReadOnlyList<PlannedSequence> Sequences)
{
    public const string TargetExtension = ".achj";

    public IEnumerable<PlannedFile> Converted => Files.Where(file => file.Converts);
    public IEnumerable<PlannedFile> Skipped => Files.Where(file => !file.Converts);
    public IEnumerable<PlannedSequence> Updated => Sequences.Where(sequence => sequence.Updates);
    public IEnumerable<PlannedSequence> NotUpdated => Sequences.Where(sequence => !sequence.Updates);

    public static AchxConversionPlan Build(IEnumerable<string> sources, string searchedFolder, OpenDocuments open)
    {
        var files = sources.Select(source => PlanFile(source, open)).ToList();
        return new AchxConversionPlan(searchedFolder, files,
            FindSequences(searchedFolder, files.Where(file => file.Converts).Select(file => file.Source), open));
    }

    private static PlannedFile PlanFile(string source, OpenDocuments open)
    {
        string target = Path.ChangeExtension(source, TargetExtension);
        bool isOpen = open.Animations.TryGetValue(new FilePath(source), out var tab);
        var skipped = new PlannedFile(source, target, IsOpen: isOpen);

        if (File.Exists(target)) return skipped with { Problem = new($"{Path.GetFileName(target)} exists") };
        if (tab.UnsavedEdits && (tab.CannotSave ?? WriteProtection(source)) is { } reason)
            return skipped with { Problem = new("Unsaved edits can't be saved", reason) };
        try
        {
            var list = AnimationFileLoader.Read(new FilePath(source));
            if (AnimationListComparison.FirstDifference(list, AnimationChainListSave.FromJsonString(list.ToJsonString())) is { } difference)
                return skipped with { Problem = new($"Not exact: {difference}", "The .achj would not hold the same data there.") };
            return new PlannedFile(source, target, null, list.AnimationChains.Count,
                list.AnimationChains.Sum(chain => chain.Frames.Count), isOpen, tab.UnsavedEdits);
        }
        catch (Exception ex)
        {
            return skipped with { Problem = new("Can't be read", ex.Message) };
        }
    }

    /// <summary>
    /// The <c>.seqj</c> files under <paramref name="folder"/>, not under <c>bin</c> or <c>obj</c>, that use one of
    /// <paramref name="animationFiles"/>, and those that could not be read to tell. None when there is nothing to look for.
    /// </summary>
    internal static List<PlannedSequence> FindSequences(string folder, IEnumerable<string> animationFiles, OpenDocuments open)
    {
        var wanted = SequenceSourcePaths.FileSet(animationFiles);
        var found = new List<PlannedSequence>();
        if (wanted.Count == 0) return found;

        var everywhere = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 };
        foreach (var path in Directory.EnumerateFiles(folder, "*", everywhere)
                     .Where(path => AchxFolderScanner.IsSequencePath(path) && !BinObjPathFilter.IsExcluded(Path.GetRelativePath(folder, path)))
                     .Order(StringComparer.OrdinalIgnoreCase))
        {
            bool isOpen = open.Sequences.TryGetValue(new FilePath(path), out var unsaved);
            SequenceListSave sequences;
            try
            {
                sequences = unsaved ?? SequenceListSave.FromFile(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                found.Add(new PlannedSequence(path, [], 0, isOpen, new("Can't be read", $"What it plays is not known. {ex.Message}")));
                continue;
            }

            var (plays, blocks) = SequenceSourcePaths.Using(sequences, new FilePath(path).GetDirectoryContainingThis(), wanted);
            if (plays.Count == 0) continue;
            found.Add(new PlannedSequence(path, plays, blocks, isOpen,
                unsaved is not null ? new("Edits can't be saved", "Its tab holds edits that its file does not.")
                : WriteProtection(path) is { } reason ? new("Read-only", reason) : null));
        }
        return found;
    }

    /// <summary>Why a file cannot be replaced, as far as that shows without trying, or null.</summary>
    private static string? WriteProtection(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0) return "The file is read-only.";
        return !OperatingSystem.IsWindows() && (File.GetUnixFileMode(Path.GetDirectoryName(path)!) & UnixFileMode.UserWrite) == 0
            ? "Its folder is read-only."
            : null;
    }
}
