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
    private const float MaxLoops = 1_000_000f;

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

    // ---- tracks ----

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
    /// chain is playing, instead of restarting.
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

    /// <summary>
    /// Adds a track named after <paramref name="target"/> that plays that sequence once from <paramref name="start"/> seconds,
    /// at zero-based <paramref name="index"/> (default 0). Its clip is block 0 of the track.
    /// </summary>
    public static EditResult AddReferenceTrack(SequenceDocument doc, IUndoManager undo, string sequence, string target, double start, int? index)
    {
        var at = SequenceAddress.ResolveSequence(doc.Save, sequence);
        if (!at.Ok) return FromAddress(at);
        var to = SequenceAddress.ResolveSequence(doc.Save, target);
        if (!to.Ok) return FromAddress(to);
        if (!double.IsFinite(start) || start < 0) return Fail(EditErrorCode.InvalidValue, "Start must be 0 or more.");
        var owner = doc.Save.Sequences[at.Value!.Sequence];
        if (index is { } i && (i < 0 || i > owner.Tracks.Count)) return OutOfRange(i, owner.Tracks.Count + 1, "track slot");
        var targetName = doc.Save.Sequences[to.Value!.Sequence].Name;
        if (!doc.CanReference(at.Value.Sequence, targetName))
            return Fail(EditErrorCode.WouldCycle, $"'{owner.Name}' cannot play '{targetName}': that would make the sequences play each other in a loop.");

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
    /// Appends a block playing <paramref name="chain"/> of a listed source on a chain track, <paramref name="loops"/> cycles
    /// long from <paramref name="start"/> seconds. The source must be in the library and have the chain.
    /// </summary>
    public static EditResult AddBlock(SequenceDocument doc, IUndoManager undo, string track, string source, string chain, double start, float loops)
    {
        var at = SequenceAddress.ResolveTrack(doc.Save, track);
        if (!at.Ok) return FromAddress(at);
        var owner = doc.Save.Sequences[at.Value!.Sequence];
        var target = owner.Tracks[at.Value.Track];
        if (target.Reference is not null)
            return Fail(EditErrorCode.WrongTrackKind, $"Track '{owner.Name}/{target.Name}' plays a sequence, so it cannot hold chain blocks.");
        var badStart = CheckStart(start);
        if (badStart is not null) return badStart;
        var badLoops = CheckLoops(loops);
        if (badLoops is not null) return badLoops;

        var listed = FindSource(doc, source);
        if (listed is null)
            return Fail(EditErrorCode.NotFound, $"Source '{source}' is not in the library; add it with add-source first.");
        var chains = doc.SourceChains(listed);
        if (chains is null)
            return Fail(EditErrorCode.NotFound, $"Source '{listed}' is missing or could not be read.");
        if (chains.AnimationChains.All(c => c.Name != chain))
            return Fail(EditErrorCode.NotFound, $"Source '{listed}' has no chain named '{chain}'.");

        undo.SealCoalescing();
        doc.PlaceChain(undo, at.Value.Sequence, at.Value.Track, listed, chain, start, loops);
        return Ok($"{owner.Name}/{target.Name}/{doc.Save.Sequences[at.Value.Sequence].Tracks[at.Value.Track].Chains.Count - 1}");
    }

    /// <summary>Moves a block to <paramref name="start"/> seconds (0 or more). On a reference track block 0 is the clip.</summary>
    public static EditResult SetBlockStart(SequenceDocument doc, IUndoManager undo, string block, double start)
    {
        var at = SequenceAddress.ResolveBlock(doc.Save, block);
        if (!at.Ok) return FromAddress(at);
        var bad = CheckStart(start);
        if (bad is not null) return bad;
        var a = at.Value!;
        var track = doc.Save.Sequences[a.Sequence].Tracks[a.Track];

        undo.SealCoalescing();
        if (track.Reference is { } clip)
        {
            if (clip.Start == start) return Ok();
            var refusedClip = doc.SetReferenceStart(undo, a.Sequence, a.Track, start);
            return refusedClip is null ? Ok(block) : Fail(EditErrorCode.InvalidValue, refusedClip);
        }
        if (track.Chains[a.Block].Start == start) return Ok();
        var refused = doc.SetChainStart(undo, a.Sequence, a.Track, a.Block, start);
        return refused is null ? Ok(block) : Fail(EditErrorCode.InvalidValue, refused);
    }

    /// <summary>Sets a block's length in playback cycles (above 0). On a reference track block 0 is the clip.</summary>
    public static EditResult SetBlockLoops(SequenceDocument doc, IUndoManager undo, string block, float loops)
    {
        var at = SequenceAddress.ResolveBlock(doc.Save, block);
        if (!at.Ok) return FromAddress(at);
        var bad = CheckLoops(loops);
        if (bad is not null) return bad;
        var a = at.Value!;
        var track = doc.Save.Sequences[a.Sequence].Tracks[a.Track];

        undo.SealCoalescing();
        if (track.Reference is { } clip)
        {
            if (clip.Loops == loops) return Ok();
            var refusedClip = doc.SetReferenceLoops(undo, a.Sequence, a.Track, loops);
            return refusedClip is null ? Ok(block) : Fail(EditErrorCode.InvalidValue, refusedClip);
        }
        if (track.Chains[a.Block].Loops == loops) return Ok();
        var refused = doc.SetChainLoops(undo, a.Sequence, a.Track, a.Block, loops);
        return refused is null ? Ok(block) : Fail(EditErrorCode.InvalidValue, refused);
    }

    /// <summary>
    /// Sets a chain block's loops so its own cycles end at <paramref name="end"/> seconds: (end - start) / chain length,
    /// keeping the fraction. A reference clip is refused, since its length depends on the played sequence.
    /// </summary>
    public static EditResult SetBlockEnd(SequenceDocument doc, IUndoManager undo, string block, double end)
    {
        var at = SequenceAddress.ResolveBlock(doc.Save, block);
        if (!at.Ok) return FromAddress(at);
        var a = at.Value!;
        var track = doc.Save.Sequences[a.Sequence].Tracks[a.Track];
        if (track.Reference is not null)
            return Fail(EditErrorCode.WrongTrackKind, "A track that plays a sequence has no chain length; set its clip's loops with set-block-loops.");

        var reference = track.Chains[a.Block];
        if (!double.IsFinite(end) || end <= reference.Start)
            return Fail(EditErrorCode.InvalidValue, $"The end {end.ToString(System.Globalization.CultureInfo.InvariantCulture)} must be after the block's start {reference.Start.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
        if (doc.CycleLength(reference) is not { } cycle || cycle <= 0)
            return Fail(EditErrorCode.NotFound, $"The chain '{reference.Chain}' of '{reference.Source}' is missing or unreadable, so its length is unknown.");

        return SetBlockLoops(doc, undo, block, (float)((end - reference.Start) / cycle));
    }

    /// <summary>Deletes a block of a chain track; later blocks of the track shift down by one. A reference track is deleted whole.</summary>
    public static EditResult DeleteBlock(SequenceDocument doc, IUndoManager undo, string block)
    {
        var at = SequenceAddress.ResolveBlock(doc.Save, block);
        if (!at.Ok) return FromAddress(at);
        var a = at.Value!;
        if (doc.Save.Sequences[a.Sequence].Tracks[a.Track].Reference is not null)
            return Fail(EditErrorCode.WrongTrackKind, "A track that plays a sequence has no chain blocks to delete; delete the track instead.");

        undo.SealCoalescing();
        doc.DeleteChain(undo, a.Sequence, a.Track, a.Block);
        return Ok(block);
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
        double.IsFinite(start) && start >= 0 ? null : Fail(EditErrorCode.InvalidValue, "Start must be 0 or more.");

    private static EditResult? CheckLoops(float loops) =>
        float.IsFinite(loops) && loops > 0 && loops <= MaxLoops ? null : Fail(EditErrorCode.InvalidValue, "Loops must be above 0.");

    /// <summary>The library entry <paramref name="source"/> names, as written in the file (case and slash style ignored), or null.</summary>
    private static string? FindSource(SequenceDocument doc, string source)
    {
        var wanted = source.Replace('\\', '/');
        return doc.Save.Sources.FirstOrDefault(s => string.Equals(s, wanted, StringComparison.OrdinalIgnoreCase));
    }

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
