using AnimationEditor.Core.IO;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Conversions;

/// <summary>The parts of a conversion that need the running editor. Each returns why it failed, or null.</summary>
/// <param name="SaveEdits">Saves the edits the tab of an <c>.achx</c> holds and its file lacks.</param>
/// <param name="Recycle">Moves a file to the Recycle Bin.</param>
/// <param name="UpdateOpenSequence">Points an open <c>.seqj</c> at the <c>.achj</c> of each given <c>.achx</c>, through its document.</param>
public sealed record ConversionSteps(
    Func<string, string?> SaveEdits,
    Func<string, string?> Recycle,
    Func<string, IReadOnlyCollection<string>, string?> UpdateOpenSequence)
{
    /// <summary>Called right after an <c>.achx</c> went to the Recycle Bin.</summary>
    public Action<string> Recycled { get; init; } = _ => { };

    /// <summary>Called with what is being done.</summary>
    public Action<ConversionProgress> Progress { get; init; } = _ => { };
}

/// <summary>What a conversion is doing, and how many of how many; a <paramref name="Total"/> of 0 is not counted.</summary>
public sealed record ConversionProgress(string Doing, int Current = 0, int Total = 0)
{
    public string Text => Total == 0 ? Doing : $"{Doing} {Current} of {Total}";
}

/// <summary>What a conversion did.</summary>
/// <param name="Converted">The <c>.achx</c> files now replaced by their <c>.achj</c>.</param>
/// <param name="Updated">The <c>.seqj</c> files now using them.</param>
/// <param name="Differences">Each thing that is not what the plan said.</param>
public sealed record ConversionOutcome(IReadOnlyList<string> Converted, IReadOnlyList<string> Updated, IReadOnlyList<string> Differences)
{
    /// <summary>The sentence for the status bar, or for the problem banner when something differs from the plan.</summary>
    public string Message(AchxConversionPlan plan)
    {
        if (Differences.Count > 0)
            return $"⚠ Converted {Converted.Count}, {Plural.Format(Differences.Count, "problem")}: {string.Join("; ", Differences)}";
        int skipped = plan.Skipped.Count();
        return $"Converted {Converted.Count} to .achj"
            + (Updated.Count > 0 ? $"; updated {Plural.Format(Updated.Count, "sequence file")}" : "")
            + (skipped > 0 ? $"; left {skipped} alone." : ".");
    }
}

/// <summary>Runs an <see cref="AchxConversionPlan"/> (#2135).</summary>
public static class AchxConversion
{
    /// <summary>
    /// Writes the <c>.achj</c> files, points the sequences at them, and only then recycles each <c>.achx</c>. One that a
    /// sequence still uses, unless the plan says that one cannot be updated, is kept, so nothing that sequence plays goes missing.
    /// </summary>
    public static ConversionOutcome Run(AchxConversionPlan plan, ConversionSteps steps)
    {
        var differences = new List<string>();
        var converting = plan.Converted.ToList();
        var written = new List<PlannedFile>();
        for (int i = 0; i < converting.Count; i++)
        {
            steps.Progress(new ConversionProgress("Converting", i + 1, converting.Count));
            if (Write(converting[i], steps, differences)) written.Add(converting[i]);
        }

        var ready = SequenceSourcePaths.FileSet(written.Select(file => file.Source));
        var kept = SequenceSourcePaths.FileSet([]);
        var updated = new List<string>();
        var failed = new HashSet<string>();
        var updating = plan.Updated.ToList();
        foreach (var sequence in updating)
        {
            steps.Progress(new ConversionProgress("Updating sequence", updated.Count + failed.Count + 1, updating.Count));
            var plays = sequence.Plays.Where(ready.Contains).ToList();
            if (plays.Count == 0) continue;
            var error = sequence.IsOpen
                ? steps.UpdateOpenSequence(sequence.Path, plays)
                : UpdateFile(sequence.Path, plays, steps.Recycle);
            if (error is null)
            {
                updated.Add(sequence.Path);
                continue;
            }
            kept.UnionWith(plays);
            failed.Add(sequence.Path);
            differences.Add($"{Name(sequence.Path)}: not updated ({error.TrimEnd('.')}). {Kept(plays)}");
        }

        // What plays an original now, whatever the plan knew.
        var listed = plan.NotUpdated.Select(sequence => sequence.Path).ToHashSet();
        foreach (var sequence in AchxConversionPlan.FindSequences(plan.SearchedFolder, ready, OpenDocuments.None))
        {
            if (sequence.Plays.Count == 0 || listed.Contains(sequence.Path) || failed.Contains(sequence.Path)) continue;
            kept.UnionWith(sequence.Plays);
            differences.Add($"{Name(sequence.Path)}: plays {Names(sequence.Plays)} since the plan. {Kept(sequence.Plays)}");
        }

        var converted = new List<string>();
        var recycling = written.Where(file => !kept.Contains(new FilePath(file.Source).FullPath)).ToList();
        for (int i = 0; i < recycling.Count; i++)
        {
            var file = recycling[i];
            steps.Progress(new ConversionProgress("Moving to the Recycle Bin", i + 1, recycling.Count));
            if (Recycle(file.Source, steps.Recycle) is { } error)
            {
                differences.Add($"{Name(file.Source)}: converted, but the original could not be recycled ({error})");
                continue;
            }
            converted.Add(file.Source);
            steps.Recycled(file.Source);
        }

        steps.Progress(new ConversionProgress("Comparing"));
        differences.AddRange(CompareSequences(plan, converted, updated));
        return new ConversionOutcome(converted, updated, differences);
    }

    private static string Kept(IReadOnlyCollection<string> files) =>
        $"{Names(files)} {Plural.Noun(files.Count, "is", "are")} kept beside the .achj. To finish, delete the .achj and convert again";

