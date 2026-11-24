using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Paths;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>
/// Result-returning edits of a <see cref="SequenceDocument"/> (#1811): sequences, tracks, reference tracks, the source
/// library and blocks. Each takes text addresses (see <see cref="SequenceAddress"/>), validates before touching the
/// document, and makes exactly one undo step (a no-op change makes none and succeeds with no changed items). Never
/// writes the file and never reads or sets the document's selection; the caller saves. A failure changes nothing.
/// <see cref="EditResult.ChangedItems"/> holds the address of the item produced or changed.
/// </summary>
public static class SequenceEdit
{
    // ---- sequences ----

    /// <summary>Appends a sequence. A null name picks a free default ("Sequence", "Sequence 2"); a given name must be unused.</summary>
    public static EditResult AddSequence(SequenceDocument doc, IUndoManager undo, string? name)
    {
        if (name is not null)
        {
            name = name.Trim();
            var bad = CheckNewName(name, doc.Save.Sequences.Select(s => s.Name), "sequence");
            if (bad is not null) return bad;
        }
        undo.SealCoalescing();
        return Ok(doc.AddSequence(undo, name));
    }

    /// <summary>Renames a sequence; tracks that play it follow. The new name must be unused.</summary>
    public static EditResult RenameSequence(SequenceDocument doc, IUndoManager undo, string sequence, string to)
    {
        var at = SequenceAddress.ResolveSequence(doc.Save, sequence);
        if (!at.Ok) return FromAddress(at);
        to = to.Trim();
        var current = doc.Save.Sequences[at.Value!.Sequence].Name;
        if (to == current) return Ok();
        var bad = CheckNewName(to, doc.Save.Sequences.Select(s => s.Name), "sequence");
        if (bad is not null) return bad;

        undo.SealCoalescing();
        var refused = doc.RenameSequence(undo, at.Value.Sequence, to);
        return refused is null ? Ok(to) : Fail(EditErrorCode.WouldCycle, refused);
    }

    public static EditResult DeleteSequence(SequenceDocument doc, IUndoManager undo, string sequence)
    {
        var at = SequenceAddress.ResolveSequence(doc.Save, sequence);
        if (!at.Ok) return FromAddress(at);
        var name = doc.Save.Sequences[at.Value!.Sequence].Name;
        undo.SealCoalescing();
        doc.DeleteSequences(undo, [at.Value.Sequence]);
        return Ok(name);
    }

    /// <summary>Moves a sequence so it ends up at zero-based <paramref name="index"/>.</summary>
    public static EditResult ReorderSequence(SequenceDocument doc, IUndoManager undo, string sequence, int index)
    {
        var at = SequenceAddress.ResolveSequence(doc.Save, sequence);
        if (!at.Ok) return FromAddress(at);
        var count = doc.Save.Sequences.Count;
        if (index < 0 || index >= count) return OutOfRange(index, count, "sequence");
        int from = at.Value!.Sequence;
        if (index == from) return Ok();

        undo.SealCoalescing();
        doc.MoveSequences(undo, [from], index > from ? index + 1 : index);
        return Ok(doc.Save.Sequences[index].Name);
    }

    public static EditResult SetSequenceLoop(SequenceDocument doc, IUndoManager undo, string sequence, bool loop)
    {
        var at = SequenceAddress.ResolveSequence(doc.Save, sequence);
        if (!at.Ok) return FromAddress(at);
        var target = doc.Save.Sequences[at.Value!.Sequence];
        if (target.Loop == loop) return Ok();

        undo.SealCoalescing();
        doc.SetLoop(undo, at.Value.Sequence, loop);
        return Ok(target.Name);
    }

    /// <summary>
    /// Inserts a deep copy of a sequence right after it, tracks, blocks and reference tracks included. A null name picks the
    /// next free numbered one ("Run 2"); a given name must be unused. Reference tracks keep their targets.
    /// </summary>
    public static EditResult DuplicateSequence(SequenceDocument doc, IUndoManager undo, string sequence, string? name)
    {
        var at = SequenceAddress.ResolveSequence(doc.Save, sequence);
        if (!at.Ok) return FromAddress(at);
        if (name is not null)
        {
            name = name.Trim();
            var bad = CheckNewName(name, doc.Save.Sequences.Select(s => s.Name), "sequence");
            if (bad is not null) return bad;
        }

        undo.SealCoalescing();
        var made = doc.DuplicateSequences(undo, [at.Value!.Sequence], name is null ? null : [name]);
        return Ok(made[0]);
    }

