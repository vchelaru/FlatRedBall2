using System.Text.Json;
using System.Text.Json.Nodes;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// A <c>.seqj</c> file: named sequences, each playing animation chains from one or more <c>.achx</c>/<c>.achj</c>
/// files on several sprites (tracks) at once. JSON only.
/// </summary>
/// <remarks>
/// Parsed and written by hand through <see cref="JsonNode"/>, like <c>.achj</c>, so it stays trim- and AOT-safe.
/// The format never resolves a reference: a chain or source that no longer exists is kept as written, and timing
/// treats it as zero length (see <see cref="SequenceTimeline"/>).
/// <para>
/// The format is shared with other tools, so a load then save must not destroy what another writer put in the file.
/// Properties this code does not know are kept as raw JSON on the object that held them (<see cref="UnknownProperties"/>
/// on the file, <see cref="SequenceSave"/>, <see cref="SequenceTrackSave"/> and <see cref="SequenceChainReferenceSave"/>)
/// and written back unchanged after the known ones, in their original relative order.
/// </para>
/// <para>
/// The top-level <c>"version"</c> is written first. A file without one is version 1. A file with a higher version than
/// <see cref="CurrentVersion"/> still loads, best effort: known fields are read, everything else is kept, and the
/// version is written back as read rather than downgraded. <see cref="IsFromNewerVersion"/> tells the UI to warn.
/// A <c>"version"</c> that is not a whole number of at least 1 is rejected with a <see cref="JsonException"/>.
/// </para>
/// </remarks>
public class SequenceListSave
{
    /// <summary>The format version this code writes for a new file.</summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// The format version of this file: <see cref="CurrentVersion"/> for a new file, 1 for a file with no
    /// <c>"version"</c>, otherwise the number the file carried. Written back as is, so a file from a newer version is
    /// never downgraded.
    /// </summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>True when the file declares a higher <see cref="Version"/> than this code knows, so some of it may be ignored.</summary>
    public bool IsFromNewerVersion => Version > CurrentVersion;

    /// <summary>
    /// Top-level properties this code does not know, kept as raw JSON and written after the known ones. Values are
    /// copied on save, so the stored nodes stay reusable.
    /// </summary>
    public JsonObject UnknownProperties { get; } = new();

    /// <summary>
    /// The <c>.achx</c>/<c>.achj</c> files this file's sequences use, relative to the <c>.seqj</c> with <c>/</c>
    /// separators. Shared by every sequence in the file.
    /// </summary>
    public List<string> Sources { get; set; } = new();

    /// <summary>The sequences in this file, each played by name.</summary>
    public List<SequenceSave> Sequences { get; set; } = new();

