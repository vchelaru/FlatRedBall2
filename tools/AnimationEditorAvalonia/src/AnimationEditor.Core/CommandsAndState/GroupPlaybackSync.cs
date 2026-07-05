using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState;

/// <summary>
/// Pure diff for keeping a per-chain <see cref="PlaybackController"/> dictionary in sync with the
/// current multi-select group (#576): which chains need a brand-new controller and which existing
/// controllers belong to chains that dropped out of the selection and should be removed.
/// Chains that are in both sets are left untouched, so an unrelated selection change never resets
/// a group track's in-flight playback position.
/// </summary>
public static class GroupPlaybackSync
{
    public static (List<AnimationChainSave> ToAdd, List<AnimationChainSave> ToRemove) ComputeDiff(
        IEnumerable<AnimationChainSave> existingKeys, IReadOnlyList<AnimationChainSave> desiredChains)
    {
        var existingSet = existingKeys.ToHashSet();
        var desiredSet = desiredChains.ToHashSet();

        var toAdd = desiredChains.Where(c => !existingSet.Contains(c)).ToList();
        var toRemove = existingSet.Where(c => !desiredSet.Contains(c)).ToList();
        return (toAdd, toRemove);
    }
}
