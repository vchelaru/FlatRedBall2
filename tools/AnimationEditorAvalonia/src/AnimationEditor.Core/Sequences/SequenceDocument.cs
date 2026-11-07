using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// An open <c>.seqj</c>: its saved data plus the source <c>.achx</c>/<c>.achj</c> files it uses, loaded in the
/// editor's UV form. Every edit is one undo entry and saves the file right away, like an <c>.achx</c> tab.
/// </summary>
/// <remarks>
/// Undo snapshots the whole file as JSON. A <c>.seqj</c> is small, so this stays cheap and needs no per-edit
/// command classes; revisit if files grow large.
/// </remarks>
public sealed class SequenceDocument
{
    private const string DefaultSequenceName = "Sequence";
    private const string DefaultTrackName = "Track";

    private int? _trackToSelect;

    // Keyed by the source string as written in the file. A null value is a source that could not be loaded.
    private readonly Dictionary<string, AnimationChainListSave?> _sources = new(StringComparer.OrdinalIgnoreCase);

    // What each source's file looked like when it was read, so a later change to the file is seen.
    private readonly Dictionary<string, FileStamp> _stamps = new(StringComparer.OrdinalIgnoreCase);

    private readonly record struct FileStamp(bool Exists, DateTime WrittenUtc, long Length);

    private SequenceDocument(string path, SequenceListSave save)
    {
        Path = new FilePath(path);
        Save = save;
        SelectedSequence = save.Sequences.Count > 0 ? 0 : -1;
        SyncSources();
    }

    public FilePath Path { get; }

    /// <summary>The file's data. Replaced by undo and redo, so do not hold on to it or to anything inside it.</summary>
    public SequenceListSave Save { get; private set; }

    /// <summary>
    /// The sequence the list has selected, or -1 for none. Every edit moves it to the sequence it changed (an add,
    /// rename or delete name theirs; any other edit is found by diffing), and undo and redo move it the same way, so
    /// the list follows the history.
    /// </summary>
    public int SelectedSequence { get; set; }

    /// <summary>Why the last save failed, or null when it succeeded.</summary>
    public string? SaveError { get; private set; }

    /// <summary>Raised after any edit, undo, redo or source reload.</summary>
    public event Action? Changed;

    /// <summary>Reads <paramref name="path"/> and loads its sources. A source that can't be read is kept and flagged.</summary>
    /// <exception cref="System.Text.Json.JsonException">The file is not a valid <c>.seqj</c>.</exception>
    public static SequenceDocument Load(string path) => new(path, SequenceListSave.FromFile(path));

    /// <summary>
    /// Re-reads every source whose file changed since it was loaded (or could not be read and now can), and raises
    /// <see cref="Changed"/> when any did. Returns whether any did.
    /// </summary>
    public bool ReloadChangedSources()
    {
        if (!SyncSources()) return false;
        Changed?.Invoke();
        return true;
    }

    /// <summary>True when <paramref name="source"/> is missing or could not be read.</summary>
    public bool IsSourceMissing(string source) => SourceChains(source) is null;

    /// <summary>The chains of <paramref name="source"/>, with UV coordinates, or null when it is missing.</summary>
    public AnimationChainListSave? SourceChains(string source) =>
        _sources.TryGetValue(source, out var list) ? list : null;

    /// <summary>
    /// Adds the <c>.achx</c>/<c>.achj</c> files at <paramref name="absolutePaths"/> as sources, in one undo entry, stored
    /// relative to this file. Returns a message for each file refused: a file of another type (a <c>.seqj</c> included)
    /// or one that already is a source. The rest are still added.
    /// </summary>
    public IReadOnlyList<string> AddSources(IUndoManager undo, IEnumerable<string> absolutePaths)
    {
        var errors = new List<string>();
        var taken = Save.Sources.Select(SourcePath).ToHashSet();
        var added = new List<FilePath>();
        foreach (var file in absolutePaths.Select(path => new FilePath(path)))
        {
            if (file.Extension is not ("achx" or "achj"))
                errors.Add($"{file.NoPath} can't be a source. Only .achx and .achj files can.");
            else if (!taken.Add(file))
                errors.Add($"{file.NoPath} is already in the library.");
            else
                added.Add(file);
        }
        if (added.Count == 0) return errors;

        string description = added.Count == 1 ? $"Add Source '{added[0].NoPath}'" : $"Add {added.Count} Sources";
        var folder = Path.GetDirectoryContainingThis();
        Edit(undo, description, save => save.Sources.AddRange(added.Select(file => file.RelativeTo(folder))));
        return errors;
    }

