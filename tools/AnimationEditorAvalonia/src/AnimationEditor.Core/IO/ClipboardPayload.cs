using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Handles the textual clipboard format used for copy/paste (IO14).
///
/// Format:  "TypeName:&lt;payload&gt;"
///   • TypeName is the simple C# type name, e.g.
///     "List&lt;AnimationChainSave&gt;", "List&lt;AnimationFrameSave&gt;",
///     "ShapesSave", "AARectSave", or "CircleSave".
///   • Every payload is written by the engine's .achx serializer, so it keeps what a save keeps.
/// </summary>
public static class ClipboardPayload
{
    private const string ShapesTypeName = nameof(ShapesSave);

    // ── Serialization ─────────────────────────────────────────────────────

    public static string Serialize(List<AnimationChainSave> chains, FrameLengthUnit sourceUnit)
    {
        var acls = new AnimationChainListSave { TimeMeasurementUnit = sourceUnit.Unit, CoordinateType = TextureCoordinateType.Pixel };
        acls.AnimationChains.AddRange(chains);
        return $"{TypeName<List<AnimationChainSave>>()}:{acls.ToXmlString()}";
    }

    public static string Serialize(List<AnimationFrameSave> frames, FrameLengthUnit sourceUnit)
    {
        var acls = new AnimationChainListSave { TimeMeasurementUnit = sourceUnit.Unit, CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave();
        chain.Frames.AddRange(frames);
        acls.AnimationChains.Add(chain);
        return $"{TypeName<List<AnimationFrameSave>>()}:{acls.ToXmlString()}";
    }

    public static string SerializeShapes(IReadOnlyList<object> shapes) => $"{ShapesTypeName}:{ShapesXml(shapes)}";

    private static string ShapesXml(IReadOnlyList<object> shapes)
    {
        var frame = new AnimationFrameSave { TextureName = "_", ShapesSave = new ShapesSave() };
        foreach (var shape in shapes)
            frame.ShapesSave!.Add(shape);
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave();
        chain.Frames.Add(frame);
        acls.AnimationChains.Add(chain);
        return acls.ToXmlString();
    }

    public static string Serialize(AARectSave rectangle)
        => $"{nameof(AARectSave)}:{ShapesXml([rectangle])}";

    public static string Serialize(CircleSave circle)
        => $"{nameof(CircleSave)}:{ShapesXml([circle])}";

    /// <param name="payload">The items to serialize.</param>
    /// <param name="sourceAchxFolder">
    /// The directory of the <c>.achx</c> the copy/cut originated from. When given, every copied
    /// frame's <c>TextureName</c> is resolved to an absolute path before being embedded, so the
    /// same physical texture is still found after a paste into a document that lives in a
    /// different folder (#1026) -- relative-to-source paths would otherwise resolve against the
    /// destination folder instead. Pass <see langword="null"/> (e.g. an unsaved document with no
    /// folder yet) to embed <c>TextureName</c> unchanged.
    /// </param>
    public static string SerializeFromPayload(CopySelectionPayload payload, FrameLengthUnit sourceUnit, string? sourceAchxFolder = null) => payload.Kind switch
    {
        CopySelectionKind.Chain => Serialize(ResolveChainTextures(
            payload.Chains.Select(AnimationCloneHelper.CloneChain).ToList(), sourceAchxFolder), sourceUnit),
        CopySelectionKind.Frame => Serialize(ResolveFrameTextures(
            payload.Frames.Select(AnimationCloneHelper.CloneFrame).ToList(), sourceAchxFolder), sourceUnit),
        CopySelectionKind.Shape => payload.Shapes.Count == 1 && payload.Shapes[0] is AARectSave r
            ? Serialize((AARectSave)AnimationCloneHelper.CloneShape(r)!)
            : payload.Shapes.Count == 1 && payload.Shapes[0] is CircleSave c
            ? Serialize((CircleSave)AnimationCloneHelper.CloneShape(c)!)
            : SerializeShapes(payload.Shapes
                .Select(s => AnimationCloneHelper.CloneShape(s)!)
                .ToList()),
        _ => throw new System.ArgumentOutOfRangeException(nameof(payload)),
    };

    /// <summary>
    /// What a copy and paste does to chains, without the text: textures named by absolute path and lengths in the
    /// pasting file's unit. Changes <paramref name="chains"/> in place, so pass ones no open document holds.
    /// </summary>
    internal static void Retarget(
        List<AnimationChainSave> chains, string sourceFolder, FrameLengthUnit sourceUnit, FrameLengthUnit destinationUnit)
    {
        ResolveChainTextures(chains, sourceFolder);
        ConvertLengths(chains.SelectMany(c => c.Frames), sourceUnit, destinationUnit);
    }

    private static List<AnimationChainSave> ResolveChainTextures(
        List<AnimationChainSave> chains, string? sourceAchxFolder)
    {
        if (!string.IsNullOrEmpty(sourceAchxFolder))
            foreach (var chain in chains)
                ResolveFrameTextures(chain.Frames, sourceAchxFolder);
        return chains;
    }

    private static List<AnimationFrameSave> ResolveFrameTextures(
        List<AnimationFrameSave> frames, string? sourceAchxFolder)
    {
        if (!string.IsNullOrEmpty(sourceAchxFolder))
            foreach (var frame in frames)
                if (!string.IsNullOrEmpty(frame.TextureName))
                    frame.TextureName = TexturePathHelper.ResolveDisplayPath(frame.TextureName, sourceAchxFolder);
        return frames;
    }

    // ── Deserialization ───────────────────────────────────────────────────

    /// <summary>
    /// True when <paramref name="text"/> starts with one of the type names <see cref="Serialize(List{AnimationChainSave})"/>
    /// and its siblings write, whether or not the rest parses. A cheap check for "the editor put this here".
    /// </summary>
    public static bool HasPayloadPrefix(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        int sep = text.IndexOf(':');
        if (sep < 0) return false;
        var typeName = text.AsSpan(0, sep);
        return typeName.SequenceEqual(TypeName<List<AnimationChainSave>>())
            || typeName.SequenceEqual(TypeName<List<AnimationFrameSave>>())
            || typeName.SequenceEqual(ShapesTypeName)
            || typeName.SequenceEqual(nameof(AARectSave))
            || typeName.SequenceEqual(nameof(CircleSave));
    }

    /// <param name="textureSize">
    /// Reads a texture's size, to convert text an editor before #1952 copied, whose regions are UV. Such a region
    /// whose texture size can't be read becomes <see cref="FrameRegion.Unset"/>.
    /// </param>
    public static bool TryDeserialize(
        string? text,
        FrameLengthUnit destinationUnit,
        System.Func<string, (int Width, int Height)?> textureSize,
        out List<AnimationChainSave>? chains,
        out List<AnimationFrameSave>? frames,
        out List<object>? shapes)
    {
        chains = null;
        frames = null;
        shapes = null;

        if (string.IsNullOrEmpty(text)) return false;
        int sep = text.IndexOf(':');
        if (sep < 0) return false;

        var typeName = text[..sep];
        var payload  = text[(sep + 1)..];

        try
        {
            if (typeName == TypeName<List<AnimationChainSave>>())
            {
                var chainList = AnimationChainListSave.FromString(payload);
                TextureCoordinateConverter.ToPixels(chainList, textureSize);
                chains = chainList.AnimationChains;
                ConvertLengths(chains.SelectMany(c => c.Frames), FrameLengthUnit.Of(chainList), destinationUnit);
                return true;
            }
            if (typeName == TypeName<List<AnimationFrameSave>>())
            {
                var frameList = AnimationChainListSave.FromString(payload);
                TextureCoordinateConverter.ToPixels(frameList, textureSize);
                frames = frameList
                    .AnimationChains.SelectMany(c => c.Frames).ToList();
                ConvertLengths(frames, FrameLengthUnit.Of(frameList), destinationUnit);
                return true;
            }
            if (typeName is ShapesTypeName or nameof(AARectSave) or nameof(CircleSave))
            {
                shapes = AnimationChainListSave.FromString(payload)
                    .AnimationChains.SelectMany(c => c.Frames)
                    .SelectMany(f => f.ShapesSave?.Shapes ?? [])
                    .Where(s => s is ShapeSave)
                    .ToList();
                return shapes.Count > 0;
            }
        }
        catch
        {
            // Malformed payload — return false
        }

        return false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Rewrites each frame's length from the copied file's unit to the pasting file's (#1829).</summary>
    private static void ConvertLengths(IEnumerable<AnimationFrameSave> frames, FrameLengthUnit source, FrameLengthUnit destination)
    {
        foreach (var frame in frames)
            frame.FrameLength = destination.Convert(frame.FrameLength, source);
    }

    private static string TypeName<T>()
    {
        var t = typeof(T);
        if (t.IsGenericType)
            return $"List<{t.GenericTypeArguments[0].Name}>";
        return t.Name;
    }
}
