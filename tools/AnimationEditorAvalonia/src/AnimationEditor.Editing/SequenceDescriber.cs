using System.Globalization;
using System.Text;
using System.Text.Json;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>
/// Human and machine readable dump of a sequence file as stored on disk (#1811): the sources (and whether each file is
/// there), the sequences with their length, the tracks with their flags, and every block with its address, start, length
/// and computed end (<see cref="SequenceTimeline"/>: the next block on the track can cut it short). A block whose
/// source or chain is missing is flagged. A track that plays other sequences shows one block per clip.
/// </summary>
public static class SequenceDescriber
{
    /// <summary>Which sequences and tracks to show; <c>null</c> shows all. <see cref="Summary"/> is one line per track.</summary>
    public sealed record Filter(IReadOnlyCollection<string>? Sequences = null, string? Track = null, bool Summary = false)
    {
        public bool Includes(SequenceSave s) => Sequences is null || Sequences.Contains(s.Name);
        public bool Includes(SequenceTrackSave t) => Track is null || (EditSelectors.IsGlob(Track) ? EditSelectors.GlobMatches(Track, t.Name) : t.Name == Track);
    }

    /// <summary>An error (with a name hint) when the filter's track pattern matches no track of the selected sequences, else <c>null</c>.</summary>
    public static string? CheckTrackFilter(SequenceListSave save, Filter filter)
    {
        if (filter.Track is null) return null;
        var tracks = save.Sequences.Where(filter.Includes).SelectMany(s => s.Tracks).ToList();
        return tracks.Any(filter.Includes) ? null : $"No track matches '{filter.Track}'." + NameHints.For(filter.Track, tracks.Select(t => t.Name), "tracks");
    }

    private static IEnumerable<(SequenceSave Sequence, SequenceTrackSave Track)> SummaryRows(SequenceListSave save, Filter filter) =>
        save.Sequences.Where(filter.Includes).SelectMany(s => s.Tracks.Where(filter.Includes).Select(t => (s, t)));

    public static string ToText(SequenceListSave save, string path, Filter? filter = null)
    {
        filter ??= new Filter();
        var sources = new SequenceSources(save, path);
        var sb = new StringBuilder();
        if (filter.Summary)
        {
            foreach (var (sequence, track) in SummaryRows(save, filter))
            {
                var blocks = Blocks(save, sequence, track, sources);
                sb.AppendLine(FormattableString.Invariant($"{sequence.Name}/{track.Name}  {blocks.Count} block{(blocks.Count == 1 ? "" : "s")}  length {Num(blocks.Count == 0 ? 0 : blocks.Max(b => b.End))}s"));
            }
            return sb.ToString();
        }
        sb.AppendLine(FormattableString.Invariant(
            $"{Path.GetFileName(path)}  version={save.Version}  sources={save.Sources.Count}  sequences={save.Sequences.Count}"));
        foreach (var source in save.Sources)
            sb.AppendLine($"source {source}{(sources.Chains(source) is null ? "  MISSING" : "")}");

        foreach (var sequence in save.Sequences.Where(filter.Includes))
        {
            var count = sequence.Tracks.Count;
            sb.AppendLine($"{sequence.Name}  ({(sequence.Loop ? "loop" : "no loop")}, {count} track{(count == 1 ? "" : "s")}, length {Num(Length(save, sequence, sources))}s)");
            foreach (var track in sequence.Tracks.Where(filter.Includes))
            {
                var flags = new List<string>();
                if (track.Continuous) flags.Add("continuous");
                if (!track.Visible) flags.Add("hidden");
                sb.AppendLine($"  {track.Name}{(flags.Count > 0 ? "  " + string.Join(" ", flags) : "")}");
                var blocks = Blocks(save, sequence, track, sources);
                foreach (var b in blocks)
                {
                    var tail = b.PlaysSequence is not null ? "" : b.MissingSource ? "  MISSING source" : b.MissingChain ? "  MISSING chain" : "";
                    var what = b.PlaysSequence is not null ? $"plays {b.PlaysSequence}" : $"{b.Chain}  {b.Source}";
                    sb.AppendLine(FormattableString.Invariant($"    {sequence.Name}/{track.Name}/{b.Index}  {what}  start {Num(b.Start)}  length {(b.Length is { } length ? Num(length) + "s" : "auto")}  end {Num(b.End)}{tail}"));
                }
            }
        }
        return sb.ToString();
    }

