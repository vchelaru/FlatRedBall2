using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using System.Linq;

namespace AnimationEditor.Core.DragDrop;

/// <summary>
/// Applies a <see cref="TextureDropResult"/> already computed by
/// <see cref="TextureDropProcessor.ComputePngDrop"/> — the undoable-command + selection side
/// effects shared by every PNG-drop entry point (ANIMATIONS tree, wireframe canvas; see #560).
/// </summary>
public static class TextureDropApplier
{
    /// <summary>Returns <see langword="true"/> when <paramref name="result"/> was applied.</summary>
    public static bool Apply(
        IAppCommands appCommands, ISelectedState selectedState,
        AnimationChainSave? targetChain, AnimationFrameSave? targetFrame,
        TextureDropResult result, string? relativePath)
    {
        switch (result)
        {
            case TextureDropResult.UpdatedFrame:
                appCommands.SetFrameTextureName(targetFrame!, relativePath);
                appCommands.RefreshTreeNode(targetFrame!);
                selectedState.SelectedFrame = targetFrame!;
                return true;

            case TextureDropResult.CreatedFrame:
                appCommands.AddFrame(targetChain!, relativePath);
                var createdFrame = targetChain!.Frames.LastOrDefault();
                if (createdFrame is not null)
                    selectedState.SelectedFrame = createdFrame;
                return true;

            case TextureDropResult.UpdatedChainFrames:
                appCommands.SetAllFramesTextureName(targetChain!, relativePath);
                appCommands.RefreshTreeNode(targetChain!);
                selectedState.SelectedChain = targetChain;
                return true;

            default:
                return false;
        }
    }
}
