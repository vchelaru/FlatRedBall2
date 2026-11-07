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
/// treats it as zero length (see <see cref="SequenceTimeline"/>). Unknown JSON properties are not preserved.
/// </remarks>
public class SequenceListSave
{
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

            sequences.Add((JsonNode)new JsonObject
            {
                ["name"] = sequence.Name,
                ["loop"] = sequence.Loop,
                ["tracks"] = tracks,
            });
        }

        return new JsonObject { ["sources"] = sources, ["sequences"] = sequences };
    }

    private static JsonObject WriteTrack(SequenceTrackSave track)
    {
        var chains = new JsonArray();
        foreach (var reference in track.Chains)
        {
            chains.Add((JsonNode)new JsonObject
            {
                ["source"] = reference.Source,
                ["chain"] = reference.Chain,
                ["start"] = reference.Start,
                ["loops"] = reference.Loops,
            });
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
        return result;
    }

    private static SequenceListSave FromJsonNode(JsonObject root)
    {
        var result = new SequenceListSave();
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
        if (node["chains"] is JsonArray chains)
        {
            foreach (var chainNode in chains.OfType<JsonObject>())
            {
                track.Chains.Add(new SequenceChainReferenceSave
                {
                    Source = chainNode["source"]?.GetValue<string>() ?? "",
                    Chain = chainNode["chain"]?.GetValue<string>() ?? "",
                    Start = chainNode["start"]?.GetValue<double>() ?? 0,
                    Loops = chainNode["loops"]?.GetValue<float>() ?? 1,
                });
            }
        }
        return track;
    }
}
