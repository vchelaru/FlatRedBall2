using System;
using System.Collections.Generic;
using AnimationEditor.Core.Export;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Views.Export;

/// <summary>Picks the encoder for an <see cref="AnimatedImageFormat"/>.</summary>
public static class AnimatedImageRenderers
{
    /// <exception cref="InvalidOperationException">Nothing drawable, the image would be too large, or encoding failed.</exception>
    public static byte[] Render(
        AnimatedImageFormat format,
        IReadOnlyList<ExportSourceChain> sources,
        Func<AnimationFrameSave, string?> resolveTexturePath,
        float offsetMultiplier,
        AnimationExportOptions options)
    {
        if (ReferenceEquals(format, AnimatedImageFormat.Gif))
            return GifRenderer.Render(sources, resolveTexturePath, offsetMultiplier, options);
        if (ReferenceEquals(format, AnimatedImageFormat.Webp))
            return WebpRenderer.Render(sources, resolveTexturePath, offsetMultiplier, options);
        throw new ArgumentOutOfRangeException(nameof(format));
    }
}
