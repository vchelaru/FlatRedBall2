using System.Globalization;
using AnimationEditor.Core.Sequences;

namespace AnimationEditor.Editing;

/// <summary>
/// The operations an ops file for a <c>.seqj</c> can hold (#1811), run by <see cref="Apply"/>. Same file shape and parser
/// as <see cref="EditOps"/> (<c>{"ops":[{"op":"add-block",...}, ...]}</c>, applied in order); addresses are described on
/// <see cref="SequenceAddress"/>. <see cref="Specs"/> is the single source for validation and for <c>--schema</c>.
/// </summary>
public static class SequenceOps
{
    /// <summary>How <c>--schema</c> explains sequence addressing; shared by the text and JSON output.</summary>
    public const string AddressSyntax =
        "Sequence files (.seqj): sequence Run, track Run/Body or Run/0 (name wins over index), block Run/Body/1 (zero-based index in the track). "
        + "A track that plays other sequences has one block per clip, in list order. "
        + "Selectors: set-sequence-loop takes a glob over sequence names (R*, *); set-track-continuous, set-track-visible and delete-track take a track name, glob or index selector "
        + "(Run/*, Run/Sh*, Run/0-1, Run/0,2, R*/Body); set-block-start, set-block-length, set-block-end, set-block-chain and delete-block take the same track part and a block index selector "
        + "(Run/Body/*, Run/*/0, Run/Body/0-1). Every other op takes one exact address. A selector matching nothing is NoMatch, an index past the end is "
        + "IndexOutOfRange, and any failing target fails the whole batch and nothing is written. delete-block and delete-track run from the highest index down. "
        + "Sources are named as listed in the file (relative to the .seqj). Times and lengths are seconds. A block with no length set plays one cycle of its chain (one run of the played sequence for a clip) and follows edits to it; a set length keeps the block's end. "
        + "Tracks draw in list order, index 0 in front; add-track and add-reference-track insert at index (default 0, the front).";

    private const string SequenceAddressDescription = "Sequence name.";
    private const string TrackAddressDescription = "Track address: Sequence/TrackName or Sequence/index.";
    private const string BlockAddressDescription = "Block address: Sequence/Track/index (zero-based). On a reference track each clip is a block, in list order.";

