using AnimationEditor.Core.Export;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System.Globalization;

namespace AnimationEditor.Editing;

/// <summary>What <c>aecli render</c> was asked to draw: a sequence or a chain, at times or (chains) frame indexes.</summary>
/// <param name="Name">The sequence or chain; null picks the file's only one.</param>
/// <param name="Times">Seconds; null when not given.</param>
/// <param name="Frames">Chain frame indexes; null when not given.</param>
public sealed record RenderRequest(string File, string? Name, IReadOnlyList<double>? Times, IReadOnlyList<int>? Frames);

/// <summary>The images to draw: one file-name stem, one caption (the time or frame, for a contact sheet) and one composed frame each, back to front, ready for the renderer.</summary>
public sealed record RenderPlan(
    string Name, IReadOnlyList<string> Stems, IReadOnlyList<string> Labels, IReadOnlyList<ExportTimelineFrame> Frames, Func<AnimationFrameSave, string?> ResolveTexture);

/// <summary>Builds a <see cref="RenderPlan"/> from a file with the editor's own scene logic; no drawing happens here.</summary>
public static class RenderPlanner
{
    /// <summary>The plan, or null with <paramref name="error"/>; <paramref name="usage"/> is true when the request itself is malformed.</summary>
    public static RenderPlan? Plan(RenderRequest request, out string? error, out bool usage)
    {
        usage = false;
        if (SequenceFileReader.IsSequenceFile(request.File))
            return PlanSequence(request, out error, out usage);

        var read = AnimationFileReader.Read(request.File);
        if (read.Document is null || Path.GetExtension(request.File).ToLowerInvariant() == ".tsx")
        {
            error = read.Error ?? "render supports .achx, .achj and .seqj files.";
            return null;
        }
        return PlanChain(request, out error, out usage);
    }

    private static RenderPlan? PlanSequence(RenderRequest request, out string? error, out bool usage)
    {
        error = null; usage = false;
        if (request.Frames is not null) { usage = true; error = "--frame is for chains; use --at with a sequence."; return null; }
        var read = SequenceFileReader.Read(request.File);
        if (read.Document is null) { error = read.Error; return null; }

        SequenceDocument document;
        try { document = SequenceDocument.Load(request.File); }
        catch (Exception ex) { error = $"Could not read {Path.GetFileName(request.File)}: {ex.Message}"; return null; }

        var sequences = document.Save.Sequences;
        int index = Pick(request.Name, sequences.Select(s => s.Name).ToList(), "sequence", "--sequence", "sequences", out error, out usage);
        if (index < 0) return null;

        var missing = document.Save.Sources.Where(document.IsSourceMissing).ToList();
        if (missing.Count > 0) { error = $"Missing or unreadable source(s): {string.Join(", ", missing)}."; return null; }

        var times = request.Times ?? [0d];
        var frames = new List<ExportTimelineFrame>();
        foreach (double time in times)
        {
            var layers = SequenceScene.At(document, index, time).Select(sprite =>
            {
                var sample = sprite.Sample();
                return new ExportLayer(sprite.Chain, sprite.Frame, sample.OffsetX, sample.OffsetY, sample.Color,
                    ExportShapes.Sample(sprite.Chain, sprite.Unit, sprite.FrameIndex, sprite.ElapsedInFrame));
            }).ToList();
            frames.Add(new ExportTimelineFrame(layers, 0));
        }
        string name = sequences[index].Name;
        var textureOf = SpriteTextures(document, index, times);
        return new RenderPlan(name, times.Select(t => Stem(name, Time(t))).ToList(), times.Select(t => Time(t) + "s").ToList(), frames, frame => textureOf.GetValueOrDefault(frame));
    }

    // The texture of every frame the requested times can show, resolved through the sprite's own source.
    private static Dictionary<AnimationFrameSave, string?> SpriteTextures(SequenceDocument document, int index, IReadOnlyList<double> times)
    {
        var map = new Dictionary<AnimationFrameSave, string?>(ReferenceEqualityComparer.Instance);
        foreach (double time in times)
            foreach (var sprite in SequenceScene.At(document, index, time))
                map[sprite.Frame] = sprite.TexturePath;
        return map;
    }

