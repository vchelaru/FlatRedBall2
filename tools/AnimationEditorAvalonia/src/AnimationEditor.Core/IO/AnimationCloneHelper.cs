using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Deep-clones animation content for copy, paste, and duplicate operations.
/// </summary>
public static class AnimationCloneHelper
{
    public static AnimationFrameSave CloneFrame(AnimationFrameSave source) => source.Clone();

    // The lock goes along to a duplicate or a paste on purpose for now; this may be revisited.
    public static AnimationChainSave CloneChain(AnimationChainSave source) => source.Clone();

    public static object? CloneShape(object shape) => (shape as ShapeSave)?.Clone();
}