    /// <summary>
    /// Removes <paramref name="source"/> from the library. Returns why it was refused (a chain on the timeline still
    /// uses it), or null.
    /// </summary>
    public string? RemoveSource(IUndoManager undo, string source)
    {
        int uses = Save.Sequences.SelectMany(s => s.Tracks).SelectMany(t => t.Chains)
            .Count(reference => string.Equals(reference.Source, source, StringComparison.OrdinalIgnoreCase));
        if (uses > 0)
            return $"{source} is used by {Plural.Format(uses, "chain")} on the timeline. Remove those first.";
        Edit(undo, $"Remove Source '{new FilePath(source).NoPath}'", save => save.Sources.Remove(source));
        return null;
    }

    /// <summary>
    /// The absolute path of <paramref name="frame"/>'s texture. A relative texture name resolves against
    /// <paramref name="source"/>'s folder, since each source can sit in a different folder from this file.
    /// </summary>
    public string? ResolveTexturePath(string source, AnimationFrameSave frame)
    {
        if (string.IsNullOrEmpty(frame.TextureName)) return null;
        if (System.IO.Path.IsPathRooted(frame.TextureName)) return new FilePath(frame.TextureName).FullPath;
        string sourceFolder = SourcePath(source).GetDirectoryContainingThis().FullPath;
        return new FilePath(System.IO.Path.Combine(sourceFolder, frame.TextureName)).FullPath;
    }

    /// <summary>Adds an empty sequence with a name no other sequence uses. Returns that name.</summary>
    public string AddSequence(IUndoManager undo)
    {
        string name = NameGenerator.Unique(Save.Sequences.Select(s => s.Name), DefaultSequenceName);
        Edit(undo, $"Add Sequence '{name}'", save => save.Sequences.Add(new SequenceSave { Name = name }),
            selectBefore: SelectedSequence, selectAfter: Save.Sequences.Count);
        return name;
    }

    /// <summary>
    /// Inserts a deep copy of the sequence at <paramref name="index"/> right after it, with the next free name, and selects
    /// it, as one undo entry. Returns the new name.
    /// </summary>
    public string DuplicateSequence(IUndoManager undo, int index)
    {
        string original = Save.Sequences[index].Name;
        string name = NameGenerator.ForCopy(Save.Sequences.Select(s => s.Name), original);
        Edit(undo, $"Duplicate Sequence '{original}'", save =>
        {
            var copy = SequenceClipboard.Clone(save.Sequences[index]);
            copy.Name = name;
            save.Sequences.Insert(index + 1, copy);
        }, selectBefore: index, selectAfter: index + 1);
        return name;
    }

    /// <summary>The clipboard text for the sequence at <paramref name="index"/> (see <see cref="SequenceClipboard"/>).</summary>
    public string CopySequence(int index) =>
        SequenceClipboard.Write(Save.Sequences[index], Path.GetDirectoryContainingThis().FullPath);

    /// <summary>
    /// Pastes a copied sequence after the selected one (at the end when none is selected) with a free name, and selects it.
    /// The sources it uses join this file's source list when not already there, all as one undo entry.
    /// </summary>
    public SequencePasteResult PasteSequence(IUndoManager undo, SequenceClipboardContent content)
    {
        int selected = SelectedSequence >= 0 && SelectedSequence < Save.Sequences.Count ? SelectedSequence : Save.Sequences.Count - 1;
        int insertAt = selected + 1;
        string name = NameGenerator.ForCopy(Save.Sequences.Select(s => s.Name),
            string.IsNullOrWhiteSpace(content.Sequence.Name) ? DefaultSequenceName : content.Sequence.Name.Trim());
        var errors = new List<string>();
        var folder = Path.GetDirectoryContainingThis();
        Edit(undo, $"Paste Sequence '{name}'", save =>
        {
            var pasted = SequenceClipboard.Clone(content.Sequence);
            pasted.Name = name;
            MergeSources(save, pasted.Tracks.SelectMany(track => track.Chains), folder, errors);
            save.Sequences.Insert(insertAt, pasted);
        }, selectBefore: SelectedSequence, selectAfter: insertAt);
        return new SequencePasteResult(name, errors);
    }

    /// <summary>The clipboard text for the given chains of <paramref name="sequence"/>, in document order (see <see cref="TimelineClipboard"/>).</summary>
    public string CopyChains(int sequence, IReadOnlyList<TimelineChainId> chains)
    {
        var tracks = Save.Sequences[sequence].Tracks;
        var ordered = chains.Distinct().OrderBy(id => id.Track).ThenBy(id => id.Chain).ToList();
        return TimelineClipboard.WriteChains(
            ordered.Select(id => new CopiedChain(tracks[id.Track].Chains[id.Chain], tracks[id.Track], id.Track)).ToList(),
            Path.GetDirectoryContainingThis().FullPath);
    }

