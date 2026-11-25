using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// Finds achx-model data a native <c>.tsx</c> save can't store. A Tiled tile animation is a rect
/// sequence with durations, nothing more, so a frame's flips, sprite offset, color, or collision
/// shapes, events, or keyframe data, and a chain's <see cref="AnimationChainSave.Loop"/> being off or its PingPong/Reverse on, are dropped by <see
/// cref="MultiTileToTiledAnimationMapper"/> without any other signal. The inspector hides the
/// controls for most of these in a tsx project, but a frame's context menu, "Duplicate flipped",
/// the Loop toggle and paste can still produce them, so the save reports what it left behind
/// rather than relying on every entry point to be gated.
/// </summary>
public static class TsxLossyDataCheck
{
    /// <summary>One warning per chain that carries anything the tsx can't hold, naming the kinds.</summary>
    public static IReadOnlyList<string> Warnings(AnimationChainListSave acls)
    {
        var warnings = new List<string>();
        foreach (var chain in acls.AnimationChains)
        {
            var kinds = DroppedKinds(chain, acls.TimeMeasurementUnit);
            if (kinds.Count > 0)
                warnings.Add($"chain \"{chain.Name}\": {ChainData.Describe(kinds)} can't be stored in a .tsx and was not saved.");
        }
        return warnings;
    }

    /// <summary>The kinds of data <paramref name="chain"/> carries that the tsx can't hold; empty when it is stored in full.</summary>
    public static IReadOnlyList<ChainDataKind> DroppedKinds(AnimationChainSave chain, TimeMeasurementUnit unit) =>
        ChainData.Dropped([chain], unit, new HashSet<AnimationFrameSave>(), ChainDataKind.FrameLength);
}
