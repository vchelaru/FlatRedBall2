using AnimationEditor.Core.Utilities;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AnimationEditor.Core.Conversions;

/// <summary>What happens to a file the plan has a problem with.</summary>
public enum ConversionMark { LeftAlone, StopsPlaying, Unknown }

/// <param name="Folder">The folder part of the path, ending in a slash; empty in the searched folder itself.</param>
/// <param name="Detail">The whole reason; empty when <paramref name="Why"/> says it all.</param>
public sealed record ConversionIssue(ConversionMark Mark, string Folder, string Name, string Why, string Detail);

/// <summary>A file the conversion changes: what it becomes, what happens to its tab, and how much is in it.</summary>
/// <param name="Losses">What it does not keep, a line for each kind of thing.</param>
public sealed record ConversionItem(string Folder, string Name, string Becomes, string Note, string Amount, string Frames, string Losses = "");

/// <summary>One kind of file: how many of them the conversion changes, then the problems, then the changed files as one group.</summary>
/// <param name="Empty">What to say when there is no file of this kind.</param>
/// <param name="GroupWarning">How many of the group lose data, shown as a warning after the summary; empty when none do.</param>
public sealed record ConversionCard(
    string Title, string Caption, IReadOnlyList<ConversionIssue> Issues, string GroupTitle, string GroupSummary,
    IReadOnlyList<ConversionItem> Items, string Empty = "", string GroupWarning = "")
{
    private const int MaxItemsShown = 6;

    public int Total => Issues.Count + Items.Count;

    /// <summary>At most one file, which needs no count and no group.</summary>
    public bool IsSingle => Total <= 1;

    /// <summary>Whether the group is open to begin with: always when a file loses data, so the list is on screen before Convert.</summary>
    public bool ShowsItems => Items.Count <= MaxItemsShown || Items.Any(item => item.Losses.Length > 0);
}

/// <param name="IsCheck">The value is the result of a check that passed.</param>
public sealed record ConversionFact(string Label, string Value, bool IsCheck = false);

