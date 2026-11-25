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
/// <param name="Pass">Sequence only: how many times a looping sequence has wrapped, so continuous tracks show their carried phase.</param>
public sealed record RenderRequest(string File, string? Name, IReadOnlyList<double>? Times, IReadOnlyList<int>? Frames, int Pass = 0);

/// <summary>The images to draw: one file-name stem, one caption (the time or frame, for a contact sheet) and one composed frame each, back to front, ready for the renderer.</summary>
/// <param name="Warnings">What to tell the caller about images that are drawn anyway (a time past the end of a looping sequence).</param>
public sealed record RenderPlan(
    string Name, IReadOnlyList<string> Stems, IReadOnlyList<string> Labels, IReadOnlyList<ExportTimelineFrame> Frames, Func<AnimationFrameSave, string?> ResolveTexture,
    IReadOnlyList<string> Warnings);

/// <summary>Builds a <see cref="RenderPlan"/> from a file with the editor's own scene logic; no drawing happens here.</summary>
public static class RenderPlanner
{
    /// <summary>Why a frame index is refused for a sequence.</summary>
    public const string FrameIsForChains = "--frame is for chains; use --at with a sequence.";

    /// <summary>The plan, or null with <paramref name="error"/>; <paramref name="usage"/> is true when the request itself is malformed.</summary>
    public static RenderPlan? Plan(RenderRequest request, out string? error, out bool usage)
    {
        usage = false;
        if (SequenceFileReader.IsSequenceFile(request.File))
            return PlanSequence(request, out error, out usage);

        error = CheckChainFile(request.File, "render");
        return error is null ? PlanChain(request, out error, out usage) : null;
    }

    /// <summary>Why <paramref name="file"/> cannot be read as an .achx/.achj, or null when it can.</summary>
    internal static string? CheckChainFile(string file, string command)
    {
        var read = AnimationFileReader.Read(file);
        return read.Document is null || Path.GetExtension(file).ToLowerInvariant() == ".tsx"
            ? read.Error ?? $"{command} supports .achx, .achj and .seqj files."
            : null;
    }

    /// <summary>The named chain (or the file's only one) and its unit, or null with <paramref name="error"/>. The chain has at least one frame.</summary>
    internal static (AnimationChainSave Chain, FrameLengthUnit Unit)? OpenChain(string file, string? name, out string? error, out bool usage)
    {
        usage = false;
        AnimationChainListSave list;
        try { list = AnimationFileLoader.Load(file).List; }
        catch (Exception ex) { error = $"Could not read {Path.GetFileName(file)}: {ex.Message}"; return null; }

        int index = Pick(name, list.AnimationChains.Select(c => c.Name).ToList(), "chain", "--chain", "chains", out error, out usage);
        if (index < 0) return null;
        var chain = list.AnimationChains[index];
        if (chain.Frames.Count == 0) { error = $"Chain '{chain.Name}' has no frames."; return null; }
        return (chain, FrameLengthUnit.Of(list));
    }

    private static RenderPlan? PlanSequence(RenderRequest request, out string? error, out bool usage)
    {
        error = null; usage = false;
        if (request.Frames is not null) { usage = true; error = FrameIsForChains; return null; }
        if (OpenSequence(request.File, request.Name, out error, out usage) is not var (document, index)) return null;

        var times = request.Times ?? [0d];
        var frames = new List<ExportTimelineFrame>();
        foreach (double time in times)
        {
            var layers = SequenceScene.At(document, index, time, request.Pass).Select(sprite =>
            {
                var sample = sprite.Sample();
                return new ExportLayer(sprite.Chain, sprite.Frame, sample.OffsetX, sample.OffsetY, sample.Color,
                    ExportShapes.Sample(sprite.Chain, sprite.Unit, sprite.FrameIndex, sprite.ElapsedInFrame));
            }).ToList();
            frames.Add(new ExportTimelineFrame(layers, 0));
        }
        string name = document.Save.Sequences[index].Name;
        var labels = times.Select(t => Time(t) + "s" + (request.Pass > 0 ? $" pass {request.Pass}" : "")).ToList();
        var pastEnd = times.Select((t, i) => frames[i].Layers.Count == 0 ? PastEnd(document, index, t) : null).ToList();
        if (frames.All(f => f.Layers.Count == 0))
        {
            error = string.Join(". ", pastEnd.Select((message, i) => message ?? $"{name} at {labels[i]}: nothing visible"));
            return null;
        }
        var textureOf = SpriteTextures(document, index, times, request.Pass);
        return new RenderPlan(name, times.Select(t => Stem(name, Time(t) + PassSuffix(request.Pass))).ToList(), labels, frames, frame => textureOf.GetValueOrDefault(frame), pastEnd.OfType<string>().ToList());
    }