    // ---- tracks ----

    /// <summary>
    /// Copies a track, blocks included, into another (or the same) sequence at zero-based <paramref name="index"/> (default 0,
    /// the front). The copy keeps the track's name when free there, else gets the next numbered one ("Body 2").
    /// A reference track is refused when its target would play the destination back.
    /// </summary>
    public static EditResult CopyTrack(SequenceDocument doc, IUndoManager undo, string track, string to, int? index)
    {
        var at = SequenceAddress.ResolveTrack(doc.Save, track);
        if (!at.Ok) return FromAddress(at);
        var dest = SequenceAddress.ResolveSequence(doc.Save, to);
        if (!dest.Ok) return FromAddress(dest);
        var owner = doc.Save.Sequences[dest.Value!.Sequence];
        if (index is { } i && (i < 0 || i > owner.Tracks.Count)) return OutOfRange(i, owner.Tracks.Count + 1, "track slot");

        if (!TimelineClipboard.TryRead(doc.CopyTracks(at.Value!.Sequence, [at.Value.Track]), out var content, out var readError) || content is null)
            return Fail(EditErrorCode.Refused, readError ?? "Could not copy the track.");
        undo.SealCoalescing();
        int slot = index ?? 0;
        var pasted = doc.PasteTracks(undo, dest.Value.Sequence, content, null, slot);
        if (pasted.Errors.Count > 0)
            return Fail(pasted.Tracks.Count == 0 ? EditErrorCode.WouldCycle : EditErrorCode.Refused, pasted.Errors[0]);
        // The edit replaced the document's objects, so the sequence resolved above is the one from before it.
        return Ok($"{owner.Name}/{doc.Save.Sequences[dest.Value.Sequence].Tracks[pasted.Tracks[0]].Name}");
    }

    /// <summary>
    /// Adds an empty track at zero-based <paramref name="index"/> (default 0, the front: the first track draws on top).
    /// A null name picks a free default ("Track", "Track 2"); a given name must be unused in the sequence.
    /// </summary>
    public static EditResult AddTrack(SequenceDocument doc, IUndoManager undo, string sequence, string? name, int? index)
    {
        var at = SequenceAddress.ResolveSequence(doc.Save, sequence);
        if (!at.Ok) return FromAddress(at);
        var owner = doc.Save.Sequences[at.Value!.Sequence];
        if (index is { } i && (i < 0 || i > owner.Tracks.Count)) return OutOfRange(i, owner.Tracks.Count + 1, "track slot");
        if (name is not null)
        {
            name = name.Trim();
            var bad = CheckNewName(name, owner.Tracks.Select(t => t.Name), "track");
            if (bad is not null) return bad;
        }

        undo.SealCoalescing();
        var added = doc.AddTrack(undo, at.Value.Sequence, name, index ?? 0);
        return Ok($"{owner.Name}/{added}");
    }

    public static EditResult RenameTrack(SequenceDocument doc, IUndoManager undo, string track, string to)
    {
        var at = SequenceAddress.ResolveTrack(doc.Save, track);
        if (!at.Ok) return FromAddress(at);
        var owner = doc.Save.Sequences[at.Value!.Sequence];
        to = to.Trim();
        if (to == owner.Tracks[at.Value.Track].Name) return Ok();
        var bad = CheckNewName(to, owner.Tracks.Select(t => t.Name), "track");
        if (bad is not null) return bad;

        undo.SealCoalescing();
        var refused = doc.RenameTrack(undo, at.Value.Sequence, at.Value.Track, to);
        return refused is null ? Ok($"{owner.Name}/{to}") : Fail(EditErrorCode.Refused, refused);
    }

    public static EditResult DeleteTrack(SequenceDocument doc, IUndoManager undo, string track)
    {
        var at = SequenceAddress.ResolveTrack(doc.Save, track);
        if (!at.Ok) return FromAddress(at);
        var owner = doc.Save.Sequences[at.Value!.Sequence];
        var name = owner.Tracks[at.Value.Track].Name;
        undo.SealCoalescing();
        doc.DeleteTrack(undo, at.Value.Sequence, at.Value.Track);
        return Ok($"{owner.Name}/{name}");
    }