    /// <summary>Recycles the file, and does not take the Recycle Bin's word for it.</summary>
    private static string? Recycle(string path, Func<string, string?> recycle)
    {
        WaitUntilFree(path);
        try
        {
            return recycle(path) ?? (File.Exists(path) ? "it is still there" : null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex.Message;
        }
    }

    // The editor reads the files meanwhile (a thumbnail, a live reload), which would refuse the move. Those take no time.
    private static void WaitUntilFree(string path)
    {
        for (int i = 0; i < 10; i++)
        {
            try
            {
                using var free = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.None);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(50);
            }
        }
    }

    private static bool Write(PlannedFile file, ConversionSteps steps, List<string> differences)
    {
        if (file.SavesEdits && steps.SaveEdits(file.Source) is { } reason)
        {
            differences.Add($"{Name(file.Source)}: not converted, its unsaved edits could not be saved ({reason})");
            return false;
        }
        if (!file.SavesEdits && HasChanged(file))
        {
            differences.Add($"{Name(file.Source)}: not converted, it is not the file the plan is for any more");
            return false;
        }
        if (file.SavesEdits && LosesMore(file))
        {
            differences.Add($"{Name(file.Source)}: not converted, with its edits saved it loses more than the plan says. Convert it again");
            return false;
        }
        var result = AchxToAchjConverter.Convert(file.Source);
        if (result.Status != AchxConversionStatus.Converted)
        {
            differences.Add($"{Name(file.Source)}: {result.Error ?? $"not converted, {Name(file.Target)} already exists"}");
            return false;
        }
        if (SameContent(file)) return true;
        differences.Add($"{Name(file.Source)}: kept, because {Name(file.Target)} does not read back the same");
        return false;
    }

    private static bool HasChanged(PlannedFile file)
    {
        try { return AchxConversionPlan.ContentOf(File.ReadAllText(file.Source)) != file.Content; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; } // converting it says why
    }

    // The plan is for the file without the edits.
    private static bool LosesMore(PlannedFile file)
    {
        try
        {
            string text = File.ReadAllText(file.Source);
            return new AchxElementCheck().Drops(XDocument.Parse(text), AnimationFileLoader.Parse(text, new FilePath(file.Source))).Except(file.Drops).Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or XmlException) { return false; } // converting it says why
    }

    // What is not kept is what the plan says: it is for this text, or the text was compared with it above.
    private static bool SameContent(PlannedFile file)
    {
        try
        {
            var original = AnimationFileLoader.Read(new FilePath(file.Source));
            // The plan read this text back and compared it, so only another text is read here.
            return File.ReadAllText(file.Target).ReplaceLineEndings() == original.ToJsonString().ReplaceLineEndings()
                || AnimationListComparison.FirstDifference(original, AnimationFileLoader.Read(new FilePath(file.Target))) is null;
        }
        catch (Exception) // whatever it is, it does not read back
        {
            return false;
        }
    }

    private static string? UpdateFile(string path, IReadOnlyCollection<string> files, Func<string, string?> recycle)
    {
        try
        {
            var save = SequenceListSave.FromFile(path);
            SequenceSourcePaths.ChangeExtension(save, new FilePath(path).GetDirectoryContainingThis(), files, AchxConversionPlan.TargetExtension);
            return ReplaceRecyclingOriginal(save, path, recycle);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Writes <paramref name="save"/> over <paramref name="path"/>. The old file goes to the Recycle Bin, once the new one is
    /// complete beside it. When that cannot take the old one's place after all, it stays there and the error names it.
    /// </summary>
    public static string? ReplaceRecyclingOriginal(SequenceListSave save, string path, Func<string, string?> recycle)
    {
        string complete = $"{path}.{Guid.NewGuid():N}.tmp";
        bool recycled = false;
        try
        {
            File.Copy(path, complete); // a save keeps the line endings of the file it replaces
            save.Save(complete);
            if (Recycle(path, recycle) is { } error) return error;
            recycled = true;
            File.Move(complete, path);
            return null;
        }
        catch (Exception ex) when (recycled && ex is IOException or UnauthorizedAccessException)
        {
            return $"{ex.Message} The original is in the Recycle Bin and the updated one is {Name(complete)}";
        }
        finally
        {
            if (!recycled && File.Exists(complete)) File.Delete(complete);
        }
    }

    // Read back from disk, so a step that reported success without doing its work shows too.
    private static IEnumerable<string> CompareSequences(AchxConversionPlan plan, IReadOnlyList<string> converted, IReadOnlyList<string> updated)
    {
        var gone = SequenceSourcePaths.FileSet(converted);
        var targets = converted.Select(source => Path.ChangeExtension(source, AchxConversionPlan.TargetExtension));
        var now = AchxConversionPlan.FindSequences(plan.SearchedFolder, targets, OpenDocuments.None).ToDictionary(sequence => sequence.Path);
        foreach (var sequence in plan.Updated.Where(sequence => updated.Contains(sequence.Path) && sequence.Plays.All(gone.Contains)))
        {
            int blocks = now.TryGetValue(sequence.Path, out var found) ? found.Blocks : 0;
            if (blocks < sequence.Blocks)
                yield return $"{Name(sequence.Path)}: {blocks} of its {Plural.Format(sequence.Blocks, "block")} {Plural.Noun(blocks, "plays", "play")} the .achj";
        }
    }

    private static string Name(string path) => new FilePath(path).NoPath;

    private static string Names(IReadOnlyCollection<string> paths)
    {
        var names = paths.Select(Name).ToList();
        return names.Count == 1 ? names[0] : $"{string.Join(", ", names.SkipLast(1))} and {names[^1]}";
    }
}
