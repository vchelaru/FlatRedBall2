using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using System.Linq;

namespace AnimationEditor.App;

public partial class MainWindow
{
    private sealed partial class SequenceTabPane
    {
        /// <summary>
        /// Placing tracks from the preview: a press on a sprite selects its track and drags every selected track, and the
        /// arrow keys nudge them while the preview or the timeline has the focus.
        /// </summary>
        private void WireTrackPlacement()
        {
            var w = _window;
            w.SequencePreview.GrabTrack = GrabTrack;
            w.SequencePreview.TrackClicked += NarrowToTrack;
            TrackNudgeKeys.Attach(w.SequencePreview, NudgeSelectedTracks, EndNudgeRun);
            TrackNudgeKeys.Attach(w.SequenceTimeline, NudgeSelectedTracks, EndNudgeRun);
        }

        /// <summary>
        /// A press landed on <paramref name="track"/>'s sprite: it becomes the selected track unless a multi-selection already
        /// holds it (then the whole set drags), and the drag over the selected tracks starts.
        /// </summary>
        private TrackMoveGesture? GrabTrack(int track)
        {
            if (_document is not { } document || SequenceIndex < 0) return null;
            var timeline = _window.SequenceTimeline;
            if (!timeline.SelectedTracks.Contains(track)) timeline.SelectTracks(new[] { track });
            return new TrackMoveGesture(document, _window._undoManager, SequenceIndex, timeline.SelectedTracks.ToList(), RefreshAfterTrackMove);
        }

        /// <summary>A press on one sprite of a multi-selection that never became a drag narrows the selection to that track, as on the timeline.</summary>
        private void NarrowToTrack(int track)
        {
            var timeline = _window.SequenceTimeline;
            if (timeline.SelectedTracks.Count > 1) timeline.SelectTracks(new[] { track });
        }

        /// <summary>A drag edits the document in place, so the views that read it are refreshed by hand on each step.</summary>
        private void RefreshAfterTrackMove()
        {
            ShowInspector();
            ShowPlayhead();
        }

        /// <summary>Moves the selected tracks by the arrow key's step. Returns false, taking no key, when no track is selected or a drag is under way.</summary>
        private bool NudgeSelectedTracks(float dx, float dy)
        {
            var w = _window;
            if (_document is not { } document || SequenceIndex < 0 || w.SequencePreview.IsDraggingTrack) return false;
            var tracks = w.SequenceTimeline.SelectedTracks;
            if (tracks.Count == 0) return false;
            document.NudgeTracks(w._undoManager, SequenceIndex, tracks.ToList(), dx, dy);
            return true;
        }

        private void EndNudgeRun() => _window._undoManager.SealCoalescing();
    }
}
