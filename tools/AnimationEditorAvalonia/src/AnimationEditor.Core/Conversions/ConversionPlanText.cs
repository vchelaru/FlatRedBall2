using AnimationEditor.Core.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AnimationEditor.Core.Conversions;

/// <summary>One line of the dialog's list: what it is about, and in grey what happens to it.</summary>
public sealed record ConversionRow(string Text, string Detail);

/// <summary>One line of the summary above the list.</summary>
public sealed record ConversionLine(string Text, bool IsProblem);

/// <param name="IsProblem">The rows are what the user may not want, so they stand out.</param>
public sealed record ConversionSection(string Title, IReadOnlyList<ConversionRow> Rows, bool IsProblem);

/// <summary>An <see cref="AchxConversionPlan"/> in words, as the Convert dialog shows it.</summary>
/// <param name="Sections">What the user may not want first, so a long list never hides it.</param>
public sealed record ConversionPlanText(IReadOnlyList<ConversionLine> Summary, IReadOnlyList<ConversionSection> Sections, IReadOnlyList<string> Notes)
{
    /// <summary>False when nothing would be converted, so there is nothing to agree to.</summary>
    public bool CanConvert { get; private init; }

    public static ConversionPlanText Of(AchxConversionPlan plan)
    {
        var converted = plan.Converted.ToList();
        var skipped = plan.Skipped.ToList();
        var updated = plan.Updated.ToList();
        var broken = plan.NotUpdated.Where(sequence => sequence.Plays.Count > 0).ToList();
        var unreadable = plan.NotUpdated.Where(sequence => sequence.Plays.Count == 0).ToList();

        var sections = new List<ConversionSection>();
        void Add(string title, IEnumerable<ConversionRow> rows, bool isProblem = false)
        {
            var list = rows.ToList();
            if (list.Count > 0) sections.Add(new ConversionSection($"{title} ({list.Count})", list, isProblem));
        }

        Add("Sequences that will stop playing", broken.Select(sequence => new ConversionRow(
            Shown(sequence.Path), $"{Blocks(sequence)}, cannot be updated: {sequence.Problem}")), isProblem: true);
        Add("Sequences that cannot be checked", unreadable.Select(sequence => new ConversionRow(
            Shown(sequence.Path), Sentence(sequence.Problem!))), isProblem: true);
        Add("Not converted", skipped.Select(file => new ConversionRow(Shown(file.Source), Sentence(file.SkipReason!))), isProblem: true);
        Add("Converted", converted.Select(file => new ConversionRow(
            $"{Shown(file.Source)} → {Path.GetFileName(file.Target)}", FileDetail(file))));
        Add("Sequences updated to play the .achj", updated.Select(sequence => new ConversionRow(
            Shown(sequence.Path), Blocks(sequence) + (sequence.IsOpen ? ", open in a tab" : ""))));

        return new ConversionPlanText(Summarize(), sections, Notes()) { CanConvert = converted.Count > 0 };

        string Shown(string path) => Path.GetRelativePath(plan.SearchedFolder, path).Replace('\\', '/');

        List<ConversionLine> Summarize()
        {
            if (converted.Count == 0) return [new ConversionLine("There is nothing to convert.", false)];
            var lines = new List<ConversionLine>();
            void Say(int count, string noun, string rest, bool isProblem = false)
            {
                if (count > 0) lines.Add(new ConversionLine($"{Plural.Format(count, noun)} {rest}", isProblem));
            }
            Say(converted.Count, ".achx file", "will become .achj.");
            Say(updated.Count, "sequence file", "will be updated to keep playing.");
            if (plan.Sequences.Count == 0)
                lines.Add(new ConversionLine($"No sequence file plays {(converted.Count == 1 ? "it" : "them")}.", false));
            Say(broken.Count, "sequence file", "will stop playing.", isProblem: true);
            Say(unreadable.Count, "sequence file", "could not be checked.", isProblem: true);
            Say(skipped.Count, ".achx file", "will be left alone.", isProblem: true);
            return lines;
        }

        List<string> Notes()
        {
            if (converted.Count == 0) return [];
            return
            [
                "Checked: every animation, frame, shape and event reads back the same from the .achj.",
                updated.Count > 0
                    ? "The original .achx and .seqj files move to the Recycle Bin. To go back, restore them and delete the .achj."
                    : "The original .achx files move to the Recycle Bin. To go back, restore them and delete the .achj.",
                $"Searched for sequences in {plan.SearchedFolder}. Sequences elsewhere, and game code that loads an .achx by name, are not changed.",
            ];
        }
    }

    private static string FileDetail(PlannedFile file)
    {
        string detail = $"{Plural.Format(file.Animations, "animation")}, {Plural.Format(file.Frames, "frame")}";
        if (file.SavesEdits) detail += ", its unsaved edits are saved first";
        return file.IsOpen ? detail + ", its tab closes" : detail;
    }

    private static string Blocks(PlannedSequence sequence) =>
        sequence.Blocks == 0 ? "In its library, played by no block" : $"{Plural.Format(sequence.Blocks, "block")}";

    private static string Sentence(string reason) => char.ToUpperInvariant(reason[0]) + reason[1..];
}
