using System.Globalization;
using System.Text;
using System.Text.Json;
using AnimationEditor.Core.Keyframes;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>
/// Human and machine readable dump of an animation file as stored on disk (#1811): chains, frames by
/// index with texture, length and region, and each frame's shapes. All numbers are invariant-culture.
/// </summary>
public static class AnimationDescriber
{
    public static string ToText(AnimationChainListSave doc, string path, IReadOnlyDictionary<AnimationChainSave, uint>? ownerTiles = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine(FormattableString.Invariant(
            $"{Path.GetFileName(path)}  coordinates={doc.CoordinateType}  unit={doc.TimeMeasurementUnit}  chains={doc.AnimationChains.Count}"));
        foreach (var chain in doc.AnimationChains)
        {
            var flags = new List<string>();
            if (chain.Loop) flags.Add("loop");
            if (chain.PingPong) flags.Add("pingpong");
            if (chain.Reverse) flags.Add("reverse");
            sb.AppendLine(FormattableString.Invariant(
                $"{chain.Name}  ({chain.Frames.Count} frames, length {Num(chain.Frames.Sum(f => f.FrameLength))}{(flags.Count > 0 ? ", " + string.Join(" ", flags) : "")}{(ownerTiles is not null && ownerTiles.TryGetValue(chain, out var owner) ? $", owner tile {owner}" : "")})"));
            for (int i = 0; i < chain.Frames.Count; i++)
            {
                var f = chain.Frames[i];
                sb.AppendLine(FormattableString.Invariant(
                    $"  {i}  {f.TextureName}  length {Num(f.FrameLength)}  region {Num(f.LeftCoordinate)},{Num(f.TopCoordinate)},{Num(f.RightCoordinate)},{Num(f.BottomCoordinate)}"));
                var color = ColorText(f);
                if (color.Length > 0) sb.AppendLine($"       color {color}");
                var frameKeys = FrameKeyframeText(chain, f);
                if (frameKeys.Length > 0) sb.AppendLine($"       keyframes {frameKeys}");
                for (int e = 0; e < f.Events.Count; e++)
                    sb.AppendLine($"       {chain.Name}/{i}/event/{e}  {f.Events[e].Name}{(f.Events[e].Data is { } d ? "  data " + d : "")}");
                foreach (var shape in Shapes(f))
                {
                    var keys = ShapeKeyframeText(chain, shape);
                    sb.AppendLine(FormattableString.Invariant($"       {chain.Name}/{i}/{EditAddress.TypeName(shape)}/{shape.Name}  {ShapeSummary(shape)}{(keys.Length > 0 ? "  " + keys : "")}"));
                }
            }
        }
        return sb.ToString();
    }

