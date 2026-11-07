using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AnimationEditor.Core.Sequences;

public enum TimelineClipboardKind { Chains, Tracks }

/// <summary>What a valid chains or tracks envelope on the clipboard holds.</summary>
/// <param name="Kind">Whether chains or whole tracks were copied.</param>
/// <param name="Chains">The copied chains (empty for tracks). Each source is an absolute path with <c>/</c> separators and each start is seconds after the earliest copied chain.</param>
/// <param name="TrackName">The name of the track the first copied chain was on (chains only).</param>
/// <param name="Tracks">The copied tracks (empty for chains), with their chains' sources as absolute paths.</param>
/// <param name="Sources">The distinct absolute source paths used, in first-use order.</param>
public sealed record TimelineClipboardContent(TimelineClipboardKind Kind, IReadOnlyList<SequenceChainReferenceSave> Chains,
    string TrackName, IReadOnlyList<SequenceTrackSave> Tracks, IReadOnlyList<string> Sources);

/// <summary>What a timeline paste or duplicate made: where the new chains and tracks are, and a message for each source refused.</summary>
public sealed record TimelinePasteResult(IReadOnlyList<TimelineChainId> Chains, IReadOnlyList<int> Tracks, IReadOnlyList<string> Errors)
{
    public static TimelinePasteResult Empty { get; } = new(Array.Empty<TimelineChainId>(), Array.Empty<int>(), Array.Empty<string>());
}

/// <summary>
/// The clipboard text for copied timeline chains or tracks, in the style of <see cref="SequenceClipboard"/>: a JSON
/// envelope with a marker and version around the items in the <c>.seqj</c> format's own shape, with sources as absolute
/// paths so it can be pasted into another <c>.seqj</c>.
/// </summary>
/// <remarks>
/// <code>{ "kind": "frb2-animation-editor-chains", "version": 1, "sources": ["C:/a/hero.achx"], "track": "Body", "chains": [ { "source": "C:/a/hero.achx", "chain": "Walk", "start": 0, "loops": 1 } ] }</code>
/// A chain's <c>start</c> is seconds after the earliest copied chain, so a paste can put that one anywhere. <c>track</c>
/// names the track the first copied chain was on. A tracks envelope holds <c>"tracks": [ ... ]</c> in the file's track shape.
/// </remarks>
public static class TimelineClipboard
{
    public const string ChainsKind = "frb2-animation-editor-chains";
    public const string TracksKind = "frb2-animation-editor-tracks";
    public const int CurrentVersion = 1;

    /// <summary>Deep copies of <paramref name="chains"/>, made through the format's own reader and writer so unknown properties survive.</summary>
    public static List<SequenceChainReferenceSave> CloneChains(IEnumerable<SequenceChainReferenceSave> chains)
    {
        var holder = new SequenceTrackSave { Name = "x" };
        holder.Chains.AddRange(chains);
        return FromWrapper(ToWrapper(new[] { holder })).Single().Chains;
    }

    /// <summary>Deep copies of <paramref name="tracks"/>, made the same way as <see cref="CloneChains"/>.</summary>
    public static List<SequenceTrackSave> CloneTracks(IEnumerable<SequenceTrackSave> tracks) => FromWrapper(ToWrapper(tracks));

