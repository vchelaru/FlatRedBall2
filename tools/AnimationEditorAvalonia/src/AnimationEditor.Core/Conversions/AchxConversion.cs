using AnimationEditor.Core.IO;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Conversions;

/// <summary>The parts of a conversion that need the running editor. Each returns why it failed, or null.</summary>
/// <param name="SaveEdits">Saves the edits the tab of an <c>.achx</c> holds and its file lacks.</param>
/// <param name="Recycle">Moves a file to the Recycle Bin.</param>
/// <param name="UpdateOpenSequence">Points an open <c>.seqj</c> at the <c>.achj</c> of each given <c>.achx</c>, through its document.</param>
public sealed record ConversionSteps(
    Func<string, string?> SaveEdits,
    Func<string, string?> Recycle,
    Func<string, IReadOnlyCollection<string>, string?> UpdateOpenSequence);

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
    /// sequence of the plan still uses because its update failed is kept, so nothing that sequence plays goes missing.
    /// </summary>
    public static ConversionOutcome Run(AchxConversionPlan plan, ConversionSteps steps)
    {
        var differences = new List<string>();
        var written = plan.Converted.Where(file => Write(file, steps, differences)).ToList();
        var ready = SequenceSourcePaths.FileSet(written.Select(file => file.Source));
        var kept = SequenceSourcePaths.FileSet([]);
        var updated = new List<string>();

        foreach (var sequence in plan.Updated)
        {
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
            differences.Add($"{Name(sequence.Path)}: not updated ({error}), so {Names(plays)} stays for it, beside the new .achj");
        }

        var converted = new List<string>();
        foreach (var file in written.Where(file => !kept.Contains(new FilePath(file.Source).FullPath)))
        {
            if (steps.Recycle(file.Source) is { } error)
                differences.Add($"{Name(file.Source)}: converted, but the original could not be recycled ({error})");
            else
                converted.Add(file.Source);
        }

        differences.AddRange(CompareSequences(plan, converted, updated));
        return new ConversionOutcome(converted, updated, differences);
    }

    private static bool Write(PlannedFile file, ConversionSteps steps, List<string> differences)
    {
        if (file.SavesEdits && steps.SaveEdits(file.Source) is { } reason)
        {
            differences.Add($"{Name(file.Source)}: not converted, its unsaved edits could not be saved ({reason})");
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

    private static bool SameContent(PlannedFile file)
    {
        try
        {
            return AnimationListComparison.FirstDifference(
                AnimationFileLoader.Read(new FilePath(file.Source)), AnimationFileLoader.Read(new FilePath(file.Target))) is null;
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

    /// <summary>Writes <paramref name="save"/> over <paramref name="path"/>. The old file goes to the Recycle Bin, once the new one is complete.</summary>
    public static string? ReplaceRecyclingOriginal(SequenceListSave save, string path, Func<string, string?> recycle)
    {
        string complete = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.Copy(path, complete); // a save keeps the line endings of the file it replaces
            save.Save(complete);
            if (recycle(path) is { } error) return error;
            File.Move(complete, path, overwrite: true);
            return null;
        }
        finally
        {
            if (File.Exists(complete)) File.Delete(complete);
        }
    }

    // Read back from disk, so a step that reported success without doing its work shows too.
    private static IEnumerable<string> CompareSequences(AchxConversionPlan plan, IReadOnlyList<string> converted, IReadOnlyList<string> updated)
    {
        var listed = plan.NotUpdated.Select(sequence => sequence.Path).ToHashSet();
        foreach (var sequence in AchxConversionPlan.FindSequences(plan.SearchedFolder, converted, OpenDocuments.None))
            if (sequence.Plays.Count > 0 && !listed.Contains(sequence.Path))
                yield return $"{Name(sequence.Path)}: still plays {Names(sequence.Plays)}, which was converted";

        var gone = SequenceSourcePaths.FileSet(converted);
        var targets = converted.Select(source => Path.ChangeExtension(source, AchxConversionPlan.TargetExtension));
        var now = AchxConversionPlan.FindSequences(plan.SearchedFolder, targets, OpenDocuments.None).ToDictionary(sequence => sequence.Path);
        foreach (var sequence in plan.Updated.Where(sequence => updated.Contains(sequence.Path) && sequence.Plays.All(gone.Contains)))
        {
            int blocks = now.TryGetValue(sequence.Path, out var found) ? found.Blocks : 0;
            if (blocks < sequence.Blocks)
                yield return $"{Name(sequence.Path)}: only {blocks} of {Plural.Format(sequence.Blocks, "block")} play the .achj";
        }
    }

    private static string Name(string path) => new FilePath(path).NoPath;

    private static string Names(IEnumerable<string> paths) => string.Join(", ", paths.Select(Name));
}