    /// <summary>Moves a track so it ends up at zero-based <paramref name="index"/> (0 draws in front).</summary>
    public static EditResult ReorderTrack(SequenceDocument doc, IUndoManager undo, string track, int index)
    {
        var at = SequenceAddress.ResolveTrack(doc.Save, track);
        if (!at.Ok) return FromAddress(at);
        var owner = doc.Save.Sequences[at.Value!.Sequence];
        if (index < 0 || index >= owner.Tracks.Count) return OutOfRange(index, owner.Tracks.Count, "track");
        int from = at.Value.Track;
        if (index == from) return Ok();

        undo.SealCoalescing();
        doc.MoveTrack(undo, at.Value.Sequence, from, index > from ? index + 1 : index);
        return Ok($"{owner.Name}/{doc.Save.Sequences[at.Value.Sequence].Tracks[index].Name}");
    }

    /// <summary>
    /// Sets the track's continuous flag: at the wrap of a looping sequence the track keeps its playback phase when the same
    /// chain is playing, instead of restarting. On a reference track the target keeps running across the wrap (see SequenceTimeline.ReferenceCarryOver).
    /// </summary>
    public static EditResult SetTrackContinuous(SequenceDocument doc, IUndoManager undo, string track, bool continuous)
    {
        var at = SequenceAddress.ResolveTrack(doc.Save, track);
        if (!at.Ok) return FromAddress(at);
        var owner = doc.Save.Sequences[at.Value!.Sequence];
        var target = owner.Tracks[at.Value.Track];
        if (target.Continuous == continuous) return Ok();

        undo.SealCoalescing();
        doc.SetTracksContinuous(undo, at.Value.Sequence, [at.Value.Track], continuous);
        return Ok($"{owner.Name}/{target.Name}");
    }

    /// <summary>Shows or hides a track. A hidden track is not drawn and keeps its blocks.</summary>
    public static EditResult SetTrackVisible(SequenceDocument doc, IUndoManager undo, string track, bool visible)
    {
        var at = SequenceAddress.ResolveTrack(doc.Save, track);
        if (!at.Ok) return FromAddress(at);
        var owner = doc.Save.Sequences[at.Value!.Sequence];
        var target = owner.Tracks[at.Value.Track];
        if (target.Visible == visible) return Ok();

        undo.SealCoalescing();
        doc.SetTracksVisible(undo, at.Value.Sequence, [at.Value.Track], visible);
        return Ok($"{owner.Name}/{target.Name}");
    }

    /// <summary>
    /// Adds a track named after <paramref name="target"/> that plays that sequence once from <paramref name="start"/> seconds,
    /// at zero-based <paramref name="index"/> (default 0). Its clip is block 0 of the track; add-block adds more.
    /// </summary>
    public static EditResult AddReferenceTrack(SequenceDocument doc, IUndoManager undo, string sequence, string target, double start, int? index)
    {
        var at = SequenceAddress.ResolveSequence(doc.Save, sequence);
        if (!at.Ok) return FromAddress(at);
        var to = SequenceAddress.ResolveSequence(doc.Save, target);
        if (!to.Ok) return FromAddress(to);
        if (CheckStart(start) is { } badStart) return badStart;
        var owner = doc.Save.Sequences[at.Value!.Sequence];
        if (index is { } i && (i < 0 || i > owner.Tracks.Count)) return OutOfRange(i, owner.Tracks.Count + 1, "track slot");
        var targetName = doc.Save.Sequences[to.Value!.Sequence].Name;
        if (!doc.CanReference(at.Value.Sequence, targetName))
            return WouldCycle(owner.Name, targetName);

        undo.SealCoalescing();
        var slot = index ?? 0;
        var refused = doc.AddSequenceReference(undo, at.Value.Sequence, targetName, start, slot);
        return refused is null ? Ok($"{owner.Name}/{doc.Save.Sequences[at.Value.Sequence].Tracks[slot].Name}") : Fail(EditErrorCode.Refused, refused);
    }

    // ---- sources ----

    /// <summary>Adds an .achx/.achj to the source library. The path is relative to the sequence file or absolute and must exist.</summary>
    public static EditResult AddSource(SequenceDocument doc, IUndoManager undo, string path)
    {
        var bad = CheckSourceFile(doc, path, out var full);
        if (bad is not null) return bad;
        if (doc.Save.Sources.Any(s => SamePath(doc, s, full)))
            return Fail(EditErrorCode.DuplicateName, $"{System.IO.Path.GetFileName(full)} is already in the library.");

        undo.SealCoalescing();
        var errors = doc.AddSources(undo, [full]);
        return errors.Count > 0 ? Fail(EditErrorCode.Refused, errors[0]) : Ok(doc.Save.Sources[^1]);
    }