    /// <summary>The clipboard text for the given tracks of <paramref name="sequence"/>, top lane first.</summary>
    public string CopyTracks(int sequence, IReadOnlyList<int> tracks) =>
        TimelineClipboard.WriteTracks(tracks.Distinct().OrderBy(track => track).Select(track => Save.Sequences[sequence].Tracks[track]).ToList(),
            Path.GetDirectoryContainingThis().FullPath);

    /// <summary>
    /// Pastes copied chains into <paramref name="sequence"/>, the earliest at <paramref name="time"/> seconds and the rest keeping
    /// their distance from it. Which track each lands on depends on <paramref name="selectedTrack"/>:
    /// with none, every chain goes back to the track it came from, found by name (a track the sequence lacks is made at the end
    /// with the copied track's offset, flip, z and visibility); with one, the first copied chain's track (the topmost lane copied
    /// from) is that track and each other copied track keeps its lane distance from it, a lane the sequence does not have
    /// yet being made at the end, one per copied track, with that track's settings. An envelope without track information
    /// puts everything on the selected track, else on the track named like the first chain's, else on a new one. The sources
    /// the chains use join this file's source list when not already there, all as one undo entry.
    /// </summary>
    public TimelinePasteResult PasteChains(IUndoManager undo, int sequence, TimelineClipboardContent content, int? selectedTrack, double time)
    {
        if (content.Chains.Count == 0) return TimelinePasteResult.Empty;
        var tracks = Save.Sequences[sequence].Tracks;
        bool legacy = content.SourceTracks.Count == 0;
        // An envelope with no track information is one track, the first chain's; every chain is on it.
        var sourceTracks = legacy
            ? new[] { new CopiedTrack(string.IsNullOrWhiteSpace(content.TrackName) ? DefaultTrackName : content.TrackName, 0, 0, 0, 0, false, true) }
            : content.SourceTracks;
        int ChainTrack(int chain) => legacy ? 0 : content.ChainTracks[chain];

        // For each source track: the existing track it lands on, or null for one to be made.
        var landing = new int?[sourceTracks.Count];
        int? anchor = selectedTrack is { } selected && selected >= 0 && selected < tracks.Count ? selected : null;
        for (int i = 0; i < sourceTracks.Count; i++)
        {
            if (anchor is { } start)
            {
                int lane = start + sourceTracks[i].Lane - sourceTracks[0].Lane;
                landing[i] = lane < tracks.Count ? lane : null;
            }
            else
            {
                int byName = tracks.FindIndex(track => track.Name == sourceTracks[i].Name);
                landing[i] = byName >= 0 ? byName : null;
            }
        }
        string firstName = landing[ChainTrack(0)] is { } existing ? tracks[existing].Name : sourceTracks[ChainTrack(0)].Name;
        string description = content.Chains.Count == 1
            ? $"Paste '{content.Chains[0].Chain}' on '{firstName}'"
            : $"Paste {Plural.Format(content.Chains.Count, "chain")}";

        var errors = new List<string>();
        var made = new List<TimelineChainId>();
        var folder = Path.GetDirectoryContainingThis();
        Edit(undo, description, save =>
        {
            var destination = save.Sequences[sequence].Tracks;
            var target = new int[sourceTracks.Count];
            for (int i = 0; i < sourceTracks.Count; i++)
            {
                if (landing[i] is { } index)
                {
                    target[i] = index;
                    continue;
                }
                var source = sourceTracks[i];
                destination.Add(new SequenceTrackSave
                {
                    Name = NameGenerator.Unique(destination.Select(t => t.Name), source.Name),
                    X = source.X,
                    Y = source.Y,
                    Z = legacy ? destination.Count : source.Z,
                    FlipHorizontal = source.FlipHorizontal,
                    Visible = source.Visible,
                });
                target[i] = destination.Count - 1;
            }
            var pasted = TimelineClipboard.CloneChains(content.Chains);
            MergeSources(save, pasted, folder, errors);
            for (int i = 0; i < pasted.Count; i++)
            {
                pasted[i].Start += Math.Max(0, time);
                var chains = destination[target[ChainTrack(i)]].Chains;
                chains.Add(pasted[i]);
                made.Add(new TimelineChainId(target[ChainTrack(i)], chains.Count - 1));
            }
        });
        return new TimelinePasteResult(made, Array.Empty<int>(), errors);
    }

