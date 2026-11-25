using AnimationEditor.Core.CommandsAndState.Commands;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Tiled;

/// <summary>Moving frames from one animation of a <c>.tsx</c> into another never takes a frame out of the file.</summary>
public static class TsxFrameMove
{
    /// <summary>
    /// Why <paramref name="frames"/> cannot move to <paramref name="insertIndex"/> of <paramref name="target"/>, or null
    /// when they can or when no <c>.tsx</c> is open. Frames of an animation the file has do not move into one the save
    /// would skip. A locked chain's frames are copied, so nothing is taken from it.
    /// </summary>
    public static string? Refusal(IProjectManager projectManager, AnimationChainSave target, IReadOnlyList<AnimationFrameSave> frames, int insertIndex)
    {
        if (!projectManager.IsNativeTsxProject || projectManager.AnimationChainListSave is not { } acls) return null;
        if (TsxFrameSize.Refusal(projectManager, target, frames) is { } otherSize) return otherSize;

        var sources = acls.AnimationChains.Where(c => c != target && !c.IsLocked && c.Frames.Any(frames.Contains)).ToList();
        if (sources.Count == 0 || projectManager.MapTsxForSave().All(r => r.SkipReason is not null || !sources.Contains(r.SourceChain)))
            return null;

        AnimationChainSave[] chains = [.. sources, target];
        var before = chains.Select(c => c.Frames.ToArray()).ToArray();
        MoveFramesCommand.Arrange(frames, chains, target, insertIndex);
        MultiTileMappingResult after;
        try
        {
            after = projectManager.MapTsxForSave().First(r => r.SourceChain == target);
        }
        finally
        {
            foreach (var (chain, order) in chains.Zip(before))
                MoveFramesCommand.RestoreOrder(chain, order);
        }

        if (after.SkipReason is null) return null;
        string what = frames.Count == 1 ? "the frame" : "the frames";
        string fix = after.TileHolder is null ? "" : $" Give \"{after.TileHolder.Name}\" another Placed Tile first.";
        return $"'{target.Name}' could not be saved with {what}, so {what} would be in no animation of the file: {after.SkipReason}.{fix}";
    }
}
