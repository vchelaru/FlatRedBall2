using AnimationEditor.Core.Sequences;
using System;
using System.Linq;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Copy, paste and duplicate of the selected chains or tracks. The host moves the clipboard text; what a paste or a
/// duplicate does, and where it lands, is decided by <see cref="SequenceDocument"/>. Each is one undo entry, and the
/// new chains or tracks become the selection.
/// </summary>
public sealed partial class SequenceTimelineControl
{
    /// <summary>The clipboard text for the selected chains or tracks (see <see cref="TimelineClipboard"/>), or null when nothing is selected.</summary>
    public string? CopySelectionText()
    {
        if (_document is null || Sequence is null) return null;
        return _model.Kind switch
        {
            TimelineSelectionKind.Chains => _document.CopyChains(_sequenceIndex, _model.Chains),
            TimelineSelectionKind.Tracks => _document.CopyTracks(_sequenceIndex, _model.Tracks),
            _ => null,
        };
    }

    /// <summary>
    /// Pastes copied chains or tracks. Chains go onto the selected track (see <see cref="SequenceDocument.PasteChains"/> for
    /// when there is none), the earliest at the playhead. Tracks go right after the lowest selected track, else at the end.
    /// What was pasted becomes the selection.
    /// </summary>
    public void Paste(TimelineClipboardContent content)
    {
        if (_document is null || _undo is null || Sequence is null) return;
        TimelinePasteResult result;
        if (content.Kind == TimelineClipboardKind.Chains)
        {
            result = _document.PasteChains(_undo, _sequenceIndex, content, Selection?.Track ?? SelectedTrack, _playhead);
            SelectChains(result.Chains);
        }
        else
        {
            result = _document.PasteTracks(_undo, _sequenceIndex, content, LowestSelectedTrack());
            SelectTracks(result.Tracks);
        }
        if (result.Errors.Count > 0) ErrorRaised?.Invoke(string.Join(" ", result.Errors));
    }

    /// <summary>
    /// Duplicates the selected chains (right after the selection, on their own tracks) or tracks (as a block right below the
    /// lowest one), as one undo entry. The copies become the selection.
    /// </summary>
    public void DuplicateSelected()
    {
        if (_document is null || _undo is null || Sequence is null) return;
        if (_model.Kind == TimelineSelectionKind.Chains)
            SelectChains(_document.DuplicateChains(_undo, _sequenceIndex, _model.Chains.ToList()).Chains);
        else if (_model.Kind == TimelineSelectionKind.Tracks)
            SelectTracks(_document.DuplicateTracks(_undo, _sequenceIndex, _model.Tracks.ToList()).Tracks);
    }

    // The lane of the lowest selected chain or header, or null when nothing is selected.
    private int? LowestSelectedTrack() => _model.Kind switch
    {
        TimelineSelectionKind.Chains => _model.Chains.Max(chain => chain.Track),
        TimelineSelectionKind.Tracks => _model.Tracks.Max(),
        _ => null,
    };
}
