using System.Globalization;
using System.Text;
using System.Text.Json;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>
/// Human and machine readable dump of an animation file as stored on disk (#1811): chains, frames by
/// index with texture, length and region, and each frame's shapes. All numbers are invariant-culture.
/// </summary>
public static class AnimationDescriber
{
    public static string ToText(AnimationChainListSave doc, string path)
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
                $"{chain.Name}  ({chain.Frames.Count} frames, length {Num(chain.Frames.Sum(f => f.FrameLength))}{(flags.Count > 0 ? ", " + string.Join(" ", flags) : "")})"));
            for (int i = 0; i < chain.Frames.Count; i++)
            {
                var f = chain.Frames[i];
                sb.AppendLine(FormattableString.Invariant(
                    $"  {i}  {f.TextureName}  length {Num(f.FrameLength)}  region {Num(f.LeftCoordinate)},{Num(f.TopCoordinate)},{Num(f.RightCoordinate)},{Num(f.BottomCoordinate)}"));
                foreach (var shape in Shapes(f))
                    sb.AppendLine(FormattableString.Invariant($"       {EditAddress.TypeName(shape)} {shape.Name}  {ShapeSummary(shape)}"));
            }
        }
        return sb.ToString();
    }

    public static void WriteJson(Utf8JsonWriter w, AnimationChainListSave doc, string path)
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
                w.WriteStartArray("shapes");
                foreach (var shape in Shapes(f))
                {
                    w.WriteStartObject();
                    w.WriteString("type", EditAddress.TypeName(shape));
                    w.WriteString("name", shape.Name);
                    w.WriteNumber("x", shape.X);
                    w.WriteNumber("y", shape.Y);
                    switch (shape)
                    {
                        case AARectSave r: w.WriteNumber("scaleX", r.ScaleX); w.WriteNumber("scaleY", r.ScaleY); break;
                        case CircleSave c: w.WriteNumber("radius", c.Radius); break;
                        case PolygonSave p: w.WriteNumber("points", p.Points.Count); break;
                    }
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

    private static IEnumerable<ShapeSave> Shapes(AnimationFrameSave frame) =>
        frame.ShapesSave?.Shapes.OfType<ShapeSave>() ?? [];

    private static string ShapeSummary(ShapeSave shape) => shape switch
    {
        AARectSave r => FormattableString.Invariant($"at {Num(r.X)},{Num(r.Y)} scale {Num(r.ScaleX)}x{Num(r.ScaleY)}"),
        CircleSave c => FormattableString.Invariant($"at {Num(c.X)},{Num(c.Y)} radius {Num(c.Radius)}"),
        PolygonSave p => FormattableString.Invariant($"at {Num(p.X)},{Num(p.Y)} {p.Points.Count} points"),
        _ => string.Empty,
    };

    private static string Num(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