    /// <summary>
    /// Pastes copied tracks into <paramref name="sequence"/> right after track <paramref name="afterTrack"/> (at the end when
    /// it is null or not a track), each with a free name. Their sources join this file's source list as
    /// <see cref="PasteChains"/> does, all as one undo entry.
    /// </summary>
    public TimelinePasteResult PasteTracks(IUndoManager undo, int sequence, TimelineClipboardContent content, int? afterTrack)
    {
        if (content.Tracks.Count == 0) return TimelinePasteResult.Empty;
        var tracks = Save.Sequences[sequence].Tracks;
        int insertAt = afterTrack is { } after && after >= 0 && after < tracks.Count ? after + 1 : tracks.Count;
        var used = tracks.Select(t => t.Name).ToList();
        var names = new List<string>();
        foreach (var track in content.Tracks)
        {
            string name = NameGenerator.ForCopy(used, string.IsNullOrWhiteSpace(track.Name) ? DefaultTrackName : track.Name.Trim());
            names.Add(name);
            used.Add(name);
        }
        string description = names.Count == 1 ? $"Paste Track '{names[0]}'" : $"Paste {Plural.Format(names.Count, "track")}";
        var errors = new List<string>();
        var folder = Path.GetDirectoryContainingThis();
        Edit(undo, description, save =>
        {
            var pasted = TimelineClipboard.CloneTracks(content.Tracks);
            for (int i = 0; i < pasted.Count; i++) pasted[i].Name = names[i];
            MergeSources(save, pasted.SelectMany(track => track.Chains), folder, errors);
            save.Sequences[sequence].Tracks.InsertRange(insertAt, pasted);
        }, trackBefore: afterTrack, trackAfter: insertAt);
        return new TimelinePasteResult(Array.Empty<TimelineChainId>(), Enumerable.Range(insertAt, names.Count).ToList(), errors);
    }

    /// <summary>
    /// Copies the given chains onto the tracks they are on, shifted so the earliest copy starts at the latest end of the
    /// selection (a missing chain counts as <see cref="TimelineLayout.MissingLength"/> long): they land right after what was
    /// selected and keep their distances from each other. One undo entry.
    /// </summary>
    public TimelinePasteResult DuplicateChains(IUndoManager undo, int sequence, IReadOnlyList<TimelineChainId> chains)
    {
        var ids = chains.Distinct().OrderBy(id => id.Track).ThenBy(id => id.Chain).ToList();
        if (ids.Count == 0) return TimelinePasteResult.Empty;
        var tracks = Save.Sequences[sequence].Tracks;
        var blocks = TimelineLayout.Blocks(Save.Sequences[sequence], CycleLength);
        var selected = ids.Select(id => blocks.First(block => block.Track == id.Track && block.Chain == id.Chain)).ToList();
        double shift = TimelineDuplicate.Shift(selected);
        string description = ids.Count == 1
            ? $"Duplicate '{tracks[ids[0].Track].Chains[ids[0].Chain].Chain}'"
            : $"Duplicate {Plural.Format(ids.Count, "chain")}";
        // Copies are appended to their track in order, so each lands after the chains the track has now and the copies before it.
        var appended = new Dictionary<int, int>();
        var made = new List<TimelineChainId>();
        foreach (var id in ids)
        {
            int already = appended.GetValueOrDefault(id.Track);
            made.Add(new TimelineChainId(id.Track, tracks[id.Track].Chains.Count + already));
            appended[id.Track] = already + 1;
        }
        Edit(undo, description, save =>
        {
            var destination = save.Sequences[sequence].Tracks;
            foreach (var id in ids)
            {
                var copy = TimelineClipboard.CloneChains(new[] { destination[id.Track].Chains[id.Chain] })[0];
                copy.Start += shift;
                destination[id.Track].Chains.Add(copy);
            }
        });
        return new TimelinePasteResult(made, Array.Empty<int>(), Array.Empty<string>());
    }

    /// <summary>Copies the given tracks, with their chains, as a block right below the lowest of them, each with a free name. One undo entry.</summary>
    public TimelinePasteResult DuplicateTracks(IUndoManager undo, int sequence, IReadOnlyList<int> tracks)
    {
        var indices = tracks.Distinct().OrderBy(track => track).ToList();
        if (indices.Count == 0) return TimelinePasteResult.Empty;
        var existing = Save.Sequences[sequence].Tracks;
        int insertAt = indices[^1] + 1;
        var used = existing.Select(t => t.Name).ToList();
        var names = new List<string>();
        foreach (int index in indices)
        {
            string name = NameGenerator.ForCopy(used, existing[index].Name);
            names.Add(name);
            used.Add(name);
        }
        string description = indices.Count == 1 ? $"Duplicate Track '{existing[indices[0]].Name}'" : $"Duplicate {Plural.Format(indices.Count, "track")}";
        Edit(undo, description, save =>
        {
            var destination = save.Sequences[sequence].Tracks;
            var copies = TimelineClipboard.CloneTracks(indices.Select(index => destination[index]).ToList());
            for (int i = 0; i < copies.Count; i++) copies[i].Name = names[i];
            destination.InsertRange(insertAt, copies);
        }, trackBefore: indices[0], trackAfter: insertAt);
        return new TimelinePasteResult(Array.Empty<TimelineChainId>(), Enumerable.Range(insertAt, names.Count).ToList(), Array.Empty<string>());
    }