    public static IReadOnlyList<OpSpec> Specs { get; } =
    [
        new("add-sequence", "Appends an empty sequence. Without a name it gets a free default (Sequence, Sequence 2).",
        [
            new("name", "string", false, "Sequence name, unique in the file, no '/'."),
        ]),
        new("rename-sequence", "Renames a sequence; tracks that play it follow the rename. The new name must be unused.",
        [
            new("sequence", "string", true, SequenceAddressDescription),
            new("to", "string", true, "New sequence name."),
        ]),
        new("delete-sequence", "Deletes a sequence. A track that still plays it is left with a missing reference target, a check error: the batch is refused when that raises the count of those errors.",
        [
            new("sequence", "string", true, SequenceAddressDescription),
        ]),
        new("reorder-sequence", "Moves a sequence so it ends up at the given position in the file.",
        [
            new("sequence", "string", true, SequenceAddressDescription),
            new("index", "integer", true, "Final zero-based position, 0 to sequence count - 1."),
        ]),
        new("set-sequence-loop", "Turns looping on or off for a sequence. Off, it stops at its end and holds its final pose.",
        [
            new("sequence", "string", true, "Sequence name or glob."),
            new("value", "boolean", true, "true to loop."),
        ]),
        new("duplicate-sequence", "Inserts a copy of a sequence right after it: tracks, blocks and reference tracks (which keep their targets). Without a name the copy gets the next numbered one (Run 2); a given name must be unused. Fails with WouldCycle if the copy would play itself.",
        [
            new("sequence", "string", true, SequenceAddressDescription),
            new("name", "string", false, "Name of the copy, unique in the file, no '/'."),
        ]),
        new("duplicate-track", "Duplicates a track with its blocks into a sequence (it may be the one it is in). The copy keeps the track's name when free there, else gets the next numbered one (Body 2); rename-track changes it. A reference track fails with WouldCycle if its target would play the destination back.",
        [
            new("track", "string", true, TrackAddressDescription),
            new("to", "string", true, "Destination sequence."),
            new("index", "integer", false, "Zero-based position in the destination, 0 to its track count. Default 0 (front)."),
        ]),
        new("add-track", "Adds an empty track to a sequence. Tracks draw in list order, index 0 in front.",
        [
            new("sequence", "string", true, SequenceAddressDescription),
            new("name", "string", false, "Track name, unique in the sequence, no '/'. Default: Track, Track 2, ..."),
            new("index", "integer", false, "Zero-based position, 0 to track count. Default 0 (front)."),
        ]),
        new("rename-track", "Renames a track. The new name must be unique in its sequence.",
        [
            new("track", "string", true, TrackAddressDescription),
            new("to", "string", true, "New track name."),
        ]),
        new("delete-track", "Deletes a track and its blocks.",
        [
            new("track", "string", true, TrackAddressDescription + " Takes selectors."),
        ]),
        new("reorder-track", "Moves a track so it ends up at the given position in its sequence (0 draws in front).",
        [
            new("track", "string", true, TrackAddressDescription),
            new("index", "integer", true, "Final zero-based position, 0 to track count - 1."),
        ]),
        new("set-track-continuous", "Sets the per-track continuous flag. At the wrap of a looping sequence a continuous chain track keeps its playback phase if the same chain is playing, instead of restarting. A continuous reference track keeps its target running across the wrap, if the clip starts at 0, the target loops and the clip lasts to the sequence end (set its length to the sequence length; check warns when ignored).",
        [
            new("track", "string", true, TrackAddressDescription + " Takes selectors."),
            new("value", "boolean", true, "true for continuous."),
        ]),
        new("set-track-visible", "Shows or hides a track. A hidden track is not drawn and keeps its blocks; describe marks it hidden.",
        [
            new("track", "string", true, TrackAddressDescription + " Takes selectors."),
            new("value", "boolean", true, "false to hide."),
        ]),
        new("add-reference-track", "Adds a track, named after the target, that plays another sequence of the file from start seconds, once. Its clip is block 0 (set-block-start, set-block-length, set-block-end). Fails if the target would play the sequence back.",
        [
            new("sequence", "string", true, "Sequence that gets the track."),
            new("target", "string", true, "Sequence to play."),
            new("start", "number", false, "Seconds from the sequence start, 0 or more. Default 0."),
            new("index", "integer", false, "Zero-based position, 0 to track count. Default 0 (front)."),
        ]),
        new("add-source", "Adds an .achx or .achj to the source library. The file must exist.",
        [
            new("path", "string", true, "Path relative to the .seqj, or absolute."),
        ]),
        new("remove-source", "Removes a source from the library. Fails with SourceInUse while blocks use it.",
        [
            new("source", "string", true, "Source as listed in the file."),
        ]),
        new("replace-source", "Swaps a library source for another file and points its blocks at it. Its blocks keep their chain names; a chain the new file lacks is a check error: the batch is refused when that raises the count of those errors.",
        [
            new("source", "string", true, "Source as listed in the file."),
            new("path", "string", true, "New file, relative to the .seqj or absolute; must exist."),
        ]),
        new("add-block", "Appends a block to a track: a chain placement on a chain track (the source must be in the library and have the chain), or a clip playing another sequence on a reference track (give sequence). A reference track holds any number of clips, like a chain track holds blocks; the next one cuts the previous one short.",
        [
            new("track", "string", true, TrackAddressDescription),
            new("source", "string", false, "Source as listed in the file. Default: the library's only source; with several sources it is required."),
            new("chain", "string", false, "Chain name in the source. Required on a chain track."),
            new("sequence", "string", false, "Name of the sequence to play. Required on a reference track, refused on a chain track."),
            new("start", "number", false, "Seconds from the sequence start, 0 or more. Default: the end of the block that starts last on the track (an auto-length block ends after one cycle), 0 on an empty track."),
            new("length", "number", false, "Length in seconds, above 0. Default: none, so the block plays one cycle of the chain and follows edits to it."),
        ]),
        new("set-block-start", "Moves a block to a start time in seconds. The next block on the track cuts it short from there.",
        [
            new("block", "string", true, BlockAddressDescription + " Takes selectors."),
            new("start", "number", true, "Seconds from the sequence start, 0 or more.", Relative: true),
        ]),
        new("set-block-length", "Sets a block's length in seconds, so its end stays put when the chain changes. Give length, or clear to unset it (describe's length auto: one cycle of the chain, or one run of a clip's target, following edits to it). A relative length on an auto block starts from that cycle or run. A following block still cuts it short.",
        [
            new("block", "string", true, BlockAddressDescription + " Takes selectors."),
            new("length", "number", false, "Seconds, above 0.", Relative: true),
            new("clear", "boolean", false, "true to unset the length."),
        ]),
        new("set-block-end", "Sets a block's length so it ends at a time in seconds: length = end - start. Uses the block's current start, so set that first. A relative end starts from start + length, even when a following block cuts it short. A following block still cuts it short.",
        [
            new("block", "string", true, BlockAddressDescription + " Takes selectors."),
            new("end", "number", true, "Seconds from the sequence start, after the block's start.", Relative: true),
        ]),
        new("set-block-chain", "Points a block at another chain, keeping its start and length. Give source to take the chain from another library source; default is the block's own source. A reference track's clips have no chain and are refused.",
        [
            new("block", "string", true, BlockAddressDescription + " Takes selectors."),
            new("source", "string", false, "Source as listed in the file. Default: the block's current source."),
            new("chain", "string", true, "Chain name in the source."),
        ]),
        new("delete-block", "Deletes a block of a chain track; later blocks shift down. To remove a reference track use delete-track.",
        [
            new("block", "string", true, BlockAddressDescription + " Takes selectors."),
        ]),
        new("duplicate-block", "Copies a block, or a clip of a reference track, keeping its length. The copy becomes the last block of the destination track. Chain blocks go to chain tracks and clips to reference tracks; a clip that would play its own sequence back fails with WouldCycle.",
        [
            new("block", "string", true, BlockAddressDescription),
            new("to", "string", false, "Destination track address, in any sequence. Default: the block's own track."),
            new("start", "number", false, "Seconds from the sequence start, 0 or more. Default: the block's start on another track; on its own track, right after the block."),
        ]),
        new("move-block", "Moves a block, or a clip of a reference track, to another track, another start, or both, keeping its length. On another track it becomes the last block and later blocks of the old track shift down; on its own track it keeps its index. Chain blocks go to chain tracks and clips to reference tracks; a clip that would play its own sequence back fails with WouldCycle.",
        [
            new("block", "string", true, BlockAddressDescription),
            new("to", "string", false, "Destination track address, in any sequence. Default: the block's own track."),
            new("start", "number", false, "Seconds from the sequence start, 0 or more. Default: unchanged.", Relative: true),
        ]),
    ];

