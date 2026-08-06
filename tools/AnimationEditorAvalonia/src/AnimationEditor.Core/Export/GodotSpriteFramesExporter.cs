using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AnimationEditor.Core.Export;

/// <summary>
/// Pure converter from the editor's save model to a Godot 4 <c>SpriteFrames</c> <c>.tres</c>
/// text resource. Stateless and dependency-free so it can be unit-tested directly.
/// </summary>
/// <remarks>
/// Each animation chain becomes one SpriteFrames animation; each frame becomes an
/// <c>AtlasTexture</c> sub-resource (shared base sheet + <c>region</c> rect) plus a per-frame
/// duration. Multiple source textures are supported natively via multiple <c>ext_resource</c>
/// entries. Per-frame flip has no Godot SpriteFrames equivalent and is dropped with a warning.
/// Resource <c>uid://</c> lines are omitted — Godot regenerates them on first import.
/// </remarks>
public static class GodotSpriteFramesExporter
{
    /// <summary>
    /// Converts <paramref name="acls"/> to a Godot 4 SpriteFrames <c>.tres</c>.
    /// <paramref name="textureSizeResolver"/> maps a frame's texture name to its pixel size, or
    /// <c>null</c> when unreadable; UV frames whose size is unresolvable are skipped with a warning.
    /// </summary>
    public static ExportResult Export(
        AnimationChainListSave acls,
        Func<string, (int Width, int Height)?> textureSizeResolver)
    {
        ArgumentNullException.ThrowIfNull(acls);
        ArgumentNullException.ThrowIfNull(textureSizeResolver);

        var warnings = new List<string>();
        var distinctTextures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orderedTextures = new List<string>();
        var animationNames = new HashSet<string>(StringComparer.Ordinal);
        bool anyFlipDropped = false;

        // Collect atlas frames first so we can emit ext_resources, then sub_resources, then [resource].
        var atlasEntries = new List<AtlasEntry>();
        var animations = new List<AnimationEntry>();

        float durationDivisor = acls.TimeMeasurementUnit == TimeMeasurementUnit.Millisecond ? 1000f : 1f;

        foreach (var chain in acls.AnimationChains)
        {
            string animationName = MakeUnique(chain.Name, animationNames);
            animationNames.Add(animationName);

            var frames = new List<(string SubId, float DurationSeconds)>();

            for (int i = 0; i < chain.Frames.Count; i++)
            {
                var frame = chain.Frames[i];

                if (frame.FlipHorizontal || frame.FlipVertical || frame.FlipDiagonal)
                    anyFlipDropped = true;

                if (!string.IsNullOrEmpty(frame.TextureName) && distinctTextures.Add(frame.TextureName))
                    orderedTextures.Add(frame.TextureName);

                if (!FrameRectResolver.TryBuildRect(frame, acls.CoordinateType, textureSizeResolver, out var rect))
                {
                    warnings.Add($"Frame {i} of '{chain.Name}' was skipped: texture " +
                                 $"'{frame.TextureName}' could not be read to convert UV coordinates to pixels.");
                    continue;
                }

                string subId = MakeUnique($"AtlasTexture_{SanitizeId(animationName)}_{i}",
                    atlasEntries.ConvertAll(e => e.SubId));
                atlasEntries.Add(new AtlasEntry(subId, frame.TextureName, rect));

                float durationSeconds = frame.FrameLength / durationDivisor;
                frames.Add((subId, durationSeconds));
            }

            animations.Add(new AnimationEntry(animationName, frames));
        }

        if (anyFlipDropped)
            warnings.Add("Per-frame flip flags have no Godot SpriteFrames equivalent and were dropped.");

        // Map texture name → ext_resource id (1-based Godot style: "1_tex0", "2_tex1", …).
        var textureExtIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < orderedTextures.Count; i++)
            textureExtIds[orderedTextures[i]] = $"{i + 1}_tex{i}";

        var sb = new StringBuilder();
        sb.AppendLine("[gd_resource type=\"SpriteFrames\" format=3]");
        sb.AppendLine();

        foreach (var textureName in orderedTextures)
        {
            string path = textureName.Replace('\\', '/');
            if (!path.StartsWith("res://", StringComparison.Ordinal))
                path = "res://" + path;
            sb.AppendLine(
                $"[ext_resource type=\"Texture2D\" path=\"{Escape(path)}\" id=\"{textureExtIds[textureName]}\"]");
        }

        if (orderedTextures.Count > 0)
            sb.AppendLine();

        foreach (var entry in atlasEntries)
        {
            string atlasRef = textureExtIds.TryGetValue(entry.TextureName, out var extId)
                ? $"ExtResource(\"{extId}\")"
                : "null";
            sb.AppendLine($"[sub_resource type=\"AtlasTexture\" id=\"{entry.SubId}\"]");
            sb.AppendLine($"atlas = {atlasRef}");
            sb.AppendLine(
                $"region = Rect2({entry.Rect.X}, {entry.Rect.Y}, {entry.Rect.W}, {entry.Rect.H})");
            sb.AppendLine();
        }

        sb.AppendLine("[resource]");
        sb.Append("animations = [");
        for (int a = 0; a < animations.Count; a++)
        {
            var anim = animations[a];
            if (a > 0) sb.Append(", ");
            sb.Append('{');
            sb.Append("\"frames\": [");
            for (int f = 0; f < anim.Frames.Count; f++)
            {
                var (subId, duration) = anim.Frames[f];
                if (f > 0) sb.Append(", ");
                sb.Append('{');
                sb.Append("\"duration\": ");
                sb.Append(duration.ToString(CultureInfo.InvariantCulture));
                sb.Append(", \"texture\": SubResource(\"");
                sb.Append(subId);
                sb.Append("\")}");
            }
            sb.Append("], \"loop\": true, \"name\": &\"");
            sb.Append(Escape(anim.Name));
            // speed=1.0 so absolute seconds = duration / speed = FrameLength (in seconds).
            sb.Append("\", \"speed\": 1.0}");
        }
        sb.AppendLine("]");

        return new ExportResult(sb.ToString(), warnings, orderedTextures);
    }

    private static string MakeUnique(string candidate, IEnumerable<string> existing)
    {
        var taken = new HashSet<string>(existing, StringComparer.Ordinal);
        if (!taken.Contains(candidate)) return candidate;

        int suffix = 2;
        while (taken.Contains($"{candidate}_{suffix}")) suffix++;
        return $"{candidate}_{suffix}";
    }

    private static string MakeUnique(string candidate, List<string> existing) =>
        MakeUnique(candidate, (IEnumerable<string>)existing);

    private static string SanitizeId(string name)
    {
        if (string.IsNullOrEmpty(name)) return "anim";
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                sb.Append(c);
            else
                sb.Append('_');
        }
        return sb.Length == 0 ? "anim" : sb.ToString();
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("\"", "\\\"", StringComparison.Ordinal);

    private sealed record AtlasEntry(string SubId, string TextureName, FramePixelRect Rect);
    private sealed record AnimationEntry(string Name, List<(string SubId, float DurationSeconds)> Frames);
}
