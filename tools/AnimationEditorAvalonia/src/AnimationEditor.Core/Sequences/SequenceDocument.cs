using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>One sprite to draw at a sequence instant: the frame its track shows and where the texture lives.</summary>
public sealed record SequenceLayer(
    int TrackIndex, SequenceTrackSave Track, AnimationChainSave Chain, int FrameIndex, double ElapsedInFrame,
    AnimationFrameSave Frame, string? TexturePath);

/// <summary>
/// An open <c>.seqj</c>: the saved data plus the loaded source files its chain references point at. Edits go through
/// <see cref="Edit"/> so each is one undo entry and the file auto-saves, like an <c>.achx</c> tab.
/// <para>Spike: undo snapshots the whole file as JSON. Fine for small files; a real version wants targeted commands.</para>
/// </summary>
public sealed class SequenceDocument
{
    private readonly Dictionary<string, AnimationChainListSave?> _sources = new(StringComparer.OrdinalIgnoreCase);

    public SequenceDocument(string path, SequenceListSave save)
    {
        FilePath = path;
        Save = save;
        ReloadSources();
    }

    /// <summary>Absolute path of the <c>.seqj</c>.</summary>
    public string FilePath { get; }

    public string Folder => Path.GetDirectoryName(FilePath) ?? "";

    public SequenceListSave Save { get; private set; }

    /// <summary>Raised after any edit, undo or redo. Indices held by the UI stay valid; object references may not.</summary>
    public event Action? Changed;

    public static SequenceDocument Load(string path) =>
        new(path, File.Exists(path) ? SequenceListSave.FromFile(path) : new SequenceListSave());

    public void ReloadSources()
    {
        _sources.Clear();
        foreach (var source in Save.Sources)
            _sources[source] = TryLoad(ToAbsolute(source));
    }

    public bool IsSourceMissing(string source) => !_sources.TryGetValue(source, out var list) || list is null;

    public AnimationChainListSave? SourceList(string source) => _sources.TryGetValue(source, out var list) ? list : null;

    public AnimationChainSave? FindChain(SequenceChainReferenceSave reference) =>
        SourceList(reference.Source)?.AnimationChains.FirstOrDefault(c => c.Name == reference.Chain);

    /// <summary>One playback cycle of the referenced chain (doubled for ping-pong), or 0 when it is missing or empty.</summary>
    public double CycleLength(SequenceChainReferenceSave reference) =>
        FindChain(reference) is { Frames.Count: > 0 } chain ? ChainClock.TotalLength(chain) : 0;

    /// <summary>
    /// Length used to draw a reference on the timeline. A missing chain has no length, so it shows as one second.
    /// Open question: should the file cache the last known length so a broken reference keeps its size?
    /// </summary>
    public double DisplayCycleLength(SequenceChainReferenceSave reference) =>
        CycleLength(reference) is > 0 and var length ? length : 1;

    public string ToAbsolute(string source) => Path.GetFullPath(Path.Combine(Folder, source));

    /// <summary>The sprites to draw at <paramref name="time"/>, back to front by Z, then track order.</summary>
    public IReadOnlyList<SequenceLayer> Sample(SequenceSave sequence, double time)
    {
        var layers = new List<SequenceLayer>();
        for (int i = 0; i < sequence.Tracks.Count; i++)
        {
            var track = sequence.Tracks[i];
            var reference = SequenceTimeline.ActiveAt(track, time, CycleLength, out double local);
            if (reference is null || FindChain(reference) is not { Frames.Count: > 0 } chain) continue;

            double cycle = ChainClock.TotalLength(chain);
            // The loop count overrides the chain's Loop flag, so always wrap inside the played span.
            double cycleTime = local % cycle;
            var at = PlaybackTimeline.Locate(chain.Frames, PlaybackController.FrameLength, cycleTime, chain.PingPong, chain.Reverse);
            var frame = chain.Frames[at.FrameIndex];
            layers.Add(new SequenceLayer(i, track, chain, at.FrameIndex, at.ElapsedInFrame, frame,
                ResolveTexture(reference.Source, frame)));
        }
        return layers.OrderBy(l => l.Track.Z).ThenBy(l => l.TrackIndex).ToList();
    }