    /// <summary>
    /// Points each pasted reference at its source as <paramref name="save"/> writes it, and lists a source the file does not
    /// have yet. The references' sources arrive as absolute paths. A source already listed (in any case) is reused, a file that
    /// can't be a source (another type, or this file) is kept as a reference the library shows as missing and reported once in
    /// <paramref name="errors"/>. Shared by every paste, so they agree on what a pasted source becomes.
    /// </summary>
    private void MergeSources(SequenceListSave save, IEnumerable<SequenceChainReferenceSave> references, FilePath folder, List<string> errors)
    {
        // Each source as this file writes it, keyed by where it points.
        var listed = new Dictionary<FilePath, string>();
        foreach (string source in save.Sources)
            listed.TryAdd(SourcePath(source), source);
        var refused = new HashSet<FilePath>();
        foreach (var reference in references.Where(reference => reference.Source.Length > 0))
        {
            var file = new FilePath(SequenceClipboard.AbsolutePath(folder.FullPath, reference.Source));
            if (!listed.TryGetValue(file, out string? written))
            {
                written = file.RelativeTo(folder);
                if (file.Extension is not ("achx" or "achj") || file == Path)
                {
                    if (refused.Add(file)) errors.Add($"{file.NoPath} can't be a source. Only .achx and .achj files can.");
                }
                else
                {
                    save.Sources.Add(written);
                    listed[file] = written;
                }
            }
            reference.Source = written;
        }
    }

    /// <summary>
    /// Renames the sequence at <paramref name="index"/>. Returns why it was refused, or null. Undo and redo select the
    /// renamed sequence; the rename itself does too, unless <paramref name="keepSelection"/> is set, for a rename
    /// committed by a click on another row, which must stay selected.
    /// </summary>
    public string? RenameSequence(IUndoManager undo, int index, string name, bool keepSelection = false)
    {
        name = name.Trim();
        string oldName = Save.Sequences[index].Name;
        if (name.Length == 0) return "A sequence needs a name.";
        if (name == oldName) return null;
        if (Save.Sequences.Where((_, i) => i != index).Any(s => s.Name == name))
            return $"A sequence named \"{name}\" already exists.";
        Edit(undo, $"Rename Sequence '{oldName}' to '{name}'", save => save.Sequences[index].Name = name,
            selectBefore: index, selectAfter: index, selectOnDo: !keepSelection);
        return null;
    }

    public void DeleteSequence(IUndoManager undo, int index)
    {
        string name = Save.Sequences[index].Name;
        // The neighbour that takes its place, or the last one when the deleted sequence was last.
        Edit(undo, $"Delete Sequence '{name}'", save => save.Sequences.RemoveAt(index),
            selectBefore: index, selectAfter: Math.Min(index, Save.Sequences.Count - 2));
    }

    /// <summary>Sets whether sequence <paramref name="index"/> restarts at 0 once it ends.</summary>
    public void SetLoop(IUndoManager undo, int index, bool loop)
    {
        string name = Save.Sequences[index].Name;
        Edit(undo, $"Turn Loop {(loop ? "On" : "Off")} for '{name}'", save => save.Sequences[index].Loop = loop);
    }

    /// <summary>The chain <paramref name="reference"/> points at, or null when its source or chain is missing.</summary>
    public AnimationChainSave? FindChain(SequenceChainReferenceSave reference) =>
        SourceChains(reference.Source)?.AnimationChains.FirstOrDefault(chain => chain.Name == reference.Chain);

    /// <summary>
    /// Where <paramref name="reference"/>'s chain is defined: the absolute path of its source file, or an error message
    /// when the source can't be read or has no such chain. Exactly one of the two is non-null. A reference with an
    /// empty <see cref="SequenceChainReferenceSave.Chain"/> locates just the source file.
    /// </summary>
    public (string? Path, string? Error) LocateDefinition(SequenceChainReferenceSave reference)
    {
        if (IsSourceMissing(reference.Source)) return (null, $"{reference.Source} is missing or could not be read.");
        if (reference.Chain.Length > 0 && FindChain(reference) is null) return (null, $"{reference.Source} has no chain named \"{reference.Chain}\".");
        return (SourcePath(reference.Source).FullPath, null);
    }

    /// <summary>
    /// Seconds of one playback cycle of <paramref name="reference"/>'s chain (twice its length for ping-pong), or null
    /// when the chain is missing.
    /// </summary>
    public double? CycleLength(SequenceChainReferenceSave reference) =>
        FindChain(reference) is { } chain ? ChainClock.TotalLength(chain) : null;

