using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// Finds achx-model data a native <c>.tsx</c> save can't store. A Tiled tile animation is a rect
/// sequence with durations, nothing more, so a frame's flips, sprite offset, color, or collision
/// shapes, and a chain's <see cref="AnimationChainSave.Loop"/> being off, are dropped by <see
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
            var kinds = new List<string>();
            if (chain.Frames.Any(f => f.FlipHorizontal || f.FlipVertical || f.FlipDiagonal)) kinds.Add("flip");
            if (chain.Frames.Any(f => f.RelativeX != 0f || f.RelativeY != 0f)) kinds.Add("sprite offset");
            if (chain.Frames.Any(f => f.Red is not null || f.Green is not null || f.Blue is not null || f.Alpha is not null || f.ColorOperation is not null)) kinds.Add("color");
            if (chain.Frames.Any(f => f.ShapesSave?.Shapes.Count > 0)) kinds.Add("collision shape");
            if (!chain.Loop) kinds.Add("loop off");

            if (kinds.Count > 0)
                warnings.Add($"chain \"{chain.Name}\": {string.Join(", ", kinds)} can't be stored in a .tsx and was not saved.");
        }
        return warnings;
    }
}
