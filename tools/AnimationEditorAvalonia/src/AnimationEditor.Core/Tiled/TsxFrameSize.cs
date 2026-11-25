using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// The frames of an animation in a <c>.tsx</c> all have one size, because
/// <see cref="MultiTileToTiledAnimationMapper"/> writes no part of an animation that mixes sizes.
/// </summary>
public static class TsxFrameSize
{
    /// <summary>
    /// Why <paramref name="frames"/> cannot be added to <paramref name="chain"/>, or null when they can or when no
    /// <c>.tsx</c> is open. Frames already in the chain are no addition.
    /// </summary>
    public static string? Refusal(IProjectManager projectManager, AnimationChainSave chain, IEnumerable<AnimationFrameSave> frames) =>
        Refusal(projectManager, chain, frames.Where(f => !chain.Frames.Contains(f)).Select(FrameRegion.Of));

    /// <inheritdoc cref="Refusal(IProjectManager, AnimationChainSave, IEnumerable{AnimationFrameSave})"/>
    public static string? Refusal(IProjectManager projectManager, AnimationChainSave chain, IEnumerable<FrameRegion> added)
    {
        if (!projectManager.IsNativeTsxProject) return null;

        var regions = chain.Frames.Take(1).Select(FrameRegion.Of).Concat(added).ToList();
        foreach (var region in regions)
            if (!SameSize(region, regions[0]))
                return $"All frames of an animation in a .tsx have one size, so a {Size(region)} frame cannot be in '{chain.Name}' with a {Size(regions[0])} frame.";
        return null;
    }

    private static bool SameSize(FrameRegion a, FrameRegion b) =>
        Math.Abs((a.Right - a.Left) - (b.Right - b.Left)) <= FrameRegion.EditTolerance
        && Math.Abs((a.Bottom - a.Top) - (b.Bottom - b.Top)) <= FrameRegion.EditTolerance;

    private static string Size(FrameRegion r) => FormattableString.Invariant($"{r.Right - r.Left:0.##}x{r.Bottom - r.Top:0.##}");
}