    /// <summary>Removes an unused source (named as listed in the file) from the library.</summary>
    public static EditResult RemoveSource(SequenceDocument doc, IUndoManager undo, string source)
    {
        var listed = FindSource(doc, source);
        if (listed is null) return NoSuchSource(source);
        undo.SealCoalescing();
        var refused = doc.RemoveSource(undo, listed);
        return refused is null ? Ok(listed) : Fail(EditErrorCode.SourceInUse, refused);
    }

    /// <summary>Swaps a source for another file and points its blocks at it. A chain the new file lacks stays, flagged missing by check.</summary>
    public static EditResult ReplaceSource(SequenceDocument doc, IUndoManager undo, string source, string path)
    {
        var listed = FindSource(doc, source);
        if (listed is null) return NoSuchSource(source);
        var bad = CheckSourceFile(doc, path, out var full);
        if (bad is not null) return bad;
        if (doc.Save.Sources.Any(s => !string.Equals(s, listed, StringComparison.OrdinalIgnoreCase) && SamePath(doc, s, full)))
            return Fail(EditErrorCode.DuplicateName, $"{System.IO.Path.GetFileName(full)} is already in the library.");
        if (SamePath(doc, listed, full)) return Ok();

        undo.SealCoalescing();
        var refused = doc.ReplaceSource(undo, listed, full);
        return refused is null ? Ok(doc.Save.Sources.FirstOrDefault(s => SamePath(doc, s, full)) ?? listed) : Fail(EditErrorCode.Refused, refused);
    }

    // ---- blocks ----

    /// <summary>
    /// Appends a block playing <paramref name="chain"/> of a listed source on a chain track from <paramref name="start"/>
    /// seconds, <paramref name="length"/> seconds long (null: one cycle, following the chain). The source must be in the
    /// library and have the chain. A null <paramref name="start"/> is the end of the block that plays last on the track
    /// (0 on an empty track); a null <paramref name="source"/> is the library's only source (several is an error that lists them).
    /// On a reference track the block is a clip playing <paramref name="sequence"/> instead, and <paramref name="chain"/> and
    /// <paramref name="source"/> are not used; the clip's end is that of its target at its current length.
    /// </summary>
    public static EditResult AddBlock(SequenceDocument doc, IUndoManager undo, string track, string? source, string? chain, double? start, double? length,
        string? sequence = null)
    {
        var at = SequenceAddress.ResolveTrack(doc.Save, track);
        if (!at.Ok) return FromAddress(at);
        var owner = doc.Save.Sequences[at.Value!.Sequence];
        var target = owner.Tracks[at.Value.Track];
        var badStart = start is { } given ? CheckStart(given) : null;
        if (badStart is not null) return badStart;
        var badLength = length is { } l ? CheckLength(l) : null;
        if (badLength is not null) return badLength;
        if (target.References is { } clips)
        {
            if (chain is not null || source is not null)
                return Fail(EditErrorCode.WrongTrackKind, $"Track '{owner.Name}/{target.Name}' plays sequences, so it holds clips: give sequence, not source or chain.");
            if (sequence is null) return Fail(EditErrorCode.InvalidValue, $"Track '{owner.Name}/{target.Name}' plays sequences: give the sequence to play.");
            var to = SequenceAddress.ResolveSequence(doc.Save, sequence);
            if (!to.Ok) return FromAddress(to);
            var targetName = doc.Save.Sequences[to.Value!.Sequence].Name;
            if (!doc.CanReference(at.Value.Sequence, targetName))
                return WouldCycle(owner.Name, targetName);
            undo.SealCoalescing();
            double clipStart = start ?? ClipTrackEnd(doc, at.Value.Sequence, target);
            var added = doc.AddReferenceClip(undo, at.Value.Sequence, at.Value.Track, targetName, clipStart, length);
            if (added is not null) return Fail(EditErrorCode.Refused, added);
            return Ok($"{owner.Name}/{target.Name}/{doc.Save.Sequences[at.Value.Sequence].Tracks[at.Value.Track].References!.Count - 1}");
        }
        if (sequence is not null)
            return Fail(EditErrorCode.WrongTrackKind, $"Track '{owner.Name}/{target.Name}' holds chain blocks, so it cannot play a sequence.");
        if (chain is null) return Fail(EditErrorCode.InvalidValue, $"Track '{owner.Name}/{target.Name}' holds chain blocks: give the chain.");

        if (source is null)
        {
            var sources = doc.Save.Sources;
            if (sources.Count == 0)
                return Fail(EditErrorCode.NotFound, "The library has no sources; add one with add-source first.");
            if (sources.Count > 1)
                return Fail(EditErrorCode.InvalidValue, $"Give source: the library has several ({string.Join(", ", sources)}).");
            source = sources[0];
        }
        var listed = FindSource(doc, source);
        if (listed is null)
            return Fail(EditErrorCode.NotFound, $"Source '{source}' is not in the library; add it with add-source first.");
        var chains = doc.SourceChains(listed);
        if (chains is null)
            return Fail(EditErrorCode.NotFound, $"Source '{listed}' is missing or could not be read.");
        if (chains.AnimationChains.All(c => c.Name != chain))
            return Fail(EditErrorCode.NotFound, $"Source '{listed}' has no chain named '{chain}'.");

        undo.SealCoalescing();
        doc.PlaceChain(undo, at.Value.Sequence, at.Value.Track, listed, chain, start ?? TrackEnd(doc, target), length);
        return Ok($"{owner.Name}/{target.Name}/{doc.Save.Sequences[at.Value.Sequence].Tracks[at.Value.Track].Chains.Count - 1}");
    }

