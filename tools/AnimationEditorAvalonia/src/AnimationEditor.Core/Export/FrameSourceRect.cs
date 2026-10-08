using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Export;

/// <summary>A frame's source rectangle on its texture, in whole pixels.</summary>
public readonly record struct FrameSourceRect(int X, int Y, int Width, int Height)
{
    /// <summary>
    /// Resolves <paramref name="frame"/>'s texture rectangle to pixels. Pixel coordinates are used
    /// directly (the resolver is never called); UV (0–1) coordinates are scaled by the size
    /// <paramref name="textureSizeResolver"/> returns for the frame's texture. Returns
    /// <c>false</c> for UV input whose texture size can't be resolved.
    /// </summary>
    public static bool TryResolve(
        AnimationFrameSave frame,
        TextureCoordinateType coordinateType,
        Func<string, (int Width, int Height)?> textureSizeResolver,
        out FrameSourceRect region)
    {
        region = default;
        float scaleX = 1f, scaleY = 1f;

        if (coordinateType != TextureCoordinateType.Pixel)
        {
            var size = textureSizeResolver(frame.TextureName);
            if (size is not { Width: > 0, Height: > 0 }) return false;
            scaleX = size.Value.Width;
            scaleY = size.Value.Height;
        }

        // Round the edges, not the width, so adjacent frames tile to exact pixel boundaries.
        int left = Round(frame.LeftCoordinate * scaleX);
        int top = Round(frame.TopCoordinate * scaleY);
        int right = Round(frame.RightCoordinate * scaleX);
        int bottom = Round(frame.BottomCoordinate * scaleY);
        region = new FrameSourceRect(left, top, right - left, bottom - top);
        return true;
    }

    private static int Round(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}

internal static class ExportNames
{
    /// <summary>Returns <paramref name="candidate"/>, or it suffixed <c>_2</c>, <c>_3</c>… if taken.</summary>
    public static string MakeUnique(string candidate, IEnumerable<string> existing)
    {
        var taken = new HashSet<string>(existing, StringComparer.Ordinal);
        if (!taken.Contains(candidate)) return candidate;

        int suffix = 2;
        while (taken.Contains($"{candidate}_{suffix}")) suffix++;
        return $"{candidate}_{suffix}";
    }
}
