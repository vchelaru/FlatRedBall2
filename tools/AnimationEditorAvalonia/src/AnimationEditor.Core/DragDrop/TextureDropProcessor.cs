using AnimationEditor.Core.Paths;
using FlatRedBall2.Animation.Content;
using System;
using System.IO;

namespace AnimationEditor.Core.DragDrop;

public static class TextureDropProcessor
{
    /// <summary>
    /// Returns the result type and resolved relative texture path for a PNG drop
    /// without mutating any model data. Use this instead of <see cref="ApplyPngDrop"/>
    /// when the caller will route through undoable commands.
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

    public static TextureDropResult ApplyPngDrop(
        AnimationChainSave? targetChain,
        AnimationFrameSave? targetFrame,
        string droppedFilePath,
        string? achxFileName,
        bool createFrameOnCtrl)
    {
        if (string.IsNullOrWhiteSpace(droppedFilePath))
            return TextureDropResult.NotApplied;

        if (!string.Equals(Path.GetExtension(droppedFilePath).TrimStart('.'), "png", StringComparison.OrdinalIgnoreCase))
            return TextureDropResult.NotApplied;

        // When no ACHX is saved yet, use the absolute texture path.
        string relativeTexturePath;
        if (string.IsNullOrWhiteSpace(achxFileName))
        {
            relativeTexturePath = droppedFilePath;
        }
        else
        {
            // Route through FilePath so Windows-style drive prefixes are recognized as absolute
            // on Linux too (stdlib Path.GetDirectoryName/GetRelativePath only honor / on Linux).
            var achxFolder = new FilePath(achxFileName).GetDirectoryContainingThis();
            relativeTexturePath = new FilePath(droppedFilePath).RelativeTo(achxFolder);
        }

        if (targetFrame is not null)
        {
            targetFrame.TextureName = relativeTexturePath;
            return TextureDropResult.UpdatedFrame;
        }

        if (targetChain is null)
            return TextureDropResult.NotApplied;

        if (createFrameOnCtrl || targetChain.Frames.Count == 0)
        {
            targetChain.Frames.Add(new AnimationFrameSave
            {
                TextureName = relativeTexturePath,
                LeftCoordinate = 0f,
                TopCoordinate = 0f,
                RightCoordinate = 1f,
                BottomCoordinate = 1f,
                FrameLength = 0.1f,
                ShapesSave = new ShapesSave()
            });

            return TextureDropResult.CreatedFrame;
        }

        foreach (var frame in targetChain.Frames)
        {
            frame.TextureName = relativeTexturePath;
        }

        return TextureDropResult.UpdatedChainFrames;
    }
}

public enum TextureDropResult
{
    NotApplied,
    UpdatedFrame,
    UpdatedChainFrames,
    CreatedFrame
}
