using System.Globalization;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>A resolved sequence, track or block as indexes into the file; unused levels are -1.</summary>
public sealed record SeqAddr(int Sequence, int Track = -1, int Block = -1);

/// <summary>A resolved address, or the reason it could not be resolved.</summary>
public sealed record SeqAddrResult(SeqAddr? Value, string? Error, EditErrorCode Code = EditErrorCode.Unspecified)
{
    public bool Ok => Value is not null;
}

/// <summary>
/// Text addresses for the items of a <c>.seqj</c> (#1811): a sequence by name (<c>Run</c>), a track by name or
/// zero-based index (<c>Run/Body</c>, <c>Run/0</c>), a block by its zero-based index in the track
/// (<c>Run/Body/1</c>). A track that plays another sequence has exactly one block, its clip, at index 0. A name
/// wins over an index, so a track named "1" is addressed by that name. Segments split on '/', so names containing
/// '/' cannot be addressed.
/// </summary>
public static class SequenceAddress
{
    public static SeqAddrResult ResolveSequence(SequenceListSave save, string address)
    {
        if (address.Length == 0) return Fail("A sequence address cannot be empty.");
        if (address.Contains('/')) return Fail($"'{address}' is not a sequence name: names containing '/' cannot be addressed.");

        var matches = Enumerable.Range(0, save.Sequences.Count).Where(i => save.Sequences[i].Name == address).ToList();
        return matches.Count switch
        {
            0 => Fail($"No sequence named '{address}'.", EditErrorCode.NotFound),
            1 => new SeqAddrResult(new SeqAddr(matches[0]), null),
            _ => Fail($"More than one sequence is named '{address}'; rename them in the editor first."),
        };
    }

    public static SeqAddrResult ResolveTrack(SequenceListSave save, string address)
    {
        var parts = address.Split('/');
        if (parts.Length != 2) return Fail($"'{address}' is not a track address; expected Sequence/Track.");
        var sequence = ResolveSequence(save, parts[0]);
        if (!sequence.Ok) return sequence;
        return TrackIn(save, sequence.Value!.Sequence, parts[1]);
    }

    public static SeqAddrResult ResolveBlock(SequenceListSave save, string address)
    {
        var parts = address.Split('/');
        if (parts.Length != 3) return Fail($"'{address}' is not a block address; expected Sequence/Track/index.");
        var track = ResolveTrack(save, $"{parts[0]}/{parts[1]}");
        if (!track.Ok) return track;
        if (parts[2].Length == 0 || !parts[2].All(char.IsAsciiDigit) || parts[2].Length > 9)
            return Fail($"Block index '{parts[2]}' is not a zero-based index.");

        int index = int.Parse(parts[2], CultureInfo.InvariantCulture);
        var owner = save.Sequences[track.Value!.Sequence].Tracks[track.Value.Track];
        int count = BlockCount(owner);
        return index >= count
            ? Fail($"Block index {index} is out of range: '{parts[0]}/{parts[1]}' has {count} block(s).", EditErrorCode.IndexOutOfRange)
            : new SeqAddrResult(track.Value with { Block = index }, null);
    }

    /// <summary>The blocks a track has: its chain references, or one clip when it plays a sequence.</summary>
    public static int BlockCount(SequenceTrackSave track) => track.Reference is not null ? 1 : track.Chains.Count;

    private static SeqAddrResult TrackIn(SequenceListSave save, int sequence, string segment)
    {
        var tracks = save.Sequences[sequence].Tracks;
        if (segment.Length == 0) return Fail("A track name or index cannot be empty.");

        var named = Enumerable.Range(0, tracks.Count).Where(i => tracks[i].Name == segment).ToList();
        if (named.Count == 1) return new SeqAddrResult(new SeqAddr(sequence, named[0]), null);
        if (named.Count > 1)
            return Fail($"More than one track of '{save.Sequences[sequence].Name}' is named '{segment}'; rename them in the editor first.");

        if (!segment.All(char.IsAsciiDigit) || segment.Length > 9)
            return Fail($"No track named '{segment}' in '{save.Sequences[sequence].Name}'.", EditErrorCode.NotFound);

        int index = int.Parse(segment, CultureInfo.InvariantCulture);
        return index >= tracks.Count
            ? Fail($"Track index {index} is out of range: '{save.Sequences[sequence].Name}' has {tracks.Count} track(s).", EditErrorCode.IndexOutOfRange)
            : new SeqAddrResult(new SeqAddr(sequence, index), null);
    }

    internal static SeqAddrResult Fail(string message, EditErrorCode code = EditErrorCode.Unspecified) => new(null, message, code);
}