    public static void WriteJson(Utf8JsonWriter w, AnimationChainListSave doc, string path, IReadOnlyDictionary<AnimationChainSave, uint>? ownerTiles = null)
    {
        w.WriteStartObject();
        w.WriteString("file", Path.GetFileName(path));
        w.WriteString("coordinateType", doc.CoordinateType.ToString());
        w.WriteString("unit", doc.TimeMeasurementUnit.ToString());
        w.WriteStartArray("chains");
        foreach (var chain in doc.AnimationChains)
        {
            w.WriteStartObject();
            w.WriteString("name", chain.Name);
            w.WriteBoolean("loop", chain.Loop);
            w.WriteBoolean("pingPong", chain.PingPong);
            w.WriteBoolean("reverse", chain.Reverse);
            if (ownerTiles is not null && ownerTiles.TryGetValue(chain, out var ownerTile)) w.WriteNumber("ownerTile", ownerTile);
            w.WriteStartArray("frames");
            for (int i = 0; i < chain.Frames.Count; i++)
            {
                var f = chain.Frames[i];
                w.WriteStartObject();
                w.WriteNumber("index", i);
                w.WriteString("texture", f.TextureName);
                w.WriteNumber("length", f.FrameLength);
                w.WriteStartObject("region");
                w.WriteNumber("left", f.LeftCoordinate);
                w.WriteNumber("top", f.TopCoordinate);
                w.WriteNumber("right", f.RightCoordinate);
                w.WriteNumber("bottom", f.BottomCoordinate);
                w.WriteEndObject();
                if (f.Red is not null || f.Green is not null || f.Blue is not null || f.Alpha is not null || f.ColorOperation is not null)
                {
                    w.WriteStartObject("color");
                    if (f.Red is { } r) w.WriteNumber("red", r);
                    if (f.Green is { } g) w.WriteNumber("green", g);
                    if (f.Blue is { } b) w.WriteNumber("blue", b);
                    if (f.Alpha is { } a) w.WriteNumber("alpha", a);
                    if (f.ColorOperation is { } op) w.WriteString("operation", op.ToString());
                    w.WriteEndObject();
                }
                w.WriteStartObject("keyframes");
                WriteKeyframe(w, "offset", KeyframeInspectorState.IsEffectivelyKeyframe(KeyframeGroup.Offset, f, chain), f.OffsetInterpolation, f.OffsetEasing);
                WriteKeyframe(w, "color", KeyframeInspectorState.IsEffectivelyKeyframe(KeyframeGroup.Color, f, chain), f.ColorInterpolation, f.ColorEasing);
                w.WriteEndObject();
                w.WriteStartArray("events");
                for (int e = 0; e < f.Events.Count; e++)
                {
                    w.WriteStartObject();
                    w.WriteString("address", $"{chain.Name}/{i}/event/{e}");
                    w.WriteString("name", f.Events[e].Name);
                    if (f.Events[e].Data is { } data) w.WriteString("data", data);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteStartArray("shapes");
                foreach (var shape in Shapes(f))
                {
                    w.WriteStartObject();
                    w.WriteString("address", $"{chain.Name}/{i}/{EditAddress.TypeName(shape)}/{shape.Name}");
                    w.WriteString("type", EditAddress.TypeName(shape));
                    w.WriteString("name", shape.Name);
                    w.WriteNumber("x", shape.X);
                    w.WriteNumber("y", shape.Y);
                    switch (shape)
                    {
                        case AARectSave r: w.WriteNumber("scaleX", r.ScaleX); w.WriteNumber("scaleY", r.ScaleY); break;
                        case CircleSave c: w.WriteNumber("radius", c.Radius); break;
                        case PolygonSave p:
                            w.WriteStartArray("points");
                            foreach (var point in p.Points)
                            {
                                w.WriteStartArray();
                                w.WriteNumberValue(point.X);
                                w.WriteNumberValue(point.Y);
                                w.WriteEndArray();
                            }
                            w.WriteEndArray();
                            break;
                    }
                    w.WriteStartObject("keyframes");
                    foreach (var group in new[] { ShapeKeyframeGroup.Offset, ShapeKeyframeGroup.Size, ShapeKeyframeGroup.Points })
                    {
                        if (!KeyframeInspectorState.HasGroup(group, shape)) continue;
                        var (type, easing) = Stored(shape, group);
                        WriteKeyframe(w, group.ToString().ToLowerInvariant(),
                            KeyframeInspectorState.IsEffectivelyKeyframe(group, shape, chain), type, easing);
                    }
                    w.WriteEndObject();
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void WriteKeyframe(Utf8JsonWriter w, string name, bool isKeyframe, string? interpolation, string? easing)
    {
        w.WriteStartObject(name);
        w.WriteBoolean("keyframe", isKeyframe);
        if (interpolation is not null) w.WriteString("interpolation", interpolation);
        if (easing is not null) w.WriteString("easing", easing);
        w.WriteEndObject();
    }

    private static (string? Type, string? Easing) Stored(ShapeSave shape, ShapeKeyframeGroup group) => group switch
    {
        ShapeKeyframeGroup.Offset => (shape.OffsetInterpolation, shape.OffsetEasing),
        ShapeKeyframeGroup.Size => shape is SizedShapeSave s ? (s.SizeInterpolation, s.SizeEasing) : (null, null),
        _ => shape is PolygonSave p ? (p.PointsInterpolation, p.PointsEasing) : (null, null),
    };

    /// <summary>Only the color parts a frame sets; parts it leaves out inherit from earlier frames.</summary>
    private static string ColorText(AnimationFrameSave f)
    {
        var parts = new List<string>();
        if (f.Red is { } r) parts.Add($"red={r}");
        if (f.Green is { } g) parts.Add($"green={g}");
        if (f.Blue is { } b) parts.Add($"blue={b}");
        if (f.Alpha is { } a) parts.Add($"alpha={a}");
        if (f.ColorOperation is { } op) parts.Add($"operation={op}");
        return string.Join(" ", parts);
    }

    /// <summary>Only what differs from the default: a keyframe turned off (<c>offset=skip</c>) or a stored curve (<c>offset-curve=Quadratic/In</c>).</summary>
    private static string FrameKeyframeText(AnimationChainSave chain, AnimationFrameSave f)
    {
        var parts = new List<string>();
        Add(parts, "offset", KeyframeInspectorState.IsEffectivelyKeyframe(KeyframeGroup.Offset, f, chain), f.OffsetInterpolation, f.OffsetEasing);
        Add(parts, "color", KeyframeInspectorState.IsEffectivelyKeyframe(KeyframeGroup.Color, f, chain), f.ColorInterpolation, f.ColorEasing);
        return string.Join(" ", parts);
    }

    private static string ShapeKeyframeText(AnimationChainSave chain, ShapeSave shape)
    {
        var parts = new List<string>();
        foreach (var group in new[] { ShapeKeyframeGroup.Offset, ShapeKeyframeGroup.Size, ShapeKeyframeGroup.Points })
        {
            if (!KeyframeInspectorState.HasGroup(group, shape)) continue;
            var (type, easing) = Stored(shape, group);
            Add(parts, group.ToString().ToLowerInvariant(), KeyframeInspectorState.IsEffectivelyKeyframe(group, shape, chain), type, easing);
        }
        return string.Join(" ", parts);
    }

    private static void Add(List<string> parts, string group, bool isKeyframe, string? type, string? easing)
    {
        if (!isKeyframe) parts.Add($"{group}=skip");
        if (type is not null) parts.Add($"{group}-curve={type}{(easing is not null ? "/" + easing : "")}");
    }

    private static IEnumerable<ShapeSave> Shapes(AnimationFrameSave frame) =>
        frame.ShapesSave?.Shapes.OfType<ShapeSave>() ?? [];

    private static string ShapeSummary(ShapeSave shape) => shape switch
    {
        AARectSave r => FormattableString.Invariant($"at {Num(r.X)},{Num(r.Y)} scale {Num(r.ScaleX)}x{Num(r.ScaleY)}"),
        CircleSave c => FormattableString.Invariant($"at {Num(c.X)},{Num(c.Y)} radius {Num(c.Radius)}"),
        PolygonSave p => FormattableString.Invariant($"at {Num(p.X)},{Num(p.Y)} points {string.Join(" ", p.Points.Select(pt => $"{Num(pt.X)},{Num(pt.Y)}"))}"),
        _ => string.Empty,
    };

    private static string Num(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
