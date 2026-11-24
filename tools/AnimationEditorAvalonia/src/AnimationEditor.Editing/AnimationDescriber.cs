using System.Globalization;
using System.Text;
using System.Text.Json;
using AnimationEditor.Core.Keyframes;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>
/// Human and machine readable dump of an animation file as stored on disk (#1811): chains, frames by
/// index with texture, length and region, and each frame's shapes. All numbers are invariant-culture.
/// </summary>
public static class AnimationDescriber
{
    public static string ToText(AnimationChainListSave doc, string path, IReadOnlyDictionary<AnimationChainSave, uint>? ownerTiles = null,
        IReadOnlyCollection<string>? only = null, bool summary = false)
    {
        var sb = new StringBuilder();
        if (summary)
        {
            foreach (var chain in Selected(doc, only))
                sb.AppendLine(FormattableString.Invariant($"{chain.Name}  {chain.Frames.Count} frames  length {Num(chain.Frames.Sum(f => f.FrameLength))}"));
            return sb.ToString();
        }
        sb.AppendLine(FormattableString.Invariant(
            $"{Path.GetFileName(path)}  coordinates={doc.CoordinateType}  unit={doc.TimeMeasurementUnit}  chains={doc.AnimationChains.Count}"));
        foreach (var chain in Selected(doc, only))
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
                    $"  {i}  {f.TextureName}  length {Num(f.FrameLength)}  region {Num(f.LeftCoordinate)},{Num(f.TopCoordinate)},{Num(f.RightCoordinate - f.LeftCoordinate)},{Num(f.BottomCoordinate - f.TopCoordinate)}"));
                var placement = PlacementText(f);
                if (placement.Length > 0) sb.AppendLine($"       {placement}");
                var color = ColorText(StoredColor(f));
                if (color.Length > 0) sb.AppendLine($"       color {color}");
                var frameKeys = FrameKeyframeText(chain, f);
                if (frameKeys.Length > 0) sb.AppendLine($"       keyframes {frameKeys}");
                for (int e = 0; e < f.Events.Count; e++)
                    sb.AppendLine($"       {chain.Name}/{i}/event/{e}  {EventText(f.Events[e])}");
                foreach (var shape in Shapes(f))
                {
                    var keys = ShapeKeyframeText(chain, shape);
                    sb.AppendLine(FormattableString.Invariant($"       {chain.Name}/{i}/{EditAddress.TypeName(shape)}/{shape.Name}  {ShapeSummary(shape)}{(keys.Length > 0 ? "  " + keys : "")}"));
                }
            }
        }
        return sb.ToString();
    }

    private static IEnumerable<AnimationChainSave> Selected(AnimationChainListSave doc, IReadOnlyCollection<string>? only) =>
        only is null ? doc.AnimationChains : doc.AnimationChains.Where(c => only.Contains(c.Name));

    public static void WriteJson(Utf8JsonWriter w, AnimationChainListSave doc, string path, IReadOnlyDictionary<AnimationChainSave, uint>? ownerTiles = null,
        IReadOnlyCollection<string>? only = null, bool summary = false)
    {
        w.WriteStartObject();
        w.WriteString("file", Path.GetFileName(path));
        w.WriteString("coordinateType", doc.CoordinateType.ToString());
        w.WriteString("unit", doc.TimeMeasurementUnit.ToString());
        w.WriteStartArray("chains");
        foreach (var chain in Selected(doc, only))
        {
            w.WriteStartObject();
            w.WriteString("name", chain.Name);
            if (summary)
            {
                w.WriteNumber("frames", chain.Frames.Count);
                w.WriteNumber("length", Math.Round(AnimationEditor.Core.Rendering.ChainClock.TotalLength(chain, AnimationEditor.Core.Utilities.FrameLengthUnit.Of(doc)), 6));
                w.WriteEndObject();
                continue;
            }
            w.WriteBoolean("loop", chain.Loop);
            w.WriteBoolean("pingPong", chain.PingPong);
            w.WriteBoolean("reverse", chain.Reverse);
            w.WriteNumber("length", Math.Round(AnimationEditor.Core.Rendering.ChainClock.TotalLength(chain, AnimationEditor.Core.Utilities.FrameLengthUnit.Of(doc)), 6));
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
                w.WriteNumber("x", f.LeftCoordinate);
                w.WriteNumber("y", f.TopCoordinate);
                w.WriteNumber("width", f.RightCoordinate - f.LeftCoordinate);
                w.WriteNumber("height", f.BottomCoordinate - f.TopCoordinate);
                w.WriteEndObject();
                w.WriteStartObject("offset");
                w.WriteNumber("x", f.RelativeX);
                w.WriteNumber("y", f.RelativeY);
                w.WriteEndObject();
                WriteFlips(w, f.FlipHorizontal, f.FlipVertical, f.FlipDiagonal);
                WriteColor(w, StoredColor(f));
                w.WriteStartObject("keyframes");
                WriteKeyframe(w, "offset", KeyframeInspectorState.IsEffectivelyKeyframe(KeyframeGroup.Offset, f, chain), f.OffsetInterpolation, f.OffsetEasing);
                WriteKeyframe(w, "color", KeyframeInspectorState.IsEffectivelyKeyframe(KeyframeGroup.Color, f, chain), f.ColorInterpolation, f.ColorEasing);
                w.WriteEndObject();
                w.WriteStartArray("events");
                for (int e = 0; e < f.Events.Count; e++)
                {
                    w.WriteStartObject();
                    w.WriteString("address", $"{chain.Name}/{i}/event/{e}");
                    WriteEventBody(w, f.Events[e]);
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

    /// <summary>Offset and flip flags, each only when non-default (<c>offset 3,-2.5  flip H D</c>).</summary>
    private static string PlacementText(AnimationFrameSave f)
    {
        var parts = new List<string>();
        if (f.RelativeX != 0 || f.RelativeY != 0) parts.Add(FormattableString.Invariant($"offset {Num(f.RelativeX)},{Num(f.RelativeY)}"));
        var flips = FlipText(f.FlipHorizontal, f.FlipVertical, f.FlipDiagonal);
        if (flips.Length > 0) parts.Add(flips);
        return string.Join("  ", parts);
    }

    /// <summary><c>flip H D</c>, or empty when no flag is set.</summary>
    internal static string FlipText(bool horizontal, bool vertical, bool diagonal)
    {
        var flips = new List<string>();
        if (horizontal) flips.Add("H");
        if (vertical) flips.Add("V");
        if (diagonal) flips.Add("D");
        return flips.Count > 0 ? "flip " + string.Join(" ", flips) : "";
    }

    internal static void WriteFlips(Utf8JsonWriter w, bool horizontal, bool vertical, bool diagonal)
    {
        w.WriteBoolean("flipHorizontal", horizontal);
        w.WriteBoolean("flipVertical", vertical);
        w.WriteBoolean("flipDiagonal", diagonal);
    }

    // Only the color parts the frame itself sets; parts it leaves out inherit from earlier frames.
    private static ResolvedFrameColor StoredColor(AnimationFrameSave f) => new(f.Red, f.Green, f.Blue, f.Alpha, f.ColorOperation);

    /// <summary>The parts that are set, or empty.</summary>
    internal static string ColorText(ResolvedFrameColor c)
    {
        var parts = new List<string>();
        if (c.Red is { } r) parts.Add($"red={r}");
        if (c.Green is { } g) parts.Add($"green={g}");
        if (c.Blue is { } b) parts.Add($"blue={b}");
        if (c.Alpha is { } a) parts.Add($"alpha={a}");
        if (c.Operation is { } op) parts.Add($"operation={op}");
        return string.Join(" ", parts);
    }

    /// <summary>A <c>color</c> object holding the parts that are set; writes nothing when none is.</summary>
    internal static void WriteColor(Utf8JsonWriter w, ResolvedFrameColor c)
    {
        if (c == default) return;
        w.WriteStartObject("color");
        if (c.Red is { } r) w.WriteNumber("red", r);
        if (c.Green is { } g) w.WriteNumber("green", g);
        if (c.Blue is { } b) w.WriteNumber("blue", b);
        if (c.Alpha is { } a) w.WriteNumber("alpha", a);
        if (c.Operation is { } op) w.WriteString("operation", op.ToString());
        w.WriteEndObject();
    }

    internal static string EventText(AnimationFrameEvent e) => e.Name + (e.Data is { } data ? "  data " + data : "");

    /// <summary>The event's properties, inside an object the caller opened.</summary>
    internal static void WriteEventBody(Utf8JsonWriter w, AnimationFrameEvent e)
    {
        w.WriteString("name", e.Name);
        if (e.Data is { } data) w.WriteString("data", data);
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
