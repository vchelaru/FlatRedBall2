using System.Collections.Generic;
using System.Linq;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.ViewModels;

/// <summary>
/// What several selected animations share in the ANIMATION panel; a null flag means they disagree
/// (the checkbox shows its mixed state). <see cref="IsMultiple"/> disables the Name box, since one
/// name cannot go on several animations.
/// </summary>
public sealed record AnimationBulkState(bool? Locked, bool? Loop, bool? PingPong, bool? Reverse, bool IsMultiple)
{
    public static AnimationBulkState For(IReadOnlyList<AnimationChainSave> chains)
    {
        return new AnimationBulkState(
            MixedState.Common(chains.Select(c => c.IsLocked)),
            MixedState.Common(chains.Select(c => c.Loop)),
            MixedState.Common(chains.Select(c => c.PingPong)),
            MixedState.Common(chains.Select(c => c.Reverse)),
            chains.Count > 1);
    }
}