    /// <summary>Loads a <c>.seqj</c> from disk.</summary>
    /// <exception cref="JsonException">The file is not valid JSON.</exception>
    public static SequenceListSave FromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return FromJsonNode(ParseRoot(JsonNode.Parse(stream)));
    }

    /// <summary>Parses <c>.seqj</c> JSON. A leading byte-order mark is allowed.</summary>
    /// <exception cref="JsonException"><paramref name="json"/> is not valid JSON.</exception>
    public static SequenceListSave FromJsonString(string json) =>
        // JsonNode.Parse(string) rejects a BOM, unlike the stream overload.
        FromJsonNode(ParseRoot(JsonNode.Parse(json.TrimStart('﻿'))));

    /// <summary>Writes this file as indented JSON. Crash-safe: the old file stays intact until the new one is complete.</summary>
    public void Save(string path) => AtomicFile.Write(path, stream =>
    {
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        ToJsonNode().WriteTo(writer);
    });

    /// <summary>The same indented JSON <see cref="Save"/> writes.</summary>
    public string ToJsonString() => ToJsonNode().ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    private static JsonObject ParseRoot(JsonNode? node) =>
        node as JsonObject ?? throw new JsonException("A .seqj file must contain a JSON object.");

    // Every JsonArray.Add casts to JsonNode: the generic Add<T> overload is not trim- or AOT-safe.
    private JsonObject ToJsonNode()
    {
        var sources = new JsonArray();
        foreach (var source in Sources)
            sources.Add((JsonNode?)JsonValue.Create(source));

        var sequences = new JsonArray();
        foreach (var sequence in Sequences)
        {
            var tracks = new JsonArray();
            foreach (var track in sequence.Tracks)
                tracks.Add((JsonNode)WriteTrack(track));

            sequences.Add((JsonNode)AppendUnknown(new JsonObject
            {
                ["name"] = sequence.Name,
                ["loop"] = sequence.Loop,
                ["tracks"] = tracks,
            }, sequence.UnknownProperties));
        }

        return AppendUnknown(new JsonObject
        {
            ["version"] = Version,
            ["sources"] = sources,
            ["sequences"] = sequences,
        }, UnknownProperties);
    }

    private static JsonObject WriteTrack(SequenceTrackSave track)
    {
        var chains = new JsonArray();
        foreach (var reference in track.Chains)
        {
            chains.Add((JsonNode)AppendUnknown(new JsonObject
            {
                ["source"] = reference.Source,
                ["chain"] = reference.Chain,
                ["start"] = reference.Start,
                ["loops"] = reference.Loops,
            }, reference.UnknownProperties));
        }

        var result = new JsonObject
        {
            ["name"] = track.Name,
            ["x"] = track.X,
            ["y"] = track.Y,
            ["z"] = track.Z,
        };
        // Written only when set, so the common unflipped track stays short.
        if (track.FlipHorizontal) result["flipHorizontal"] = true;
        result["chains"] = chains;
        return AppendUnknown(result, track.UnknownProperties);
    }

    // Copies, because a JsonNode has one parent and the stored node must stay usable for the next save. A key the known
    // fields already wrote wins, so a hand-added unknown entry cannot clobber a real value.
    private static JsonObject AppendUnknown(JsonObject target, JsonObject unknown)
    {
        foreach (var (key, value) in unknown)
            if (!target.ContainsKey(key)) target[key] = value?.DeepClone();
        return target;
    }

    // Stores every property the known-field reads do not consume, in file order.
    private static void CaptureUnknown(JsonObject node, JsonObject unknown, params string[] knownKeys)
    {
        foreach (var (key, value) in node)
            if (Array.IndexOf(knownKeys, key) < 0) unknown[key] = value?.DeepClone();
    }

    private static int ReadVersion(JsonObject root)
    {
        if (!root.TryGetPropertyValue("version", out var node)) return 1;
        if (node is JsonValue value && value.TryGetValue(out int version) && version >= 1) return version;
        throw new JsonException("A .seqj \"version\" must be a whole number of at least 1.");
    }

    private static SequenceListSave FromJsonNode(JsonObject root)
    {
        var result = new SequenceListSave { Version = ReadVersion(root) };
        CaptureUnknown(root, result.UnknownProperties, "version", "sources", "sequences");
        if (root["sources"] is JsonArray sources)
        {
            foreach (var source in sources)
                if (source?.GetValue<string>() is { } path) result.Sources.Add(path);
        }

        if (root["sequences"] is JsonArray sequences)
        {
            foreach (var sequenceNode in sequences.OfType<JsonObject>())
                result.Sequences.Add(ReadSequence(sequenceNode));
        }
        return result;
    }

    private static SequenceSave ReadSequence(JsonObject node)
    {
        var sequence = new SequenceSave
        {
            Name = node["name"]?.GetValue<string>() ?? "",
            Loop = node["loop"]?.GetValue<bool>() ?? false,
        };
        CaptureUnknown(node, sequence.UnknownProperties, "name", "loop", "tracks");
        if (node["tracks"] is JsonArray tracks)
        {
            foreach (var trackNode in tracks.OfType<JsonObject>())
                sequence.Tracks.Add(ReadTrack(trackNode));
        }
        return sequence;
    }

    private static SequenceTrackSave ReadTrack(JsonObject node)
    {
        var track = new SequenceTrackSave
        {
            Name = node["name"]?.GetValue<string>() ?? "",
            X = node["x"]?.GetValue<float>() ?? 0,
            Y = node["y"]?.GetValue<float>() ?? 0,
            Z = node["z"]?.GetValue<int>() ?? 0,
            FlipHorizontal = node["flipHorizontal"]?.GetValue<bool>() ?? false,
        };
        CaptureUnknown(node, track.UnknownProperties, "name", "x", "y", "z", "flipHorizontal", "chains");
        if (node["chains"] is JsonArray chains)
        {
            foreach (var chainNode in chains.OfType<JsonObject>())
            {
                var reference = new SequenceChainReferenceSave
                {
                    Source = chainNode["source"]?.GetValue<string>() ?? "",
                    Chain = chainNode["chain"]?.GetValue<string>() ?? "",
                    Start = chainNode["start"]?.GetValue<double>() ?? 0,
                    Loops = chainNode["loops"]?.GetValue<float>() ?? 1,
                };
                CaptureUnknown(chainNode, reference.UnknownProperties, "source", "chain", "start", "loops");
                track.Chains.Add(reference);
            }
        }
        return track;
    }
}
