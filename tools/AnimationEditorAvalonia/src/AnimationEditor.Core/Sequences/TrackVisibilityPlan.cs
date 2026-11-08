using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// Which tracks a Hide / Show command acts on and which way it flips them. See <see cref="For"/>.
/// </summary>
public readonly record struct TrackVisibilityPlan(IReadOnlyList<int> Tracks, bool Visible)
{
    /// <summary>
    /// The eye or menu item on track <paramref name="clicked"/> acts on the whole track selection when the clicked
    /// track is part of a multi-selection, otherwise on that track alone. The set flips together: if any track in it is
    /// visible they all hide, else they all show.
    /// </summary>
    public static TrackVisibilityPlan For(IReadOnlyList<int> selectedTracks, int clicked, Func<int, bool> isHidden)
    {
        var tracks = selectedTracks.Count > 1 && selectedTracks.Contains(clicked) ? selectedTracks : new[] { clicked };
        bool anyVisible = false;
        foreach (int track in tracks) anyVisible |= !isHidden(track);
        return new TrackVisibilityPlan(tracks, !anyVisible);
    }
}