    private static RenderPlan? PlanChain(RenderRequest request, out string? error, out bool usage)
    {
        error = null; usage = false;
        if (request.Frames is not null && request.Times is not null) { usage = true; error = "give --frame or --at, not both."; return null; }

        AnimationChainListSave list;
        try { list = AnimationFileLoader.Load(request.File).List; }
        catch (Exception ex) { error = $"Could not read {Path.GetFileName(request.File)}: {ex.Message}"; return null; }

        int index = Pick(request.Name, list.AnimationChains.Select(c => c.Name).ToList(), "chain", "--chain", "chains", out error, out usage);
        if (index < 0) return null;
        var chain = list.AnimationChains[index];
        if (chain.Frames.Count == 0) { error = $"Chain '{chain.Name}' has no frames."; return null; }

        var unit = FrameLengthUnit.Of(list);
        var spots = new List<(int Frame, double Elapsed, string Stem, string Label)>();
        if (request.Times is { } times)
            foreach (double time in times)
            {
                var (frame, elapsed) = ChainClock.Locate(chain, time, unit);
                spots.Add((frame, elapsed, Stem(chain.Name, Time(time)), Time(time) + "s"));
            }
        else
            foreach (int frame in request.Frames ?? [0])
            {
                if (frame < 0 || frame >= chain.Frames.Count)
                { error = $"Frame {frame} is out of range; chain '{chain.Name}' has {chain.Frames.Count} frame(s), 0 to {chain.Frames.Count - 1}."; return null; }
                spots.Add((frame, 0, Stem(chain.Name, $"frame{frame}"), $"frame {frame}"));
            }

        var frames = spots.Select(spot =>
        {
            var sample = KeyframeSampler.Sample(chain, unit, spot.Frame, spot.Elapsed);
            var layer = new ExportLayer(chain, chain.Frames[spot.Frame], sample.OffsetX, sample.OffsetY, sample.Color,
                ExportShapes.Sample(chain, unit, spot.Frame, spot.Elapsed));
            return new ExportTimelineFrame([layer], 0);
        }).ToList();
        string folder = Path.GetDirectoryName(Path.GetFullPath(request.File)) ?? "";
        string? Resolve(AnimationFrameSave frame) =>
            string.IsNullOrEmpty(frame.TextureName) ? null
            : Path.GetFullPath(Path.IsPathRooted(frame.TextureName) ? frame.TextureName : Path.Combine(folder, frame.TextureName));
        return new RenderPlan(chain.Name, spots.Select(s => s.Stem).ToList(), spots.Select(s => s.Label).ToList(), frames, Resolve);
    }

    // The index of the named item, or the only one when no name is given; -1 with an error (usage when the request is at fault).
    private static int Pick(string? name, IReadOnlyList<string> names, string kind, string option, string plural, out string? error, out bool usage)
    {
        error = null; usage = false;
        if (name is null)
        {
            if (names.Count == 1) return 0;
            usage = true;
            error = names.Count == 0 ? $"the file has no {plural}." : $"{option} is needed; the file has several.{NameHints.For("", names, plural)}";
            return -1;
        }
        int found = names.ToList().FindIndex(n => n == name);
        if (found < 0) found = names.ToList().FindIndex(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        if (found >= 0) return found;
        error = $"No {kind} named '{name}'." + NameHints.For(name, names, plural);
        return -1;
    }

    private static string Time(double seconds) => seconds.ToString("0.######", CultureInfo.InvariantCulture);

    private static string Stem(string name, string suffix)
    {
        var bad = Path.GetInvalidFileNameChars();
        return new string($"{name}_{suffix}".Select(c => Array.IndexOf(bad, c) >= 0 ? '_' : c).ToArray());
    }
}
