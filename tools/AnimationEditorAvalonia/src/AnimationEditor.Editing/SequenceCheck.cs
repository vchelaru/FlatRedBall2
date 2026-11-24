using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>
/// Validation of a sequence file (#1811), producing the same <see cref="CheckReport"/> as the animation check.
/// Errors, which a game would trip on: missing-source (a library source whose file is missing or unreadable, or a block
/// using a source not in the library), missing-chain, missing-reference-target, reference-cycle, duplicate-sequence-name,
/// duplicate-track-name. Warnings, which are legal but suspect: continuous-ignored (a continuous reference track whose target cannot carry across the wrap), cut-short (the next block on the track starts before
/// this one reaches its length) and unreachable-block (the next block starts at the same time, so this one never plays).
/// </summary>
public static class SequenceCheck
{
    private const double Tolerance = 1e-6;

    public static CheckReport Run(SequenceListSave save, string path)
    {
        var sources = new SequenceSources(save, path);
        var issues = new List<CheckIssue>();

        foreach (var source in save.Sources)
        {
            if (sources.Chains(source) is null)
                issues.Add(new(CheckSeverity.Error, "missing-source",
                    sources.Exists(source) ? $"Source '{source}' could not be read." : $"Source '{source}' not found.", source));
        }

        foreach (var group in save.Sequences.GroupBy(s => s.Name).Where(g => g.Count() > 1))
            issues.Add(new(CheckSeverity.Error, "duplicate-sequence-name", $"{group.Count()} sequences are named '{group.Key}'.", group.Key));

        var unlisted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sequence in save.Sequences)
        {
            foreach (var group in sequence.Tracks.GroupBy(t => t.Name).Where(g => g.Count() > 1))
                issues.Add(new(CheckSeverity.Error, "duplicate-track-name",
                    $"{group.Count()} tracks of '{sequence.Name}' are named '{group.Key}'.", $"{sequence.Name}/{group.Key}"));

            foreach (var track in sequence.Tracks)
            {
                if (track.Reference is { } clip)
                {
                    if (SequenceReferences.Find(save.Sequences, clip.Sequence) is null)
                        issues.Add(new(CheckSeverity.Error, "missing-reference-target",
                            $"Track '{sequence.Name}/{track.Name}' plays '{clip.Sequence}', which does not exist."));
                    else if (SequenceReferences.WouldCycle(save.Sequences, sequence.Name, clip.Sequence))
                        issues.Add(new(CheckSeverity.Error, "reference-cycle",
                            $"Track '{sequence.Name}/{track.Name}' plays '{clip.Sequence}', which plays '{sequence.Name}' back."));
                    else if (track.Continuous && ContinuousIgnoredReason(save, sequence, clip, sources) is { } reason)
                        issues.Add(new(CheckSeverity.Warning, "continuous-ignored",
                            $"Track '{sequence.Name}/{track.Name}' is continuous but {reason}, so '{clip.Sequence}' restarts at each wrap."));
                    continue;
                }

                for (int b = 0; b < track.Chains.Count; b++)
                {
                    var block = track.Chains[b];
                    var where = $"{sequence.Name}/{track.Name}/{b}";
                    if (!sources.IsListed(block.Source))
                    {
                        if (unlisted.Add(block.Source))
                            issues.Add(new(CheckSeverity.Error, "missing-source",
                                $"{where} uses source '{block.Source}', which is not in the sources list.", block.Source));
                        continue;
                    }
                    if (sources.Chains(block.Source) is null) continue; // the source error above covers it
                    if (!sources.HasChain(block))
                    {
                        var hint = NameHints.For(block.Chain, sources.Chains(block.Source)!.AnimationChains.Select(c => c.Name), "chains");
                        issues.Add(new(CheckSeverity.Error, "missing-chain", $"{where}: source '{block.Source}' has no chain '{block.Chain}'.{hint}"));
                        continue;
                    }

                    double cycle = sources.CycleLength(block);
                    if (cycle <= 0) continue;
                    double full = SequenceTimeline.FullLength(block.Length, cycle);
                    double played = SequenceTimeline.PlayedLength(track, block, cycle);
                    if (played <= Tolerance && full > Tolerance)
                        issues.Add(new(CheckSeverity.Warning, "unreachable-block", $"{where} never plays: the next block on the track starts at the same time."));
                    else if (played < full - Tolerance * Math.Max(1, full))
                        issues.Add(new(CheckSeverity.Warning, "cut-short", $"{where} is cut short: it plays {Num(played)} of {Num(full)} seconds."));
                }
            }
        }
        return new CheckReport(issues);
    }

    // Why a continuous reference track does not carry its target across the wrap (see SequenceTimeline.CarriesAcrossWrap), or null when it does.
    private static string? ContinuousIgnoredReason(SequenceListSave save, SequenceSave holder, SequenceReferenceSave clip, SequenceSources sources)
    {
        var target = SequenceReferences.Find(save.Sequences, clip.Sequence)!;
        double targetLength = SequenceTimeline.Length(save.Sequences, target, sources.CycleLength);
        double holderLength = SequenceTimeline.Length(save.Sequences, holder, sources.CycleLength);
        if (!holder.Loop) return $"'{holder.Name}' does not loop";
        if (!target.Loop) return $"'{target.Name}' does not loop";
        if (clip.Start > Tolerance) return $"the clip starts at {Num(clip.Start)}, not 0";
        double end = clip.Start + SequenceTimeline.FullLength(clip.Length, targetLength);
        if (targetLength > 0 && end < holderLength - Tolerance * Math.Max(1, holderLength))
            return $"the clip ends at {Num(end)}, before '{holder.Name}' ends at {Num(holderLength)} (set the clip length to {Num(holderLength)})";
        return null;
    }

    internal static string Num(double value) => Math.Round(value, 4).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
}
