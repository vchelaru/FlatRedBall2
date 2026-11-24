using AnimationEditor.Core.Paths;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;

namespace AnimationEditor.Core.DragDrop;

public static class TextureDropProcessor
{
    /// <summary>
    /// Returns the result type and resolved relative texture path for a PNG drop
    /// without mutating any model data; <see cref="TextureDropApplier"/> applies it through undoable commands.
    /// Returns <c>(NotApplied, null)</c> when the drop should be ignored.
    /// </summary>
    public static (TextureDropResult Result, string? RelativePath) ComputePngDrop(
        AnimationChainSave? targetChain,
        AnimationFrameSave? targetFrame,
        string droppedFilePath,
        string? achxFileName,
        bool createFrameOnCtrl)
    {
        if (string.IsNullOrWhiteSpace(droppedFilePath))
            return (TextureDropResult.NotApplied, null);

        if (!string.Equals(Path.GetExtension(droppedFilePath).TrimStart('.'), "png", StringComparison.OrdinalIgnoreCase))
            return (TextureDropResult.NotApplied, null);

        string relativeTexturePath;
        if (string.IsNullOrWhiteSpace(achxFileName))
        {
            relativeTexturePath = droppedFilePath;
        }
        else
        {
            var achxFolder = new FilePath(achxFileName).GetDirectoryContainingThis();
            relativeTexturePath = new FilePath(droppedFilePath).RelativeTo(achxFolder);
        }

        if (targetFrame is not null)
            return (TextureDropResult.UpdatedFrame, relativeTexturePath);

        if (targetChain is null)
            return (TextureDropResult.NotApplied, null);

        if (createFrameOnCtrl || targetChain.Frames.Count == 0)
            return (TextureDropResult.CreatedFrame, relativeTexturePath);

        return (TextureDropResult.UpdatedChainFrames, relativeTexturePath);
    }
}

public enum TextureDropResult
{
    NotApplied,
    UpdatedFrame,
    UpdatedChainFrames,
    CreatedFrame
}