    /// <summary>
    /// Adds <paramref name="chain"/> of <paramref name="source"/> at <paramref name="start"/> seconds, playing once, on
    /// track <paramref name="track"/> of sequence <paramref name="sequence"/>. A null track adds a new track named
    /// after the chain.
    /// </summary>
    public void PlaceChain(IUndoManager undo, int sequence, int? track, string source, string chain, double start)
    {
        var reference = new SequenceChainReferenceSave { Source = source, Chain = chain, Start = Math.Max(0, start) };
        if (track is { } index)
        {
            string trackName = Save.Sequences[sequence].Tracks[index].Name;
            Edit(undo, $"Add '{chain}' to '{trackName}'", save => save.Sequences[sequence].Tracks[index].Chains.Add(reference));
            return;
        }
        string name = NameGenerator.Unique(Save.Sequences[sequence].Tracks.Select(t => t.Name), chain);
        Edit(undo, $"Add Track '{name}'", save =>
        {
            var tracks = save.Sequences[sequence].Tracks;
            tracks.Add(new SequenceTrackSave { Name = name, Z = tracks.Count, Chains = { reference } });
        });
    }

    /// <summary>
    /// Adds an empty track to the end of sequence <paramref name="sequence"/>, named <c>Track</c> (or <c>Track 2</c>, and
    /// so on, when that is taken). Returns the name.
    /// </summary>
    public string AddTrack(IUndoManager undo, int sequence)
    {
        string name = NameGenerator.Unique(Save.Sequences[sequence].Tracks.Select(t => t.Name), DefaultTrackName);
        Edit(undo, $"Add Track '{name}'", save =>
        {
            var tracks = save.Sequences[sequence].Tracks;
            tracks.Add(new SequenceTrackSave { Name = name, Z = tracks.Count });
        });
        return name;
    }

    /// <summary>Removes chain reference <paramref name="chain"/> (an index into the track's list) from its track.</summary>
    public void DeleteChain(IUndoManager undo, int sequence, int track, int chain)
    {
        var owner = Save.Sequences[sequence].Tracks[track];
        Edit(undo, $"Remove '{owner.Chains[chain].Chain}' from '{owner.Name}'",
            save => save.Sequences[sequence].Tracks[track].Chains.RemoveAt(chain));
    }

    /// <summary>Removes the given chain references, wherever they sit, as one undo entry.</summary>
    public void DeleteChains(IUndoManager undo, int sequence, IReadOnlyList<TimelineChainId> chains)
    {
        var distinct = chains.Distinct().ToList();
        if (distinct.Count == 0) return;
        if (distinct.Count == 1)
        {
            DeleteChain(undo, sequence, distinct[0].Track, distinct[0].Chain);
            return;
        }
        Edit(undo, $"Remove {Plural.Format(distinct.Count, "chain")}", save =>
        {
            var tracks = save.Sequences[sequence].Tracks;
            // Highest index first, so earlier removals do not move the ones still to go.
            foreach (var id in distinct.OrderByDescending(id => id.Chain))
                tracks[id.Track].Chains.RemoveAt(id.Chain);
        });
    }

    /// <summary>Removes the given tracks with their chains as one undo entry.</summary>
    public void DeleteTracks(IUndoManager undo, int sequence, IReadOnlyList<int> tracks)
    {
        var distinct = tracks.Distinct().ToList();
        if (distinct.Count == 0) return;
        if (distinct.Count == 1)
        {
            DeleteTrack(undo, sequence, distinct[0]);
            return;
        }
        Edit(undo, $"Delete {Plural.Format(distinct.Count, "track")}", save =>
        {
            foreach (int track in distinct.OrderByDescending(track => track))
                save.Sequences[sequence].Tracks.RemoveAt(track);
        });
    }

    public void DeleteTrack(IUndoManager undo, int sequence, int track) =>
        Edit(undo, $"Delete Track '{Save.Sequences[sequence].Tracks[track].Name}'",
            save => save.Sequences[sequence].Tracks.RemoveAt(track));

    /// <summary>Renames a track. Returns why it was refused (empty, or another track of the sequence has the name), or null.</summary>
    public string? RenameTrack(IUndoManager undo, int sequence, int track, string name)
    {
        name = name.Trim();
        var tracks = Save.Sequences[sequence].Tracks;
        string oldName = tracks[track].Name;
        if (name.Length == 0) return "A track needs a name.";
        if (name == oldName) return null;
        if (tracks.Where((_, i) => i != track).Any(t => t.Name == name))
            return $"A track named \"{name}\" already exists.";
        Edit(undo, $"Rename Track '{oldName}' to '{name}'", save => save.Sequences[sequence].Tracks[track].Name = name);
        return null;
    }