    /// <summary>
    /// The envelope for <paramref name="chains"/> (in the order given), whose relative sources are resolved against
    /// <paramref name="sourceFolder"/>. <paramref name="trackName"/> is the track of the first one.
    /// </summary>
    public static string WriteChains(IReadOnlyList<SequenceChainReferenceSave> chains, string trackName, string sourceFolder)
    {
        var copies = CloneChains(chains);
        double earliest = copies.Count > 0 ? copies.Min(chain => chain.Start) : 0;
        foreach (var chain in copies) chain.Start -= earliest;
        var sources = AbsoluteSources(copies, sourceFolder);

        var holder = new SequenceTrackSave { Name = trackName };
        holder.Chains.AddRange(copies);
        var envelope = Envelope(ChainsKind, sources);
        envelope["track"] = trackName;
        envelope["chains"] = JsonNode.Parse(ToWrapper(new[] { holder }))!["sequences"]![0]!["tracks"]![0]!["chains"]!.DeepClone();
        return envelope.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>The envelope for <paramref name="tracks"/> (in the order given), with sources resolved against <paramref name="sourceFolder"/>.</summary>
    public static string WriteTracks(IReadOnlyList<SequenceTrackSave> tracks, string sourceFolder)
    {
        var copies = CloneTracks(tracks);
        var sources = AbsoluteSources(copies.SelectMany(track => track.Chains), sourceFolder);
        var envelope = Envelope(TracksKind, sources);
        envelope["tracks"] = JsonNode.Parse(ToWrapper(copies))!["sequences"]![0]!["tracks"]!.DeepClone();
        return envelope.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Reads clipboard text. False with a null <paramref name="error"/> when the text is not a chains or tracks envelope
    /// at all (ordinary text, other JSON, a copied sequence), so the caller does nothing; false with a message when it
    /// carries the marker but is broken.
    /// </summary>
    public static bool TryRead(string? text, out TimelineClipboardContent? content, out string? error)
    {
        content = null;
        error = null;
        if (string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith('{')) return false;

        JsonObject envelope;
        try
        {
            if (JsonNode.Parse(text) is not JsonObject parsed) return false;
            envelope = parsed;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return false;
        }
        if (!envelope.TryGetPropertyValue("kind", out var kind) || kind is not JsonValue kindValue
            || !kindValue.TryGetValue<string>(out var kindText) || kindText is not (ChainsKind or TracksKind))
            return false;
        bool chains = kindText == ChainsKind;
        string noun = chains ? "chains" : "tracks";

        if (!envelope.TryGetPropertyValue("version", out var version) || version is not JsonValue versionValue
            || !versionValue.TryGetValue<int>(out int versionNumber) || versionNumber < 1)
            return Fail(noun, "it has no valid version.", out error);
        if (envelope.TryGetPropertyValue("sources", out var sources)
            && (sources is not JsonArray list || list.Any(source => source is not JsonValue value || !value.TryGetValue<string>(out _))))
            return Fail(noun, "its sources are not a list of paths.", out error);
        if (!envelope.TryGetPropertyValue(noun, out var items) || items is not JsonArray array)
            return Fail(noun, $"it has no {noun}.", out error);
        string trackName = envelope.TryGetPropertyValue("track", out var track) && track is JsonValue trackValue
            && trackValue.TryGetValue<string>(out var named) ? named : string.Empty;

        List<SequenceTrackSave> read;
        try
        {
            var holder = new JsonObject { ["name"] = "x" };
            holder["tracks"] = chains
                ? new JsonArray(new JsonObject { ["name"] = "x", ["chains"] = array.DeepClone() })
                : array.DeepClone();
            read = FromWrapper(new JsonObject
            {
                ["version"] = 1,
                ["sources"] = new JsonArray(),
                ["sequences"] = new JsonArray(holder),
            }.ToJsonString());
        }
        catch (JsonException ex)
        {
            return Fail(noun, ex.Message, out error);
        }

        var allChains = read.SelectMany(t => t.Chains).ToList();
        var used = new List<string>();
        foreach (var reference in allChains)
            if (reference.Source.Length > 0 && !used.Contains(reference.Source, StringComparer.OrdinalIgnoreCase))
                used.Add(reference.Source);
        content = chains
            ? new TimelineClipboardContent(TimelineClipboardKind.Chains, read.Single().Chains, trackName, Array.Empty<SequenceTrackSave>(), used)
            : new TimelineClipboardContent(TimelineClipboardKind.Tracks, Array.Empty<SequenceChainReferenceSave>(), string.Empty, read, used);
        return true;
    }

    private static JsonObject Envelope(string kind, List<string> sources)
    {
        var array = new JsonArray();
        foreach (string source in sources) array.Add((JsonNode?)JsonValue.Create(source));
        return new JsonObject { ["kind"] = kind, ["version"] = CurrentVersion, ["sources"] = array };
    }

    // Rewrites each reference's source to an absolute path and lists them once each, in first-use order.
    private static List<string> AbsoluteSources(IEnumerable<SequenceChainReferenceSave> references, string folder)
    {
        var sources = new List<string>();
        foreach (var reference in references)
        {
            if (reference.Source.Length == 0) continue;
            reference.Source = SequenceClipboard.AbsolutePath(folder, reference.Source);
            if (!sources.Contains(reference.Source, StringComparer.OrdinalIgnoreCase)) sources.Add(reference.Source);
        }
        return sources;
    }

    private static bool Fail(string noun, string reason, out string error)
    {
        error = $"Can't paste: the copied {noun} are damaged, {reason}";
        return false;
    }

    private static string ToWrapper(IEnumerable<SequenceTrackSave> tracks)
    {
        var sequence = new SequenceSave { Name = "x" };
        sequence.Tracks.AddRange(tracks);
        var wrapper = new SequenceListSave();
        wrapper.Sequences.Add(sequence);
        return wrapper.ToJsonString();
    }

    private static List<SequenceTrackSave> FromWrapper(string json) => SequenceListSave.FromJsonString(json).Sequences.Single().Tracks;
}