    public static void WriteJson(Utf8JsonWriter w, SequenceListSave save, string path, Filter? filter = null)
    {
        filter ??= new Filter();
        var sources = new SequenceSources(save, path);
        w.WriteStartObject();
        w.WriteString("kind", "sequences");
        w.WriteString("file", Path.GetFileName(path));
        if (filter.Summary)
        {
            w.WriteStartArray("tracks");
            foreach (var (sequence, track) in SummaryRows(save, filter))
            {
                var blocks = Blocks(save, sequence, track, sources);
                w.WriteStartObject();
                w.WriteString("sequence", sequence.Name);
                w.WriteString("name", track.Name);
                w.WriteNumber("blocks", blocks.Count);
                w.WriteNumber("length", Math.Round(blocks.Count == 0 ? 0 : blocks.Max(b => b.End), 6));
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
            return;
        }
        w.WriteNumber("version", save.Version);
        w.WriteStartArray("sources");
        foreach (var source in save.Sources)
        {
            w.WriteStartObject();
            w.WriteString("path", source);
            w.WriteBoolean("exists", sources.Chains(source) is not null);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartArray("sequences");
        foreach (var sequence in save.Sequences.Where(filter.Includes))
        {
            w.WriteStartObject();
            w.WriteString("name", sequence.Name);
            w.WriteBoolean("loop", sequence.Loop);
            w.WriteNumber("length", Math.Round(Length(save, sequence, sources), 6));
            w.WriteStartArray("tracks");
            foreach (var track in sequence.Tracks.Where(filter.Includes))
            {
                w.WriteStartObject();
                w.WriteString("name", track.Name);
                w.WriteBoolean("visible", track.Visible);
                w.WriteBoolean("continuous", track.Continuous);
                w.WriteStartArray("blocks");
                foreach (var b in Blocks(save, sequence, track, sources))
                {
                    w.WriteStartObject();
                    w.WriteNumber("index", b.Index);
                    w.WriteString("address", $"{sequence.Name}/{track.Name}/{b.Index}");
                    if (b.PlaysSequence is not null)
                    {
                        w.WriteString("plays", b.PlaysSequence);
                    }
                    else
                    {
                        w.WriteString("source", b.Source);
                        w.WriteString("chain", b.Chain);
                        w.WriteBoolean("missingSource", b.MissingSource);
                        w.WriteBoolean("missingChain", b.MissingChain);
                    }
                    w.WriteNumber("start", Math.Round(b.Start, 6));
                    if (b.PlaysSequence is null && !b.MissingSource && !b.MissingChain)
                        w.WriteNumber("chainLength", Math.Round(b.ChainLength, 6));
                    if (b.Length is { } length) w.WriteNumber("length", Math.Round(length, 6));
                    else w.WriteNull("length");
                    w.WriteNumber("end", Math.Round(b.End, 6));
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    private sealed record BlockInfo(int Index, string Source, string Chain, string? PlaysSequence, double Start, double? Length, double End,
        bool MissingSource, bool MissingChain, double ChainLength = 0);

    private static double Length(SequenceListSave save, SequenceSave sequence, SequenceSources sources) =>
        SequenceTimeline.Length(save.Sequences, sequence, sources.CycleLength);

    private static IReadOnlyList<BlockInfo> Blocks(SequenceListSave save, SequenceSave sequence, SequenceTrackSave track, SequenceSources sources)
    {
        if (track.References is { } clips)
        {
            var playing = new List<BlockInfo>();
            for (int c = 0; c < clips.Count; c++)
            {
                var clip = clips[c];
                double end = clip.Start;
                if (SequenceReferences.Plays(save.Sequences, sequence.Name, clip.Sequence)
                    && SequenceReferences.Find(save.Sequences, clip.Sequence) is { } target)
                    end = clip.Start + SequenceTimeline.PlayedLength(clip.Start,
                        SequenceTimeline.FullLength(clip.Length, Length(save, target, sources)), SequenceTimeline.NextStart(track, clip));
                playing.Add(new BlockInfo(c, "", "", clip.Sequence, clip.Start, clip.Length, end, false, false));
            }
            return playing;
        }

        var result = new List<BlockInfo>();
        for (int i = 0; i < track.Chains.Count; i++)
        {
            var block = track.Chains[i];
            bool missingSource = !sources.IsListed(block.Source) || sources.Chains(block.Source) is null;
            bool missingChain = !missingSource && !sources.HasChain(block);
            double played = SequenceTimeline.PlayedLength(track, block, sources.CycleLength(block));
            result.Add(new BlockInfo(i, block.Source, block.Chain, null, block.Start, block.Length, block.Start + played, missingSource, missingChain, sources.CycleLength(block) ?? 0));
        }
        return result;
    }

    private static string Num(double value) => SequenceCheck.Num(value);
}