    /// <summary>
    /// The document and the index of the named sequence (or the file's only one), or null with <paramref name="error"/>: also when a
    /// source is missing, or the sequence or one it plays uses a chain, source or sequence that is not there or plays itself (as <c>check</c> says it).
    /// </summary>
    internal static (SequenceDocument Document, int Index)? OpenSequence(string file, string? name, out string? error, out bool usage)
    {
        usage = false;
        var read = SequenceFileReader.Read(file);
        if (read.Document is null) { error = read.Error; return null; }

        SequenceDocument document;
        try { document = SequenceDocument.Load(file); }
        catch (Exception ex) { error = $"Could not read {Path.GetFileName(file)}: {ex.Message}"; return null; }

        var sequences = document.Save.Sequences;
        int index = Pick(name, sequences.Select(s => s.Name).ToList(), "sequence", "--sequence", "sequences", out error, out usage);
        if (index < 0) return null;

        var missing = document.Save.Sources.Where(document.IsSourceMissing).ToList();
        if (missing.Count > 0) { error = $"Missing or unreadable source(s): {string.Join(", ", missing)}."; return null; }

        // WouldCycle(s, picked) is true for the picked sequence and every sequence it plays.
        string picked = sequences[index].Name;
        var asked = new SequenceListSave { Sources = document.Save.Sources, Sequences = sequences.Where(s => SequenceReferences.WouldCycle(sequences, s.Name, picked)).ToList() };
        var absent = SequenceCheck.Run(asked, file).Errors.Where(e => e.Code.StartsWith("missing-") || e.Code == "reference-cycle").Select(e => e.Message).ToList();
        if (absent.Count > 0) { error = string.Join(" ", absent); return null; }
        return (document, index);
    }

    /// <summary>The message for a <paramref name="time"/> at or past the end of a looping sequence, which shows nothing there; null otherwise.</summary>
    internal static string? PastEnd(SequenceDocument document, int index, double time)
    {
        var sequence = document.Save.Sequences[index];
        double length = document.SequenceLength(index);
        SequenceTimeline.WrapTime(time, length, out int passes);
        return sequence.Loop && passes > 0
            ? $"{Time(time)}s is past the end of {sequence.Name} ({Time(length)}s); use --pass for later loops"
            : null;
    }

    /// <summary>The code of an error <see cref="Plan"/> or <see cref="AtReport"/> gave.</summary>
    public static EditErrorCode CodeOf(string error) =>
        error.StartsWith("No ") || error.StartsWith("File not found") || error.StartsWith("Missing")
            || error.Contains(" has no chain '") || error.Contains("which does not exist") || error.Contains("not in the sources list") ? EditErrorCode.NotFound
        : error.Contains("', which plays '") ? EditErrorCode.WouldCycle
        : error.Contains("out of range") ? EditErrorCode.IndexOutOfRange
        : EditErrorCode.Unspecified;

    // The texture of every frame the requested times can show, resolved through the sprite's own source.
    private static Dictionary<AnimationFrameSave, string?> SpriteTextures(SequenceDocument document, int index, IReadOnlyList<double> times, int pass)
    {
        var map = new Dictionary<AnimationFrameSave, string?>(ReferenceEqualityComparer.Instance);
        foreach (double time in times)
            foreach (var sprite in SequenceScene.At(document, index, time, pass))
                map[sprite.Frame] = sprite.TexturePath;
        return map;
    }

    private static RenderPlan? PlanChain(RenderRequest request, out string? error, out bool usage)
    {
        error = null; usage = false;
        if (request.Frames is not null && request.Times is not null) { usage = true; error = "give --frame or --at, not both."; return null; }

        if (OpenChain(request.File, request.Name, out error, out usage) is not var (chain, unit)) return null;

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
        return new RenderPlan(chain.Name, spots.Select(s => s.Stem).ToList(), spots.Select(s => s.Label).ToList(), frames, Resolve, []);
    }

    // The index of the named item, or the only one when no name is given; -1 with an error (usage when the request is at fault).
    internal static int Pick(string? name, IReadOnlyList<string> names, string kind, string option, string plural, out string? error, out bool usage)
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
        if (found < 0)
        {
            // Names may differ only by case, so a name in another case is used only when it can mean one item.
            var inAnyCase = Enumerable.Range(0, names.Count).Where(i => string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (inAnyCase.Count == 1) found = inAnyCase[0];
        }
        if (found >= 0) return found;
        error = $"No {kind} named '{name}'." + NameHints.For(name, names, plural);
        return -1;
    }

    private static string PassSuffix(int pass) => pass > 0 ? "_pass" + pass.ToString(CultureInfo.InvariantCulture) : "";

    private static string Time(double seconds) => seconds.ToString("0.######", CultureInfo.InvariantCulture);

    private static string Stem(string name, string suffix)
    {
        var bad = Path.GetInvalidFileNameChars();
        return new string($"{name}_{suffix}".Select(c => Array.IndexOf(bad, c) >= 0 ? '_' : c).ToArray());
    }
}