    /// <summary>Sets a track's offset from the entity origin.</summary>
    public void SetTrackOffset(IUndoManager undo, int sequence, int track, float x, float y) =>
        Edit(undo, $"Set Offset of '{Save.Sequences[sequence].Tracks[track].Name}' to ({Format(x)}, {Format(y)})", save =>
        {
            var target = save.Sequences[sequence].Tracks[track];
            target.X = x;
            target.Y = y;
        });

    /// <summary>
    /// Hides or shows a track: its <see cref="SequenceTrackSave.Visible"/> flag, which is saved in the file. A no-op
    /// (no undo entry) when the track already is that way.
    /// </summary>
    public void SetTrackVisible(IUndoManager undo, int sequence, int track, bool visible) =>
        Edit(undo, $"{(visible ? "Show" : "Hide")} Track '{Save.Sequences[sequence].Tracks[track].Name}'",
            save => save.Sequences[sequence].Tracks[track].Visible = visible);

    /// <summary>Shows every track of a sequence again, as one undo entry; none when all are already shown.</summary>
    public void ShowAllTracks(IUndoManager undo, int sequence) =>
        Edit(undo, "Show All Tracks", save =>
        {
            foreach (var track in save.Sequences[sequence].Tracks) track.Visible = true;
        });

    public void SetTrackFlip(IUndoManager undo, int sequence, int track, bool flip) =>
        Edit(undo, $"Turn Flip {(flip ? "On" : "Off")} for '{Save.Sequences[sequence].Tracks[track].Name}'",
            save => save.Sequences[sequence].Tracks[track].FlipHorizontal = flip);

    /// <summary>
    /// Moves track <paramref name="from"/> of sequence <paramref name="sequence"/> to the gap <paramref name="slot"/>
    /// (0 above the first lane, the track count below the last) as one undo entry, renumbering Z as
    /// <see cref="TrackReorder"/> describes. Returns the track's new index, or null when the drop changes nothing.
    /// </summary>
    public int? MoveTrack(IUndoManager undo, int sequence, int from, int slot)
    {
        var tracks = Save.Sequences[sequence].Tracks;
        if (TrackReorder.Plan(tracks.Select(t => t.Z).ToList(), from, slot) is not { } plan) return null;
        Edit(undo, $"Move Track '{tracks[from].Name}'", save =>
        {
            var target = save.Sequences[sequence].Tracks;
            var moved = plan.Order.Select(old => target[old]).ToList();
            for (int lane = 0; lane < moved.Count; lane++) moved[lane].Z = plan.Z[lane];
            target.Clear();
            target.AddRange(moved);
        }, trackBefore: from, trackAfter: plan.To);
        return plan.To;
    }

    /// <summary>
    /// The track a track move, or its undo or redo, left selected, or null when the last change names none. Reading it
    /// clears it, so the timeline takes it once on the refresh that follows.
    /// </summary>
    public int? TakeTrackSelection()
    {
        int? track = _trackToSelect;
        _trackToSelect = null;
        return track;
    }

    public void SetTrackZ(IUndoManager undo, int sequence, int track, int z) =>
        Edit(undo, $"Set Z of '{Save.Sequences[sequence].Tracks[track].Name}' to {z}",
            save => save.Sequences[sequence].Tracks[track].Z = z);

    /// <summary>Moves a chain to <paramref name="start"/> seconds. Returns why it was refused (negative or not a number), or null.</summary>
    public string? SetChainStart(IUndoManager undo, int sequence, int track, int chain, double start)
    {
        if (!double.IsFinite(start) || start < 0) return "Start must be 0 or more.";
        string name = Save.Sequences[sequence].Tracks[track].Chains[chain].Chain;
        Edit(undo, $"Set Start of '{name}' to {Format(start)}",
            save => save.Sequences[sequence].Tracks[track].Chains[chain].Start = start);
        return null;
    }

    /// <summary>Sets how many cycles a chain plays. Returns why it was refused (zero, negative or not a number), or null.</summary>
    public string? SetChainLoops(IUndoManager undo, int sequence, int track, int chain, float loops)
    {
        if (!float.IsFinite(loops) || loops <= 0) return "Loops must be above 0.";
        string name = Save.Sequences[sequence].Tracks[track].Chains[chain].Chain;
        Edit(undo, $"Set Loops of '{name}' to {Format(loops)}",
            save => save.Sequences[sequence].Tracks[track].Chains[chain].Loops = loops);
        return null;
    }