/// <summary>
/// Bulk addressing for sequence files (#1811), mirroring <see cref="EditSelectors"/>. A sequence may be a glob
/// (<c>R*</c>, <c>*</c>); a track a name, glob, or index selector (<c>*</c>, <c>0-1</c>, <c>0,2</c>); a block an index
/// selector. A selector expands to plain addresses (track names, not indexes) in file order. Matching nothing is
/// <see cref="EditErrorCode.NoMatch"/>, any index past the end is <see cref="EditErrorCode.IndexOutOfRange"/>. A
/// glob over sequences skips those lacking the named track. Tracks sharing a name in one sequence cannot be expanded.
/// </summary>
public static class SequenceSelectors
{
    public static SelectorExpansion ExpandSequences(SequenceListSave save, string pattern)
    {
        if (pattern.Length == 0) return Fail("A sequence address cannot be empty.");
        if (pattern.Contains('/')) return Fail($"'{pattern}' is not a sequence name: names containing '/' cannot be addressed.");

        var names = save.Sequences.Select(s => s.Name).Where(n => EditSelectors.IsGlob(pattern) ? EditSelectors.GlobMatches(pattern, n) : n == pattern).Distinct().ToList();
        return names.Count == 0
            ? Fail($"No sequence matches '{pattern}'.", EditErrorCode.NoMatch)
            : new SelectorExpansion(names, null);
    }

    public static SelectorExpansion ExpandTracks(SequenceListSave save, string selector)
    {
        var parts = selector.Split('/');
        if (parts.Length != 2) return Fail($"'{selector}' is not a track address; expected Sequence/Track.");
        var sequences = ExpandSequences(save, parts[0]);
        if (!sequences.Ok) return sequences;

        var targets = new List<string>();
        foreach (var name in sequences.Targets!)
        {
            var tracks = save.Sequences.First(s => s.Name == name).Tracks;
            var chosen = new List<int>();
            var sel = parts[1];
            if (sel.Length == 0) return Fail("A track name or index cannot be empty.");

            if (!EditSelectors.IsGlob(sel) && tracks.Any(t => t.Name == sel))
                chosen.AddRange(Enumerable.Range(0, tracks.Count).Where(i => tracks[i].Name == sel));
            else if (sel == "*" || IsIndexSelector(sel))
            {
                var indexes = EditSelectors.ParseIndexes(sel, tracks.Count, name, "track");
                if (indexes.Error is not null) return Fail(indexes.Error, indexes.Code);
                chosen.AddRange(indexes.Indexes!);
            }
            else if (EditSelectors.IsGlob(sel))
                chosen.AddRange(Enumerable.Range(0, tracks.Count).Where(i => EditSelectors.GlobMatches(sel, tracks[i].Name)));

            foreach (int i in chosen)
            {
                var trackName = tracks[i].Name;
                if (trackName.Length == 0 || trackName.Contains('/') || tracks.Count(t => t.Name == trackName) > 1)
                    return Fail($"Track {i} of '{name}' has a name that cannot be addressed ('{trackName}'); rename it in the editor first.");
                targets.Add($"{name}/{trackName}");
            }
        }
        return targets.Count == 0
            ? Fail($"'{selector}' matches no tracks.", EditErrorCode.NoMatch)
            : new SelectorExpansion(targets, null);
    }

    public static SelectorExpansion ExpandBlocks(SequenceListSave save, string selector)
    {
        var parts = selector.Split('/');
        if (parts.Length != 3) return Fail($"'{selector}' is not a block address; expected Sequence/Track/index.");
        var tracks = ExpandTracks(save, $"{parts[0]}/{parts[1]}");
        if (!tracks.Ok) return tracks;

        var targets = new List<string>();
        foreach (var address in tracks.Targets!)
        {
            var track = SequenceAddress.ResolveTrack(save, address).Value!;
            var count = SequenceAddress.BlockCount(save.Sequences[track.Sequence].Tracks[track.Track]);
            var indexes = EditSelectors.ParseIndexes(parts[2], count, address, "block");
            if (indexes.Error is not null) return Fail(indexes.Error, indexes.Code);
            targets.AddRange(indexes.Indexes!.Select(i => $"{address}/{i.ToString(CultureInfo.InvariantCulture)}"));
        }
        return targets.Count == 0
            ? Fail($"'{selector}' matches no blocks.", EditErrorCode.NoMatch)
            : new SelectorExpansion(targets, null);
    }

    private static bool IsIndexSelector(string text) => text.All(c => char.IsAsciiDigit(c) || c is ',' or '-');

    private static SelectorExpansion Fail(string message, EditErrorCode code = EditErrorCode.Unspecified) => new(null, message, code);
}
