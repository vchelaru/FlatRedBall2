using AnimationEditor.Core.CommandsAndState.Commands;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// A drag of one or several tracks' offsets from the preview. Each <see cref="Update"/> edits the document in place so the
/// Inspector and the preview follow live; <see cref="Commit"/> turns the whole drag into one undo entry, and
/// <see cref="Cancel"/> puts everything back with none.
/// </summary>
public sealed class TrackMoveGesture
{
    private readonly SequenceDocument _document;
    private readonly IUndoManager _undo;
    private readonly int _sequence;
    private readonly int[] _tracks;
    private readonly (float X, float Y)[] _starts;
    private readonly string _before;
    private readonly Action? _applied;

    /// <param name="applied">Called after every <see cref="Update"/>, so the views that read the document can refresh.</param>
    public TrackMoveGesture(SequenceDocument document, IUndoManager undo, int sequence, IReadOnlyList<int> tracks, Action? applied = null)
    {
        _document = document;
        _undo = undo;
        _sequence = sequence;
        _tracks = tracks.Distinct().OrderBy(track => track).ToArray();
        var saved = document.Save.Sequences[sequence].Tracks;
        _starts = _tracks.Select(track => (saved[track].X, saved[track].Y)).ToArray();
        _before = document.BeginGesture();
        _applied = applied;
    }

    /// <summary>The tracks this drag moves, ascending.</summary>
    public IReadOnlyList<int> Tracks => _tracks;

    /// <summary>
    /// Places every track at its start plus the world delta, snapped to whole pixels (see <see cref="TrackPlacement.Dragged"/>).
    /// The delta is from the press, not from the last update.
    /// </summary>
    public void Update(float worldDx, float worldDy, bool axisLock)
    {
        var placed = TrackPlacement.Dragged(_starts, worldDx, worldDy, axisLock);
        Place(placed);
        _applied?.Invoke();
    }

    /// <summary>Records the drag as one undo entry and saves; nothing when the tracks ended where they began.</summary>
    public void Commit() => _document.CommitGesture(_undo, _document.DescribeMove(_sequence, _tracks), _before);

    /// <summary>Puts the tracks back where they started, with no undo entry.</summary>
    public void Cancel() => _document.CancelGesture(_before);

    private void Place(IReadOnlyList<(float X, float Y)> positions)
    {
        var tracks = _document.Save.Sequences[_sequence].Tracks;
        for (int i = 0; i < _tracks.Length; i++)
        {
            tracks[_tracks[i]].X = positions[i].X;
            tracks[_tracks[i]].Y = positions[i].Y;
        }
    }
}
