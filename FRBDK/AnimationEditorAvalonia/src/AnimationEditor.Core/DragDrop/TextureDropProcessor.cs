using FlatRedBall2.Animation.Content;
using System;
using System.IO;

namespace AnimationEditor.Core.DragDrop;

public static class TextureDropProcessor
{
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
            var achxFolder = Path.GetDirectoryName(achxFileName) ?? string.Empty;
            relativeTexturePath = Path.GetRelativePath(achxFolder, droppedFilePath).Replace('\\', '/');
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
