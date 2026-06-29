using FlatRedBall2.Animation.Content;
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
///   • Chains, frames, and multi-shape payloads use the engine's .achx serializer.
///   • Single shapes are flat POCOs on the simple <see cref="XmlFile"/> path.
/// </summary>
public static class ClipboardPayload
{
    private const string ShapesTypeName = nameof(ShapesSave);

    // ── Serialization ─────────────────────────────────────────────────────

    public static string Serialize(List<AnimationChainSave> chains)
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.AddRange(chains);
        return $"{TypeName<List<AnimationChainSave>>()}:{acls.ToXmlString()}";
    }

    public static string Serialize(List<AnimationFrameSave> frames)
    {
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave();
        chain.Frames.AddRange(frames);
        acls.AnimationChains.Add(chain);
        return $"{TypeName<List<AnimationFrameSave>>()}:{acls.ToXmlString()}";
    }

    public static string SerializeShapes(IReadOnlyList<object> shapes)
    {
        var frame = new AnimationFrameSave { TextureName = "_", ShapesSave = new ShapesSave() };
        foreach (var shape in shapes)
            frame.ShapesSave!.Shapes.Add(shape);
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave();
        chain.Frames.Add(frame);
        acls.AnimationChains.Add(chain);
        return $"{ShapesTypeName}:{acls.ToXmlString()}";
    }

    public static string Serialize(AARectSave rectangle)
        => Encode(rectangle);

    public static string Serialize(CircleSave circle)
        => Encode(circle);

  public static string SerializeFromPayload(CopySelectionPayload payload) => payload.Kind switch
    {
        CopySelectionKind.Chain => Serialize(payload.Chains.Select(AnimationCloneHelper.CloneChain).ToList()),
        CopySelectionKind.Frame => Serialize(payload.Frames.Select(AnimationCloneHelper.CloneFrame).ToList()),
        CopySelectionKind.Shape => payload.Shapes.Count == 1 && payload.Shapes[0] is AARectSave r
            ? Serialize((AARectSave)AnimationCloneHelper.CloneShape(r)!)
            : payload.Shapes.Count == 1 && payload.Shapes[0] is CircleSave c
            ? Serialize((CircleSave)AnimationCloneHelper.CloneShape(c)!)
            : SerializeShapes(payload.Shapes
                .Select(s => AnimationCloneHelper.CloneShape(s)!)
                .ToList()),
        _ => throw new System.ArgumentOutOfRangeException(nameof(payload)),
    };

    // ── Deserialization ───────────────────────────────────────────────────

    public static bool TryDeserialize(
        string? text,
        out List<AnimationChainSave>? chains,
        out List<AnimationFrameSave>? frames,
        out List<AARectSave>? rectangles,
        out List<CircleSave>? circles)
    {
        chains     = null;
        frames     = null;
        rectangles = null;
        circles    = null;

        if (string.IsNullOrEmpty(text)) return false;
        int sep = text.IndexOf(':');
        if (sep < 0) return false;

        var typeName = text[..sep];
        var payload  = text[(sep + 1)..];

        try
        {
            if (typeName == TypeName<List<AnimationChainSave>>())
            {
                chains = AnimationChainListSave.FromString(payload).AnimationChains;
                return true;
            }
            if (typeName == TypeName<List<AnimationFrameSave>>())
            {
                frames = AnimationChainListSave.FromString(payload)
                    .AnimationChains.SelectMany(c => c.Frames).ToList();
                return true;
            }
            if (typeName == ShapesTypeName)
            {
                var shapes = AnimationChainListSave.FromString(payload)
                    .AnimationChains.SelectMany(c => c.Frames)
                    .SelectMany(f => f.ShapesSave?.Shapes ?? [])
                    .ToList();
                rectangles = shapes.OfType<AARectSave>().ToList();
                circles    = shapes.OfType<CircleSave>().ToList();
                return rectangles.Count + circles.Count > 0;
            }
            if (typeName == nameof(AARectSave))
            {
                var rect = XmlFile.DeserializeFromString<AARectSave>(payload);
                if (rect is null) return false;
                rectangles = new List<AARectSave> { rect };
                return true;
            }
            if (typeName == nameof(CircleSave))
            {
                var circle = XmlFile.DeserializeFromString<CircleSave>(payload);
                if (circle is null) return false;
                circles = new List<CircleSave> { circle };
                return true;
            }
        }
        catch
        {
            // Malformed payload — return false
        }

        return false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string Encode<T>(T obj)
    {
        XmlFile.SerializeToString(obj, out string xml);
        return $"{TypeName<T>()}:{xml}";
    }

    private static string TypeName<T>()
    {
        var t = typeof(T);
        if (t.IsGenericType)
            return $"List<{t.GenericTypeArguments[0].Name}>";
        return t.Name;
    }
}