/// <summary>An <see cref="AchxConversionPlan"/> as the Convert dialog shows it.</summary>
/// <param name="Sequences">Null when nothing converts, since then no sequence is looked for.</param>
public sealed record ConversionPlanView(
    ConversionCard Animations, ConversionCard? Sequences, IReadOnlyList<ConversionFact> Facts)
{
    /// <summary>False when nothing would be converted, so there is nothing to agree to.</summary>
    public bool CanConvert => Animations.Items.Count > 0;

    public static ConversionPlanView Of(AchxConversionPlan plan)
    {
        var converted = plan.Converted.ToList();
        var updated = plan.Updated.ToList();
        var animations = new ConversionCard("Animations", "will become .achj",
            plan.Skipped.Select(file => Issue(ConversionMark.LeftAlone, file.Source, file.Problem!.Why, file.Problem.Detail)).ToList(),
            $"{converted.Count} {Plural.Noun(converted.Count, "becomes", "become")} .achj", AnimationsSummary(converted),
            converted.OrderByDescending(file => file.Drops.Count > 0).Select(file => new ConversionItem(Folder(file.Source), Path.GetFileName(file.Source), Path.GetFileName(file.Target),
                Join(", ", file.SavesEdits ? "saves edits" : "", file.IsOpen ? "tab closes" : "", file.HasComments ? "comments are not kept" : ""),
                Plural.Format(file.Animations, "animation"), Plural.Format(file.Frames, "frame"), string.Join('\n', ConversionDrop.Describe(file.Drops)))).ToList(),
            GroupWarning: converted.Count(file => file.Drops.Count > 0) is var losing and > 0 ? $"{losing} {Plural.Noun(losing, "loses", "lose")} data" : "");
        if (converted.Count == 0) return new ConversionPlanView(animations, null, []);

        var sequences = new ConversionCard("Sequences", "will keep playing",
            plan.NotUpdated.OrderBy(sequence => sequence.Plays.Count == 0).Select(NotUpdated).ToList(),
            $"{updated.Count} {Plural.Noun(updated.Count, "switches", "switch")} to .achj", SequencesSummary(updated),
            updated.Select(sequence => new ConversionItem(Folder(sequence.Path), Path.GetFileName(sequence.Path), "",
                sequence.IsOpen ? "open in a tab" : "", Plural.Format(sequence.Blocks, "block"), "")).ToList(),
            Empty: converted.Count == 1 ? $"No sequence plays {Path.GetFileName(converted[0].Source)}" : "No sequence plays these files");
        return new ConversionPlanView(animations, sequences,
        [
            new ConversionFact("Check", (converted.Count == 1 ? "Reads" : $"All {Number(converted.Count)} read")
                + " back the same from .achj"
                + (converted.Any(file => file.Drops.Count > 0) ? ", apart from what is not kept" : ": animations, frames, shapes, events"), IsCheck: true),
            new ConversionFact("Originals", "Go to the Recycle Bin. To go back, restore them and delete the .achj."),
            new ConversionFact("Looks in", plan.SearchedFolder),
            new ConversionFact("Unchanged", "Sequences elsewhere, code that names an .achx"),
        ]);

        string Folder(string path)
        {
            string folder = Path.GetDirectoryName(Path.GetRelativePath(plan.SearchedFolder, path))!.Replace('\\', '/');
            return folder.Length == 0 ? "" : folder + "/";
        }

        ConversionIssue Issue(ConversionMark mark, string path, string why, string detail) =>
            new(mark, Folder(path), Path.GetFileName(path), why, detail);

        ConversionIssue NotUpdated(PlannedSequence sequence)
        {
            var problem = sequence.Problem!;
            if (sequence.Plays.Count == 0) return Issue(ConversionMark.Unknown, sequence.Path, $"{problem.Why} · Unknown", problem.Detail);
            string stops = $"{Plural.Format(sequence.Blocks, "block")} {Plural.Noun(sequence.Blocks, "stops", "stop")} playing";
            return Issue(ConversionMark.StopsPlaying, sequence.Path, $"{problem.Why} · {stops}",
                $"{problem.Detail} It still names {string.Join(", ", sequence.Plays.Select(Path.GetFileName))}, which {Plural.Noun(sequence.Plays.Count, "goes", "go")} to the Recycle Bin.");
        }
    }

    private static string AnimationsSummary(IReadOnlyCollection<PlannedFile> files)
    {
        int tabs = files.Count(file => file.IsOpen), saves = files.Count(file => file.SavesEdits), comments = files.Count(file => file.HasComments);
        bool isLong = new[] { tabs, saves, comments }.Count(count => count > 0) > 2;
        return Join(" · ", isLong ? "" : Amount(files.Sum(file => file.Animations), "animation"), Amount(files.Sum(file => file.Frames), "frame"),
            tabs == 0 ? "" : $"{Plural.Format(tabs, "tab")} {Plural.Noun(tabs, "closes", "close")}",
            saves == 0 ? "" : $"{saves} {Plural.Noun(saves, "saves", "save")} edits",
            comments == 0 ? "" : $"{comments} {Plural.Noun(comments, "loses", "lose")} comments");
    }

    private static string SequencesSummary(IReadOnlyCollection<PlannedSequence> sequences)
    {
        int open = sequences.Count(sequence => sequence.IsOpen);
        return Join(" · ", Amount(sequences.Sum(sequence => sequence.Blocks), "block"), open == 0 ? "" : $"{open} open in {Plural.Noun(open, "a tab", "tabs")}");
    }

    private static string Number(int count) => count.ToString("N0", CultureInfo.InvariantCulture);

    private static string Amount(int count, string noun) => $"{Number(count)} {Plural.Noun(count, noun)}";

    private static string Join(string separator, params string[] parts) => string.Join(separator, parts.Where(part => part.Length > 0));
}
