using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.DragDrop;

/// <summary>
/// Resolves the animation-tree drop target from the node under the pointer.
/// </summary>
public static class TreePngDropTarget
{
    /// <summary>
    /// Maps a tree node's <see cref="ViewModels.TreeNodeVm.Data"/> to the chain/frame
    /// pair passed to <see cref="TextureDropProcessor.ComputePngDrop"/>. Returns
    /// <c>(null, null)</c> when the pointer is not over a chain or frame node —
    /// blank tree chrome must not fall back to the current selection.
    /// </summary>
    public static (AnimationChainSave? Chain, AnimationFrameSave? Frame) FromNodeData(
        object? nodeData,
        Func<AnimationFrameSave, AnimationChainSave?> getChainContainingFrame)
    {
        return nodeData switch
        {
            AnimationFrameSave frame => (getChainContainingFrame(frame), frame),
            AnimationChainSave chain => (chain, null),
            _ => (null, null),
        };
    }
}