    /// <summary>The end in seconds of the clip that starts last on a reference track (its full length); 0 when empty or when its target plays nothing.</summary>
    private static double ClipTrackEnd(SequenceDocument doc, int sequence, SequenceTrackSave track)
    {
        var ordered = SequenceTimeline.OrderedClips(track);
        if (ordered.Count == 0) return 0;
        var last = ordered[^1];
        double targetLength = SequenceReferences.Plays(doc.Save.Sequences, doc.Save.Sequences[sequence].Name, last.Sequence)
            && SequenceReferences.Find(doc.Save.Sequences, last.Sequence) is { } found
            ? SequenceTimeline.Length(doc.Save.Sequences, found, r => doc.CycleLength(r) ?? 0) : 0;
        return Math.Round(last.Start + SequenceTimeline.FullLength(last.Length, targetLength), 6);
    }

    /// <summary>The end in seconds of the block that starts last on a chain track (its full length, nothing follows to cut it); 0 when empty.</summary>
    private static double TrackEnd(SequenceDocument doc, SequenceTrackSave track)
    {
        var ordered = SequenceTimeline.Ordered(track);
        if (ordered.Count == 0) return 0;
        var last = ordered[^1];
        return Math.Round(last.Start + SequenceTimeline.FullLength(last.Length, doc.CycleLength(last) ?? 0), 6);
    }

    /// <summary>Moves a block to <paramref name="start"/> seconds (0 or more). On a reference track the block is a clip (index in list order).</summary>
    public static EditResult SetBlockStart(SequenceDocument doc, IUndoManager undo, string block, double start)
    {
        var at = SequenceAddress.ResolveBlock(doc.Save, block);
        if (!at.Ok) return FromAddress(at);
        var bad = CheckStart(start);
        if (bad is not null) return bad;
        var a = at.Value!;
        var track = doc.Save.Sequences[a.Sequence].Tracks[a.Track];

        undo.SealCoalescing();
        if (track.References is { } clips)
        {
            if (clips[a.Block].Start == start) return Ok();
            var refusedClip = doc.SetReferenceStart(undo, a.Sequence, a.Track, start, a.Block);
            return refusedClip is null ? Ok(block) : Fail(EditErrorCode.InvalidValue, refusedClip);
        }
        if (track.Chains[a.Block].Start == start) return Ok();
        var refused = doc.SetChainStart(undo, a.Sequence, a.Track, a.Block, start);
        return refused is null ? Ok(block) : Fail(EditErrorCode.InvalidValue, refused);
    }

    /// <summary>
    /// Sets a block's length in seconds (above 0), so its end no longer follows its chain. On a reference track block 0 is
    /// the clip.
    /// </summary>
    public static EditResult SetBlockLength(SequenceDocument doc, IUndoManager undo, string block, double length)
    {
        var at = SequenceAddress.ResolveBlock(doc.Save, block);
        if (!at.Ok) return FromAddress(at);
        var bad = CheckLength(length);
        if (bad is not null) return bad;
        var a = at.Value!;
        var track = doc.Save.Sequences[a.Sequence].Tracks[a.Track];

        undo.SealCoalescing();
        if (track.References is { } clips)
        {
            if (clips[a.Block].Length == length) return Ok();
            var refusedClip = doc.SetReferenceLength(undo, a.Sequence, a.Track, length, a.Block);
            return refusedClip is null ? Ok(block) : Fail(EditErrorCode.InvalidValue, refusedClip);
        }
        if (track.Chains[a.Block].Length == length) return Ok();
        var refused = doc.SetChainLength(undo, a.Sequence, a.Track, a.Block, length);
        return refused is null ? Ok(block) : Fail(EditErrorCode.InvalidValue, refused);
    }

