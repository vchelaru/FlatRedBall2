using AnimationEditor.Core.Utilities;
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
        int textureWidth = 0, textureHeight = 0;

        if (coordinateType != TextureCoordinateType.Pixel)
        {
            var size = textureSizeResolver(frame.TextureName);
            if (size is not { Width: > 0, Height: > 0 }) return false;
            (textureWidth, textureHeight) = size.Value;
        }

        var pixels = PixelRectangle.FromFrame(frame, coordinateType, textureWidth, textureHeight);
        region = new FrameSourceRect(pixels.X, pixels.Y, pixels.Width, pixels.Height);
        return true;
    }
}

internal static class ExportWarning
{
    /// <summary>One sentence naming every kind an export dropped.</summary>
    public static string Dropped(IReadOnlyList<ChainDataKind> kinds, string format)
    {
        string names = ChainData.Describe(kinds);
        return $"{char.ToUpperInvariant(names[0])}{names[1..]} can't be stored in {format} and {(kinds.Count == 1 ? "was" : "were")} not exported.";
    }
}

internal static class ExportNames
{
    public static string RenameWarning(string name, string exportedName) =>
        $"Another animation is already named '{name}', so this one was exported as '{exportedName}'.";

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
