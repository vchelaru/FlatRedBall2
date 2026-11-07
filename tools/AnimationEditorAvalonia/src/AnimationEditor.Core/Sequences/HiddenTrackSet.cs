using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>One hidden track as stored in the editor settings: the track's name within its sequence's name.</summary>
public sealed class HiddenTrackEntry
{
    public string Sequence { get; set; } = string.Empty;
    public string Track { get; set; } = string.Empty;
}

/// <summary>
/// Which tracks of a <c>.seqj</c> the user hid in the preview, keyed by sequence name and track name so the state
/// survives the whole-file snapshots undo restores. View state: never part of the document or its undo history.
/// The document carries it across renames and prunes it when a track or sequence goes. Like
/// <see cref="CollapsedSourceSet"/>, <see cref="Changed"/> fires on toggles, renames and prunes (so the host can
/// persist), <see cref="Load"/> does not.
/// </summary>
public sealed class HiddenTrackSet
{
    private readonly List<HiddenTrackEntry> _entries = new();

    public IReadOnlyList<HiddenTrackEntry> Entries => _entries;

    public event Action? Changed;

    public bool IsHidden(string sequence, string track) => _entries.Exists(e => Matches(e, sequence, track));

    /// <summary>Whether any track of <paramref name="sequence"/> is hidden.</summary>
    public bool AnyHidden(string sequence) => _entries.Exists(e => e.Sequence == sequence);

    public void Toggle(string sequence, string track)
    {
        if (_entries.RemoveAll(e => Matches(e, sequence, track)) == 0)
            _entries.Add(new HiddenTrackEntry { Sequence = sequence, Track = track });
        Changed?.Invoke();
    }

    /// <summary>Shows every track of <paramref name="sequence"/> again.</summary>
    public void ShowAll(string sequence)
    {
        if (_entries.RemoveAll(e => e.Sequence == sequence) > 0) Changed?.Invoke();
    }

    /// <summary>Moves a hidden track's state to its new name. Does nothing for a shown track.</summary>
    public void RenameTrack(string sequence, string oldName, string newName)
    {
        bool renamed = false;
        foreach (var entry in _entries.Where(e => Matches(e, sequence, oldName)))
        {
            entry.Track = newName;
            renamed = true;
        }
        if (renamed) Changed?.Invoke();
    }

    /// <summary>Moves every hidden track of a sequence to its new name.</summary>
    public void RenameSequence(string oldName, string newName)
    {
        bool renamed = false;
        foreach (var entry in _entries.Where(e => e.Sequence == oldName))
        {
            entry.Sequence = newName;
            renamed = true;
        }
        if (renamed) Changed?.Invoke();
    }

    /// <summary>Replaces the hidden tracks, e.g. from persisted settings. The entries are copied.</summary>
    public void Load(IEnumerable<HiddenTrackEntry>? entries)
    {
        _entries.Clear();
        if (entries is null) return;
        foreach (var entry in entries)
            if (!IsHidden(entry.Sequence, entry.Track))
                _entries.Add(new HiddenTrackEntry { Sequence = entry.Sequence, Track = entry.Track });
    }

    /// <summary>Forgets hidden tracks whose sequence or track is no longer in <paramref name="save"/>.</summary>
    public void Prune(SequenceListSave save)
    {
        bool Exists(HiddenTrackEntry entry) =>
            save.Sequences.Any(sequence => sequence.Name == entry.Sequence && sequence.Tracks.Any(track => track.Name == entry.Track));
        if (_entries.RemoveAll(entry => !Exists(entry)) > 0) Changed?.Invoke();
    }

    private static bool Matches(HiddenTrackEntry entry, string sequence, string track) =>
        entry.Sequence == sequence && entry.Track == track;
}
