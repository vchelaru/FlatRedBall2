using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.Animation;
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

/// <summary>
/// One sprite at an instant: its chain, frame, sampled offset and color, and what the frame stores (shapes, events, flip flags).
/// <paramref name="Events"/> fire when the frame is entered, so a time inside the frame lists them without meaning they fire then.
/// </summary>
/// <param name="Track">The sequence track; null for a chain played on its own.</param>
public sealed record AtSprite(
    int? Track, string TrackName, string Chain, int Frame, float OffsetX, float OffsetY, IReadOnlyList<AtShape> Shapes,
    ResolvedFrameColor Color, bool FlipHorizontal, bool FlipVertical, bool FlipDiagonal, IReadOnlyList<AnimationFrameEvent> Events);

/// <summary>What <c>aecli at</c> reports at one time; a sequence's sprites are front to back (track 0 first), a chain has one.</summary>
/// <param name="Message">Why there are no sprites when the time is past the end of a looping sequence; null otherwise.</param>
public sealed record AtMoment(double Time, IReadOnlyList<AtSprite> Sprites, string? Message = null);

/// <summary>The report of one sequence or chain.</summary>
/// <param name="Kind"><c>sequence</c> or <c>chain</c>.</param>
/// <param name="Pass">How many times a looping sequence has wrapped; 0 for a chain.</param>
public sealed record AtResult(string Kind, string Name, IReadOnlyList<AtMoment> Moments, int Pass = 0);

/// <summary>
/// Builds and prints the <c>aecli at</c> report from <see cref="SequenceScene"/> (a sequence) or <see cref="ChainClock"/> (a chain),
/// as the preview and render do.
/// </summary>
public static class AtReport
{
    /// <summary>The report of a .seqj's sequence, or null with <paramref name="error"/> (<paramref name="usage"/> when the request itself is malformed).</summary>
    /// <param name="pass">How many times a looping sequence has wrapped, so continuous tracks show their carried phase.</param>
    /// <param name="trackPattern">Track name or glob (as <c>describe --track</c>); other tracks are left out. A pattern matching no track is an error.</param>
    /// <param name="shapesOnly">Leave out tracks whose active frame has no shapes.</param>
    public static AtResult? BuildSequence(
        string file, string? sequenceName, IReadOnlyList<double> times, int pass, string? trackPattern, bool shapesOnly, out string? error, out bool usage)
    {
        if (RenderPlanner.OpenSequence(file, sequenceName, out error, out usage) is not var (document, index)) return null;

        var sequences = document.Save.Sequences;
        var tracks = sequences[index].Tracks;
        var filter = new SequenceDescriber.Filter([sequences[index].Name], trackPattern);
        error = SequenceDescriber.CheckTrackFilter(document.Save, filter);
        if (error is not null) return null;

        var moments = times.Select(time =>
        {
            var sprites = SequenceScene.At(document, index, time, pass).Reverse()
                .Where(sprite => trackPattern is null || (sprite.Track >= 0 && sprite.Track < tracks.Count && filter.Includes(tracks[sprite.Track])))
                .Select(sprite => ToSprite(sprite, tracks))
                .Where(sprite => !shapesOnly || sprite.Shapes.Count > 0).ToList();
            return new AtMoment(time, sprites, sprites.Count == 0 ? RenderPlanner.PastEnd(document, index, time) : null);
        }).ToList();
        return new AtResult("sequence", sequences[index].Name, moments, pass);
    }

    /// <summary>The report of an .achx/.achj chain, or null with <paramref name="error"/> (<paramref name="usage"/> when the request itself is malformed).</summary>
    /// <param name="shapesOnly">Leave out the sprite at times whose frame has no shapes.</param>
    public static AtResult? BuildChain(string file, string? chainName, IReadOnlyList<double> times, bool shapesOnly, out string? error, out bool usage)
    {
        usage = false;
        error = RenderPlanner.CheckChainFile(file, "at");
        if (error is not null || RenderPlanner.OpenChain(file, chainName, out error, out usage) is not var (chain, unit)) return null;

        var moments = times.Select(time =>
        {
            var (frame, elapsed) = ChainClock.Locate(chain, time, unit);
            var sprite = ToSprite(new SequenceSprite(0, chain, unit, frame, elapsed, null), []) with { Track = null };
            return new AtMoment(time, shapesOnly && sprite.Shapes.Count == 0 ? [] : [sprite]);
        }).ToList();
        return new AtResult("chain", chain.Name, moments);
    }

