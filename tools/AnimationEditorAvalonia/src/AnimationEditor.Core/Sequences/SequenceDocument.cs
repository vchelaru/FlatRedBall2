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

    // Keyed by the source string as written in the file. A null value is a source that could not be loaded.
    private readonly Dictionary<string, AnimationChainListSave?> _sources = new(StringComparer.OrdinalIgnoreCase);

    private SequenceDocument(string path, SequenceListSave save)
    {
        Path = new FilePath(path);
        Save = save;
        SelectedSequence = save.Sequences.Count > 0 ? 0 : -1;
        LoadMissingSources();
    }

    public FilePath Path { get; }

    /// <summary>The file's data. Replaced by undo and redo, so do not hold on to it or to anything inside it.</summary>
    public SequenceListSave Save { get; private set; }

    /// <summary>
    /// The sequence the list has selected, or -1 for none. Adding, renaming and deleting a sequence move it to the
    /// sequence that changed, and undo and redo move it the same way, so the list follows the history.
    /// </summary>
    public int SelectedSequence { get; set; }

    /// <summary>Why the last save failed, or null when it succeeded.</summary>
    public string? SaveError { get; private set; }

    /// <summary>Raised after any edit, undo or redo.</summary>
    public event Action? Changed;

    /// <summary>Reads <paramref name="path"/> and loads its sources. A source that can't be read is kept and flagged.</summary>
    /// <exception cref="System.Text.Json.JsonException">The file is not a valid <c>.seqj</c>.</exception>
    public static SequenceDocument Load(string path) => new(path, SequenceListSave.FromFile(path));

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
        string name = UniqueName(DefaultSequenceName);
        Edit(undo, $"Add Sequence '{name}'", save => save.Sequences.Add(new SequenceSave { Name = name }),
            selectBefore: SelectedSequence, selectAfter: Save.Sequences.Count);
        return name;
    }

    /// <summary>Renames the sequence at <paramref name="index"/>. Returns why it was refused, or null.</summary>
    public string? RenameSequence(IUndoManager undo, int index, string name)
    {
        name = name.Trim();
        string oldName = Save.Sequences[index].Name;
        if (name.Length == 0) return "A sequence needs a name.";
        if (name == oldName) return null;
        if (Save.Sequences.Where((_, i) => i != index).Any(s => s.Name == name))
            return $"A sequence named \"{name}\" already exists.";
        Edit(undo, $"Rename Sequence '{oldName}' to '{name}'", save => save.Sequences[index].Name = name,
            selectBefore: index, selectAfter: index);
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
        string name = UniqueTrackName(Save.Sequences[sequence], chain);
        Edit(undo, $"Add Track '{name}'", save =>
        {
            var tracks = save.Sequences[sequence].Tracks;
            tracks.Add(new SequenceTrackSave { Name = name, Z = tracks.Count, Chains = { reference } });
        });
    }

    /// <summary>Removes chain reference <paramref name="chain"/> (an index into the track's list) from its track.</summary>
    public void DeleteChain(IUndoManager undo, int sequence, int track, int chain)
    {
        var owner = Save.Sequences[sequence].Tracks[track];
        Edit(undo, $"Remove '{owner.Chains[chain].Chain}' from '{owner.Name}'",
            save => save.Sequences[sequence].Tracks[track].Chains.RemoveAt(chain));
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

    public void SetTrackFlip(IUndoManager undo, int sequence, int track, bool flip) =>
        Edit(undo, $"Turn Flip {(flip ? "On" : "Off")} for '{Save.Sequences[sequence].Tracks[track].Name}'",
            save => save.Sequences[sequence].Tracks[track].FlipHorizontal = flip);

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
        undo.Execute(new SnapshotCommand(this, description, before, after));
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

    private static string UniqueTrackName(SequenceSave sequence, string baseName)
    {
        var used = sequence.Tracks.Select(t => t.Name).ToHashSet();
        if (!used.Contains(baseName)) return baseName;
        int suffix = 2;
        while (used.Contains($"{baseName} {suffix}")) suffix++;
        return $"{baseName} {suffix}";
    }

    private string UniqueName(string baseName)
    {
        var used = Save.Sequences.Select(s => s.Name).ToHashSet();
        if (!used.Contains(baseName)) return baseName;
        int suffix = 2;
        while (used.Contains($"{baseName} {suffix}")) suffix++;
        return $"{baseName} {suffix}";
    }

    /// <summary>Applies <paramref name="mutate"/> to a copy of the file and runs the result as one undo entry.</summary>
    private void Edit(IUndoManager undo, string description, Action<SequenceListSave> mutate, int? selectBefore = null, int? selectAfter = null)
    {
        string before = Save.ToJsonString();
        var copy = SequenceListSave.FromJsonString(before);
        mutate(copy);
        string after = copy.ToJsonString();
        if (after == before) return;
        undo.Execute(new SnapshotCommand(this, description, before, after, selectBefore, selectAfter));
    }

    private void Restore(string json, int? selectedSequence = null)
    {
        Save = SequenceListSave.FromJsonString(json);
        if (selectedSequence is { } selected) SelectedSequence = selected;
        LoadMissingSources();
        SaveToDisk();
        Changed?.Invoke();
    }

    private FilePath SourcePath(string source) =>
        new(System.IO.Path.Combine(Path.GetDirectoryContainingThis().FullPath, source));

    private void LoadMissingSources()
    {
        foreach (var source in Save.Sources.Where(s => !_sources.ContainsKey(s)))
            _sources[source] = TryLoad(SourcePath(source).FullPath);
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
    private sealed class SnapshotCommand(SequenceDocument document, string description, string before, string after,
        int? selectBefore = null, int? selectAfter = null) : IUndoableCommand
    {
        public string Description => description;

        public bool Do()
        {
            document.Restore(after, selectAfter);
            return true;
        }

        public void Undo() => document.Restore(before, selectBefore);
    }
}
