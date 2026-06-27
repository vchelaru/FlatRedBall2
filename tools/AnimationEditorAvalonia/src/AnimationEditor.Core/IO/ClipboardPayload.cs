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
///     "AARectSave", or "CircleSave".
///   • Chains and frames are serialized through the engine's own .achx serializer
///     (<see cref="AnimationChainListSave.ToXmlString"/> / <see cref="AnimationChainListSave.FromString"/>)
///     so per-frame shapes — which live in a polymorphic <c>List&lt;object&gt;</c> that
///     <c>XmlSerializer</c> cannot serialize — round-trip exactly as the on-disk format does.
///   • Single shapes are flat POCOs and stay on the simple <see cref="XmlFile"/> path.
///
/// This class handles only serialization/deserialization — clipboard I/O is the
/// responsibility of the app layer.
/// </summary>
public static class ClipboardPayload
{
    // ── Serialization ─────────────────────────────────────────────────────

    public static string Serialize(List<AnimationChainSave> chains)
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.AddRange(chains);
        return $"{TypeName<List<AnimationChainSave>>()}:{acls.ToXmlString()}";
    }

    public static string Serialize(List<AnimationFrameSave> frames)
    {
        // Wrap the loose frames in a single throwaway chain so they go through the same
        // .achx serializer as a chain copy. Deserialize unwraps them back to a flat list.
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave();
        chain.Frames.AddRange(frames);
        acls.AnimationChains.Add(chain);
        return $"{TypeName<List<AnimationFrameSave>>()}:{acls.ToXmlString()}";
    }

    public static string Serialize(AARectSave rectangle)
        => Encode(rectangle);

    public static string Serialize(CircleSave circle)
        => Encode(circle);

    // ── Deserialization ───────────────────────────────────────────────────

    /// <summary>
    /// Attempts to parse a clipboard string into one of the supported payload types.
    /// Returns <c>true</c> when the string was valid and the appropriate out-parameter
    /// is populated; all other out-parameters will be <c>null</c>.
    /// Returns <c>false</c> when the string is unrecognised or malformed.
    /// </summary>
    public static bool TryDeserialize(
        string? text,
        out List<AnimationChainSave>? chains,
        out List<AnimationFrameSave>? frames,
        out AARectSave? rectangle,
        out CircleSave? circle)
    {
        chains    = null;
        frames    = null;
        rectangle = null;
        circle    = null;

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
            if (typeName == nameof(AARectSave))
            {
                rectangle = XmlFile.DeserializeFromString<AARectSave>(payload);
                return rectangle != null;
            }
            if (typeName == nameof(CircleSave))
            {
                circle = XmlFile.DeserializeFromString<CircleSave>(payload);
                return circle != null;
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
