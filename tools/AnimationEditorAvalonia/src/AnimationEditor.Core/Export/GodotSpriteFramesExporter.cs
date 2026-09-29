using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AnimationEditor.Core.Export;

/// <summary>
/// Pure converter from the editor's save model to a Godot 4 <c>SpriteFrames</c> text resource
/// (<c>.tres</c>), played back by <c>AnimatedSprite2D</c>. Each chain becomes one animation; each
/// frame becomes an <c>AtlasTexture</c> sub-resource (source texture + <c>region</c>).
/// </summary>
/// <remarks>
/// <para>Durations: Godot shows a frame for <c>duration / speed</c> seconds, where <c>speed</c> is
/// per animation (FPS) and <c>duration</c> is per frame (relative). The exporter sets
/// <c>speed = 1 / shortest frame length</c>, so the shortest frame has duration 1 and the rest are
/// multiples of it, which reproduces every .achx frame length exactly. Millisecond files are
/// converted to seconds first.</para>
/// <para>Texture paths are written relative to the exported file (Godot resolves a non-<c>res://</c>
/// path against the <c>.tres</c>'s folder), so export into the Godot project next to the copied
/// textures. No <c>uid</c>s are written; Godot assigns them on import.</para>
/// <para>Dropped with a warning: per-frame flip (AtlasTexture has no flip; use the node's
/// <c>flip_h</c>/<c>flip_v</c>) and per-frame RelativeX/RelativeY offsets.</para>
/// </remarks>
public static class GodotSpriteFramesExporter
{
    /// <summary>Godot's default <c>SpriteFrames</c> animation speed, used for chains with no timed frames.</summary>
    private const double DefaultSpeed = 5.0;

    /// <summary>Godot clamps frame durations below this (<c>SPRITE_FRAME_MINIMUM_DURATION</c>).</summary>
    private const double MinimumDuration = 0.01;

    /// <summary>
    /// Converts <paramref name="acls"/> to <c>SpriteFrames</c> <c>.tres</c> text.
    /// <paramref name="textureSizeResolver"/> is consulted only for UV-coordinate input; frames
    /// whose texture size can't be resolved are skipped with a warning.
    /// </summary>
    public static ExportResult Export(
        AnimationChainListSave acls,
        Func<string, (int Width, int Height)?> textureSizeResolver)
    {
        ArgumentNullException.ThrowIfNull(acls);
        ArgumentNullException.ThrowIfNull(textureSizeResolver);

        var warnings = new List<string>();
        var textureIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var orderedTextures = new List<string>();
        var subResources = new StringBuilder();
        var animations = new List<string>();
        var animationNames = new List<string>();
        int atlasCount = 0;
        bool anyFlip = false, anyOffset = false, anyZeroLength = false;
        double secondsDivisor = acls.TimeMeasurementUnit == TimeMeasurementUnit.Millisecond ? 1000.0 : 1.0;

        foreach (var chain in acls.AnimationChains)
        {
            string name = ExportNames.MakeUnique(chain.Name, animationNames);
            if (name != chain.Name)
                warnings.Add($"Another animation is already named '{chain.Name}', so this one was exported as '{name}'.");
            animationNames.Add(name);

            var frames = new List<(int AtlasId, double Seconds)>();
            for (int i = 0; i < chain.Frames.Count; i++)
            {
                var frame = chain.Frames[i];

                if (string.IsNullOrEmpty(frame.TextureName))
                {
                    warnings.Add($"Frame {i} of '{chain.Name}' was skipped: it has no texture.");
                    continue;
                }
                if (!textureIds.ContainsKey(frame.TextureName))
                {
                    orderedTextures.Add(frame.TextureName);
                    textureIds[frame.TextureName] = orderedTextures.Count;
                }
                if (!FrameSourceRect.TryResolve(frame, acls.CoordinateType, textureSizeResolver, out var region))
                {
                    warnings.Add($"Frame {i} of '{chain.Name}' was skipped: texture " +
                                 $"'{frame.TextureName}' could not be read to convert UV coordinates to pixels.");
                    continue;
                }

                anyFlip |= frame.FlipHorizontal || frame.FlipVertical || frame.FlipDiagonal;
                anyOffset |= frame.RelativeX != 0f || frame.RelativeY != 0f;

                atlasCount++;
                subResources.Append('\n')
                    .Append($"[sub_resource type=\"AtlasTexture\" id=\"AtlasTexture_{atlasCount}\"]\n")
                    .Append($"atlas = ExtResource(\"{textureIds[frame.TextureName]}\")\n")
                    .Append($"region = Rect2({region.X}, {region.Y}, {region.Width}, {region.Height})\n");
                frames.Add((atlasCount, Math.Round(frame.FrameLength / secondsDivisor, 6)));
            }

            var timed = frames.Where(f => f.Seconds > 0).Select(f => f.Seconds).ToList();
            double speed = timed.Count > 0 ? Math.Round(1.0 / timed.Min(), 6) : DefaultSpeed;
            anyZeroLength |= frames.Count > timed.Count;

            var frameEntries = frames.Select(f =>
            {
                double duration = f.Seconds > 0 ? Math.Round(f.Seconds * speed, 6) : MinimumDuration;
                return $"{{\n\"duration\": {Number(duration)},\n\"texture\": SubResource(\"AtlasTexture_{f.AtlasId}\")\n}}";
            });

            animations.Add("{\n" +
                           $"\"frames\": [{string.Join(", ", frameEntries)}],\n" +
                           $"\"loop\": {(chain.Loop ? "true" : "false")},\n" +
                           $"\"name\": &\"{Escape(name)}\",\n" +
                           $"\"speed\": {Number(speed)}\n" +
                           "}");
        }

        if (anyFlip)
            warnings.Add("Some frames are flipped, but a Godot SpriteFrames frame has no flip; " +
                         "flip the AnimatedSprite2D (flip_h/flip_v) instead.");
        if (anyOffset)
            warnings.Add("Some frames have a RelativeX/RelativeY offset, which SpriteFrames cannot store; it was dropped.");
        if (anyZeroLength)
            warnings.Add($"Some frames have zero duration; they were exported with Godot's minimum relative duration ({Number(MinimumDuration)}).");
        if (orderedTextures.Any(t => System.IO.Path.IsPathRooted(t) || t.Contains(':')))
            warnings.Add("Some textures use an absolute path, which Godot can only load from inside the project; " +
                         "move them next to the .achx.");

        var text = new StringBuilder("[gd_resource type=\"SpriteFrames\" format=3]\n");
        if (orderedTextures.Count > 0)
        {
            text.Append('\n');
            for (int i = 0; i < orderedTextures.Count; i++)
                text.Append($"[ext_resource type=\"Texture2D\" path=\"{Escape(orderedTextures[i].Replace('\\', '/'))}\" id=\"{i + 1}\"]\n");
        }
        text.Append(subResources)
            .Append("\n[resource]\n")
            .Append($"animations = [{string.Join(", ", animations)}]\n");

        return new ExportResult(text.ToString(), warnings, orderedTextures);
    }

    /// <summary>Godot float literal: always has a decimal point (<c>10.0</c>, <c>2.5</c>).</summary>
    private static string Number(double value) => value.ToString("0.0#####", CultureInfo.InvariantCulture);

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
}