    /// <summary>
    /// Unsets a block's length, so it plays one cycle of its chain (a clip, one run of its target) and follows edits to
    /// it. On a reference track the block is a clip (index in list order). Already unset is a no-op.
    /// </summary>
    public static EditResult ClearBlockLength(SequenceDocument doc, IUndoManager undo, string block)
    {
        var at = SequenceAddress.ResolveBlock(doc.Save, block);
        if (!at.Ok) return FromAddress(at);
        var a = at.Value!;
        var track = doc.Save.Sequences[a.Sequence].Tracks[a.Track];

        undo.SealCoalescing();
        if (track.References is { } clips)
        {
            if (clips[a.Block].Length is null) return Ok();
            var refused = doc.ClearReferenceLength(undo, a.Sequence, a.Track, a.Block);
            return refused is null ? Ok(block) : Fail(EditErrorCode.InvalidValue, refused);
        }
        if (track.Chains[a.Block].Length is null) return Ok();
        doc.ClearChainsLength(undo, a.Sequence, [new TimelineChainId(a.Track, a.Block)]);
        return Ok(block);
    }

    /// <summary>
    /// Sets a block's length so it ends at <paramref name="end"/> seconds: end - start, from the block's current start.
    /// On a reference track the block is a clip (index in list order).
    /// </summary>
    public static EditResult SetBlockEnd(SequenceDocument doc, IUndoManager undo, string block, double end)
    {
        var at = SequenceAddress.ResolveBlock(doc.Save, block);
        if (!at.Ok) return FromAddress(at);
        var a = at.Value!;
        var track = doc.Save.Sequences[a.Sequence].Tracks[a.Track];
        double start = track.References is { } clips ? clips[a.Block].Start : track.Chains[a.Block].Start;
        if (!double.IsFinite(end) || end <= start)
            return Fail(EditErrorCode.InvalidValue, $"The end {end.ToString(System.Globalization.CultureInfo.InvariantCulture)} must be after the block's start {start.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");

        return SetBlockLength(doc, undo, block, Math.Round(end - start, 6));
    }

    /// <summary>
    /// Points a block at <paramref name="chain"/> of <paramref name="source"/> (null: the block's current source), keeping
    /// its start and length. A reference track's clip has no chain, so it is refused.
    /// </summary>
    public static EditResult SetBlockChain(SequenceDocument doc, IUndoManager undo, string block, string? source, string chain)
    {
        var at = SequenceAddress.ResolveBlock(doc.Save, block);
        if (!at.Ok) return FromAddress(at);
        var a = at.Value!;
        var owner = doc.Save.Sequences[a.Sequence];
        var track = owner.Tracks[a.Track];
        if (track.References is not null)
            return Fail(EditErrorCode.WrongTrackKind, $"Track '{owner.Name}/{track.Name}' plays sequences, so its clips have no chain to set.");

        var wanted = source ?? track.Chains[a.Block].Source;
        var listed = FindSource(doc, wanted);
        if (listed is null)
            return Fail(EditErrorCode.NotFound, $"Source '{wanted}' is not in the library; add it with add-source first.");
        var chains = doc.SourceChains(listed);
        if (chains is null)
            return Fail(EditErrorCode.NotFound, $"Source '{listed}' is missing or could not be read.");
        if (chains.AnimationChains.All(c => c.Name != chain))
            return Fail(EditErrorCode.NotFound, $"Source '{listed}' has no chain named '{chain}'.");

        undo.SealCoalescing();
        doc.SetChainTarget(undo, a.Sequence, a.Track, a.Block, listed, chain);
        return Ok(block);
    }