    /// <summary>Validates the text of an ops file against <see cref="Specs"/>. Nothing is applied.</summary>
    public static OpsParse Parse(string json) => EditOps.Parse(json, Specs);

    /// <summary>
    /// Applies <paramref name="ops"/> to <paramref name="session"/>'s document in order and, only if every op succeeded and no
    /// check error code has more errors than before, saves once. Otherwise nothing is written. A batch that changes nothing writes nothing either,
    /// so the file stays byte-identical.
    /// </summary>
    public static EditRunResult Apply(SequenceSession session, IReadOnlyList<EditOp> ops)
    {
        var doc = session.Document;
        var before = SequenceCheck.Run(doc.Save, session.Path);
        var original = doc.Save.ToJsonString();
        var changed = new List<string>();
        var warnings = new List<string>();
        if (!before.Ok)
            warnings.Add($"The file already had {before.Errors.Count()} check error(s) before the edit.");

        foreach (var op in ops)
        {
            var targets = Expand(session, op);
            if (targets.Error is not null)
                return Failed(warnings, op, targets.Error, targets.Code);

            EditRunResult TargetFailed(EditResult result, string target) =>
                EditOps.FailedOp(warnings, op, SelectorField(op) is null ? "" : $"target {target}: ", result);

            // Every target first: changing one can change the length of a sequence a later one plays.
            var resolved = new List<(EditOp Op, string Target)>();
            foreach (var target in targets.List!)
            {
                var one = NumberFields.ResolveRelative(op, field => CurrentValue(doc, op, field, target));
                if (one.Error is not null) return TargetFailed(one.Error, target);
                resolved.Add((one.Op, target));
            }

            foreach (var (absolute, target) in resolved)
            {
                var result = ApplyOne(session, absolute, target);
                if (!result.Success) return TargetFailed(result, target);
                changed.AddRange(result.ChangedItems);
            }
        }

        var after = SequenceCheck.Run(doc.Save, session.Path);
        var added = CheckReport.NewErrors(before, after);
        if (added.Count > 0)
            return new EditRunResult(
                new EditResult([], added.Select(i => $"The edit would raise the count of check errors [{i.Code}]: {i.Message}").ToList(), warnings,
                added.Select(_ => EditErrorCode.WouldBreakFile).ToList()),
                added.Select(_ => (int?)null).ToList());

        if (changed.Count == 0 || doc.Save.ToJsonString() == original)
            return new EditRunResult(new EditResult([], [], warnings), []);

        var save = session.Save();
        return save.Success
            ? new EditRunResult(new EditResult(changed.Distinct().ToList(), [], [.. warnings, .. CheckReport.RaisedWarnings(before, after)]), [])
            : new EditRunResult(new EditResult([], save.Errors, warnings), save.Errors.Select(_ => (int?)null).ToList());
    }