    /// <summary>Absolute texture path of a frame in <paramref name="source"/>, resolved against that source's folder.</summary>
    public string? ResolveTexture(string source, AnimationFrameSave frame)
    {
        if (string.IsNullOrEmpty(frame.TextureName)) return null;
        string sourceFolder = Path.GetDirectoryName(ToAbsolute(source)) ?? "";
        string full = Path.GetFullPath(Path.Combine(sourceFolder, frame.TextureName)).Replace('\\', '/');
        return File.Exists(full) ? full : null;
    }

    // ── Edits ────────────────────────────────────────────────────────────────

    /// <summary>The current state, for an edit that mutates live (a drag) and commits on release.</summary>
    public string Capture() => Save.ToJsonString();

    /// <summary>Records everything changed since <paramref name="before"/> as one undo entry. No-op when nothing changed.</summary>
    public void Commit(IUndoManager undo, string description, string before)
    {
        string after = Capture();
        if (after == before) return;
        undo.Record(new SnapshotCommand(this, description, before, after));
        Persist();
    }

    /// <summary>Applies <paramref name="mutate"/> as one undo entry.</summary>
    public void Edit(IUndoManager undo, string description, Action<SequenceListSave> mutate)
    {
        string before = Capture();
        mutate(Save);
        Commit(undo, description, before);
        Changed?.Invoke();
    }

    /// <summary>Adds an <c>.achx</c>/<c>.achj</c> as a source. Returns an error message when it can't be one.</summary>
    public string? AddSource(IUndoManager undo, string absolutePath)
    {
        string ext = Path.GetExtension(absolutePath).ToLowerInvariant();
        if (ext == ".seqj") return "A sequence file can't be a source.";
        if (ext is not (".achx" or ".achj")) return "Only .achx and .achj files can be sources.";
        string relative = Path.GetRelativePath(Folder, absolutePath).Replace('\\', '/');
        if (Save.Sources.Contains(relative, StringComparer.OrdinalIgnoreCase)) return null;
        Edit(undo, $"Add Source '{Path.GetFileName(absolutePath)}'", s => s.Sources.Add(relative));
        _sources[relative] = TryLoad(absolutePath);
        Changed?.Invoke();
        return null;
    }

    /// <summary>Places a chain on track <paramref name="trackIndex"/>, or on a new track when it is past the last one.</summary>
    public void PlaceChain(IUndoManager undo, int sequenceIndex, int trackIndex, string source, string chain, double start)
    {
        Edit(undo, $"Place '{chain}'", s =>
        {
            var sequence = s.Sequences[sequenceIndex];
            if (trackIndex >= sequence.Tracks.Count)
                sequence.Tracks.Add(new SequenceTrackSave { Name = chain, Z = sequence.Tracks.Count });
            var track = sequence.Tracks[Math.Min(trackIndex, sequence.Tracks.Count - 1)];
            track.Chains.Add(new SequenceChainReferenceSave { Source = source, Chain = chain, Start = Math.Max(0, start), Loops = 1 });
        });
    }

    private void Persist()
    {
        try { Save.Save(FilePath); }
        catch (IOException ex) { System.Diagnostics.Debug.WriteLine($"[Sequence] save failed: {ex.Message}"); }
    }

    private void Restore(string json)
    {
        Save = SequenceListSave.FromJsonString(json);
        Persist();
        Changed?.Invoke();
    }

    private static AnimationChainListSave? TryLoad(string absolutePath)
    {
        try
        {
            if (!File.Exists(absolutePath)) return null;
            var list = AnimationChainListSave.FromFile(absolutePath);
            // The editor's drawing code expects the in-memory UV form that ProjectManager.LoadAnimationChain produces.
            ProjectManager.ConvertCoordinates(list, Path.GetDirectoryName(absolutePath) ?? "", TextureCoordinateType.UV);
            return list;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Text.Json.JsonException or System.Xml.XmlException)
        {
            return null;
        }
    }

    private sealed class SnapshotCommand(SequenceDocument document, string description, string before, string after) : IUndoableCommand
    {
        public string Description => description;
        public bool Do() { document.Restore(after); return true; }
        public void Undo() => document.Restore(before);
    }
}