    private static AtSprite ToSprite(SequenceSprite sprite, IList<SequenceTrackSave> tracks)
    {
        var sample = sprite.Sample();
        var shapes = sprite.Frame.ShapesSave?.Shapes.OfType<ShapeSave>().Select(shape =>
        {
            var v = KeyframeSampler.SampleShape(sprite.Chain, sprite.Unit, sprite.FrameIndex, sprite.ElapsedInFrame, shape);
            var points = shape is PolygonSave polygon
                ? KeyframeSampler.SampleShapePoints(sprite.Chain, sprite.Unit, sprite.FrameIndex, sprite.ElapsedInFrame, polygon).Select(p => (AnimationDescriber.PlainZero(p.X), AnimationDescriber.PlainZero(p.Y))).ToList()
                : [];
            return new AtShape(EditAddress.TypeName(shape), shape.Name, AnimationDescriber.PlainZero(v.X), AnimationDescriber.PlainZero(v.Y), shape is PolygonSave ? 0 : v.SizeX, shape is AARectSave ? v.SizeY : 0, points);
        }).ToList() ?? [];
        string trackName = sprite.Track >= 0 && sprite.Track < tracks.Count ? tracks[sprite.Track].Name : "";
        return new AtSprite(sprite.Track, trackName, sprite.Chain.Name, sprite.FrameIndex, AnimationDescriber.PlainZero(sample.OffsetX), AnimationDescriber.PlainZero(sample.OffsetY), shapes,
            sample.Color, sprite.Frame.FlipHorizontal, sprite.Frame.FlipVertical, sprite.Frame.FlipDiagonal, sprite.Frame.Events);
    }

    public static string ToText(AtResult result)
    {
        var sb = new StringBuilder();
        foreach (var moment in result.Moments)
        {
            sb.AppendLine($"{result.Name} at {Num(moment.Time)}s{(result.Pass > 0 ? $" pass {result.Pass}" : "")}");
            if (moment.Sprites.Count == 0) sb.AppendLine("  " + (moment.Message ?? "(nothing visible)"));
            foreach (var s in moment.Sprites)
            {
                string track = s.Track is { } t ? $"track {t}{(s.TrackName.Length > 0 ? " " + s.TrackName : "")}: " : "";
                string flips = AnimationDescriber.FlipText(s.FlipHorizontal, s.FlipVertical, s.FlipDiagonal);
                sb.AppendLine($"  {track}{s.Chain} frame {s.Frame} offset {Num(s.OffsetX)},{Num(s.OffsetY)}{(flips.Length > 0 ? "  " + flips : "")}");
                string color = AnimationDescriber.ColorText(s.Color);
                if (color.Length > 0) sb.AppendLine($"    color {color}");
                foreach (var e in s.Events) sb.AppendLine($"    event {AnimationDescriber.EventText(e)}");
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

    public static void WriteJson(Utf8JsonWriter w, AtResult result)
    {
        w.WriteStartObject();
        w.WriteBoolean("success", true);
        w.WriteString(result.Kind, result.Name);
        if (result.Pass > 0) w.WriteNumber("pass", result.Pass);
        w.WriteStartArray("times");
        foreach (var moment in result.Moments)
        {
            w.WriteStartObject();
            w.WriteNumber("time", moment.Time);
            if (moment.Message is not null) w.WriteString("message", moment.Message);
            w.WriteStartArray("sprites");
            foreach (var s in moment.Sprites)
            {
                w.WriteStartObject();
                if (s.Track is { } track)
                {
                    w.WriteNumber("track", track);
                    w.WriteString("trackName", s.TrackName);
                }
                w.WriteString("chain", s.Chain);
                w.WriteNumber("frame", s.Frame);
                w.WriteReal("offsetX", s.OffsetX);
                w.WriteReal("offsetY", s.OffsetY);
                AnimationDescriber.WriteFlips(w, s.FlipHorizontal, s.FlipVertical, s.FlipDiagonal);
                AnimationDescriber.WriteColor(w, s.Color);
                w.WriteStartArray("events");
                foreach (var e in s.Events) { w.WriteStartObject(); AnimationDescriber.WriteEventBody(w, e); w.WriteEndObject(); }
                w.WriteEndArray();
                w.WriteStartArray("shapes");
                foreach (var shape in s.Shapes)
                {
                    w.WriteStartObject();
                    w.WriteString("type", shape.Type);
                    w.WriteString("name", shape.Name);
                    w.WriteReal("x", shape.X);
                    w.WriteReal("y", shape.Y);
                    if (shape.Type == "rect") { w.WriteReal("scaleX", shape.SizeX); w.WriteReal("scaleY", shape.SizeY); }
                    else if (shape.Type == "circle") w.WriteReal("radius", shape.SizeX);
                    else
                    {
                        w.WriteStartArray("points");
                        foreach (var (px, py) in shape.Points) { w.WriteStartObject(); w.WriteReal("x", px); w.WriteReal("y", py); w.WriteEndObject(); }
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

    private static string Num(double v) => AnimationDescriber.PlainZero(v.ToString("0.###", CultureInfo.InvariantCulture));
}