    private static string Format(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Starts a gesture that edits <see cref="Save"/> in place on every pointer move (a drag). Returns the state to pass
    /// to <see cref="CommitGesture"/> or <see cref="CancelGesture"/> when it ends.
    /// </summary>
    public string BeginGesture() => Save.ToJsonString();

    /// <summary>Records everything since <paramref name="before"/> as one undo entry and saves. Does nothing when nothing changed.</summary>
    public void CommitGesture(IUndoManager undo, string description, string before)
    {
        string after = Save.ToJsonString();
        if (after == before) return;
        int? changed = ChangedSequence(before, after);
        undo.Execute(new SnapshotCommand(this, description, before, after, changed, changed));
    }

    /// <summary>Puts <see cref="Save"/> back as it was at <paramref name="before"/>, with no undo entry.</summary>
    public void CancelGesture(string before) => Restore(before);

    /// <summary>Writes the file. A failure is reported through <see cref="SaveError"/>, not thrown.</summary>
    public void SaveToDisk()
    {
        try
        {
            Save.Save(Path.FullPath);
            SaveError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveError = ex.Message;
        }
    }

    /// <summary>Applies <paramref name="mutate"/> to a copy of the file and runs the result as one undo entry.</summary>
    private void Edit(IUndoManager undo, string description, Action<SequenceListSave> mutate, int? selectBefore = null, int? selectAfter = null,
        int? trackBefore = null, int? trackAfter = null, bool selectOnDo = true)
    {
        string before = Save.ToJsonString();
        var copy = SequenceListSave.FromJsonString(before);
        mutate(copy);
        string after = copy.ToJsonString();
        if (after == before) return;
        // An edit that names no sequence selects the one it changed, so every edit, present and future, is followed.
        if (selectBefore is null && selectAfter is null && ChangedSequence(before, after) is { } changed)
            selectBefore = selectAfter = changed;
        undo.Execute(new SnapshotCommand(this, description, before, after, selectBefore, selectAfter, trackBefore, trackAfter, selectOnDo));
    }

    /// <summary>
    /// The index of the first sequence that differs between two snapshots, or null when none does (a sources-only
    /// edit) or the sequence count changed (an add or delete says which sequence it selects itself).
    /// </summary>
    private static int? ChangedSequence(string before, string after)
    {
        var was = JsonNode.Parse(before)!["sequences"]!.AsArray();
        var now = JsonNode.Parse(after)!["sequences"]!.AsArray();
        if (was.Count != now.Count) return null;
        for (int i = 0; i < was.Count; i++)
            if (!JsonNode.DeepEquals(was[i], now[i])) return i;
        return null;
    }

    private void Restore(string json, int? selectedSequence = null, int? selectedTrack = null)
    {
        Save = SequenceListSave.FromJsonString(json);
        if (selectedSequence is { } selected) SelectedSequence = selected;
        _trackToSelect = selectedTrack;
        SyncSources();
        SaveToDisk();
        Changed?.Invoke();
    }

    private FilePath SourcePath(string source) =>
        new(System.IO.Path.Combine(Path.GetDirectoryContainingThis().FullPath, source));

    /// <summary>Reads every source in the file whose own file is new or differs from when it was last read. Returns whether any was read.</summary>
    private bool SyncSources()
    {
        bool any = false;
        foreach (var source in Save.Sources)
        {
            string full = SourcePath(source).FullPath;
            var info = new FileInfo(full);
            var stamp = info.Exists ? new FileStamp(true, info.LastWriteTimeUtc, info.Length) : default;
            if (_stamps.TryGetValue(source, out var known) && known == stamp) continue;
            _sources[source] = TryLoad(full);
            _stamps[source] = stamp;
            any = true;
        }
        return any;
    }

    private static AnimationChainListSave? TryLoad(string absolutePath)
    {
        try
        {
            return AnimationFileLoader.Load(absolutePath).List;
        }
        catch (Exception ex)
        {
            // Unreadable sources are flagged, not fatal: the sequence file still opens and keeps the reference.
            System.Diagnostics.Debug.WriteLine($"[Sequence] source {absolutePath} not loaded: {ex.Message}");
            return null;
        }
    }

    // selectBefore and selectAfter are the selected sequence once undone and once done; null leaves the selection alone.
    // trackBefore and trackAfter are the same for the selected track; null when the edit names none. selectOnDo false
    // leaves the selection alone the first time the edit runs (redo still applies selectAfter).
    private sealed class SnapshotCommand(SequenceDocument document, string description, string before, string after,
        int? selectBefore = null, int? selectAfter = null, int? trackBefore = null, int? trackAfter = null, bool selectOnDo = true) : IUndoableCommand
    {
        public string Description => description;

        public bool Do()
        {
            document.Restore(after, selectOnDo ? selectAfter : null, trackAfter);
            return true;
        }

        public void Redo() => document.Restore(after, selectAfter, trackAfter);

        public void Undo() => document.Restore(before, selectBefore, trackBefore);
    }
}