    private static EditRunResult Failed(List<string> warnings, EditOp op, string error, EditErrorCode code) =>
        new(new EditResult([], [$"op {op.Index} ({op.Spec.Name}): {error}"], warnings, [code]), [op.Index]);

    private sealed record Targets(IReadOnlyList<string>? List, string? Error, EditErrorCode Code);

    /// <summary>The field holding the selector for ops that take one; null for exact-address ops.</summary>
    private static string? SelectorField(EditOp op) => op.Spec.Name switch
    {
        "set-sequence-loop" => "sequence",
        "delete-track" or "set-track-continuous" or "set-track-visible" => "track",
        "set-block-start" or "set-block-length" or "set-block-end" or "set-block-chain" or "delete-block" => "block",
        _ => null,
    };

    private static Targets Expand(SequenceSession session, EditOp op)
    {
        var field = SelectorField(op);
        if (field is null) return new Targets([""], null, EditErrorCode.Unspecified);

        var save = session.Document.Save;
        var text = op.Values[field];
        var expansion = field switch
        {
            "sequence" => SequenceSelectors.ExpandSequences(save, text),
            "track" => SequenceSelectors.ExpandTracks(save, text),
            _ => SequenceSelectors.ExpandBlocks(save, text),
        };
        if (!expansion.Ok) return new Targets(null, expansion.Error, expansion.Code);
        // Deleting shifts the indexes after a block, so blocks go from the highest down.
        var list = op.Spec.Name == "delete-block" ? expansion.Targets!.Reverse().ToList() : expansion.Targets!;
        return new Targets(list, null, EditErrorCode.Unspecified);
    }

    /// <summary>What a relative value of <paramref name="field"/> starts from, as the editor shows it, or why the block cannot be resolved.</summary>
    private static (decimal Value, EditResult? Error) CurrentValue(SequenceDocument doc, EditOp op, OpField field, string target)
    {
        var at = SequenceAddress.ResolveBlock(doc.Save, target.Length > 0 ? target : op.Values["block"]);
        if (!at.Ok) return (0, new EditResult([], [at.Error!], [], [at.Code]));

        var a = at.Value!;
        var track = doc.Save.Sequences[a.Sequence].Tracks[a.Track];
        var (start, length) = track.References is { } clips
            ? (clips[a.Block].Start, doc.ClipLength(clips[a.Block]))
            : (track.Chains[a.Block].Start, doc.ChainLength(track.Chains[a.Block]));
        return (field.Name switch
        {
            "start" => (decimal)start,
            "length" => (decimal)length,
            "end" => (decimal)start + (decimal)length,
            _ => throw new InvalidOperationException($"No current value for '{field.Name}' of '{op.Spec.Name}'."),
        }, null);
    }