    /// <summary>Deletes a block of a chain track, or a clip of a reference track (the track stays, possibly empty); later blocks of the track shift down by one.</summary>
    public static EditResult DeleteBlock(SequenceDocument doc, IUndoManager undo, string block)
    {
        var at = SequenceAddress.ResolveBlock(doc.Save, block);
        if (!at.Ok) return FromAddress(at);
        var a = at.Value!;
        if (doc.Save.Sequences[a.Sequence].Tracks[a.Track].References is not null)
        {
            undo.SealCoalescing();
            var refused = doc.DeleteReferenceClip(undo, a.Sequence, a.Track, a.Block);
            return refused is null ? Ok(block) : Fail(EditErrorCode.Refused, refused);
        }

        undo.SealCoalescing();
        doc.DeleteChain(undo, a.Sequence, a.Track, a.Block);
        return Ok(block);
    }

    /// <summary>
    /// Copies a block (or a clip of a reference track) to the end of the block list of track <paramref name="to"/> (default its
    /// own), in any sequence of the file. A null <paramref name="start"/> is the block's own start on another track, and right
    /// after the block on its own, where the editor's Duplicate puts it.
    /// </summary>
    public static EditResult DuplicateBlock(SequenceDocument doc, IUndoManager undo, string block, string? to, double? start)
    {
        var ends = ResolveEnds(doc, block, to, start, copy: true);
        if (ends.Error is not null) return ends.Error;
        var (from, dest) = (ends.From!, ends.To!);
        if (start is not null || !SameTrack(from, dest)) return Place(doc, undo, from, dest, start, copy: true);

        var track = doc.Save.Sequences[from.Sequence].Tracks[from.Track];
        if (track.References is { } clips)
            return Place(doc, undo, from, dest, Math.Round(clips[from.Block].Start + doc.ClipLength(clips[from.Block]), 6), copy: true);
        undo.SealCoalescing();
        doc.DuplicateChains(undo, from.Sequence, [new TimelineChainId(from.Track, from.Block)]);
        return Ok(LastBlock(doc, dest));
    }

    /// <summary>
    /// Moves a block (or a clip of a reference track) to track <paramref name="to"/> (default its own), in any sequence of the
    /// file, and to <paramref name="start"/> seconds (default unchanged). On another track it becomes the last in the block
    /// list, and later blocks of the old track shift down by one; on its own track it keeps its index.
    /// </summary>
    public static EditResult MoveBlock(SequenceDocument doc, IUndoManager undo, string block, string? to, double? start)
    {
        if (to is null && start is null) return Fail(EditErrorCode.InvalidValue, "move-block needs to, start or both.");
        var ends = ResolveEnds(doc, block, to, start, copy: false);
        if (ends.Error is not null) return ends.Error;
        if (!SameTrack(ends.From!, ends.To!)) return Place(doc, undo, ends.From!, ends.To!, start, copy: false);
        return start is { } at ? SetBlockStart(doc, undo, block, at) : Ok();
    }

    private static bool SameTrack(SeqAddr a, SeqAddr b) => a.Sequence == b.Sequence && a.Track == b.Track;

    /// <summary>The block and the destination track of a move or copy, or why it is refused.</summary>
    private static (SeqAddr? From, SeqAddr? To, EditResult? Error) ResolveEnds(SequenceDocument doc, string block, string? to, double? start, bool copy)
    {
        var at = SequenceAddress.ResolveBlock(doc.Save, block);
        if (!at.Ok) return (null, null, FromAddress(at));
        var dest = to is null ? new SeqAddrResult(at.Value! with { Block = -1 }, null) : SequenceAddress.ResolveTrack(doc.Save, to);
        if (!dest.Ok) return (null, null, FromAddress(dest));
        if (start is { } given && CheckStart(given) is { } badStart) return (null, null, badStart);

        var (a, b) = (at.Value!, dest.Value!);
        var owner = doc.Save.Sequences[b.Sequence];
        var (from, target) = (doc.Save.Sequences[a.Sequence].Tracks[a.Track], owner.Tracks[b.Track]);
        if (from.References is null != target.References is null)
            return (null, null, Fail(EditErrorCode.WrongTrackKind, target.References is null
                ? $"Track '{owner.Name}/{target.Name}' holds chain blocks, so it cannot take a clip."
                : $"Track '{owner.Name}/{target.Name}' plays sequences, so it cannot take a chain block."));
        // A clip that stays in its sequence closes no cycle that is not there already; another copy of one would.
        if (from.References is { } clips && (copy || a.Sequence != b.Sequence)
            && SequenceReferences.WouldCycle(doc.Save.Sequences, owner.Name, clips[a.Block].Sequence))
            return (null, null, WouldCycle(owner.Name, clips[a.Block].Sequence));
        return (a, b, null);
    }

