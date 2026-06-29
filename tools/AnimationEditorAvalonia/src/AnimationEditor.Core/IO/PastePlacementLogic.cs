using AnimationEditor.Core;
using FlatRedBall2.Animation.Content;
using System.Linq;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Resolves where clipboard frames should be pasted in the project.
/// </summary>
public static class PastePlacementLogic
{
    public static (AnimationChainSave? TargetChain, int? InsertIndex) ResolveFramePasteTarget(
        AnimationChainListSave acls,
        object? selectedData,
        IObjectFinder finder,
        ISelectedState? state = null)
    {
        AnimationChainSave? targetChain = null;
        int? insertIndex = null;

        var selectedFrames = state?.SelectedFrames;
        if (selectedFrames is { Count: > 0 })
        {
            var anchor = selectedFrames
                .Select(f => (Frame: f, Chain: finder.GetAnimationChainContaining(f)))
                .Where(x => x.Chain is not null)
                .OrderBy(x => acls.AnimationChains.IndexOf(x.Chain!))
                .ThenBy(x => x.Chain!.Frames.IndexOf(x.Frame))
                .LastOrDefault();
            if (anchor.Chain is not null)
            {
                targetChain = anchor.Chain;
                int idx = anchor.Chain.Frames.IndexOf(anchor.Frame);
                if (idx >= 0) insertIndex = idx + 1;
            }
        }
        else if (selectedData is AnimationChainSave chain) targetChain = chain;
        else if (selectedData is AnimationFrameSave frame)
        {
            targetChain = finder.GetAnimationChainContaining(frame);
            int idx = targetChain?.Frames.IndexOf(frame) ?? -1;
            if (idx >= 0) insertIndex = idx + 1;
        }

        if (targetChain is null && acls.AnimationChains.Count > 0)
            targetChain = acls.AnimationChains[^1];

        return (targetChain, insertIndex);
    }
}