    private static EditResult ApplyOne(SequenceSession session, EditOp op, string target)
    {
        var doc = session.Document;
        var undo = session.Undo;
        var v = op.Values;
        string? Opt(string field) => v.GetValueOrDefault(field);
        int? OptInt(string field) => v.TryGetValue(field, out var t) ? int.Parse(t, CultureInfo.InvariantCulture) : null;
        double Num(string field, double fallback = 0) => v.TryGetValue(field, out var t) ? double.Parse(t, CultureInfo.InvariantCulture) : fallback;
        bool Flag(string field) => v[field] == "true";

        switch (op.Spec.Name)
        {
            case "add-sequence": return SequenceEdit.AddSequence(doc, undo, Opt("name"));
            case "rename-sequence": return SequenceEdit.RenameSequence(doc, undo, v["sequence"], v["to"]);
            case "delete-sequence": return SequenceEdit.DeleteSequence(doc, undo, v["sequence"]);
            case "reorder-sequence": return SequenceEdit.ReorderSequence(doc, undo, v["sequence"], OptInt("index")!.Value);
            case "set-sequence-loop": return SequenceEdit.SetSequenceLoop(doc, undo, target, Flag("value"));
            case "duplicate-sequence": return SequenceEdit.DuplicateSequence(doc, undo, v["sequence"], Opt("name"));
            case "duplicate-track": return SequenceEdit.CopyTrack(doc, undo, v["track"], v["to"], OptInt("index"));
            case "add-track": return SequenceEdit.AddTrack(doc, undo, v["sequence"], Opt("name"), OptInt("index"));
            case "rename-track": return SequenceEdit.RenameTrack(doc, undo, v["track"], v["to"]);
            case "delete-track": return SequenceEdit.DeleteTrack(doc, undo, target);
            case "reorder-track": return SequenceEdit.ReorderTrack(doc, undo, v["track"], OptInt("index")!.Value);
            case "set-track-continuous": return SequenceEdit.SetTrackContinuous(doc, undo, target, Flag("value"));
            case "set-track-visible": return SequenceEdit.SetTrackVisible(doc, undo, target, Flag("value"));
            case "add-reference-track": return SequenceEdit.AddReferenceTrack(doc, undo, v["sequence"], v["target"], Num("start"), OptInt("index"));
            case "add-source": return SequenceEdit.AddSource(doc, undo, v["path"]);
            case "remove-source": return SequenceEdit.RemoveSource(doc, undo, v["source"]);
            case "replace-source": return SequenceEdit.ReplaceSource(doc, undo, v["source"], v["path"]);
            case "add-block": return SequenceEdit.AddBlock(doc, undo, v["track"], Opt("source"), Opt("chain"), v.ContainsKey("start") ? Num("start") : (double?)null, v.ContainsKey("length") ? Num("length") : (double?)null, Opt("sequence"));
            case "set-block-start": return SequenceEdit.SetBlockStart(doc, undo, target, Num("start"));
            case "set-block-length":
                bool clear = v.TryGetValue("clear", out var c) && c == "true";
                if (v.ContainsKey("length") == clear)
                    return new EditResult([], ["set-block-length needs exactly one of length and clear."], [], [EditErrorCode.InvalidValue]);
                return clear ? SequenceEdit.ClearBlockLength(doc, undo, target) : SequenceEdit.SetBlockLength(doc, undo, target, Num("length"));
            case "set-block-end": return SequenceEdit.SetBlockEnd(doc, undo, target, Num("end"));
            case "set-block-chain": return SequenceEdit.SetBlockChain(doc, undo, target, Opt("source"), v["chain"]);
            case "delete-block": return SequenceEdit.DeleteBlock(doc, undo, target);
            case "duplicate-block": return SequenceEdit.DuplicateBlock(doc, undo, v["block"], Opt("to"), v.ContainsKey("start") ? Num("start") : null);
            case "move-block": return SequenceEdit.MoveBlock(doc, undo, v["block"], Opt("to"), v.ContainsKey("start") ? Num("start") : null);
            default: throw new InvalidOperationException($"No handler for op '{op.Spec.Name}'.");
        }
    }
}
