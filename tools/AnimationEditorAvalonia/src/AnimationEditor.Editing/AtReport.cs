using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AnimationEditor.Editing;

/// <summary>One shape of the active frame at an instant, its offset and size glided by the keyframe curves.</summary>
/// <param name="Type"><c>rect</c>, <c>circle</c> or <c>polygon</c>.</param>
/// <param name="SizeX">Rect <c>scaleX</c>; circle radius; 0 for a polygon.</param>
/// <param name="SizeY">Rect <c>scaleY</c>; 0 otherwise.</param>
/// <param name="Points">Polygon vertices relative to the shape's offset; empty otherwise.</param>
public sealed record AtShape(string Type, string Name, float X, float Y, float SizeX, float SizeY, IReadOnlyList<(float X, float Y)> Points);

/// <summary>One visible track at an instant: its active chain, frame, sampled sprite offset and the frame's shapes.</summary>
public sealed record AtSprite(int Track, string TrackName, string Chain, int Frame, float OffsetX, float OffsetY, IReadOnlyList<AtShape> Shapes);

/// <summary>What <c>aecli at</c> reports for one sequence at one time; sprites are front to back (track 0 first).</summary>
public sealed record AtMoment(double Time, IReadOnlyList<AtSprite> Sprites);

/// <summary>Builds and prints the <c>aecli at</c> report from <see cref="SequenceScene"/>, the same scene the preview and render use.</summary>
public static class AtReport
{
    /// <summary>The sequence and its moments, or null with <paramref name="error"/> (<paramref name="usage"/> when the request itself is malformed).</summary>
    public static (string Sequence, IReadOnlyList<AtMoment> Moments)? Build(
        string file, string? sequenceName, IReadOnlyList<double> times, out string? error, out bool usage)
    {
        error = null; usage = false;
        var read = SequenceFileReader.Read(file);
        if (read.Document is null) { error = read.Error; return null; }

        SequenceDocument document;
        try { document = SequenceDocument.Load(file); }
        catch (Exception ex) { error = $"Could not read {Path.GetFileName(file)}: {ex.Message}"; return null; }

        var sequences = document.Save.Sequences;
        int index = RenderPlanner.Pick(sequenceName, sequences.Select(s => s.Name).ToList(), "sequence", "--sequence", "sequences", out error, out usage);
        if (index < 0) return null;

        var missing = document.Save.Sources.Where(document.IsSourceMissing).ToList();
        if (missing.Count > 0) { error = $"Missing or unreadable source(s): {string.Join(", ", missing)}."; return null; }

        var tracks = sequences[index].Tracks;
        var moments = times.Select(time => new AtMoment(time,
            SequenceScene.At(document, index, time).Reverse().Select(sprite => ToSprite(sprite, tracks)).ToList())).ToList();
        return (sequences[index].Name, moments);
    }

    private static AtSprite ToSprite(SequenceSprite sprite, IList<SequenceTrackSave> tracks)
    {
        var sample = sprite.Sample();
        var shapes = sprite.Frame.ShapesSave?.Shapes.OfType<ShapeSave>().Select(shape =>
        {
            var v = KeyframeSampler.SampleShape(sprite.Chain, sprite.Unit, sprite.FrameIndex, sprite.ElapsedInFrame, shape);
            var points = shape is PolygonSave polygon
                ? KeyframeSampler.SampleShapePoints(sprite.Chain, sprite.Unit, sprite.FrameIndex, sprite.ElapsedInFrame, polygon).Select(p => (p.X, p.Y)).ToList()
                : [];
            return new AtShape(EditAddress.TypeName(shape), shape.Name, v.X, v.Y, shape is PolygonSave ? 0 : v.SizeX, shape is AARectSave ? v.SizeY : 0, points);
        }).ToList() ?? [];
        string trackName = sprite.Track >= 0 && sprite.Track < tracks.Count ? tracks[sprite.Track].Name : "";
        return new AtSprite(sprite.Track, trackName, sprite.Chain.Name, sprite.FrameIndex, sample.OffsetX, sample.OffsetY, shapes);
    }

    public static string ToText(string sequence, IReadOnlyList<AtMoment> moments)
    {
        var sb = new StringBuilder();
        foreach (var moment in moments)
        {
            sb.AppendLine($"{sequence} at {Num(moment.Time)}s");
            if (moment.Sprites.Count == 0) sb.AppendLine("  (nothing visible)");
            foreach (var s in moment.Sprites)
            {
                sb.AppendLine($"  track {s.Track}{(s.TrackName.Length > 0 ? " " + s.TrackName : "")}: {s.Chain} frame {s.Frame} offset {Num(s.OffsetX)},{Num(s.OffsetY)}");
                foreach (var shape in s.Shapes)
                {
                    string detail = shape.Type switch
                    {
                        "rect" => $"scale {Num(shape.SizeX)}x{Num(shape.SizeY)}",
                        "circle" => $"radius {Num(shape.SizeX)}",
                        _ => "points " + string.Join(" ", shape.Points.Select(p => $"{Num(p.X)},{Num(p.Y)}")),
                    };
                    sb.AppendLine($"    {shape.Type} {shape.Name} at {Num(shape.X)},{Num(shape.Y)} {detail}");
                }
            }
        }
        return sb.ToString();
    }

    public static void WriteJson(Utf8JsonWriter w, string sequence, IReadOnlyList<AtMoment> moments)
    {
        w.WriteStartObject();
        w.WriteBoolean("success", true);
        w.WriteString("sequence", sequence);
        w.WriteStartArray("times");
        foreach (var moment in moments)
        {
            w.WriteStartObject();
            w.WriteNumber("time", moment.Time);
            w.WriteStartArray("sprites");
            foreach (var s in moment.Sprites)
            {
                w.WriteStartObject();
                w.WriteNumber("track", s.Track);
                w.WriteString("trackName", s.TrackName);
                w.WriteString("chain", s.Chain);
                w.WriteNumber("frame", s.Frame);
                w.WriteNumber("offsetX", s.OffsetX);
                w.WriteNumber("offsetY", s.OffsetY);
                w.WriteStartArray("shapes");
                foreach (var shape in s.Shapes)
                {
                    w.WriteStartObject();
                    w.WriteString("type", shape.Type);
                    w.WriteString("name", shape.Name);
                    w.WriteNumber("x", shape.X);
                    w.WriteNumber("y", shape.Y);
                    if (shape.Type == "rect") { w.WriteNumber("scaleX", shape.SizeX); w.WriteNumber("scaleY", shape.SizeY); }
                    else if (shape.Type == "circle") w.WriteNumber("radius", shape.SizeX);
                    else
                    {
                        w.WriteStartArray("points");
                        foreach (var (px, py) in shape.Points) { w.WriteStartObject(); w.WriteNumber("x", px); w.WriteNumber("y", py); w.WriteEndObject(); }
                        w.WriteEndArray();
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

    private static string Num(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