    // Edits in place inside a gesture, as dragging a block to another track does, so it is one undo entry.
    private static EditResult Place(SequenceDocument doc, IUndoManager undo, SeqAddr from, SeqAddr to, double? start, bool copy)
    {
        undo.SealCoalescing();
        var before = doc.BeginGesture();
        var source = doc.Save.Sequences[from.Sequence].Tracks[from.Track];
        var target = doc.Save.Sequences[to.Sequence].Tracks[to.Track];
        string name;
        if (source.References is { } clips)
        {
            var clip = copy ? TimelineClipboard.CloneTracks([source])[0].References![from.Block] : clips[from.Block];
            name = clip.Sequence;
            clip.Start = start ?? clip.Start;
            if (!copy) clips.RemoveAt(from.Block);
            target.References!.Add(clip);
        }
        else
        {
            var chain = copy ? TimelineClipboard.CloneChains([source.Chains[from.Block]])[0] : source.Chains[from.Block];
            name = chain.Chain;
            chain.Start = start ?? chain.Start;
            if (!copy) source.Chains.RemoveAt(from.Block);
            target.Chains.Add(chain);
        }
        doc.CommitGesture(undo, $"{(copy ? "Duplicate" : "Move")} '{name}' to '{target.Name}'", before);
        return Ok(LastBlock(doc, to));
    }

    private static string LastBlock(SequenceDocument doc, SeqAddr track)
    {
        var owner = doc.Save.Sequences[track.Sequence];
        var target = owner.Tracks[track.Track];
        return $"{owner.Name}/{target.Name}/{SequenceAddress.BlockCount(target) - 1}";
    }

    // ---- helpers ----

    private static EditResult? CheckNewName(string name, IEnumerable<string> used, string what)
    {
        if (name.Length == 0) return Fail(EditErrorCode.EmptyName, $"A {what} needs a name.");
        if (name.Contains('/')) return Fail(EditErrorCode.InvalidAddress, $"A {what} name cannot contain '/'.");
        if (used.Contains(name)) return Fail(EditErrorCode.DuplicateName, $"A {what} named '{name}' already exists.");
        return null;
    }

    private static EditResult? CheckStart(double start) =>
        NumberFields.InRange(start) && start >= 0 ? null : Fail(EditErrorCode.InvalidValue, $"Start must be 0 or more, up to {NumberFields.Max}.");

    private static EditResult? CheckLength(double length) =>
        NumberFields.InRange(length) && length > 0 ? null : Fail(EditErrorCode.InvalidValue, $"Length must be above 0, up to {NumberFields.Max}.");

    /// <summary>The library entry <paramref name="source"/> names, as written in the file (case and slash style ignored), or null.</summary>
    private static string? FindSource(SequenceDocument doc, string source)
    {
        var wanted = source.Replace('\\', '/');
        return doc.Save.Sources.FirstOrDefault(s => string.Equals(s, wanted, StringComparison.OrdinalIgnoreCase));
    }

    private static EditResult WouldCycle(string owner, string target) =>
        Fail(EditErrorCode.WouldCycle, $"'{owner}' cannot play '{target}': that would make the sequences play each other in a loop.");

    private static EditResult NoSuchSource(string source) =>
        Fail(EditErrorCode.NotFound, $"No source named '{source}' in the library.");

    private static EditResult? CheckSourceFile(SequenceDocument doc, string path, out string full)
    {
        var folder = doc.Path.GetDirectoryContainingThis().FullPath;
        full = System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, path));
        var ext = System.IO.Path.GetExtension(full).ToLowerInvariant();
        if (ext is not (".achx" or ".achj"))
            return Fail(EditErrorCode.UnsupportedFile, $"{System.IO.Path.GetFileName(full)} can't be a source. Only .achx and .achj files can.");
        if (!File.Exists(full))
            return Fail(EditErrorCode.NotFound, $"File not found: {full}");
        return null;
    }

    private static bool SamePath(SequenceDocument doc, string listed, string full) =>
        new FilePath(System.IO.Path.Combine(doc.Path.GetDirectoryContainingThis().FullPath, listed)) == new FilePath(full);

    private static EditResult FromAddress(SeqAddrResult result) => Fail(result.Code, result.Error!);

    private static EditResult OutOfRange(int index, int count, string what) =>
        Fail(EditErrorCode.IndexOutOfRange, $"Index {index} is out of range: there are {count} {what}(s).");

    private static EditResult Ok(params string[] changed) => new(changed, [], []);

    private static EditResult Fail(EditErrorCode code, string message) => new([], [message], [], [code]);
}
